using System.Text;
using System.Text.Json;
using Maroik.Core.Contract.Messages;
using Maroik.Worker.Interfaces;
using Maroik.Worker.Tests.Fixtures;
using Maroik.Worker.Workers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using RabbitMQ.Client;
// ReSharper disable ClassNeverInstantiated.Global

namespace Maroik.Worker.Tests.Workers;

/// <summary>
/// <see cref="EmailConsumerWorker"/> against a real RabbitMQ broker (Testcontainers; Docker required): the topology it
/// declares, and — end to end through the broker — the ack, delayed-retry and dead-letter decisions and the graceful drain.
/// </summary>
public class EmailConsumerWorkerBrokerTests(WorkerBrokerFixture broker) : IClassFixture<WorkerBrokerFixture>
{
    /// <summary>The message most tests publish.</summary>
    private static readonly SendEmailMessage _message = new("to@test.com", "Subject", "Body", "corr-7");
    /// <summary>The current test's cancellation token.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>A message handler whose behavior per call is scripted by the test.</summary>
    private sealed class ScriptedHandler(Func<SendEmailMessage, int, Task> onCall) : IEmailMessageHandler
    {
        /// <summary>Number of calls so far; read and written atomically.</summary>
        private int _calls;
        /// <summary>How many times <see cref="HandleAsync"/> has been called.</summary>
        public int Calls => Volatile.Read(ref _calls);
        /// <summary>Every message handed to the handler, in arrival order (lock it before reading while the worker runs).</summary>
        public List<SendEmailMessage> Received { get; } = [];

        /// <summary>Records the message, then runs the scripted behavior for this call number (1-based).</summary>
        public async Task HandleAsync(SendEmailMessage message, CancellationToken ct = default)
        {
            int n = Interlocked.Increment(ref _calls);
            lock (Received) Received.Add(message);
            await onCall(message, n);
        }
    }

    /// <summary>Short retry delays for the tests (one per retry), so a retried message comes back within a fraction of a second.</summary>
    private static readonly TimeSpan[] _shortRetryDelays = [.. Enumerable.Repeat(TimeSpan.FromMilliseconds(100), EmailConsumerWorker.MaxRetries)];

    /// <summary>A worker against the container, resolving <paramref name="handler"/>, with short retry and drain timings.</summary>
    private EmailConsumerWorker CreateWorker(ScriptedHandler handler, IReadOnlyList<TimeSpan>? retryDelays = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IEmailMessageHandler>(handler);
        var worker = new EmailConsumerWorker(broker.CreateFactory(), services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            Mock.Of<ILogger<EmailConsumerWorker>>())
        {
            ConnectRetryInitialDelay = TimeSpan.FromMilliseconds(100),
            DrainTimeout = TimeSpan.FromSeconds(10),
            RetryDelays = retryDelays ?? _shortRetryDelays,
        };
        return worker;
    }

    /// <summary>Declares both queues exactly as the worker does, then publishes <paramref name="body"/> (persistent) to the e-mail queue.</summary>
    private async Task PublishRawAsync(byte[] body, IDictionary<string, object?>? headers = null)
    {
        await using IConnection connection = await broker.CreateFactory().CreateConnectionAsync(Ct);
        await using IChannel channel = await connection.CreateChannelAsync(new CreateChannelOptions(true, true), Ct);
        // the worker creates the queues; publishing into the default exchange before that would be dropped, so declare identically first
        await channel.QueueDeclareAsync(QueueNames.EmailDeadLetter, durable: true, exclusive: false, autoDelete: false, cancellationToken: Ct);
        await channel.QueueDeclareAsync(QueueNames.Email, durable: true, exclusive: false, autoDelete: false, arguments: QueueNames.CreateEmailQueueArguments(), cancellationToken: Ct);
        await channel.BasicPublishAsync("", QueueNames.Email, mandatory: false, new BasicProperties { Persistent = true, Headers = headers }, body, Ct);
    }

    /// <summary>Publishes <paramref name="message"/> serialized as JSON.</summary>
    private Task PublishAsync(SendEmailMessage message) => PublishRawAsync(JsonSerializer.SerializeToUtf8Bytes(message));

    /// <summary>The number of ready messages in <paramref name="queue"/>; throws if the queue does not exist.</summary>
    private async Task<uint> CountAsync(string queue)
    {
        await using IConnection connection = await broker.CreateFactory().CreateConnectionAsync(Ct);
        await using IChannel channel = await connection.CreateChannelAsync(cancellationToken: Ct);
        return (await channel.QueueDeclarePassiveAsync(queue, Ct)).MessageCount;
    }

