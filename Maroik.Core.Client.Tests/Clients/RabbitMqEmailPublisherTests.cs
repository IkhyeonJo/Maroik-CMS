using Maroik.Core.Client.Clients;
using Maroik.Core.Contract.Misc.Messaging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RabbitMQ.Client;

namespace Maroik.Core.Client.Tests.Clients;

/// <summary>
/// Unit tests for <see cref="RabbitMqEmailPublisher"/>.
/// Verifies lazy connection creation, connection and channel reuse while open, reconnection once
/// the cached connection drops, and the shape of the published AMQP message.
/// </summary>
public class RabbitMqEmailPublisherTests
{
    private readonly Mock<IConnectionFactory> _factory = new();
    private static readonly SendEmailMessage _message = new("to@test.com", "Subject", "Body", "corr-1");

    private static Mock<IChannel> MakeChannelMock()
    {
        var channel = new Mock<IChannel>();
        channel.Setup(c => c.IsOpen).Returns(true);
        channel.Setup(c => c.QueueDeclareAsync(
                It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(),
                It.IsAny<IDictionary<string, object?>>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((QueueDeclareOk)null!);
        return channel;
    }

    private static Mock<IConnection> MakeConnectionMock(Mock<IChannel> channel, bool isOpen) =>
        MakeConnectionMock(channel.Object, isOpen);

    private static Mock<IConnection> MakeConnectionMock(IChannel channel, bool isOpen)
    {
        var connection = new Mock<IConnection>();
        connection.Setup(c => c.IsOpen).Returns(isOpen);
        connection.Setup(c => c.CreateChannelAsync(It.IsAny<CreateChannelOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(channel);
        return connection;
    }

    // A short grace window so tests exercising the "connection dropped" path don't pay a real
    // multi-second sleep (the production default, sourced from ConnectionFactory.NetworkRecoveryInterval,
    // is 5s — see RabbitMqEmailPublisher's constructor).
    private static readonly TimeSpan _testRecoveryGraceWindow = TimeSpan.FromMilliseconds(20);

    private RabbitMqEmailPublisher CreateSut() =>
        new(_factory.Object, NullLogger<RabbitMqEmailPublisher>.Instance, _testRecoveryGraceWindow);

    /// <summary>Publish async no cached connection opens connection and publishes.</summary>
    [Fact]
    public async Task PublishAsync_NoCachedConnection_OpensConnectionAndPublishes()
    {
        var channel = MakeChannelMock();
        var connection = MakeConnectionMock(channel, isOpen: true);
        _factory.Setup(f => f.CreateConnectionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(connection.Object);

        await CreateSut().PublishAsync(_message, TestContext.Current.CancellationToken);

        _factory.Verify(f => f.CreateConnectionAsync(It.IsAny<CancellationToken>()), Times.Once);
        channel.Verify(c => c.BasicPublishAsync(
                "",
                QueueNames.Email,
                false,
                It.Is<BasicProperties>(p => p.Persistent == true),
                It.IsAny<ReadOnlyMemory<byte>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Regression test: without publisher confirmations enabled, BasicPublishAsync returns as
    /// soon as the message is locally buffered, so a dropped connection right after the write
    /// silently loses the message even though PublishAsync reported success. Verifies the channel
    /// is created with confirmations (and tracking) enabled so BasicPublishAsync actually waits
    /// for and validates the broker's ack.
    /// </summary>
    [Fact]
    public async Task PublishAsync_CreatesChannel_WithPublisherConfirmationsEnabled()
    {
        var channel = MakeChannelMock();
        var connection = MakeConnectionMock(channel, isOpen: true);
        _factory.Setup(f => f.CreateConnectionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(connection.Object);

        await CreateSut().PublishAsync(_message, TestContext.Current.CancellationToken);

        connection.Verify(c => c.CreateChannelAsync(
                It.Is<CreateChannelOptions>(o => o.PublisherConfirmationsEnabled && o.PublisherConfirmationTrackingEnabled),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>Publish async publishes message body as JSON.</summary>
    [Fact]
    public async Task PublishAsync_PublishesMessageBodyAsJson()
    {
        var channel = MakeChannelMock();
        var connection = MakeConnectionMock(channel, isOpen: true);
        _factory.Setup(f => f.CreateConnectionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(connection.Object);
        byte[]? capturedBody = null;
        channel.Setup(c => c.BasicPublishAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<BasicProperties>(),
                It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, bool, BasicProperties, ReadOnlyMemory<byte>, CancellationToken>(
                (_, _, _, _, body, _) => capturedBody = body.ToArray());

        await CreateSut().PublishAsync(_message, TestContext.Current.CancellationToken);

        var deserialized = System.Text.Json.JsonSerializer.Deserialize<SendEmailMessage>(capturedBody!);
        Assert.Equal(_message, deserialized);
    }

    /// <summary>Publish async connection stays open reuses connection across calls.</summary>
    [Fact]
    public async Task PublishAsync_ConnectionStaysOpen_ReusesConnectionAcrossCalls()
    {
        var channel = MakeChannelMock();
        var connection = MakeConnectionMock(channel, isOpen: true);
        _factory.Setup(f => f.CreateConnectionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(connection.Object);
        var sut = CreateSut();

        await sut.PublishAsync(_message, TestContext.Current.CancellationToken);
        await sut.PublishAsync(_message, TestContext.Current.CancellationToken);

        _factory.Verify(f => f.CreateConnectionAsync(It.IsAny<CancellationToken>()), Times.Once);
        channel.Verify(c => c.BasicPublishAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<BasicProperties>(),
                It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    /// <summary>
    /// Publish async, channel stays open: the channel (and its one-time queue declare) is reused
    /// across calls rather than a fresh channel being opened and the queue redeclared per publish.
    /// </summary>
    [Fact]
    public async Task PublishAsync_ChannelStaysOpen_ReusesChannelAndDeclaresQueueOnce()
    {
        var channel = MakeChannelMock();
        var connection = MakeConnectionMock(channel, isOpen: true);
        _factory.Setup(f => f.CreateConnectionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(connection.Object);
        var sut = CreateSut();

        await sut.PublishAsync(_message, TestContext.Current.CancellationToken);
        await sut.PublishAsync(_message, TestContext.Current.CancellationToken);

        connection.Verify(c => c.CreateChannelAsync(It.IsAny<CreateChannelOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        channel.Verify(c => c.QueueDeclareAsync(
                QueueNames.Email, true, false, false,
                It.IsAny<IDictionary<string, object?>>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Once);
        channel.Verify(c => c.BasicPublishAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<BasicProperties>(),
                It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    /// <summary>Publish async cached connection closed disposes it and opens a new one.</summary>
    [Fact]
    public async Task PublishAsync_CachedConnectionClosed_DisposesItAndOpensANewOne()
    {
        var channel1 = MakeChannelMock();
        var deadConnection = MakeConnectionMock(channel1, isOpen: false);
        var channel2 = MakeChannelMock();
        var freshConnection = MakeConnectionMock(channel2, isOpen: true);
        _factory.SetupSequence(f => f.CreateConnectionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(deadConnection.Object)
            .ReturnsAsync(freshConnection.Object);
        var sut = CreateSut();

        await sut.PublishAsync(_message, TestContext.Current.CancellationToken); // caches deadConnection (its own open state is irrelevant on creation)
        await sut.PublishAsync(_message, TestContext.Current.CancellationToken); // now sees deadConnection.IsOpen == false, must reconnect

        _factory.Verify(f => f.CreateConnectionAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
        deadConnection.Verify(c => c.DisposeAsync(), Times.Once);
        channel2.Verify(c => c.BasicPublishAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<BasicProperties>(),
                It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Verifies that when the cached connection is seen as closed but flips back to open by the
    /// time the short recovery grace window elapses (AutomaticRecoveryEnabled's own reconnection
    /// loop finishing first), GetChannelAsync reuses the same connection instead of disposing it
    /// and racing that recovery with a brand-new one.
    /// </summary>
    [Fact]
    public async Task PublishAsync_ConnectionRecoversDuringGraceWindow_ReusesConnection_WithoutReconnecting()
    {
        var channel1 = MakeChannelMock();
        var channel2 = MakeChannelMock();
        var connection = new Mock<IConnection>();
        // IsOpen is never read while _connection is still null (pattern-matching a null reference
        // short-circuits without touching the member), so the first real invocation happens on the
        // second publishes cached-fast-path check below.
        connection.SetupSequence(c => c.IsOpen)
            .Returns(false) // second publish: cached-fast-path check — looks dropped
            .Returns(false) // entering the "not open" branch
            .Returns(true) // re-checked after the grace-window delay: recovered
            .Returns(true); // final re-check before falling through to a fresh connection
        connection.SetupSequence(c => c.CreateChannelAsync(It.IsAny<CreateChannelOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(channel1.Object)
            .ReturnsAsync(channel2.Object);
        _factory.Setup(f => f.CreateConnectionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(connection.Object);
        var sut = CreateSut();

        await sut.PublishAsync(_message, TestContext.Current.CancellationToken); // caches the connection
        await sut.PublishAsync(_message, TestContext.Current.CancellationToken); // sees it "closed", waits, sees it recovered

        _factory.Verify(f => f.CreateConnectionAsync(It.IsAny<CancellationToken>()), Times.Once);
        connection.Verify(c => c.DisposeAsync(), Times.Never);
        channel2.Verify(c => c.BasicPublishAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<BasicProperties>(),
                It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Regression: while the broker is down, a failed connect attempt makes the publishes that follow
    /// within the grace window fail fast — they do not each queue behind the lock and pay another
    /// (multi-second) connect attempt, which stacked up request latency during an outage.
    /// </summary>
    [Fact]
    public async Task PublishAsync_AfterAFailedConnect_FailsFast_WithoutAnotherConnectAttempt()
    {
        _factory.Setup(f => f.CreateConnectionAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("broker down"));
        var sut = new RabbitMqEmailPublisher(_factory.Object, NullLogger<RabbitMqEmailPublisher>.Instance, TimeSpan.FromSeconds(30));

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.PublishAsync(_message, TestContext.Current.CancellationToken));
        var fastFail = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.PublishAsync(_message, TestContext.Current.CancellationToken));

        Assert.Contains("unavailable", fastFail.Message);
        _factory.Verify(f => f.CreateConnectionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Once the grace window has passed the publisher tries to connect again, and a success clears the failure.</summary>
    [Fact]
    public async Task PublishAsync_ReconnectsAfterTheGraceWindow_AndPublishes()
    {
        var channel = MakeChannelMock();
        var connection = MakeConnectionMock(channel, isOpen: true);
        _factory.SetupSequence(f => f.CreateConnectionAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("broker down"))
            .ReturnsAsync(connection.Object);
        var sut = new RabbitMqEmailPublisher(_factory.Object, NullLogger<RabbitMqEmailPublisher>.Instance, TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.PublishAsync(_message, TestContext.Current.CancellationToken));
        await Task.Delay(150, TestContext.Current.CancellationToken);
        await sut.PublishAsync(_message, TestContext.Current.CancellationToken);

        _factory.Verify(f => f.CreateConnectionAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
        channel.Verify(c => c.BasicPublishAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<BasicProperties>(),
                It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Regression: the recovery-grace-window fix must actually track whatever
    /// <c>NetworkRecoveryInterval</c> a real <see cref="ConnectionFactory"/> is configured with
    /// (defaulting to a fixed constant here previously either stalled every reconnect on a mismatch
    /// or, before that, was too short to ever observe a real recovery). Reflection reaches the
    /// private field directly since the grace window is otherwise only observable by actually
    /// waiting it out in real time.
    /// </summary>
    [Fact]
    public void Constructor_SourcesRecoveryGraceWindow_FromConnectionFactoryNetworkRecoveryInterval()
    {
        var realFactory = new ConnectionFactory { NetworkRecoveryInterval = TimeSpan.FromSeconds(42) };

        var sut = new RabbitMqEmailPublisher(realFactory, NullLogger<RabbitMqEmailPublisher>.Instance);

        var field = typeof(RabbitMqEmailPublisher).GetField(
            "_connectionRecoveryGraceWindow", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.Equal(TimeSpan.FromSeconds(42), (TimeSpan)field!.GetValue(sut)!);
    }

    /// <summary>Dispose async with open connection disposes connection.</summary>
    [Fact]
    public async Task DisposeAsync_WithOpenConnection_DisposesConnection()
    {
        var channel = MakeChannelMock();
        var connection = MakeConnectionMock(channel, isOpen: true);
        _factory.Setup(f => f.CreateConnectionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(connection.Object);
        var sut = CreateSut();
        await sut.PublishAsync(_message, TestContext.Current.CancellationToken);

        await sut.DisposeAsync();

        connection.Verify(c => c.DisposeAsync(), Times.Once);
    }

    /// <summary>Dispose async never published does not throw.</summary>
    [Fact]
    public async Task DisposeAsync_NeverPublished_DoesNotThrow()
    {
        var sut = CreateSut();

        await sut.DisposeAsync();

        _factory.Verify(f => f.CreateConnectionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Regression test: DisposeAsync must not hang forever if a PublishAsync call is stuck holding
    /// the publishing lock (e.g. a broker that never confirms, with the caller having passed
    /// CancellationToken.None). Verifies dispose completes within a bounded time instead of
    /// deadlocking application shutdown, and -- since the stuck publishing still owns them -- that it
    /// does NOT dispose the channel/connection out from under that still-running call.
    /// </summary>
    [Fact]
    public async Task DisposeAsync_DoesNotHangForever_WhenPublishIsStuckHoldingTheLock()
    {
        var channel = MakeChannelMock();
        var neverCompletes = new TaskCompletionSource();
        channel.Setup(c => c.BasicPublishAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<BasicProperties>(),
                It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()))
            .Returns(new ValueTask(neverCompletes.Task));
        var connection = MakeConnectionMock(channel, isOpen: true);
        _factory.Setup(f => f.CreateConnectionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(connection.Object);
        var sut = CreateSut();

        Task stuckPublish = sut.PublishAsync(_message, CancellationToken.None);

        Task disposeTask = sut.DisposeAsync().AsTask();
        Task completedFirst = await Task.WhenAny(disposeTask, Task.Delay(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));

        Assert.Same(disposeTask, completedFirst);
        channel.Verify(c => c.DisposeAsync(), Times.Never);
        connection.Verify(c => c.DisposeAsync(), Times.Never);

        neverCompletes.SetResult(); // unblock the stuck publishing so it doesn't leak past this test
        await stuckPublish;
    }
}
