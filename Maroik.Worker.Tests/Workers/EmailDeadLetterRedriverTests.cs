using System.Text;
using Maroik.Core.Contract.Misc.Messaging;
using Maroik.Worker.Workers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using RabbitMQ.Client;

namespace Maroik.Worker.Tests.Workers;

/// <summary>
/// <see cref="EmailDeadLetterRedriver"/> against a real RabbitMQ broker (Testcontainers; Docker required): an operator's
/// re-drive moves the dead-lettered e-mails back into the e-mail queue with a fresh retry budget, once each.
/// </summary>
public class EmailDeadLetterRedriverTests(WorkerBrokerFixture broker) : IClassFixture<WorkerBrokerFixture>
{
    /// <summary>The current test's cancellation token.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Declares the e-mail queue and its dead-letter queue as the worker does, and publishes <paramref name="bodies"/> straight to the dead-letter queue.</summary>
    private async Task DeadLetterAsync(params string[] bodies)
    {
        await using IConnection connection = await broker.CreateFactory().CreateConnectionAsync(Ct);
        await using IChannel channel = await connection.CreateChannelAsync(new CreateChannelOptions(true, true), Ct);
        await channel.QueueDeclareAsync(QueueNames.EmailDeadLetter, durable: true, exclusive: false, autoDelete: false, cancellationToken: Ct);
        await channel.QueueDeclareAsync(QueueNames.Email, durable: true, exclusive: false, autoDelete: false, arguments: QueueNames.CreateEmailQueueArguments(), cancellationToken: Ct);
        foreach (string body in bodies)
        {
            var headers = new Dictionary<string, object?>
            {
                [EmailConsumerWorker.RetryCountHeader] = EmailConsumerWorker.MaxRetries,
                ["x-death"] = new List<object?> { new Dictionary<string, object?> { ["queue"] = QueueNames.Email } },
                ["x-first-death-queue"] = QueueNames.Email,
                ["x-maroik-other"] = "kept",
            };
            await channel.BasicPublishAsync("", QueueNames.EmailDeadLetter, mandatory: false,
                new BasicProperties { Persistent = true, Headers = headers }, Encoding.UTF8.GetBytes(body), Ct);
        }
    }

    /// <summary>Every message left in <paramref name="queue"/>, taken off it (auto-ack).</summary>
    private async Task<List<BasicGetResult>> DrainAsync(string queue)
    {
        await using IConnection connection = await broker.CreateFactory().CreateConnectionAsync(Ct);
        await using IChannel channel = await connection.CreateChannelAsync(cancellationToken: Ct);
        List<BasicGetResult> taken = [];
        while (await channel.BasicGetAsync(queue, autoAck: true, Ct) is { } got) taken.Add(got);
        return taken;
    }

    /// <summary>
    /// Each dead letter goes back to the e-mail queue, body and other headers intact, without the retry counter or the broker's
    /// death records — so the worker gives it the full retry budget again. The dead-letter queue ends up empty and the count is
    /// logged at Information.
    /// </summary>
    [Fact]
    public async Task RedriveAsync_MovesEveryDeadLetter_BackToTheEmailQueue_WithAFreshRetryBudget()
    {
        await broker.ResetQueuesAsync(Ct);
        await DeadLetterAsync("first", "second");
        var logger = new FakeLogger<EmailDeadLetterRedriver>();

        int moved = await new EmailDeadLetterRedriver(broker.CreateFactory(), logger).RedriveAsync(Ct);

        Assert.Equal(2, moved);
        Assert.Empty(await DrainAsync(QueueNames.EmailDeadLetter));
        List<BasicGetResult> back = await DrainAsync(QueueNames.Email);
        Assert.Equal(["first", "second"], back.Select(m => Encoding.UTF8.GetString(m.Body.Span)));
        Assert.All(back, m =>
        {
            IDictionary<string, object?> headers = m.BasicProperties.Headers!;
            Assert.False(headers.ContainsKey(EmailConsumerWorker.RetryCountHeader));
            Assert.False(headers.ContainsKey("x-death"));
            Assert.False(headers.ContainsKey("x-first-death-queue"));
            Assert.Equal("kept", Encoding.UTF8.GetString(Assert.IsType<byte[]>(headers["x-maroik-other"])));
            Assert.True(m.BasicProperties.Persistent);
        });
        FakeLogRecord log = Assert.Single(logger.Collector.GetSnapshot(), r => r.Level == LogLevel.Information);
        Assert.Contains("2", log.Message);
        Assert.Contains(QueueNames.EmailDeadLetter, log.Message);
    }

    /// <summary>An empty dead-letter queue is a no-op that reports zero.</summary>
    [Fact]
    public async Task RedriveAsync_ReportsZero_WhenNothingIsDeadLettered()
    {
        await broker.ResetQueuesAsync(Ct);
        await DeadLetterAsync();

        int moved = await new EmailDeadLetterRedriver(broker.CreateFactory(), new FakeLogger<EmailDeadLetterRedriver>()).RedriveAsync(Ct);

        Assert.Equal(0, moved);
        Assert.Empty(await DrainAsync(QueueNames.Email));
    }
}
