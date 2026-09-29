using Maroik.Core.Contract.Misc.Messaging;

namespace Maroik.Worker.Contracts;

/// <summary>Handles a single dequeued <see cref="SendEmailMessage"/>.</summary>
public interface IEmailMessageHandler
{
    /// <summary>
    /// Sends the email via SMTP. On success, clears a previously recorded send failure on the
    /// corresponding account; on failure, marks the account's status as <c>FailToMailSent</c> so it
    /// is visible from the admin/account screens and throws <see cref="InvalidOperationException"/>
    /// (<c>SendMailAsync</c> reports a failed send as a result, not an exception), so the caller can
    /// decide whether to requeue or dead-letter the message.
    /// </summary>
    Task HandleAsync(SendEmailMessage message, CancellationToken ct = default);
}
