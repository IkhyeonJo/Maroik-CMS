// ReSharper disable PropertyCanBeMadeInitOnly.Global
using Maroik.Website.Constants;
using System.ComponentModel.DataAnnotations;
using Maroik.Core.Domain.Account;

namespace Maroik.Website.Models.ViewModels.Management;

/// <summary>
/// View model for the self-service profile page (Admin and User).
/// Pre-populates the form with the current account values so the user can
/// review them and update their avatar, password and time zone (the nickname is shown read-only).
/// </summary>
public class ProfileOutputViewModel
{
    /// <summary>Account email address (read-only; displayed as the account identifier).</summary>
    [Required(ErrorMessage = "Please enter Email")]
    [Display(Name = "Email")]
    public string? Email { get; set; }

    /// <summary>Server-side path to the current avatar image file.</summary>
    [Required(ErrorMessage = "Please enter AvatarImagePath")]
    [Display(Name = "AvatarImagePath")]
    public string? AvatarImagePath { get; set; }

    /// <summary>Current display name (nickname) shown in the profile form.</summary>
    [Required(ErrorMessage = "Please enter Nickname")]
    [Display(Name = "Nickname")]
    public string? Nickname { get; set; }

    /// <summary>When the account was first created, converted to the account's own time zone.</summary>
    [Required(ErrorMessage = "Please enter Created")]
    [Display(Name = "Created")]
    public DateTime Created { get; set; }

    /// <summary>
    /// Current (existing) password — required to authorize a password change (the avatar and time-zone
    /// updates do not ask for it). Carries the complexity rule only for the form's client-side hints; the
    /// server checks the current password against the stored hash, not against the rule.
    /// </summary>
    [Required(ErrorMessage = "Password is required")]
    [StringLength(100, ErrorMessage = "The {0} must be at least {2} characters long.", MinimumLength = 8)]
    [DataType(DataType.Password)]
    [RegularExpression(PasswordPolicy.Pattern, ErrorMessage = ValidationMessages.PasswordComplexity)]
    public string? Password { get; set; }

    /// <summary>
    /// New password the user wishes to set.
    /// Must satisfy the password complexity rule.
    /// </summary>
    [Required(ErrorMessage = "New password is required")]
    [StringLength(100, ErrorMessage = "The {0} must be at least {2} characters long.", MinimumLength = 8)]
    [DataType(DataType.Password)]
    [RegularExpression(PasswordPolicy.Pattern, ErrorMessage = ValidationMessages.PasswordComplexity)]
    public string? NewPassword { get; set; }


    /// <summary>
    /// Confirmation of the new password — must match <see cref="NewPassword"/> exactly.
    /// </summary>
    [Required(ErrorMessage = "Confirm new password is required")]
    [StringLength(100, ErrorMessage = "The {0} must be at least {2} characters long.", MinimumLength = 8)]
    [DataType(DataType.Password)]
    [Compare(nameof(NewPassword), ErrorMessage = "The new password do not match")]
    public string? ConfirmNewPassword { get; set; }

    /// <summary>IANA time-zone ID currently set on the account (e.g. "Asia/Seoul").</summary>
    [Required(ErrorMessage = "Please select time zone")]
    [Display(Name = "Time zone")]
    public string? TimeZoneIanaId { get; set; }

    /// <summary>
    /// When true, an admin has reset this account's password and the user must set a new one
    /// (meeting the password policy) before continuing. Drives the forced-change notice banner.
    /// </summary>
    public bool MustChangePassword { get; set; }
}
