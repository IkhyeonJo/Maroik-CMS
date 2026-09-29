using System.Text;
using System.Text.Json;
using Maroik.Core.Contract.Misc.Messaging;
using Maroik.Worker.Contracts;
using Maroik.Worker.Workers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Moq;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
// ReSharper disable AccessToDisposedClosure

namespace Maroik.Worker.Tests.Workers;

/// <summary>
/// Unit tests for <see cref="EmailConsumerWorker.OnMessageReceivedAsync"/> — the per-message
/// ack/nack decision that keeps a single poison message from blocking the "maroik.email" queue.
/// The channel is injected directly (via the internal <c>_channel</c> field, exposed to this
/// assembly through <see cref="System.Runtime.CompilerServices.InternalsVisibleToAttribute"/>)
/// so the connection/consume loop in <c>ExecuteAsync</c> doesn't need to run.
/// </summary>
public class EmailConsumerWorkerTests
{
    /// <summary>The message most tests deliver.</summary>
    private static readonly SendEmailMessage _message = new("to@test.com", "Subject", "Body", "corr-1");

    /// <summary>A scope factory whose scopes resolve <paramref name="handler"/>.</summary>
    private static IServiceScopeFactory BuildScopeFactory(IEmailMessageHandler handler)
    {
        var services = new ServiceCollection();
        services.AddSingleton(handler);
        return services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
    }

    /// <summary>A worker resolving <paramref name="handler"/> with a mocked channel already attached (returned in <paramref name="channel"/>).</summary>
    private static EmailConsumerWorker CreateSut(IEmailMessageHandler handler, out Mock<IChannel> channel, ILogger<EmailConsumerWorker>? logger = null)
    {
        var factory = new Mock<IConnectionFactory>();
        var worker = new EmailConsumerWorker(factory.Object, BuildScopeFactory(handler), logger ?? Mock.Of<ILogger<EmailConsumerWorker>>());
        channel = new Mock<IChannel>();
        worker.Channel = channel.Object;
        return worker;
    }

    /// <param name="redelivered"></param>
    /// <param name="retryCount">
    /// Sets <see cref="EmailConsumerWorker.RetryCountHeader"/> on the delivery's properties —
    /// this worker's own retry counter, tracked in a republished header rather than via
    /// <c>BasicDeliverEventArgs.Redelivered</c> (see that constant's doc comment for why).
    /// </param>
    /// <param name="body"></param>
    /// <param name="deliveryTag"></param>
    private static BasicDeliverEventArgs MakeDeliverArgs(byte[] body, ulong deliveryTag = 1, bool redelivered = false, int retryCount = 0)
    {
        var properties = new BasicProperties();
        if (retryCount > 0)
            properties.Headers = new Dictionary<string, object?> { [EmailConsumerWorker.RetryCountHeader] = retryCount };

        return new BasicDeliverEventArgs("consumer-tag", deliveryTag, redelivered, exchange: "", routingKey: QueueNames.Email,
            properties, body, CancellationToken.None);
    }

