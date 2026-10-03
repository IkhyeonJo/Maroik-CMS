// ReSharper disable PropertyCanBeMadeInitOnly.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Maroik.Core.Contract.Dtos;

/// <summary>
/// Data transfer object returned when reading account information.
/// The read-side counterpart of the account request DTOs, used exclusively for output (read) operations.
/// </summary>
public class AccountResponse
{
    /// <summary>Email address (primary key / login ID).</summary>
    public string? Email { get; set; }

    /// <summary>Unique display name shown in the UI.</summary>
    public string? Nickname { get; set; }

    /// <summary>Relative path to the user's avatar image.</summary>
    public string? AvatarImagePath { get; set; }

    /// <summary>Account role: "Admin" or "User".</summary>
    public string? Role { get; set; }

    /// <summary>IANA time-zone ID (e.g. "Asia/Seoul", "UTC").</summary>
    public string? TimeZoneIanaId { get; set; }

    /// <summary>Default currency unit for the account-book (e.g. "KRW", "USD").</summary>
    public string? DefaultMonetaryUnit { get; set; }

    /// <summary>Whether the account is locked (cannot log in).</summary>
    public bool Locked { get; set; }

    /// <summary>Number of consecutive failed login attempts.</summary>
    public long LoginAttempt { get; set; }

    /// <summary>Whether the email address has been verified.</summary>
    public bool EmailConfirmed { get; set; }

    /// <summary>Whether the user agreed to the service terms of use.</summary>
    public bool AgreedServiceTerms { get; set; }

    /// <summary>UTC timestamp when the account was created.</summary>
    public DateTime Created { get; set; }

    /// <summary>UTC timestamp of the most recent account update.</summary>
    public DateTime Updated { get; set; }

    /// <summary>Optional admin note attached to the account.</summary>
    public string? Message { get; set; }

    /// <summary>Soft-delete flag.</summary>
    public bool Deleted { get; set; }

    /// <summary>
    /// Opaque value that changes on every password change. Carried in the session and compared
    /// against the current DB value on every request to invalidate stale sessions after a
    /// password change (self-service, forgot-password reset, or admin override).
    /// </summary>
    public string? SecurityStamp { get; set; }

    /// <summary>When true, the account must set a new password before it can do anything else.</summary>
    public bool MustChangePassword { get; set; }

    /// <summary>The account's device stamp, written into the trusted-device cookie a successful sign-in issues.</summary>
    public string? DeviceStamp { get; set; }
}
