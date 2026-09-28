// ReSharper disable UnusedAutoPropertyAccessor.Global
using System.ComponentModel.DataAnnotations;

namespace Maroik.Website.Models.ViewModels.Management;

/// <summary>
/// Form model for admin create / edit operations on user accounts.
/// Includes all editable account fields, including role, lock status,
/// email-confirmation state, and token management.
/// </summary>
public class AccountInputViewModel
{
    /// <summary>Account email address ?? acts as the primary key in the database.</summary>
    [Required(ErrorMessage = "Please enter Email")]
    [Display(Name = "Email")]
    public string? Email { get; set; }

    /// <summary>Plain-text password submitted by the admin; hashed before persistence.</summary>
    [Required(ErrorMessage = "Please enter Password")]
    [Display(Name = "Password")]
    public string? Password { get; set; }

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

    /// <summary>When <see langword="true"/> the account's email has been verified via the confirmation link.</summary>
    [Display(Name = "EmailConfirmed")]
    public bool EmailConfirmed { get; set; }

    /// <summary>Whether the user has agreed to the service terms of use.</summary>
    [Display(Name = "AgreedServiceTerms")]
    public bool AgreedServiceTerms { get; set; }

    /// <summary>One-time token sent via email for account email confirmation.</summary>
    [Required(ErrorMessage = "Please enter RegistrationToken")]
    [Display(Name = "RegistrationToken")]
    public string? RegistrationToken { get; set; }

    /// <summary>One-time token sent via email for the password-reset flow.</summary>
    [Required(ErrorMessage = "Please enter ResetPasswordToken")]
    [Display(Name = "ResetPasswordToken")]
    public string? ResetPasswordToken { get; set; }

    /// <summary>Status or feedback message displayed to the user after an account operation.</summary>
    [Required(ErrorMessage = "Please enter Message")]
    [Display(Name = "Message")]
    public string? Message { get; set; }

    /// <summary>Soft-delete flag; when <see langword="true"/> the account is hidden from normal views.</summary>
    [Display(Name = "Deleted")]
    public bool Deleted { get; set; }
}