    /// <summary>Waits for and removes (auto-ack) one message from <paramref name="queue"/>, returning its body.</summary>
    private async Task<byte[]> TakeAsync(string queue)
    {
        await using IConnection connection = await broker.CreateFactory().CreateConnectionAsync(Ct);
        await using IChannel channel = await connection.CreateChannelAsync(cancellationToken: Ct);
        BasicGetResult got = await Eventually(async () => await channel.BasicGetAsync(queue, autoAck: true, Ct), r => r != null) ?? throw new TimeoutException(queue);
        return got.Body.ToArray();
    }

    /// <summary>Polls <paramref name="read"/> every 100 ms until <paramref name="done"/> holds or <paramref name="seconds"/> pass, returning the last value read.</summary>
    private static async Task<T?> Eventually<T>(Func<Task<T?>> read, Func<T?, bool> done, int seconds = 15)
    {
        T? value = default;
        for (var deadline = DateTime.UtcNow.AddSeconds(seconds); DateTime.UtcNow < deadline; await Task.Delay(100, Ct))
        {
            value = await read();
            if (done(value)) return value;
        }
        return value;
    }

    /// <summary>Polls <paramref name="condition"/> every 50 ms; throws <see cref="TimeoutException"/> if it is not met within <paramref name="seconds"/>.</summary>
    private static async Task WaitAsync(Func<bool> condition, int seconds = 15)
    {
        for (var deadline = DateTime.UtcNow.AddSeconds(seconds); DateTime.UtcNow < deadline; await Task.Delay(50, Ct))
            if (condition()) return;
        throw new TimeoutException("condition not reached in time");
    }

    /// <summary>Starting the worker declares the durable email queue (with the shared dead-letter arguments) and its durable dead-letter queue.</summary>
    [Fact]
    public async Task Start_DeclaresTheDurableTopology()
    {
        await broker.ResetQueuesAsync(Ct);
        using var worker = CreateWorker(new ScriptedHandler((_, _) => Task.CompletedTask));
        await worker.StartAsync(Ct);
        try
        {
            await Eventually(async () => await CountSafeAsync(QueueNames.Email), c => c != null);
            await using IConnection connection = await broker.CreateFactory().CreateConnectionAsync(Ct);
            await using IChannel channel = await connection.CreateChannelAsync(cancellationToken: Ct);
            // a re-declaration with the shared arguments is accepted, i.e. the worker declared exactly those
            await channel.QueueDeclareAsync(QueueNames.Email, durable: true, exclusive: false, autoDelete: false, arguments: QueueNames.CreateEmailQueueArguments(), cancellationToken: Ct);
            await channel.QueueDeclareAsync(QueueNames.EmailDeadLetter, durable: true, exclusive: false, autoDelete: false, cancellationToken: Ct);
            // each retry queue dead-letters an expired message back into the email queue
            for (int retry = 1; retry <= EmailConsumerWorker.MaxRetries; retry++)
                await channel.QueueDeclareAsync(EmailConsumerWorker.RetryQueueName(retry), durable: true, exclusive: false, autoDelete: false,
                    arguments: new Dictionary<string, object?> { ["x-dead-letter-exchange"] = "", ["x-dead-letter-routing-key"] = QueueNames.Email }, cancellationToken: Ct);
        }
        finally
        {
            await worker.StopAsync(Ct);
        }
    }

    /// <summary><see cref="CountAsync"/>, or <see langword="null"/> when the queue does not exist (yet).</summary>
    private async Task<uint?> CountSafeAsync(string queue)
    {
        try { return await CountAsync(queue); }
        catch (Exception) { return null; }
    }

    /// <summary>A message the handler processes is acked: the handler saw it once and nothing is left on either queue.</summary>
    [Fact]
    public async Task ASuccessfullyHandledMessage_IsAcked()
    {
        await broker.ResetQueuesAsync(Ct);
        var handler = new ScriptedHandler((_, _) => Task.CompletedTask);
        using var worker = CreateWorker(handler);
        await worker.StartAsync(Ct);
        try
        {
            await PublishAsync(_message);
            await WaitAsync(() => handler.Calls == 1);
            await Task.Delay(500, Ct); // room for any (wrong) redelivery
            Assert.Equal(1, handler.Calls);
            Assert.Equal(_message, handler.Received[0]);
        }
        finally
        {
            await worker.StopAsync(Ct);
        }
        Assert.Equal(0u, await CountAsync(QueueNames.Email));
        Assert.Equal(0u, await CountAsync(QueueNames.EmailDeadLetter));
    }

