using System.Collections.Concurrent;
using System.Text.Json;
using Maroik.Core.Contract.Misc.Messaging;
using Maroik.Worker.Contracts;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Serilog.Context;
// ReSharper disable PropertyCanBeMadeInitOnly.Global

namespace Maroik.Worker.Workers;

/// <summary>
/// Long-running consumer for the "maroik.email" queue.
/// Connects to RabbitMQ, dequeues <see cref="SendEmailMessage"/> items, and delegates
/// each one to a scoped <see cref="IEmailMessageHandler"/> (a new DI scope per message,
/// since the handler depends on scoped services such as the EF Core DbContext).
/// </summary>
public sealed class EmailConsumerWorker(
    IConnectionFactory connectionFactory,
    IServiceScopeFactory scopeFactory,
    ILogger<EmailConsumerWorker> logger) : BackgroundService
{
    /// <summary>The AMQP connection opened in <see cref="ConnectAndConsumeAsync"/>; <see langword="null"/> until connected.</summary>
    private IConnection? _connection;
    /// <summary>The RabbitMQ channel opened in <c>ExecuteAsync</c>; <see langword="null"/> until connected. Internal so tests can inject a mock channel.</summary>
    internal IChannel? Channel;
    /// <summary>Tag of the registered consumer, used by <see cref="StopAsync"/> to cancel it; <see langword="null"/> until consuming.</summary>
    private string? _consumerTag;

    /// <summary>
    /// Passed to every IEmailMessageHandler.HandleAsync call. Never canceled during normal
    /// operation; StopAsync cancels it only once its drain deadline is reached with a send still
    /// running, so that in-progress SendMailAsync call aborts cleanly (triggering the normal
    /// nack/dead-letter path in OnMessageReceivedAsync's catch) instead of the channel being
    /// disposed out from under it, which would throw from ack/nack and force a redelivery/resend
    /// of a send that may already have completed.
    /// </summary>
    private readonly CancellationTokenSource _sendCts = new();

    /// <summary>Overridable in tests so the drain/unwind waits below don't need to run for real seconds.</summary>
    internal TimeSpan DrainTimeout { get; set; } = TimeSpan.FromSeconds(25);

    /// <summary>Overridable in tests — see <see cref="DrainTimeout"/>.</summary>
    internal TimeSpan CancelUnwindTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>First wait between failed initial connection attempts (doubled up to a 30s cap). Overridable in tests — see <see cref="DrainTimeout"/>.</summary>
    internal TimeSpan ConnectRetryInitialDelay { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Delivery tags currently being handled (the value is unused). The consumer dispatches one message
    /// at a time (RabbitMQ.Client's default ConsumerDispatchConcurrency is 1); prefetch just lets the
    /// broker keep the next few buffered at the client so there is no round-trip stall between messages.
    /// OnMessageReceivedAsync still tracks the delivery tag(s) here so StopAsync can wait for the current
    /// one to finish ack/nack before the channel is disposed out from under it.
    /// </summary>
    private readonly ConcurrentDictionary<ulong, byte> _inFlight = new();

    /// <summary>
    /// Header this worker sets on its own retry republish, holding how many times it has already
    /// retried this logical message. Tracked ourselves rather than via <c>BasicDeliverEventArgs
    /// .Redelivered</c>: the broker also sets that flag on a redelivery caused by a connection/consumer
    /// drop unrelated to this class's own retry decision (e.g. this process crashing and restarting),
    /// which would otherwise immediately dead-letter a message that this consumer instance never
    /// actually attempted.
    /// </summary>
    internal const string RetryCountHeader = "x-maroik-retry-count";

    /// <summary>How many of our own retries (via <see cref="RetryCountHeader"/>) a message gets before dead-lettering.</summary>
    internal const int MaxRetries = 1;

    /// <summary>
    /// Establishes the RabbitMQ topology and starts consuming, then runs for the lifetime of the
    /// host; returns only when <paramref name="stoppingToken"/> is canceled.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await ConnectAndConsumeAsync(stoppingToken);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Expected during graceful shutdown.
        }
    }

    /// <summary>
    /// Opens the connection and channel, declares the durable email + dead-letter queues, and
    /// registers the consumer. The broker often is not reachable the instant the worker starts
    /// (both come up together under docker-compose), so the initial connect is retried with a
    /// capped back-off rather than letting <see cref="ExecuteAsync"/> fault and stop the host.
    /// Once this returns, the client's automatic/topology recovery handles any later drop.
    /// </summary>
    private async Task ConnectAndConsumeAsync(CancellationToken stoppingToken)
    {
        var delay = ConnectRetryInitialDelay;
        var maxDelay = TimeSpan.FromSeconds(30);

        while (true)
        {
            stoppingToken.ThrowIfCancellationRequested();
            try
            {
                _connection = await connectionFactory.CreateConnectionAsync(stoppingToken);
                Channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);

                // Dead-letter queue first, so it exists before the main queue can route a rejected
                // message to it.
                await Channel.QueueDeclareAsync(
                    queue: QueueNames.EmailDeadLetter,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    cancellationToken: stoppingToken);

                // Arguments must match the Website publisher's declaration of the same queue exactly, or
                // whichever side declares second fails with PRECONDITION_FAILED. The policy dead-letters
                // reject-without-requeue messages to QueueNames.EmailDeadLetter.
                await Channel.QueueDeclareAsync(
                    queue: QueueNames.Email,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: QueueNames.CreateEmailQueueArguments(),
                    cancellationToken: stoppingToken);

                // Cap unacked messages held at the client so one slow SMTP send doesn't let the
                // broker push unbounded messages into this process's memory.
                await Channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 10, global: false, cancellationToken: stoppingToken);

                var consumer = new AsyncEventingBasicConsumer(Channel);
                consumer.ReceivedAsync += OnMessageReceivedAsync;

                _consumerTag = await Channel.BasicConsumeAsync(
                    queue: QueueNames.Email,
                    autoAck: false,
                    consumer: consumer,
                    cancellationToken: stoppingToken);

#pragma warning disable CA1873
                logger.LogInformation("Email consumer started, listening on queue '{Queue}'", QueueNames.Email);
#pragma warning restore CA1873
                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Shutdown was requested mid-connect (e.g. right after CreateConnectionAsync
                // succeeded but before the queue topology finished declaring). Tear down whatever
                // was opened, same as the generic catch below, so this doesn't leak an open
                // connection when ExecuteAsync's caller doesn't get a chance to clean up.
                if (Channel != null)
                {
                    await Channel.DisposeAsync();
                    Channel = null;
                }
                if (_connection != null)
                {
                    await _connection.DisposeAsync();
                    _connection = null;
                }
                throw;
            }
            catch (Exception ex)
            {
                // Tear down whatever half-opened so the next attempt starts from a clean slate.
                if (Channel != null)
                {
                    await Channel.DisposeAsync();
                    Channel = null;
                }
                if (_connection != null)
                {
                    await _connection.DisposeAsync();
                    _connection = null;
                }

                logger.LogWarning(ex, "Could not connect to RabbitMQ; retrying in {DelaySeconds}s", delay.TotalSeconds);
                await Task.Delay(delay, stoppingToken);
                delay = TimeSpan.FromSeconds(Math.Min(maxDelay.TotalSeconds, delay.TotalSeconds * 2));
            }
        }
    }

    /// <summary>
    /// Deserializes and processes one delivered message in a fresh DI scope, then acks it.
    /// A failure gets <see cref="MaxRetries"/> retries (via <see cref="RetryCountHeader"/>, our own
    /// counter — see its doc comment for why this isn't just <c>BasicDeliverEventArgs.Redelivered</c>)
    /// to survive a transient SMTP/network blip; once exhausted, the message is nacked without
    /// requeue so a poison message cannot block the queue forever — the queue's dead-letter policy
    /// then routes it to <see cref="QueueNames.EmailDeadLetter"/> for inspection instead of
    /// discarding it. A failure to ack a message that was already handled successfully is logged
    /// and left unacked rather than nacked, since nacking it would requeue and resend an email that
    /// was already delivered (there is no idempotency key to suppress the duplicate).
    /// </summary>
    internal async Task OnMessageReceivedAsync(object sender, BasicDeliverEventArgs ea)
    {
        // DeliveryTag tags every log line for this message even when deserialization fails before
        // a CorrelationId is known, and distinguishes a redelivery of the same message in the log.
        using (LogContext.PushProperty("DeliveryTag", ea.DeliveryTag))
        {
            _inFlight[ea.DeliveryTag] = 0;
            try
            {
                SendEmailMessage message = JsonSerializer.Deserialize<SendEmailMessage>(ea.Body.Span)
                    ?? throw new InvalidOperationException("Deserialized email message was null");

                // Threads the originating Website request's id through so this message's logs
                // can be traced back to the request that published it.
                using (LogContext.PushProperty("CorrelationId", message.CorrelationId))
                {
                    await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
                    var handler = scope.ServiceProvider.GetRequiredService<IEmailMessageHandler>();
                    await handler.HandleAsync(message, _sendCts.Token);
                }

                try
                {
                    await Channel!.BasicAckAsync(ea.DeliveryTag, multiple: false);
                }
                catch (Exception ackEx)
                {
                    // The email was already sent successfully by this point, so this catch must
                    // not fall through to the outer catch's requeue/nack logic below — doing so
                    // would resend an email that already went out.
                    logger.LogError(ackEx, "Failed to ack email message (delivery tag {DeliveryTag}) after a successful send; leaving it unacked", ea.DeliveryTag);
                }
            }
            catch (Exception ex)
            {
                // Give a message MaxRetries retries (our own counter, not the broker's Redelivered
                // flag -- see RetryCountHeader's doc comment) so a transient SMTP/network blip
                // doesn't permanently drop the email; only give up once that's exhausted, so a
                // genuinely poison message still can't block the queue forever.
                try
                {
                    int retryCount = GetRetryCount(ea.BasicProperties);
                    if (retryCount < MaxRetries)
                    {
                        logger.LogWarning(ex, "Failed to process email message (delivery tag {DeliveryTag}, retry {RetryCount}); republishing for retry", ea.DeliveryTag, retryCount);
                        // A plain nack(requeue:true) can't carry our incremented counter -- RabbitMQ
                        // requeues the original message unchanged, headers included. Publish a copy
                        // with RetryCountHeader bumped, then ack the original so it's this republished
                        // copy (not the original) that the broker redelivers next.
                        await RepublishWithIncrementedRetryAsync(ea, retryCount + 1);
                        await Channel!.BasicAckAsync(ea.DeliveryTag, multiple: false);
                    }
                    else
                    {
                        // requeue: false triggers the queue's dead-letter policy, so the message is
                        // routed to QueueNames.EmailDeadLetter rather than discarded.
                        logger.LogError(ex, "Failed to process email message (delivery tag {DeliveryTag}) after {RetryCount} retries; dead-lettering to '{DeadLetterQueue}'", ea.DeliveryTag, retryCount, QueueNames.EmailDeadLetter);
                        await Channel!.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false);
                    }
                }
                catch (Exception nackEx)
                {
                    // Republish/ack or nack failed (e.g. channel mid-recovery). The message will only
                    // come back via connection-level recovery rather than this class's documented
                    // retry/dead-letter policy, so at least surface that loudly instead of losing the
                    // exception silently.
                    logger.LogError(nackEx, "Failed to republish/ack or nack email message (delivery tag {DeliveryTag}) after a processing failure", ea.DeliveryTag);
                }
            }
            finally
            {
                _inFlight.TryRemove(ea.DeliveryTag, out _);
            }
        }
    }

    /// <summary>Reads <see cref="RetryCountHeader"/> off a delivery, defaulting to 0 (first attempt) when absent or unparsable.</summary>
    private static int GetRetryCount(IReadOnlyBasicProperties properties)
    {
        if (properties.Headers == null || !properties.Headers.TryGetValue(RetryCountHeader, out object? value) || value == null)
            return 0;

        return value switch
        {
            int i => i,
            long l => (int)l,
            byte[] bytes when int.TryParse(System.Text.Encoding.UTF8.GetString(bytes), out int parsed) => parsed,
            _ => 0
        };
    }

    /// <summary>
    /// Publishes a copy of <paramref name="ea"/>'s body back onto the "maroik.email" queue with
    /// <see cref="RetryCountHeader"/> set to <paramref name="nextRetryCount"/>. The caller acks the
    /// original delivery once this returns, so exactly one copy (this republished one) remains live
    /// -- the original is never itself requeued. Note: if this publish succeeds but the following
    /// ack then fails (e.g. the channel drops in between), both the original (redelivered via
    /// connection recovery) and this republished copy can end up live, risking a duplicate send on
    /// retry -- the same class of narrow, accepted race as a post-send ack failure elsewhere in this
    /// worker (see OnMessageReceivedAsync's own doc comment).
    /// </summary>
    private async Task RepublishWithIncrementedRetryAsync(BasicDeliverEventArgs ea, int nextRetryCount)
    {
        var headers = ea.BasicProperties.Headers != null
            ? new Dictionary<string, object?>(ea.BasicProperties.Headers)
            : [];
        headers[RetryCountHeader] = nextRetryCount;

        var properties = new BasicProperties(ea.BasicProperties) { Headers = headers };

        await Channel!.BasicPublishAsync(
            exchange: "",
            routingKey: QueueNames.Email,
            mandatory: false,
            basicProperties: properties,
            body: ea.Body);
    }

    /// <summary>
    /// Cancels the consumer so no new deliveries arrive, stops the base <see cref="BackgroundService"/>,
    /// waits (up to a bounded grace period or the host's own shutdown deadline, whichever comes first)
    /// for any already-in-flight messages to finish being acked/nacked, and then releases the RabbitMQ
    /// channel and connection. Disposing immediately would pull the channel out from under a handler
    /// that is still mid-send, causing its ack/nack to throw and the message to be redelivered and resent.
    /// </summary>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (Channel != null && _consumerTag != null)
        {
            try
            {
                await Channel.BasicCancelAsync(_consumerTag, noWait: false, cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to cancel RabbitMQ consumer '{ConsumerTag}' during shutdown", _consumerTag);
            }
        }

        await base.StopAsync(cancellationToken);

        using var drainCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        drainCts.CancelAfter(DrainTimeout);
        try
        {
            while (!_inFlight.IsEmpty)
                await Task.Delay(100, drainCts.Token);
        }
        catch (OperationCanceledException)
        {
            // Either the host's own shutdown deadline or the DrainTimeout deadline was reached. If a
            // send is still running at this point, cancel it via _sendCts (every HandleAsync call
            // is given its token) instead of disposing the channel straight out from under it below
            // -- that would throw from the handler's own ack/nack and force a redelivery/resend of
            // a send that may already have completed. Cancellation instead lets
            // OnMessageReceivedAsync's own catch nack the message cleanly. Give it a short, separate
            // window to actually unwind before disposing regardless.
            if (!_inFlight.IsEmpty)
            {
                logger.LogWarning(
                    "Shutdown drain deadline reached with {Count} message(s) still in flight; cancelling in-progress send(s)",
                    _inFlight.Count);
                await _sendCts.CancelAsync();

                using var unwindCts = new CancellationTokenSource(CancelUnwindTimeout);
                try
                {
                    while (!_inFlight.IsEmpty)
                        await Task.Delay(100, unwindCts.Token);
                }
                catch (OperationCanceledException)
                {
                    // Still not drained after the extra grace window; proceed to dispose regardless.
                    logger.LogWarning(
                        "{Count} send(s) did not stop within the unwind window; closing the channel anyway (unacknowledged messages will be redelivered)",
                        _inFlight.Count);
                }
            }
        }

        if (Channel != null)
            await Channel.DisposeAsync();
        if (_connection != null)
            await _connection.DisposeAsync();
        _sendCts.Dispose();
    }
}
