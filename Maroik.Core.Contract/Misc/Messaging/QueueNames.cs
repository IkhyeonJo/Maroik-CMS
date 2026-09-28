namespace Maroik.Core.Contract.Misc.Messaging;

/// <summary>Well-known RabbitMQ queue names shared between publishers and Maroik.Worker consumers.</summary>
public static class QueueNames
{
    /// <summary>Durable work queue carrying <see cref="SendEmailMessage"/> items.</summary>
    public const string Email = "maroik.email";

    /// <summary>
    /// Durable dead-letter queue for <see cref="Email"/>. A message that still fails after its one
    /// retry is rejected without requeue, and the broker routes it here (see
    /// <see cref="CreateEmailQueueArguments"/>) instead of discarding it — so a poison email can be
    /// inspected and re-driven by an operator rather than being lost silently.
    /// </summary>
    public const string EmailDeadLetter = "maroik.email.dead";

    /// <summary>
    /// Declaration arguments for the <see cref="Email"/> queue: reject-without-requeue messages are
    /// dead-lettered to <see cref="EmailDeadLetter"/> via the default exchange.
    /// <para>
    /// Every declarer of <see cref="Email"/> (the Website publisher and the Worker consumer) must
    /// pass these exact arguments — RabbitMQ fails a re-declaration whose arguments differ from the
    /// existing queue with <c>PRECONDITION_FAILED</c>. If a <see cref="Email"/> queue created by an
    /// older build (without a dead-letter policy) already exists on the broker, delete it once so it
    /// is recreated with this policy.
    /// </para>
    /// </summary>
    public static Dictionary<string, object?> CreateEmailQueueArguments() => new()
    {
        ["x-dead-letter-exchange"] = "",
        ["x-dead-letter-routing-key"] = EmailDeadLetter,
    };
}
