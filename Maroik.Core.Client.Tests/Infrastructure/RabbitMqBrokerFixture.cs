using RabbitMQ.Client;
using Testcontainers.RabbitMq;

namespace Maroik.Core.Client.Tests.Infrastructure;

/// <summary>
/// One throwaway RabbitMQ 4 container for a test class (xUnit class fixture). Tests share it, so each starts by
/// calling <see cref="ResetQueuesAsync"/> to delete the two well-known queues and get a clean slate.
/// </summary>
// ReSharper disable once ClassNeverInstantiated.Global
public sealed class RabbitMqBrokerFixture : IAsyncLifetime
{
    /// <summary>The throwaway RabbitMQ broker container shared by the assembly's tests.</summary>
    private readonly RabbitMqContainer _broker = new RabbitMqBuilder("rabbitmq:4-alpine").Build();

    /// <summary>The broker's AMQP connection string (guest login).</summary>
    private string ConnectionString => _broker.GetConnectionString();

    /// <summary>A connection factory for the broker; automatic recovery is on, as in production.</summary>
    public ConnectionFactory CreateFactory() => new() { Uri = new Uri(ConnectionString), AutomaticRecoveryEnabled = true };

    /// <summary>Starts the broker.</summary>
    public async ValueTask InitializeAsync() => await _broker.StartAsync();

    /// <summary>Stops and removes the broker.</summary>
    public async ValueTask DisposeAsync() => await _broker.DisposeAsync();

    /// <summary>Deletes the email queue and its dead-letter queue (if they exist).</summary>
    public async Task ResetQueuesAsync(CancellationToken ct)
    {
        await using IConnection connection = await CreateFactory().CreateConnectionAsync(ct);
        await using IChannel channel = await connection.CreateChannelAsync(cancellationToken: ct);
        await channel.QueueDeleteAsync("maroik.email", ifUnused: false, ifEmpty: false, cancellationToken: ct);
        await channel.QueueDeleteAsync("maroik.email.dead", ifUnused: false, ifEmpty: false, cancellationToken: ct);
    }

    /// <summary>Force-closes every client connection on the broker, as a network blip or broker restart would.</summary>
    public async Task CloseAllConnectionsAsync(CancellationToken ct)
    {
        var result = await _broker.ExecAsync(["rabbitmqctl", "close_all_connections", "test: simulated drop"], ct);
        Assert.Equal(0, result.ExitCode);
    }
}
