using Maroik.Core.Contract.Misc.Messaging;

namespace Maroik.Core.Contract.Interfaces;

/// <summary>
/// Publishes <see cref="SendEmailMessage"/> to a durable queue so the actual SMTP send
/// happens out-of-process (Maroik.Worker) instead of blocking the caller's request.
/// </summary>
public interface IEmailPublisher
{
    /// <summary>Enqueues an email for asynchronous delivery. Throws if the message could not be enqueued.</summary>
    Task PublishAsync(SendEmailMessage message, CancellationToken ct = default);
}
