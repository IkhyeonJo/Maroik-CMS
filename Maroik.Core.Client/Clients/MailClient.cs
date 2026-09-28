using System.Net;
using System.Net.Security;
using MailKit.Net.Smtp;
using MailKit.Security;
using Maroik.Core.Contract.Dtos;
using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Contract.Misc.Settings;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace Maroik.Core.Client.Clients;

/// <summary>
/// Concrete implementation of <see cref="IMailClient"/> that builds transactional email bodies
/// and delivers them via SMTP using MailKit.
/// </summary>
public class MailClient(ILogger<MailClient> logger) : IMailClient
{
    /// <summary>Creates the SMTP client for one send. Overridable in tests (MailKit never fails its own QUIT/close, so the "closing failed" path needs a client that does).</summary>
    internal Func<SmtpClient> SmtpClientFactory { get; init; } = () => new SmtpClient();

    /// <summary>
    /// Joins the configured public base URL to an app-relative path, tolerating a
    /// <c>DomainName</c> configured either with or without a trailing "/". Previously the callers
    /// assumed a trailing slash and a value like "https://maroik.com" (no slash) produced the
    /// broken link "https://maroik.comAccount/ConfirmEmail".
    /// </summary>
    private static string BuildUrl(string domainName, string relativePath) =>
        $"{(domainName).TrimEnd('/')}/{relativePath.TrimStart('/')}";

    /// <summary>
    /// The centred HTML block shared by the confirmation and reset-password mails. Everything interpolated into the markup is
    /// HTML-encoded (the texts come from resx files and the link from configuration today, but nothing here should be able to
    /// turn into markup — or break out of the <c>href</c> attribute — if that ever stops being true).
    /// </summary>
    private static string BuildLinkBody(string title, string content0, string content1, string url)
    {
        string link = WebUtility.HtmlEncode(url);
        return $"""
                <div style='text-align:center;'>
                                        <h1>{WebUtility.HtmlEncode(title)}</h1>
                                        <h3>{WebUtility.HtmlEncode(content0)}</h3>
                                        <h3>{WebUtility.HtmlEncode(content1)}</h3>
                                        <a href='{link}' target='_blank' rel='noopener noreferrer'>{link}</a>
                                      </div>
                """;
    }

    /// <summary>
    /// Decides whether to tolerate an SMTP server certificate when <c>SmtpSsl</c> is disabled for a
    /// relay. Accept only a bare chain / self-signed-root problem (the realistic "operator's own
    /// relay" case); reject a hostname mismatch or a missing certificate outright, since both are the
    /// signature of an active man-in-the-middle rather than a benign self-signed relay. Extracted as a
    /// pure function (no logging, no capture) so this security-sensitive decision has direct unit-test
    /// coverage independent of an actual SMTP connection.
    /// </summary>
    public static bool ShouldAcceptCertificate(SslPolicyErrors errors) => errors switch
    {
        SslPolicyErrors.None => true,
        SslPolicyErrors.RemoteCertificateChainErrors => true,
        _ => false
    };

    /// <summary>
    /// Picks the TLS mode for the SMTP connection. <c>SmtpSsl</c> on means TLS is <b>required</b>:
    /// implicit TLS on the conventional implicit-TLS ports (465 / 2465), otherwise a mandatory
    /// STARTTLS upgrade — the connect fails rather than continuing in cleartext if the server does
    /// not offer it, so an attacker who strips the STARTTLS capability cannot make the client send
    /// its SMTP credentials or the token-bearing mail body unencrypted (the previous
    /// <c>SecureSocketOptions.Auto</c> silently fell back to no encryption). <c>SmtpSsl</c> off keeps
    /// the deliberate opportunistic behavior for an operator's own relay. Extracted as a pure
    /// function so this security-sensitive decision is unit-testable without an SMTP connection.
    /// </summary>
    public static SecureSocketOptions GetSecureSocketOptions(bool smtpSsl, int port)
    {
        if (!smtpSsl)
            return SecureSocketOptions.StartTlsWhenAvailable;

        return port is 465 or 2465
            ? SecureSocketOptions.SslOnConnect
            : SecureSocketOptions.StartTls;
    }