    /// <summary>On message received async valid message invokes handler and acks.</summary>
    [Fact]
    public async Task OnMessageReceivedAsync_ValidMessage_InvokesHandlerAndAcks()
    {
        var handler = new Mock<IEmailMessageHandler>();
        var worker = CreateSut(handler.Object, out var channel);
        byte[] body = JsonSerializer.SerializeToUtf8Bytes(_message);

        await worker.OnMessageReceivedAsync(this, MakeDeliverArgs(body, deliveryTag: 7));

        handler.Verify(h => h.HandleAsync(
            It.Is<SendEmailMessage>(m => m == _message), It.IsAny<CancellationToken>()), Times.Once);
        channel.Verify(c => c.BasicAckAsync(7, false, It.IsAny<CancellationToken>()), Times.Once);
        channel.Verify(c => c.BasicNackAsync(It.IsAny<ulong>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>On message received async invalid JSON on first delivery republishes for retry (ack, not nack) and never calls handler.</summary>
    [Fact]
    public async Task OnMessageReceivedAsync_InvalidJson_FirstDelivery_RepublishesForRetryAndNeverCallsHandler()
    {
        var handler = new Mock<IEmailMessageHandler>();
        var worker = CreateSut(handler.Object, out var channel);
        byte[] body = Encoding.UTF8.GetBytes("not valid json");
        BasicProperties? published = null;
        channel.Setup(c => c.BasicPublishAsync(
                "", QueueNames.Email, false, It.IsAny<BasicProperties>(), It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, bool, BasicProperties, ReadOnlyMemory<byte>, CancellationToken>((_, _, _, props, _, _) => published = props)
            .Returns(ValueTask.CompletedTask);

        await worker.OnMessageReceivedAsync(this, MakeDeliverArgs(body, deliveryTag: 3, redelivered: false));

        handler.Verify(h => h.HandleAsync(It.IsAny<SendEmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.NotNull(published);
        Assert.Equal(1, published!.Headers![EmailConsumerWorker.RetryCountHeader]);
        channel.Verify(c => c.BasicAckAsync(3, false, It.IsAny<CancellationToken>()), Times.Once);
        channel.Verify(c => c.BasicNackAsync(It.IsAny<ulong>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>On message received async invalid JSON already retried once nacks without requeue and never calls handler.</summary>
    [Fact]
    public async Task OnMessageReceivedAsync_InvalidJson_AlreadyRetried_NacksWithoutRequeueAndNeverCallsHandler()
    {
        var handler = new Mock<IEmailMessageHandler>();
        var worker = CreateSut(handler.Object, out var channel);
        byte[] body = Encoding.UTF8.GetBytes("not valid json");

        await worker.OnMessageReceivedAsync(this, MakeDeliverArgs(body, deliveryTag: 3, redelivered: true, retryCount: EmailConsumerWorker.MaxRetries));

        handler.Verify(h => h.HandleAsync(It.IsAny<SendEmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
        channel.Verify(c => c.BasicNackAsync(3, false, false, It.IsAny<CancellationToken>()), Times.Once);
        channel.Verify(c => c.BasicAckAsync(It.IsAny<ulong>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>On message received async null JSON body on first delivery republishes for retry.</summary>
    [Fact]
    public async Task OnMessageReceivedAsync_NullJsonBody_FirstDelivery_RepublishesForRetry()
    {
        var handler = new Mock<IEmailMessageHandler>();
        var worker = CreateSut(handler.Object, out var channel);
        byte[] body = "null"u8.ToArray();
        channel.Setup(c => c.BasicPublishAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<BasicProperties>(), It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        await worker.OnMessageReceivedAsync(this, MakeDeliverArgs(body, deliveryTag: 4, redelivered: false));

        channel.Verify(c => c.BasicAckAsync(4, false, It.IsAny<CancellationToken>()), Times.Once);
        channel.Verify(c => c.BasicNackAsync(It.IsAny<ulong>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>On message received async null JSON body already retried once nacks without requeue.</summary>
    [Fact]
    public async Task OnMessageReceivedAsync_NullJsonBody_AlreadyRetried_NacksWithoutRequeue()
    {
        var handler = new Mock<IEmailMessageHandler>();
        var worker = CreateSut(handler.Object, out var channel);
        byte[] body =
        [
            .. "null"u8
        ];

        await worker.OnMessageReceivedAsync(this, MakeDeliverArgs(body, deliveryTag: 4, redelivered: true, retryCount: EmailConsumerWorker.MaxRetries));

        channel.Verify(c => c.BasicNackAsync(4, false, false, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>On message received async handler throws on first delivery republishes for retry, giving a transient SMTP blip a second chance.</summary>
    [Fact]
    public async Task OnMessageReceivedAsync_HandlerThrows_FirstDelivery_RepublishesForRetry()
    {
        var handler = new Mock<IEmailMessageHandler>();
        handler.Setup(h => h.HandleAsync(It.IsAny<SendEmailMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SMTP down"));
        var worker = CreateSut(handler.Object, out var channel);
        byte[] body = JsonSerializer.SerializeToUtf8Bytes(_message);
        channel.Setup(c => c.BasicPublishAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<BasicProperties>(), It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        await worker.OnMessageReceivedAsync(this, MakeDeliverArgs(body, deliveryTag: 9, redelivered: false));

        channel.Verify(c => c.BasicAckAsync(9, false, It.IsAny<CancellationToken>()), Times.Once);
        channel.Verify(c => c.BasicNackAsync(It.IsAny<ulong>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>On message received async, when the handler succeeds but the ack itself throws, the message is neither nacked nor re-queued — it must not be resent since the email already went out.</summary>
    [Fact]
    public async Task OnMessageReceivedAsync_AckThrowsAfterSuccessfulHandle_DoesNotNack()
    {
        var handler = new Mock<IEmailMessageHandler>();
        var worker = CreateSut(handler.Object, out var channel);
        channel.Setup(c => c.BasicAckAsync(It.IsAny<ulong>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("channel closed"));
        byte[] body = JsonSerializer.SerializeToUtf8Bytes(_message);

        await worker.OnMessageReceivedAsync(this, MakeDeliverArgs(body, deliveryTag: 11));

        handler.Verify(h => h.HandleAsync(
            It.Is<SendEmailMessage>(m => m == _message), It.IsAny<CancellationToken>()), Times.Once);
        channel.Verify(c => c.BasicNackAsync(It.IsAny<ulong>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Regression test: if the failure-path <c>BasicNackAsync</c> call itself throws (e.g. the
    /// channel is mid-recovery), that exception must be caught rather than escaping
    /// <c>OnMessageReceivedAsync</c> unhandled -- the success-path ack already has this protection,
    /// and the nack path needs the same so a broker blip during error handling doesn't surface as
    /// an unobserved/unhandled exception from the message loop.
    /// </summary>
    [Fact]
    public async Task OnMessageReceivedAsync_NackThrowsAfterHandlerFailure_DoesNotThrow()
    {
        var handler = new Mock<IEmailMessageHandler>();
        handler.Setup(h => h.HandleAsync(It.IsAny<SendEmailMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SMTP down"));
        var worker = CreateSut(handler.Object, out var channel);
        channel.Setup(c => c.BasicNackAsync(It.IsAny<ulong>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("channel closed"));
        byte[] body = JsonSerializer.SerializeToUtf8Bytes(_message);

        var exception = await Record.ExceptionAsync(() =>
            worker.OnMessageReceivedAsync(this, MakeDeliverArgs(body, deliveryTag: 13, redelivered: true, retryCount: EmailConsumerWorker.MaxRetries)));

        Assert.Null(exception);
    }

    /// <summary>
    /// Regression test: if the retry-path <c>BasicPublishAsync</c> (or the ack that follows it)
    /// itself throws (e.g. the channel is mid-recovery), that exception must be caught rather than
    /// escaping <c>OnMessageReceivedAsync</c> unhandled.
    /// </summary>
    [Fact]
    public async Task OnMessageReceivedAsync_RepublishThrowsAfterHandlerFailure_DoesNotThrow()
    {
        var handler = new Mock<IEmailMessageHandler>();
        handler.Setup(h => h.HandleAsync(It.IsAny<SendEmailMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SMTP down"));
        var worker = CreateSut(handler.Object, out var channel);
        channel.Setup(c => c.BasicPublishAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<BasicProperties>(), It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("channel closed"));
        byte[] body = JsonSerializer.SerializeToUtf8Bytes(_message);

        var exception = await Record.ExceptionAsync(() =>
            worker.OnMessageReceivedAsync(this, MakeDeliverArgs(body, deliveryTag: 14, redelivered: false)));

        Assert.Null(exception);
    }

    /// <summary>
    /// Regression test: a send still running when the shutdown drain deadline is reached must be
    /// canceled (via the token every <c>HandleAsync</c> call is given), not left to race the
    /// channel disposal that follows <c>StopAsync</c>'s drain wait -- disposing the channel out
    /// from under a still-running send would throw from its own ack/nack and force a
    /// redelivery/resend of a send that may already have completed.
    /// </summary>
    [Fact]
    public async Task StopAsync_CancelsInFlightSend_WhenDrainDeadlineReached()
    {
        var handlerStarted = new TaskCompletionSource();
        var handler = new Mock<IEmailMessageHandler>();
        handler.Setup(h => h.HandleAsync(It.IsAny<SendEmailMessage>(), It.IsAny<CancellationToken>()))
            .Returns(async (SendEmailMessage _, CancellationToken ct) =>
            {
                handlerStarted.SetResult();
                await Task.Delay(Timeout.Infinite, ct);
            });
        var worker = CreateSut(handler.Object, out var channel);
        worker.DrainTimeout = TimeSpan.FromMilliseconds(50);
        worker.CancelUnwindTimeout = TimeSpan.FromMilliseconds(500);
        byte[] body = JsonSerializer.SerializeToUtf8Bytes(_message);
        channel.Setup(c => c.BasicPublishAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<BasicProperties>(), It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        Task received = worker.OnMessageReceivedAsync(this, MakeDeliverArgs(body, deliveryTag: 21, redelivered: false));
        await handlerStarted.Task;

        // StopAsync itself must return (the unwind wait isn't left hanging on the now-canceled
        // send), and OnMessageReceivedAsync's own catch -- not an unhandled exception -- must be
        // what observes the cancellation: it never rethrows, it republishes for a first retry
        // (this is a first-attempt failure, so it isn't dead-lettered yet) and acks the original.
        await worker.StopAsync(CancellationToken.None);
        await received;

        channel.Verify(c => c.BasicAckAsync(21, false, It.IsAny<CancellationToken>()), Times.Once);
        channel.Verify(c => c.BasicNackAsync(It.IsAny<ulong>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>On message received async handler throws after already being retried once nacks without requeue, so a genuinely poison message can't block the queue forever.</summary>
    [Fact]
    public async Task OnMessageReceivedAsync_HandlerThrows_AlreadyRetried_NacksWithoutRequeue()
    {
        var handler = new Mock<IEmailMessageHandler>();
        handler.Setup(h => h.HandleAsync(It.IsAny<SendEmailMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SMTP down"));
        var worker = CreateSut(handler.Object, out var channel);
        byte[] body = JsonSerializer.SerializeToUtf8Bytes(_message);

        await worker.OnMessageReceivedAsync(this, MakeDeliverArgs(body, deliveryTag: 9, redelivered: true, retryCount: EmailConsumerWorker.MaxRetries));

        channel.Verify(c => c.BasicNackAsync(9, false, false, It.IsAny<CancellationToken>()), Times.Once);
        channel.Verify(c => c.BasicAckAsync(It.IsAny<ulong>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Regression test: a message that the broker marks <c>Redelivered</c> for a reason unrelated
    /// to this worker's own retry decision (e.g. a connection drop right after this process started,
    /// before it ever attempted the message) must still get its full retry budget — the decision is
    /// keyed on <see cref="EmailConsumerWorker.RetryCountHeader"/>, not <c>Redelivered</c>.
    /// </summary>
    [Fact]
    public async Task OnMessageReceivedAsync_HandlerThrows_RedeliveredButNoRetryHeader_StillRepublishesForRetry()
    {
        var handler = new Mock<IEmailMessageHandler>();
        handler.Setup(h => h.HandleAsync(It.IsAny<SendEmailMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SMTP down"));
        var worker = CreateSut(handler.Object, out var channel);
        byte[] body = JsonSerializer.SerializeToUtf8Bytes(_message);
        channel.Setup(c => c.BasicPublishAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<BasicProperties>(), It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        // redelivered: true, but no retry header -- simulates a broker-level redelivery this
        // consumer never itself attempted (e.g. a fresh process after a crash).
        await worker.OnMessageReceivedAsync(this, MakeDeliverArgs(body, deliveryTag: 10, redelivered: true));

        channel.Verify(c => c.BasicAckAsync(10, false, It.IsAny<CancellationToken>()), Times.Once);
        channel.Verify(c => c.BasicNackAsync(It.IsAny<ulong>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // -- Connect / topology / consume loop -----------------------------------------------------

    /// <summary>BackgroundService starts <c>ExecuteAsync</c> without awaiting it, so wait (bounded) for the observable effect instead of sleeping.</summary>
    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!condition())
            await Task.Delay(5, timeout.Token);
    }

    /// <summary>Mocked connection factory, connection and channel that record the worker's startup calls.</summary>
    private sealed class BrokerFixture
    {
        /// <summary>Connection factory handed to the worker; returns <see cref="Connection"/>.</summary>
        public Mock<IConnectionFactory> Factory { get; } = new();
        /// <summary>Connection whose channel is <see cref="Channel"/>.</summary>
        public Mock<IConnection> Connection { get; } = new();
        /// <summary>Channel whose declare / QoS / consume calls are recorded.</summary>
        public Mock<IChannel> Channel { get; } = new();
        /// <summary>Channel calls in the order the worker made them (<c>declare:…</c>, <c>qos</c>, <c>consume</c>).</summary>
        public List<string> Calls { get; } = [];
        /// <summary>Arguments the email queue was declared with.</summary>
        public IDictionary<string, object?>? EmailQueueArguments { get; private set; }
        /// <summary>The last <c>BasicQos</c> settings.</summary>
        public (uint PrefetchSize, ushort PrefetchCount, bool Global)? Qos { get; private set; }
        /// <summary>The queue and auto-ack flag of the last <c>BasicConsume</c>.</summary>
        public (string Queue, bool AutoAck)? Consume { get; private set; }

        /// <summary>Wires the factory to return <see cref="Connection"/> and the connection to hand out <see cref="Channel"/>.</summary>
        public BrokerFixture()
        {
            Factory.Setup(f => f.CreateConnectionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Connection.Object);
            WireChannel(Connection, Channel);
        }

        /// <summary>Makes <paramref name="connection"/> return <paramref name="channel"/> and records each declare / QoS / consume call on it.</summary>
        private void WireChannel(Mock<IConnection> connection, Mock<IChannel> channel)
        {
            connection.Setup(c => c.CreateChannelAsync(It.IsAny<CreateChannelOptions?>(), It.IsAny<CancellationToken>())).ReturnsAsync(channel.Object);
            channel.Setup(c => c.QueueDeclareAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(),
                    It.IsAny<IDictionary<string, object?>?>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Callback((string queue, bool durable, bool exclusive, bool autoDelete, IDictionary<string, object?>? args, bool _, bool _, CancellationToken _) =>
                {
                    Calls.Add($"declare:{queue}:durable={durable}:exclusive={exclusive}:autoDelete={autoDelete}");
                    if (queue == QueueNames.Email) EmailQueueArguments = args;
                })
                .ReturnsAsync((string queue, bool _, bool _, bool _, IDictionary<string, object?>? _, bool _, bool _, CancellationToken _) => new QueueDeclareOk(queue, 0, 0));
            channel.Setup(c => c.BasicQosAsync(It.IsAny<uint>(), It.IsAny<ushort>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Callback((uint size, ushort count, bool global, CancellationToken _) => { Calls.Add("qos"); Qos = (size, count, global); })
                .Returns(Task.CompletedTask);
            channel.Setup(c => c.BasicConsumeAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(),
                    It.IsAny<IDictionary<string, object?>?>(), It.IsAny<IAsyncBasicConsumer>(), It.IsAny<CancellationToken>()))
                .Callback((string queue, bool autoAck, string _, bool _, bool _, IDictionary<string, object?>? _, IAsyncBasicConsumer _, CancellationToken _) =>
                {
                    Calls.Add("consume");
                    Consume = (queue, autoAck);
                })
                .ReturnsAsync("consumer-tag-1");
        }

        /// <summary>Creates a worker wired to this fake broker, with short retry and drain timeouts.</summary>
        public EmailConsumerWorker CreateWorker() => new(Factory.Object, BuildScopeFactory(Mock.Of<IEmailMessageHandler>()), Mock.Of<ILogger<EmailConsumerWorker>>())
        {
            ConnectRetryInitialDelay = TimeSpan.FromMilliseconds(10),
            DrainTimeout = TimeSpan.FromMilliseconds(50),
        };
    }

    /// <summary>
    /// Startup declares the dead-letter queue BEFORE the main queue (so the main queue's dead-letter
    /// policy has somewhere to route), with the exact arguments the Website publisher uses (a mismatch
    /// makes whichever side declares second fail with PRECONDITION_FAILED), caps unacked deliveries, and
    /// consumes with manual acknowledgements.
    /// </summary>
    [Fact]
    public async Task StartAsync_DeclaresDeadLetterQueueFirst_SetsPrefetch_AndConsumesWithManualAck()
    {
        var broker = new BrokerFixture();
        var worker = broker.CreateWorker();

        await worker.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            await WaitUntilAsync(() => broker.Consume != null);
            Assert.Equal(
            [
                $"declare:{QueueNames.EmailDeadLetter}:durable=True:exclusive=False:autoDelete=False",
                $"declare:{QueueNames.Email}:durable=True:exclusive=False:autoDelete=False",
                "qos",
                "consume",
            ], broker.Calls);
            Assert.Equal(QueueNames.CreateEmailQueueArguments().Keys.Order(), broker.EmailQueueArguments?.Keys.Order());
            foreach (var pair in QueueNames.CreateEmailQueueArguments())
                Assert.Equal(pair.Value, broker.EmailQueueArguments![pair.Key]);
            Assert.Equal((0u, (ushort)10, false), broker.Qos);
            Assert.Equal((QueueNames.Email, false), broker.Consume);
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }
    }

    /// <summary>The broker often is not reachable the instant the worker starts: the initial connect is retried instead of faulting the host.</summary>
    [Fact]
    public async Task StartAsync_RetriesTheInitialConnection_UntilTheBrokerIsReachable()
    {
        var broker = new BrokerFixture();
        int attempts = 0;
        broker.Factory.Setup(f => f.CreateConnectionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => ++attempts < 3 ? throw new InvalidOperationException("broker not ready") : broker.Connection.Object);
        var worker = broker.CreateWorker();

        await worker.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            await WaitUntilAsync(() => broker.Consume != null);
            Assert.Equal(3, attempts);
            Assert.Equal((QueueNames.Email, false), broker.Consume);
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }
    }

    /// <summary>A failure part-way through the topology setup tears down the half-opened channel and connection before the next attempt (no leak).</summary>
    [Fact]
    public async Task StartAsync_DisposesTheHalfOpenedChannelAndConnection_WhenTopologySetupFailsThenRetries()
    {
        var broker = new BrokerFixture();
        var failingConnection = new Mock<IConnection>();
        var failingChannel = new Mock<IChannel>();
        failingConnection.Setup(c => c.CreateChannelAsync(It.IsAny<CreateChannelOptions?>(), It.IsAny<CancellationToken>())).ReturnsAsync(failingChannel.Object);
        failingChannel.Setup(c => c.QueueDeclareAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(),
                It.IsAny<IDictionary<string, object?>?>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("declare failed"));
        broker.Factory.SetupSequence(f => f.CreateConnectionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(failingConnection.Object)
            .ReturnsAsync(broker.Connection.Object);
        var worker = broker.CreateWorker();

        await worker.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            await WaitUntilAsync(() => broker.Consume != null);
            failingChannel.Verify(c => c.DisposeAsync(), Times.Once);
            failingConnection.Verify(c => c.DisposeAsync(), Times.Once);
            Assert.Equal((QueueNames.Email, false), broker.Consume);
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }
    }

    /// <summary>Shutdown requested mid-connect closes what was already opened and stops (it neither retries nor leaks the connection).</summary>
    [Fact]
    public async Task StartAsync_DisposesWhatWasOpenedAndStops_WhenShutdownIsRequestedMidConnect()
    {
        var broker = new BrokerFixture();
        using var cts = new CancellationTokenSource();
        broker.Channel.Setup(c => c.QueueDeclareAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(),
                It.IsAny<IDictionary<string, object?>?>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                cts.Cancel();
                throw new OperationCanceledException(cts.Token);
            });
        var worker = broker.CreateWorker();

        await worker.StartAsync(cts.Token);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => worker.ExecuteTask!);

        broker.Channel.Verify(c => c.DisposeAsync(), Times.Once);
        broker.Connection.Verify(c => c.DisposeAsync(), Times.Once);
        broker.Factory.Verify(f => f.CreateConnectionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Stopping cancels the consumer first (no new deliveries), then releases the channel and connection.</summary>
    [Fact]
    public async Task StopAsync_CancelsTheConsumer_ThenDisposesTheChannelAndConnection()
    {
        var broker = new BrokerFixture();
        broker.Channel.Setup(c => c.BasicCancelAsync("consumer-tag-1", It.IsAny<bool>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var worker = broker.CreateWorker();
        await worker.StartAsync(TestContext.Current.CancellationToken);
        await WaitUntilAsync(() => broker.Consume != null);

        await worker.StopAsync(CancellationToken.None);

        broker.Channel.Verify(c => c.BasicCancelAsync("consumer-tag-1", false, It.IsAny<CancellationToken>()), Times.Once);
        broker.Channel.Verify(c => c.DisposeAsync(), Times.Once);
        broker.Connection.Verify(c => c.DisposeAsync(), Times.Once);
    }

    /// <summary>A failure cancelling the consumer during shutdown is logged, not thrown: the channel and connection are still released.</summary>
    [Fact]
    public async Task StopAsync_StillDisposesEverything_WhenCancellingTheConsumerFails()
    {
        var broker = new BrokerFixture();
        broker.Channel.Setup(c => c.BasicCancelAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("channel already closed"));
        var worker = broker.CreateWorker();
        await worker.StartAsync(TestContext.Current.CancellationToken);
        await WaitUntilAsync(() => broker.Consume != null);

        await worker.StopAsync(CancellationToken.None);

        broker.Channel.Verify(c => c.DisposeAsync(), Times.Once);
        broker.Connection.Verify(c => c.DisposeAsync(), Times.Once);
    }

    // -- Retry-count header parsing ---------------------------------------------------------------

    /// <summary>A delivery on the e-mail queue whose retry-count header holds <paramref name="headerValue"/> as-is.</summary>
    private static BasicDeliverEventArgs MakeDeliverArgsWithRawRetryHeader(byte[] body, object? headerValue, ulong deliveryTag) =>
        new("consumer-tag", deliveryTag, false, exchange: "", routingKey: QueueNames.Email,
            new BasicProperties { Headers = new Dictionary<string, object?> { [EmailConsumerWorker.RetryCountHeader] = headerValue } },
            body, CancellationToken.None);

    /// <summary>
    /// The retry counter survives however the broker hands the header back (an int we set, a long, or a
    /// UTF-8 byte string): a value already at the limit dead-letters instead of retrying forever.
    /// </summary>
    [Theory]
    [MemberData(nameof(ExhaustedRetryHeaders))]
    public async Task OnMessageReceivedAsync_DeadLettersInsteadOfRetrying_WhenTheRetryHeaderIsAtTheLimit(object headerValue)
    {
        var handler = new Mock<IEmailMessageHandler>();
        handler.Setup(h => h.HandleAsync(It.IsAny<SendEmailMessage>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("SMTP down"));
        var worker = CreateSut(handler.Object, out var channel);

        await worker.OnMessageReceivedAsync(this, MakeDeliverArgsWithRawRetryHeader(JsonSerializer.SerializeToUtf8Bytes(_message), headerValue, deliveryTag: 31));

        channel.Verify(c => c.BasicNackAsync(31, false, false, It.IsAny<CancellationToken>()), Times.Once);
        channel.Verify(c => c.BasicPublishAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<BasicProperties>(), It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Header values that were exhausted, in each representation the client may deliver.</summary>
    public static TheoryData<object> ExhaustedRetryHeaders =>
    [

        EmailConsumerWorker.MaxRetries,
        (long)EmailConsumerWorker.MaxRetries,
        Encoding.UTF8.GetBytes(EmailConsumerWorker.MaxRetries.ToString())
    ];

    /// <summary>A garbage or non-numeric header counts as "first attempt": the message still gets its retry.</summary>
    [Theory]
 #pragma warning disable xUnit1045
    [MemberData(nameof(UnusableRetryHeaders))]
 #pragma warning restore xUnit1045
    public async Task OnMessageReceivedAsync_TreatsAnUnusableRetryHeaderAsAFirstAttempt(object headerValue)
    {
        var handler = new Mock<IEmailMessageHandler>();
        handler.Setup(h => h.HandleAsync(It.IsAny<SendEmailMessage>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("SMTP down"));
        var worker = CreateSut(handler.Object, out var channel);
        channel.Setup(c => c.BasicPublishAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<BasicProperties>(), It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        await worker.OnMessageReceivedAsync(this, MakeDeliverArgsWithRawRetryHeader(JsonSerializer.SerializeToUtf8Bytes(_message), headerValue, deliveryTag: 32));

        channel.Verify(c => c.BasicAckAsync(32, false, It.IsAny<CancellationToken>()), Times.Once);
        channel.Verify(c => c.BasicNackAsync(It.IsAny<ulong>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Header values that must not count as an exhausted retry budget.</summary>
    public static TheoryData<object> UnusableRetryHeaders =>
    [

        "not-a-number",
        "abc"u8.ToArray(),
        3.5
    ];

    /// <summary>
    /// Send that ignores cancellation is still running when the unwind window ends: the worker moves on
    /// to closing the channel regardless. That message will be redelivered, so it is logged as a warning
    /// (the drain-deadline warning before it only says cancellation was requested).
    /// </summary>
    [Fact]
    public async Task StopAsync_LogsAWarning_WhenASendIgnoresCancellationPastTheUnwindWindow()
    {
        var handlerStarted = new TaskCompletionSource();
        var release = new TaskCompletionSource();
        var handler = new Mock<IEmailMessageHandler>();
        handler.Setup(h => h.HandleAsync(It.IsAny<SendEmailMessage>(), It.IsAny<CancellationToken>()))
            .Returns((SendEmailMessage _, CancellationToken _) =>
            {
                handlerStarted.SetResult();
                return release.Task; // ignores the token on purpose
            });
        var logger = new FakeLogger<EmailConsumerWorker>();
        var worker = CreateSut(handler.Object, out var channel, logger);
        worker.DrainTimeout = TimeSpan.FromMilliseconds(50);
        worker.CancelUnwindTimeout = TimeSpan.FromMilliseconds(100);
        channel.Setup(c => c.BasicPublishAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<BasicProperties>(), It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        byte[] body = JsonSerializer.SerializeToUtf8Bytes(_message);
        Task received = worker.OnMessageReceivedAsync(this, MakeDeliverArgs(body, deliveryTag: 31, redelivered: false));
        await handlerStarted.Task;

        await worker.StopAsync(CancellationToken.None);

        FakeLogRecord warning = Assert.Single(logger.Collector.GetSnapshot(),
            r => r.Level == LogLevel.Warning && r.Message.Contains("did not stop within the unwind window", StringComparison.Ordinal));
        Assert.Contains("1", warning.Message); // one send still in flight
        release.SetResult();
        await received;
    }
}
