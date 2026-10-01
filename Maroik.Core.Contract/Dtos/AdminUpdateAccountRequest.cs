// ReSharper disable PropertyCanBeMadeInitOnly.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Maroik.Core.Contract.Dtos;

/// <summary>
/// Input of the administrator's "update account" use case: the desired role, time zone and
/// status flags of an existing account. The service turns each change into the matching
/// account domain operation.
/// </summary>
public class AdminUpdateAccountRequest
{
    /// <summary>Email address of the account to update.</summary>
    public string? Email { get; set; }

    /// <summary>Account role: "Admin" or "User". Null keeps the current role.</summary>
    public string? Role { get; set; }

    /// <summary>IANA time-zone ID. Null keeps the current time zone.</summary>
    public string? TimeZoneIanaId { get; set; }

    /// <summary>Whether the account is locked and cannot log in.</summary>
    public bool Locked { get; set; }

    /// <summary>Whether the account's email address is confirmed.</summary>
    public bool EmailConfirmed { get; set; }

    /// <summary>Whether the account has accepted the service terms of use.</summary>
    public bool AgreedServiceTerms { get; set; }

    /// <summary>Optional admin-facing note attached to the account.</summary>
    public string? Message { get; set; }

    /// <summary>Soft-delete flag. True means the account is deleted but the row is kept.</summary>
    public bool Deleted { get; set; }
}
