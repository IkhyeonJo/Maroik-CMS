using Maroik.Core.Contract.Dtos;
using Maroik.Core.Contract.Misc.Settings;

namespace Maroik.Core.Contract.Interfaces;

/// <summary>
/// Client interface for composing and sending transactional emails via SMTP.
/// </summary>
public interface IMailClient
{
    /// <summary>
    /// Builds the HTML body for a password-reset email containing the reset link.
    /// </summary>
    /// <param name="resetPasswordToken">The time-limited reset token embedded in the link.</param>
    /// <param name="title">Email heading text.</param>
    /// <param name="content0">First body paragraph.</param>
    /// <param name="content1">Second body paragraph.</param>
    /// <param name="domainName">Application domain used to construct the reset URL.</param>
    public string GetMailResetPasswordBody(string resetPasswordToken, string title, string content0, string content1, string domainName);

    /// <summary>
    /// Builds the HTML body for a registration confirmation email containing the verification link.
    /// </summary>
    /// <param name="registrationToken">The time-limited registration token embedded in the link.</param>
    /// <param name="title">Email heading text.</param>
    /// <param name="content0">First body paragraph.</param>
    /// <param name="content1">Second body paragraph.</param>
    /// <param name="domainName">Application domain used to construct the confirmation URL.</param>
    public string GetMailConfirmationBody(string registrationToken, string title, string content0, string content1, string domainName);

    /// <summary>
    /// Builds the HTML body of the alert mailed when several sign-ins to an account failed; it links to the
    /// forgot-password page.
    /// </summary>
    /// <param name="title">Email heading text.</param>
    /// <param name="content0">First body paragraph.</param>
    /// <param name="content1">Second body paragraph.</param>
    /// <param name="domainName">Application domain used to construct the forgot-password URL.</param>
    public string GetMailLoginAlertBody(string title, string content0, string content1, string domainName);

    /// <summary>
    /// Sends an HTML email via SMTP. Returns a successful <see cref="ServiceResult"/>, or a
    /// <see cref="ServiceErrorType.Failure"/> carrying a generic message (the exception itself is logged,
    /// never returned, so SMTP server details cannot reach a caller).
    /// </summary>
    public Task<ServiceResult> SendMailAsync(string toEmail, string subject, string body, ServerSetting settings, CancellationToken ct = default);
}