    /// <inheritdoc />
    public string GetMailConfirmationBody(string registrationToken, string title, string content0, string content1, string domainName)
    {
        // URL-encode the token so special characters survive in the query string
        string url = BuildUrl(domainName, $"Account/ConfirmEmail?registrationToken={Uri.EscapeDataString(registrationToken)}");

        // Return a centred HTML block containing the confirmation link
        return BuildLinkBody(title, content0, content1, url);
    }

    /// <inheritdoc />
    public string GetMailResetPasswordBody(string resetPasswordToken, string title, string content0, string content1, string domainName)
    {
        // URL-encode the token so special characters survive in the query string
        string url = BuildUrl(domainName, $"Account/ResetPassword?resetPasswordToken={Uri.EscapeDataString(resetPasswordToken)}");

        // Return a centred HTML block containing the password-reset link
        return BuildLinkBody(title, content0, content1, url);
    }

    /// <inheritdoc />
    public async Task<ServiceResult> SendMailAsync(string toEmail, string subject, string body, ServerSetting settings, CancellationToken ct = default)
    {
        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(settings.FromFullName ?? "", settings.FromEmail ?? ""));
            message.To.Add(new MailboxAddress("", toEmail));
            message.Subject = subject;
            message.Body = new BodyBuilder { HtmlBody = body }.ToMessageBody();

            using SmtpClient smtp = SmtpClientFactory();
            // Bounded well under the worker's shutdown drain budget so a hung connection
            // can't outlive it and get killed mid-send (which would cause a duplicate
            // send on redelivery). MailKit's default is ~100s.
            smtp.Timeout = 20_000;
            // SmtpSsl on  => TLS is required (implicit TLS or mandatory STARTTLS, see
            //               GetSecureSocketOptions) AND the server certificate is validated normally.
            // SmtpSsl off => still upgrade opportunistically via STARTTLS when the server advertises
            //               it (so credentials are never sent in the clear against a relay that
            //               offers TLS). The certificate check is relaxed only for an untrusted /
            //               self-signed chain — the realistic "operator's own relay" case. A
            //               hostname mismatch or a missing certificate is still rejected, since both
            //               are the signature of an active man-in-the-middle rather than a benign
            //               self-signed relay.
            SecureSocketOptions socketOptions = GetSecureSocketOptions(settings.SmtpSsl, settings.SmtpPort);
            if (!settings.SmtpSsl)
            {
                smtp.ServerCertificateValidationCallback = (_, _, _, errors) =>
                {
                    bool accept = ShouldAcceptCertificate(errors);
                    if (errors != SslPolicyErrors.None)
                    {
                        if (accept)
                            logger.LogWarning(
                                "Accepting an untrusted SMTP server certificate for {SmtpHost} because SmtpSsl is disabled for this relay",
                                settings.SmtpHost);
                        else
                            logger.LogWarning(
                                "Rejecting the SMTP server certificate for {SmtpHost}: {SslPolicyErrors}",
                                settings.SmtpHost, errors);
                    }
                    return accept;
                };
            }
            await smtp.ConnectAsync(settings.SmtpHost ?? "", settings.SmtpPort, socketOptions, ct);
            await smtp.AuthenticateAsync(settings.SmtpUserName ?? "", settings.SmtpPassword ?? "", ct);
            await smtp.SendAsync(message, ct);

            // The server has accepted the message at this point, so a failure while closing the
            // connection (20 s timeout, socket reset, a canceled token at worker shutdown) must not
            // be reported as a failed send: the caller would mark the mail failed and republish it,
            // and the recipient would get a duplicate confirmation / reset mail. Log and move on;
            // disposing `smtp` tears the connection down anyway.
            try
            {
                await smtp.DisconnectAsync(true, ct);
            }
            catch (Exception disconnectEx)
            {
                logger.LogWarning(disconnectEx, "Mail to {ToEmail} was sent, but closing the SMTP connection failed", toEmail);
            }

            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            // Log the detail; return a generic message so raw SMTP/exception text (server names,
            // internal hosts) never reaches a caller that might surface it to a user.
            logger.LogError(ex, "Failed to send mail to {ToEmail}", toEmail);
            return ServiceResult.Failure("Mail.SendFailed", "Failed to send the email.");
        }
    }
}
