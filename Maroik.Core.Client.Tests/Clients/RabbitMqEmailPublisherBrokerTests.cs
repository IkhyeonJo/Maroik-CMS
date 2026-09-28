using System.Text.Json;
using Maroik.Core.Client.Clients;
using Maroik.Core.Client.Tests.Infrastructure;
using Maroik.Core.Contract.Misc.Messaging;
using Microsoft.Extensions.Logging;
using Moq;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace Maroik.Core.Client.Tests.Clients;

/// <summary>
/// <see cref="RabbitMqEmailPublisher"/> against a real RabbitMQ broker (Testcontainers; Docker required): what actually
/// lands on the queue, that the queue is declared the way the worker declares it, that a rejected message is
/// dead-lettered by the broker, and that publishing survives the broker dropping the connection.
/// </summary>
public class RabbitMqEmailPublisherBrokerTests(RabbitMqBrokerFixture broker) : IClassFixture<RabbitMqBrokerFixture>
{
    private static readonly SendEmailMessage _message = new("to@test.com", "Subject", "<p>Body</p>", "corr-42");

    private RabbitMqEmailPublisher CreatePublisher(TimeSpan? grace = null) =>
        new(broker.CreateFactory(), Mock.Of<ILogger<RabbitMqEmailPublisher>>(), grace ?? TimeSpan.FromMilliseconds(300));

    private async Task<(IConnection Connection, IChannel Channel)> OpenAsync()
    {
        IConnection connection = await broker.CreateFactory().CreateConnectionAsync(TestContext.Current.CancellationToken);
        return (connection, await connection.CreateChannelAsync(cancellationToken: TestContext.Current.CancellationToken));
    }

    private static async Task<BasicGetResult> GetAsync(IChannel channel, string queue)
    {
        for (int i = 0; i < 50; i++)
        {
            BasicGetResult? got = await channel.BasicGetAsync(queue, autoAck: false, TestContext.Current.CancellationToken);
            if (got != null) return got;
            await Task.Delay(100, TestContext.Current.CancellationToken);
        }
        throw new TimeoutException($"nothing arrived on '{queue}'");
    }

    /// <summary>The published message is on the durable queue as persistent JSON, exactly as the worker will read it.</summary>
    [Fact]
    public async Task PublishAsync_PutsAPersistentJsonMessageOnTheEmailQueue()
    {
        await broker.ResetQueuesAsync(TestContext.Current.CancellationToken);
        await using var publisher = CreatePublisher();

        await publisher.PublishAsync(_message, TestContext.Current.CancellationToken);

        var (connection, channel) = await OpenAsync();
        await using (connection) await using (channel)
        {
            BasicGetResult got = await GetAsync(channel, QueueNames.Email);
            Assert.Equal(_message, JsonSerializer.Deserialize<SendEmailMessage>(got.Body.Span));
            Assert.Equal(DeliveryModes.Persistent, got.BasicProperties.DeliveryMode);
            await channel.BasicAckAsync(got.DeliveryTag, false, TestContext.Current.CancellationToken);
        }
    }

    /// <summary>Messages published one after another arrive in order, through one reused connection and channel.</summary>
    [Fact]
    public async Task PublishAsync_ManyMessages_ArriveInOrder()
    {
        await broker.ResetQueuesAsync(TestContext.Current.CancellationToken);
        await using var publisher = CreatePublisher();

        for (int i = 0; i < 10; i++)
            await publisher.PublishAsync(_message with { Subject = $"#{i}" }, TestContext.Current.CancellationToken);

        var (connection, channel) = await OpenAsync();
        await using (connection) await using (channel)
        {
            var subjects = new List<string>();
            for (int i = 0; i < 10; i++)
            {
                BasicGetResult got = await GetAsync(channel, QueueNames.Email);
                subjects.Add(JsonSerializer.Deserialize<SendEmailMessage>(got.Body.Span)!.Subject);
                await channel.BasicAckAsync(got.DeliveryTag, false, TestContext.Current.CancellationToken);
            }
            Assert.Equal(Enumerable.Range(0, 10).Select(i => $"#{i}"), subjects);
        }
    }

