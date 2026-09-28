using System.Text.Json;
using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Contract.Misc.Messaging;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Maroik.Core.Client.Clients;

/// <summary>
/// Publishes <see cref="SendEmailMessage"/> to the durable "maroik.email" queue.
/// The connection and a publishing channel are opened lazily on first publish (so a RabbitMQ outage
/// at application startup does not prevent the web app from starting) and then cached and reused
/// across publishes; the queue is declared once per channel rather than on every call.
/// </summary>
public sealed class RabbitMqEmailPublisher(
    IConnectionFactory factory,
    ILogger<RabbitMqEmailPublisher> logger,
    TimeSpan? connectionRecoveryGraceWindow = null)
    : IEmailPublisher, IAsyncDisposable
{
    /// <summary>
    /// Bound on how long <see cref="DisposeAsync"/> waits for an in-flight <see cref="PublishAsync"/>
    /// to release <see cref="_publishLock"/>. Without a bound, one publish wedged on a broker that
    /// never confirms (e.g. a caller that passed <see cref="CancellationToken.None"/>) would make
    /// graceful application shutdown hang forever.
    /// </summary>
    private static readonly TimeSpan _disposeLockTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// How long <see cref="GetChannelAsync"/> waits for a dropped connection to recover on its own
    /// (see the comment there) before giving up and opening a new one. Defaults to the factory's own
    /// configured <see cref="ConnectionFactory.NetworkRecoveryInterval"/> when <paramref
    /// name="factory"/> is a concrete <see cref="ConnectionFactory"/> (so this always matches
    /// whatever automatic-recovery timing is actually configured, 5s unless overridden), falling
    /// back to 5s — <see cref="ConnectionFactory"/>'s own default — for any other
    /// <see cref="IConnectionFactory"/> implementation. Overridable via
    /// <paramref name="connectionRecoveryGraceWindow"/> (tests use a short window so they don't pay
    /// a real multi-second sleep).
    /// </summary>
    private readonly TimeSpan _connectionRecoveryGraceWindow = connectionRecoveryGraceWindow
        ?? (factory as ConnectionFactory)?.NetworkRecoveryInterval
        ?? TimeSpan.FromSeconds(5);

    // RabbitMQ.Client's own guidance is a "hard requirement for publishers": a channel must not
    // be shared by threads that publish on it concurrently, or frames from separate publishes can
    // interleave at the protocol level. This channel is cached and shared across every caller of
    // PublishAsync (this class is registered as a singleton), so every publish -- including the
    // channel (re)creation it may trigger -- is serialized through this lock.
    private readonly SemaphoreSlim _publishLock = new(1, 1);
    private IConnection? _connection;
    private IChannel? _channel;

    // Stopwatch timestamp of the last time (re)connecting failed, or null. While the broker is down every
    // publish would otherwise queue behind _publishLock and each pay the recovery grace wait plus a full
    // connect attempt in turn, so a burst of registrations / password resets stacks up multi-second
    // latencies. After a failure, callers within one grace window fail fast instead (PublishAsync throws,
    // which the callers already treat as "mail could not be queued").
    private long? _lastConnectFailureTimestamp;

    /// <inheritdoc />
    public async Task PublishAsync(SendEmailMessage message, CancellationToken ct = default)
    {
        byte[] body = JsonSerializer.SerializeToUtf8Bytes(message);
        var properties = new BasicProperties { Persistent = true };

        await _publishLock.WaitAsync(ct);
        try
        {
            IChannel channel = await GetChannelAsync(ct);
            await channel.BasicPublishAsync(
                exchange: "",
                routingKey: QueueNames.Email,
                mandatory: false,
                basicProperties: properties,
                body: body,
                cancellationToken: ct);
        }
        finally
        {
            _publishLock.Release();
        }
    }

    /// <summary>
    /// Returns a ready-to-publish channel, opening the connection and channel (and declaring the
    /// queue once) on first use or after either has dropped. Only ever called from
    /// <see cref="PublishAsync"/> while holding <see cref="_publishLock"/>, so no locking of its
    /// own is needed here. The client's automatic / topology recovery re-opens and re-declares
    /// transparently after a broker blip, so the cached instances stay valid across one.
    /// </summary>
    private async Task<IChannel> GetChannelAsync(CancellationToken ct)
    {
        if (_connection is { IsOpen: true } && _channel is { IsOpen: true })
            return _channel;

        if (_channel != null)
        {
            await _channel.DisposeAsync();
            _channel = null;
        }

        if (_connection is not { IsOpen: true })
        {
            if (_lastConnectFailureTimestamp is { } failedAt
                && System.Diagnostics.Stopwatch.GetElapsedTime(failedAt) < _connectionRecoveryGraceWindow)
            {
                throw new InvalidOperationException(
                    "RabbitMQ is unavailable: a reconnect attempt failed moments ago, so this publish is not retried yet.");
            }

            if (_connection != null)
            {
                // AutomaticRecoveryEnabled means a connection that just dropped is very likely
                // already being reconnected by the client's own recovery loop (same IConnection
                // instance, reopened transparently) rather than genuinely dead. Give it one bounded
                // chance to come back on its own before tearing it down and opening a brand-new
                // connection, so a brief broker blip doesn't race its own recovery. The wait is
                // _connectionRecoveryGraceWindow — the factory's own configured
                // NetworkRecoveryInterval (5s by default) rather than an arbitrary short constant,
                // so this actually has a chance to observe a real recovery completing instead of
                // almost always falling through to a needless reconnect. This still runs under
                // _publishLock: with one shared channel, every queued publish needs the same
                // outcome (recovered connection or a fresh one) before any of them can proceed, so
                // serializing the wait here is correct, not just incidental.
                await Task.Delay(_connectionRecoveryGraceWindow, ct);
                if (!_connection.IsOpen)
                {
                    await _connection.DisposeAsync();
                    _connection = null;
                }
            }

            if (_connection is not { IsOpen: true })
            {
                try
                {
                    _connection = await factory.CreateConnectionAsync(ct);
                    _lastConnectFailureTimestamp = null;
                }
                catch (Exception) when (!ct.IsCancellationRequested)
                {
                    // Remember the failure so the publishes queued behind this one fail fast (see the field).
                    _lastConnectFailureTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
                    throw;
                }
            }
        }

        // Publisher confirms make BasicPublishAsync wait for the broker to actually accept the
        // message (and throw PublishException on a nack/basic.return) instead of returning as
        // soon as the frame is locally buffered, so PublishAsync's "enqueued" contract is real.
        var channelOptions = new CreateChannelOptions(
            publisherConfirmationsEnabled: true,
            publisherConfirmationTrackingEnabled: true);
        _channel = await _connection.CreateChannelAsync(channelOptions, ct);

        // Arguments must match the Worker's declaration of the same queue exactly, or whichever
        // side declares second fails with PRECONDITION_FAILED (see QueueNames.CreateEmailQueueArguments).
        await _channel.QueueDeclareAsync(
            queue: QueueNames.Email,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: QueueNames.CreateEmailQueueArguments(),
            cancellationToken: ct);

        return _channel;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        // Waits (bounded -- see DisposeLockTimeout) for any in-flight PublishAsync to release the
        // lock first, so the channel/connection it's using is never disposed out from under it.
        bool acquired = await _publishLock.WaitAsync(_disposeLockTimeout);
        if (!acquired)
        {
            // The in-flight publishing still owns _channel/_connection and will Release() the lock
            // itself once it unblocks -- disposing them here anyway would pull them out from under
            // that still-running call, so leave cleanup to the process exit instead.
            logger.LogWarning(
                "Timed out after {Timeout} waiting for an in-flight publish to finish; skipping disposal of the channel/connection",
                _disposeLockTimeout);
            return;
        }

        try
        {
            if (_channel != null)
                await _channel.DisposeAsync();
            if (_connection != null)
                await _connection.DisposeAsync();
        }
        finally
        {
            _publishLock.Release();
            _publishLock.Dispose();
        }
    }
}
