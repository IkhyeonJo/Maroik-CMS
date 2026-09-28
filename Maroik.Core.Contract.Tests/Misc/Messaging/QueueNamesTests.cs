using Maroik.Core.Contract.Misc.Messaging;

namespace Maroik.Core.Contract.Tests.Misc.Messaging;

/// <summary>
/// Unit tests for <see cref="QueueNames.CreateEmailQueueArguments"/>. Every declarer of the
/// <see cref="QueueNames.Email"/> queue (the Website publisher and the Worker consumer) must pass
/// these exact arguments -- RabbitMQ rejects a re-declaration whose arguments differ from the
/// existing queue with <c>PRECONDITION_FAILED</c>, so a typo'd key/value here would break the
/// broker connection for the whole app, not just fail a test.
/// </summary>
public class QueueNamesTests
{
    /// <summary>The dead-letter exchange is the default (unnamed) exchange.</summary>
    [Fact]
    public void CreateEmailQueueArguments_UsesTheDefaultExchange()
    {
        var args = QueueNames.CreateEmailQueueArguments();

        Assert.Equal("", args["x-dead-letter-exchange"]);
    }

    /// <summary>The dead-letter routing key points at the dead-letter queue name.</summary>
    [Fact]
    public void CreateEmailQueueArguments_RoutesToTheDeadLetterQueue()
    {
        var args = QueueNames.CreateEmailQueueArguments();

        Assert.Equal(QueueNames.EmailDeadLetter, args["x-dead-letter-routing-key"]);
    }

    /// <summary>Only the two expected arguments are present -- an extra/renamed key would also trip RabbitMQ's PRECONDITION_FAILED re-declaration check.</summary>
    [Fact]
    public void CreateEmailQueueArguments_HasExactlyTheExpectedKeys()
    {
        var args = QueueNames.CreateEmailQueueArguments();

        Assert.Equal(["x-dead-letter-exchange", "x-dead-letter-routing-key"], args.Keys.OrderBy(k => k));
    }

    /// <summary>The main and dead-letter queue names are distinct, non-blank, well-known constants.</summary>
    [Fact]
    public void QueueNames_AreDistinctAndNonBlank()
    {
        Assert.False(string.IsNullOrWhiteSpace(QueueNames.Email));
        Assert.False(string.IsNullOrWhiteSpace(QueueNames.EmailDeadLetter));
        Assert.NotEqual(QueueNames.Email, QueueNames.EmailDeadLetter);
    }
}