    /// <summary>
    /// The publisher declares the queue with the shared arguments, so the worker's identical declaration is accepted —
    /// while a declaration without the dead-letter policy is refused by the broker (PRECONDITION_FAILED).
    /// </summary>
    [Fact]
    public async Task PublishAsync_DeclaresTheQueueWithTheSharedArguments()
    {
        await broker.ResetQueuesAsync(TestContext.Current.CancellationToken);
        await using var publisher = CreatePublisher();
        await publisher.PublishAsync(_message, TestContext.Current.CancellationToken);

        var (connection, channel) = await OpenAsync();
        await using (connection) await using (channel)
        {
            QueueDeclareOk same = await channel.QueueDeclareAsync(QueueNames.Email, durable: true, exclusive: false, autoDelete: false,
                arguments: QueueNames.CreateEmailQueueArguments(), cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal(1u, same.MessageCount);
        }

        var (connection2, channel2) = await OpenAsync();
        await using (connection2) await using (channel2)
        {
            var refused = await Assert.ThrowsAsync<OperationInterruptedException>(() => channel2.QueueDeclareAsync(
                QueueNames.Email, durable: true, exclusive: false, autoDelete: false, arguments: null, cancellationToken: TestContext.Current.CancellationToken));
            Assert.Equal((ushort)406, refused.ShutdownReason!.ReplyCode); // PRECONDITION_FAILED
        }
    }

    /// <summary>A message the consumer rejects without requeue is routed by the broker to the dead-letter queue, not lost.</summary>
    [Fact]
    public async Task ARejectedMessage_IsDeadLetteredByTheBroker()
    {
        await broker.ResetQueuesAsync(TestContext.Current.CancellationToken);
        await using var publisher = CreatePublisher();
        await publisher.PublishAsync(_message, TestContext.Current.CancellationToken);

        var (connection, channel) = await OpenAsync();
        await using (connection) await using (channel)
        {
            // the dead-letter queue must exist for the broker to route into it (the worker declares it first)
            await channel.QueueDeclareAsync(QueueNames.EmailDeadLetter, durable: true, exclusive: false, autoDelete: false, cancellationToken: TestContext.Current.CancellationToken);

            BasicGetResult got = await GetAsync(channel, QueueNames.Email);
            await channel.BasicNackAsync(got.DeliveryTag, multiple: false, requeue: false, TestContext.Current.CancellationToken);

            BasicGetResult dead = await GetAsync(channel, QueueNames.EmailDeadLetter);
            Assert.Equal(_message, JsonSerializer.Deserialize<SendEmailMessage>(dead.Body.Span));
        }
    }

    /// <summary>After the broker drops the publisher's connection, the next publish still gets through (recovery or a fresh connection).</summary>
    [Fact]
    public async Task PublishAsync_AfterTheBrokerDropsTheConnection_StillDelivers()
    {
        await broker.ResetQueuesAsync(TestContext.Current.CancellationToken);
        await using var publisher = CreatePublisher();
        await publisher.PublishAsync(_message with { Subject = "before" }, TestContext.Current.CancellationToken);

        await broker.CloseAllConnectionsAsync(TestContext.Current.CancellationToken);

        Exception? last = null;
        for (int attempt = 0; attempt < 20; attempt++)
        {
            try
            {
                await publisher.PublishAsync(_message with { Subject = "after" }, TestContext.Current.CancellationToken);
                last = null;
                break;
            }
            catch (Exception ex) // a publishing inside the fail-fast window right after a failed reconnect is allowed to throw; a later one must succeed
            {
                last = ex;
                await Task.Delay(300, TestContext.Current.CancellationToken);
            }
        }
        Assert.Null(last);

        var (connection, channel) = await OpenAsync();
        await using (connection) await using (channel)
        {
            var subjects = new List<string>();
            for (int i = 0; i < 2; i++)
            {
                BasicGetResult got = await GetAsync(channel, QueueNames.Email);
                subjects.Add(JsonSerializer.Deserialize<SendEmailMessage>(got.Body.Span)!.Subject);
                await channel.BasicAckAsync(got.DeliveryTag, false, TestContext.Current.CancellationToken);
            }
            Assert.Equal(["before", "after"], subjects);
        }
    }
}
