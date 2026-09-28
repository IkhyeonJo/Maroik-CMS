using Maroik.Core.Contract.Misc.Messaging;

namespace Maroik.Worker.Contracts;

/// <summary>Handles a single dequeued <see cref="SendEmailMessage"/>.</summary>
public interface IEmailMessageHandler
{
    /// <summary>
    /// Sends the email via SMTP. On failure, marks the corresponding account's status
    /// as <c>FailToMailSent</c> so it is visible from the admin/account screens.
    /// Throws <see cref="InvalidOperationException"/> whenever send did not succeed —
    /// whether SMTP itself threw, or <c>SendMailAsync</c> merely returned a failure result —
    /// so the caller can decide whether to requeue or dead-letter the message.
    /// </summary>
    Task HandleAsync(SendEmailMessage message, CancellationToken ct = default);
}
