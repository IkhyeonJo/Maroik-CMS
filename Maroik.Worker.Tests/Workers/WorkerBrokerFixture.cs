using Maroik.Core.Contract.Messages;
using Maroik.Worker.Workers;
using RabbitMQ.Client;
using Testcontainers.RabbitMq;

namespace Maroik.Worker.Tests.Workers;

/// <summary>One throwaway RabbitMQ 4 container for the class (xUnit class fixture); each test resets the two queues first.</summary>
public sealed class WorkerBrokerFixture : IAsyncLifetime
{
    /// <summary>The RabbitMQ 4 container.</summary>
    private readonly RabbitMqContainer _broker = new RabbitMqBuilder("rabbitmq:4-alpine").Build();

    /// <summary>The broker's AMQP connection string (guest login).</summary>
    public string ConnectionString => _broker.GetConnectionString();

    /// <summary>A connection factory for the broker; automatic recovery is on, as in production.</summary>
    public ConnectionFactory CreateFactory() => new() { Uri = new Uri(ConnectionString), AutomaticRecoveryEnabled = true };

    /// <summary>Starts the broker.</summary>
    public async ValueTask InitializeAsync() => await _broker.StartAsync();

    /// <summary>Stops and removes the broker.</summary>
    public async ValueTask DisposeAsync() => await _broker.DisposeAsync();

    /// <summary>Deletes the email queue, its retry queues and its dead-letter queue (if they exist).</summary>
    public async Task ResetQueuesAsync(CancellationToken ct)
    {
        await using IConnection connection = await CreateFactory().CreateConnectionAsync(ct);
        await using IChannel channel = await connection.CreateChannelAsync(cancellationToken: ct);
        await channel.QueueDeleteAsync(QueueNames.Email, ifUnused: false, ifEmpty: false, cancellationToken: ct);
        await channel.QueueDeleteAsync(QueueNames.EmailDeadLetter, ifUnused: false, ifEmpty: false, cancellationToken: ct);
        for (int retry = 1; retry <= EmailConsumerWorker.MaxRetries; retry++)
            await channel.QueueDeleteAsync(EmailConsumerWorker.RetryQueueName(retry), ifUnused: false, ifEmpty: false, cancellationToken: ct);
    }
}
