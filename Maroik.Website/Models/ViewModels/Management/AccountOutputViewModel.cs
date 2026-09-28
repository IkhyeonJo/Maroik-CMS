// ReSharper disable PropertyCanBeMadeInitOnly.Global
using System.ComponentModel.DataAnnotations;

namespace Maroik.Website.Models.ViewModels.Management;

/// <summary>
/// Read-only view model for displaying a user account record in the admin management area.
/// Exposes all stored account fields including the BCrypt-hashed password and audit timestamps.
/// </summary>
public class AccountOutputViewModel
{
    /// <summary>Account email address ?? the primary key in the database.</summary>
    [Required(ErrorMessage = "Please enter Email")]
    [Display(Name = "Email")]
    public string? Email { get; set; }

    /// <summary>BCrypt-hashed password stored in the database (never the plain-text value).</summary>
    [Required(ErrorMessage = "Please enter HashedPassword")]
    [Display(Name = "HashedPassword")]
    public string? HashedPassword { get; set; }

    /// <summary>Display name (nickname) visible to other users.</summary>
    [Required(ErrorMessage = "Please enter Nickname")]
    [Display(Name = "Nickname")]
    public string? Nickname { get; set; }

    /// <summary>Server-side path to the account's avatar image file.</summary>
    [Required(ErrorMessage = "Please enter AvatarImagePath")]
    [Display(Name = "AvatarImagePath")]
    public string? AvatarImagePath { get; set; }

    /// <summary>Role assigned to the account (e.g. "Admin", "User").</summary>
    [Required(ErrorMessage = "Please enter Role")]
    [Display(Name = "Role")]
    public string? Role { get; set; }

    /// <summary>IANA time-zone ID for date/time display (e.g. "Asia/Seoul").</summary>
    [Required(ErrorMessage = "Please select time zone")]
    [Display(Name = "Time zone")]
    public string? TimeZoneIanaId { get; set; }

    /// <summary>When <see langword="true"/> the account cannot log in until manually unlocked by an admin.</summary>
    [Display(Name = "Locked")]
    public bool Locked { get; set; }

    /// <summary>Cumulative count of failed login attempts.</summary>
    [Required(ErrorMessage = "Please enter LoginAttempt")]
    [Display(Name = "LoginAttempt")]
    public long LoginAttempt { get; set; }

    /// <summary>When <see langword="true"/> the email has been verified via the confirmation link.</summary>
    [Display(Name = "EmailConfirmed")]
    public bool EmailConfirmed { get; set; }

    /// <summary>Whether the user agreed to the service terms of use at registration.</summary>
    [Display(Name = "AgreedServiceTerms")]
    public bool AgreedServiceTerms { get; set; }

    /// <summary>One-time registration confirmation token (empty string once confirmed).</summary>
    [Required(ErrorMessage = "Please enter RegistrationToken")]
    [Display(Name = "RegistrationToken")]
    public string? RegistrationToken { get; set; }

    /// <summary>One-time password-reset token (empty string when no reset is pending).</summary>
    [Required(ErrorMessage = "Please enter ResetPasswordToken")]
    [Display(Name = "ResetPasswordToken")]
    public string? ResetPasswordToken { get; set; }

    /// <summary>UTC timestamp when the account was first created.</summary>
    [Required(ErrorMessage = "Please enter Created")]
    [Display(Name = "Created")]
    public DateTime Created { get; set; }

    /// <summary>UTC timestamp of the most recent update to this account record.</summary>
    [Required(ErrorMessage = "Please enter Updated")]
    [Display(Name = "Updated")]
    public DateTime Updated { get; set; }

    /// <summary>Status or feedback message last set by an admin or automated process.</summary>
    [Required(ErrorMessage = "Please enter Message")]
    [Display(Name = "Message")]
    public string? Message { get; set; }

    /// <summary>Soft-delete flag; when <see langword="true"/> the account is hidden from normal views.</summary>
    [Display(Name = "Deleted")]
    public bool Deleted { get; set; }
}