    /// <summary>A message whose first attempt fails is retried (through the first retry queue, with the retry header) and then succeeds — never dead-lettered.</summary>
    [Fact]
    public async Task AFirstFailure_IsRetriedOnce_ThenSucceeds()
    {
        await broker.ResetQueuesAsync(Ct);
        var handler = new ScriptedHandler((_, n) => n == 1 ? throw new InvalidOperationException("smtp blip") : Task.CompletedTask);
        using var worker = CreateWorker(handler);
        await worker.StartAsync(Ct);
        try
        {
            await PublishAsync(_message);
            await WaitAsync(() => handler.Calls == 2);
            await Task.Delay(500, Ct);
            Assert.Equal(2, handler.Calls);
        }
        finally
        {
            await worker.StopAsync(Ct);
        }
        Assert.Equal(0u, await CountAsync(QueueNames.Email));
        Assert.Equal(0u, await CountAsync(QueueNames.EmailDeadLetter));
    }

    /// <summary>
    /// A failed message is not handed straight back: it waits in the first retry queue for the first delay, and only then
    /// returns to the email queue and is retried.
    /// </summary>
    [Fact]
    public async Task AFailure_WaitsInTheRetryQueue_ForTheRetryDelay_BeforeItIsRetried()
    {
        await broker.ResetQueuesAsync(Ct);
        var handler = new ScriptedHandler((_, n) => n == 1 ? throw new InvalidOperationException("smtp down") : Task.CompletedTask);
        using var worker = CreateWorker(handler, [TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2)]);
        await worker.StartAsync(Ct);
        try
        {
            await PublishAsync(_message);
            await WaitAsync(() => handler.Calls == 1);
            await Task.Delay(500, Ct);
            Assert.Equal(1, handler.Calls);
            Assert.Equal(1u, await CountAsync(EmailConsumerWorker.RetryQueueName(1)));

            await WaitAsync(() => handler.Calls == 2);
        }
        finally
        {
            await worker.StopAsync(Ct);
        }
        Assert.Equal(0u, await CountAsync(EmailConsumerWorker.RetryQueueName(1)));
        Assert.Equal(0u, await CountAsync(QueueNames.EmailDeadLetter));
    }

    /// <summary>A message that keeps failing gets <see cref="EmailConsumerWorker.MaxRetries"/> retries and is then dead-lettered by the broker, body intact.</summary>
    [Fact]
    public async Task APoisonMessage_IsRetriedMaxRetriesTimes_ThenDeadLettered()
    {
        await broker.ResetQueuesAsync(Ct);
        var handler = new ScriptedHandler((_, _) => throw new InvalidOperationException("always fails"));
        using var worker = CreateWorker(handler);
        await worker.StartAsync(Ct);
        try
        {
            await PublishAsync(_message);
            await WaitAsync(() => handler.Calls == 1 + EmailConsumerWorker.MaxRetries);
            byte[] dead = await TakeAsync(QueueNames.EmailDeadLetter);
            Assert.Equal(_message, JsonSerializer.Deserialize<SendEmailMessage>(dead));
            Assert.Equal(1 + EmailConsumerWorker.MaxRetries, handler.Calls); // the first attempt + every retry
        }
        finally
        {
            await worker.StopAsync(Ct);
        }
        Assert.Equal(0u, await CountAsync(QueueNames.Email));
    }

    /// <summary>A body that is not a message at all never reaches the handler and ends up in the dead-letter queue untouched.</summary>
    [Fact]
    public async Task AnUnparsableBody_NeverReachesTheHandler_AndIsDeadLettered()
    {
        await broker.ResetQueuesAsync(Ct);
        var handler = new ScriptedHandler((_, _) => Task.CompletedTask);
        using var worker = CreateWorker(handler);
        await worker.StartAsync(Ct);
        try
        {
            await PublishRawAsync([
                .. "this is not json"u8
            ]);
            byte[] dead = await TakeAsync(QueueNames.EmailDeadLetter);
            Assert.Equal("this is not json", Encoding.UTF8.GetString(dead));
            Assert.Equal(0, handler.Calls);
        }
        finally
        {
            await worker.StopAsync(Ct);
        }
    }

    /// <summary>A delivery that already carries the maximum retry count is dead-lettered on its first failure here (no further retry).</summary>
    [Fact]
    public async Task AMessageAlreadyRetried_IsDeadLetteredOnItsNextFailure()
    {
        await broker.ResetQueuesAsync(Ct);
        var handler = new ScriptedHandler((_, _) => throw new InvalidOperationException("still failing"));
        using var worker = CreateWorker(handler);
        await worker.StartAsync(Ct);
        try
        {
            await PublishRawAsync(JsonSerializer.SerializeToUtf8Bytes(_message), new Dictionary<string, object?> { [EmailConsumerWorker.RetryCountHeader] = EmailConsumerWorker.MaxRetries });
            await TakeAsync(QueueNames.EmailDeadLetter);
            Assert.Equal(1, handler.Calls);
        }
        finally
        {
            await worker.StopAsync(Ct);
        }
    }

    /// <summary>Stopping the worker while a sent is in flight waits for it to finish and ack — the message is not redelivered (no duplicate mail).</summary>
    [Fact]
    public async Task StopAsync_WaitsForTheInFlightSend_ThenAcksIt()
    {
        await broker.ResetQueuesAsync(Ct);
        var release = new TaskCompletionSource();
        var handler = new ScriptedHandler((_, _) => release.Task);
        using var worker = CreateWorker(handler);
        await worker.StartAsync(Ct);
        await PublishAsync(_message);
        await WaitAsync(() => handler.Calls == 1);

        Task stopping = worker.StopAsync(Ct);
        await Task.Delay(500, Ct);
        Assert.False(stopping.IsCompleted, "the worker must wait for the send that is still running");

        release.SetResult();
        await stopping.WaitAsync(TimeSpan.FromSeconds(15), Ct);

        Assert.Equal(1, handler.Calls);
        Assert.Equal(0u, await CountAsync(QueueNames.Email)); // acked, not left for redelivery
        Assert.Equal(0u, await CountAsync(QueueNames.EmailDeadLetter));
    }

    /// <summary>
    /// Sent that ignores cancellation is not waited for indefinitely by the worker's own drain: after the drain window (in which it
    /// asks the send to cancel) and the extra unwind window it moves on to closing the channel, which itself only completes once that
    /// last send has returned. Shutdown therefore takes at least the two windows, and finishes as soon as the send does.
    /// </summary>
    [Fact]
    public async Task StopAsync_AfterTheDrainAndUnwindWindows_ClosesOnceTheStubbornSendReturns()
    {
        await broker.ResetQueuesAsync(Ct);
        var release = new TaskCompletionSource();
        var handler = new ScriptedHandler((_, _) => release.Task); // ignores the cancellation token on purpose
        using var worker = CreateWorker(handler);
        worker.DrainTimeout = TimeSpan.FromMilliseconds(300);
        worker.CancelUnwindTimeout = TimeSpan.FromMilliseconds(300);
        await worker.StartAsync(Ct);
        await PublishAsync(_message);
        await WaitAsync(() => handler.Calls == 1);

        var clock = System.Diagnostics.Stopwatch.StartNew();
        var stopping = worker.StopAsync(Ct);
        await Task.Delay(1000, Ct); // well past both windows: the worker has given up waiting and is closing the channel
        Assert.False(stopping.IsCompleted, "closing the channel waits for the send that is still running");
        release.SetResult();
        await stopping.WaitAsync(TimeSpan.FromSeconds(15), Ct);

        Assert.True(clock.ElapsedMilliseconds >= 1000);
        Assert.Equal(1, handler.Calls);
    }

    /// <summary>Messages published before the worker starts are consumed once it does (the durable queue held them).</summary>
    [Fact]
    public async Task MessagesPublishedBeforeStart_AreConsumedAfterStart()
    {
        await broker.ResetQueuesAsync(Ct);
        for (int i = 0; i < 3; i++) await PublishAsync(_message with { Subject = $"#{i}" });
        var handler = new ScriptedHandler((_, _) => Task.CompletedTask);
        using var worker = CreateWorker(handler);
        await worker.StartAsync(Ct);
        try
        {
            await WaitAsync(() => handler.Calls == 3);
            Assert.Equal(["#0", "#1", "#2"], handler.Received.Select(m => m.Subject));
        }
        finally
        {
            await worker.StopAsync(Ct);
        }
    }
}
