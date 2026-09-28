namespace Maroik.Core.Contract.Interfaces;

/// <summary>
/// Service interface for recording the outcome of asynchronously delivered account emails.
/// Deliberately kept separate from <see cref="IAccountService"/>: consumers of this interface
/// (e.g. Maroik.Worker, reacting to an SMTP failure reported by <c>IMailClient</c>) only need
/// to update the account's status, not the full login/registration/password-reset surface,
/// so this interface carries no dependency on password hashing, RSA token encryption, or
/// mail composition.
/// </summary>
public interface IAccountMailStatusService
{
    /// <summary>
    /// Records that an email queued for this account failed to send, so the account reflects
    /// the failure. No-op if the account no longer exists.
    /// </summary>
    Task MarkMailSendFailedAsync(string email, CancellationToken ct = default);

    /// <summary>
    /// Clears a previously recorded send failure once a retried delivery has succeeded, so the
    /// admin/account screens stop showing a stale failure for an email that was ultimately
    /// delivered. No-op if the account no longer exists or its message isn't currently the
    /// failure state.
    /// </summary>
    Task ClearMailSendFailedAsync(string email, CancellationToken ct = default);
}
