// ReSharper disable PropertyCanBeMadeInitOnly.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Maroik.Core.Contract.Dtos;

/// <summary>
/// Self-registration input. Carries only what a visitor may choose for their own new account:
/// no role, lock, confirmation or deletion state — the service always creates an unconfirmed
/// <c>User</c> account and generates the registration token itself.
/// </summary>
public class RegisterAccountRequest
{
    /// <summary>Email address used as the primary key / login ID.</summary>
    public string? Email { get; set; }

    /// <summary>
    /// Plain-text password as typed by the user. The service validates it against the password
    /// policy and hashes it before the domain sees it; it is never persisted or returned.
    /// </summary>
    public string? PlainPassword { get; set; }

    /// <summary>Unique display name shown throughout the UI.</summary>
    public string? Nickname { get; set; }

    /// <summary>IANA time-zone ID used for date/time display (e.g. "Asia/Seoul").</summary>
    public string? TimeZoneIanaId { get; set; }

    /// <summary>Whether the user agreed to the service terms of use.</summary>
    public bool AgreedServiceTerms { get; set; }
}
