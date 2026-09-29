// ReSharper disable PropertyCanBeMadeInitOnly.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Maroik.Core.Contract.Dtos;

/// <summary>
/// Data transfer object used to create or update an account.
/// Carries the account fields from the presentation layer to the service layer, which maps them onto the <c>Account</c> aggregate.
/// </summary>
public class AccountRequest
{
    /// <summary>Email address used as the primary key / login ID.</summary>
    public string? Email { get; set; }

    /// <summary>
    /// Always a BCrypt hash — never a plain-text password. On the creation/register path this stays
    /// null and <see cref="PlainPassword"/> carries the raw value for the service to hash.
    /// </summary>
    public string? HashedPassword { get; set; }

    /// <summary>
    /// Plain-text password as typed by the user, used only on the creation/register path. The service
    /// layer validates it against the password policy and hashes it into <see cref="HashedPassword"/>
    /// before the domain sees it; it is never persisted or returned.
    /// </summary>
    public string? PlainPassword { get; set; }

    /// <summary>Unique display name shown throughout the UI.</summary>
    public string? Nickname { get; set; }

    /// <summary>Relative path to the user's avatar image.</summary>
    public string? AvatarImagePath { get; set; }

    /// <summary>Account role: "Admin" or "User".</summary>
    public string? Role { get; set; }

    /// <summary>IANA time-zone ID used for date/time display (e.g. "Asia/Seoul").</summary>
    public string? TimeZoneIanaId { get; set; }

    /// <summary>Default currency unit shown in the account-book (e.g. "KRW", "USD").</summary>
    public string? DefaultMonetaryUnit { get; set; }

    /// <summary>Whether the account is locked and cannot log in.</summary>
    public bool Locked { get; set; }

    /// <summary>Whether the user has confirmed their email address.</summary>
    public bool EmailConfirmed { get; set; }

    /// <summary>Whether the user agreed to the service terms of use.</summary>
    public bool AgreedServiceTerms { get; set; }

    /// <summary>Token embedded in the registration confirmation email link (valid 24 h).</summary>
    public string? RegistrationToken { get; set; }

    /// <summary>Optional admin-facing note attached to the account.</summary>
    public string? Message { get; set; }

    /// <summary>Soft-delete flag. True means the account is deleted but the row is kept.</summary>
    public bool Deleted { get; set; }
}
