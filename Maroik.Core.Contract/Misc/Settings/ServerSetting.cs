// ReSharper disable PropertyCanBeMadeInitOnly.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
using Maroik.Core.Contract.Misc.Enums;

namespace Maroik.Core.Contract.Misc.Settings;

/// <summary>
/// Strongly-typed configuration class bound to the "ServerSetting" section of appsettings.json.
/// Injected via <c>IOptions&lt;ServerSetting&gt;</c> throughout the application.
/// </summary>
public class ServerSetting
{
    /// <summary>Public-facing base URL of the application (e.g. "https://maroik.com/"). Used to build email links.</summary>
    public string? DomainName { get; set; }

    /// <summary>SMTP login username (typically the sender email address).</summary>
    public string? SmtpUserName { get; set; }

    /// <summary>SMTP login password.</summary>
    public string? SmtpPassword { get; set; }

    /// <summary>SMTP server host name or IP address.</summary>
    public string? SmtpHost { get; set; }

    /// <summary>SMTP server port (e.g. 587 for STARTTLS, 465 for implicit TLS).</summary>
    public int SmtpPort { get; set; }

    /// <summary>Whether the SMTP connection requires SSL/TLS.</summary>
    public bool SmtpSsl { get; set; }

    /// <summary>Email address shown in the form header of outgoing emails.</summary>
    public string? FromEmail { get; set; }

    /// <summary>Display name shown next to the From address in outgoing emails.</summary>
    public string? FromFullName { get; set; }

    /// <summary>Number of consecutive failed logins allowed before the account is locked.</summary>
    public byte MaxLoginAttempt { get; set; }

    /// <summary>Number of minutes of inactivity before the server-side session expires.</summary>
    public int SessionExpireMinutes { get; set; }

    /// <summary>
    /// How many days before a fixed income/expenditure's scheduled deposit date (month/day) to start
    /// counting it as noticed (see <c>FixedSchedulePolicy.IsNoticed</c>) — not its maturity date.
    /// </summary>
    public int NoticeMaturityDateDay { get; set; }

    /// <summary>File system path to the TLS certificate used by the Docker/Kestrel host.</summary>
    public string? DockerCertPath { get; set; }

    /// <summary>File system path to the TLS private key used by the Docker/Kestrel host.</summary>
    public string? DockerKeyPath { get; set; }

    /// <summary>Base64-encoded DER (PKCS#1) RSA private key used to decrypt mailed tokens and stored image paths.</summary>
    public string? RsaPrivateKey { get; set; }

    /// <summary>Base64-encoded DER (X.509 SubjectPublicKeyInfo) RSA public key used to encrypt mailed tokens and stored image paths.</summary>
    public string? RsaPublicKey { get; set; }

    /// <summary>
    /// Legacy RSA algorithm variant. Retained for configuration back-compat only — <c>RsaService</c>
    /// now always uses SHA-256 for OAEP and signatures regardless of this value (SHA-1 is broken).
    /// </summary>
    public RsaType RsaAlgorithm { get; set; }

    /// <summary>Host name or IP address of the ClamAV antivirus daemon.</summary>
    public string? ClamavHost { get; set; }

    /// <summary>TCP port of the ClamAV daemon (clamav standard port is 3310; no default is applied here).</summary>
    public int ClamavPort { get; set; }

    /// <summary>Base URL of the Maroik.FileStorage microservice used for file upload/download.</summary>
    public string? FileStorageBaseUrl { get; set; }

    /// <summary>
    /// Maximum allowed upload file size in bytes. Defaults to 10 MB when not configured.
    /// Applies to attached files in Calendar events and Board posts, and to Summernote image uploads.
    /// </summary>
    public long MaxAttachedFileSizeBytes { get; set; } = 10L * 1024 * 1024;

    /// <summary>
    /// Email of the public demo account, if one is configured for this deployment. When the
    /// dashboard is requested for this account, <see cref="DemoDashboardYear"/>/<see cref="DemoDashboardMonth"/>
    /// are shown instead of the requested period, so visitors always see the same pre-seeded sample data.
    /// </summary>
    public string? DemoAccountEmail { get; set; }

    /// <summary>Fixed dashboard year shown to <see cref="DemoAccountEmail"/>.</summary>
    public string? DemoDashboardYear { get; set; }

    /// <summary>Fixed dashboard month shown to <see cref="DemoAccountEmail"/>.</summary>
    public string? DemoDashboardMonth { get; set; }
}
