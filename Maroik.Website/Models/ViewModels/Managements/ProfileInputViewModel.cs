// ReSharper disable UnusedAutoPropertyAccessor.Global
using System.ComponentModel.DataAnnotations;
using Maroik.Core.Domain.Accounts;
using Maroik.Website.Attributes;
using Maroik.Website.Constants;

namespace Maroik.Website.Models.ViewModels.Managements;

/// <summary>
/// Form model for the self-service profile page (Admin and User). Bound by three separate endpoints —
/// avatar upload, time-zone change and password change — each of which reads only its own fields; the
/// current password is required only for the password change. The nickname is shown but never changed.
/// </summary>
public class ProfileInputViewModel
{
    /// <summary>The account's display name as rendered on the form (read-only; no endpoint updates it).</summary>
    [Required(ErrorMessage = "Please enter Nickname")]
    [Display(Name = "Nickname")]
    public string? Nickname { get; set; }

    /// <summary>Avatar image files uploaded by the user (at most one image is processed).</summary>
    public List<IFormFile>? ProfileAvatarFiles { get; set; }

    /// <summary>
    /// Current (existing) password, required to authorize a password change. Deliberately NOT checked
    /// against the complexity rule: it only has to match what is stored (verified by the server), and
    /// an account whose password predates the rule must still be able to type it in order to change it.
    /// </summary>
    [Required(ErrorMessage = "Password is required")]
    [StringLength(PasswordPolicy.MaxByteLength)]
    [DataType(DataType.Password)]
    public string? Password { get; set; }

    /// <summary>
    /// New password the user wishes to set.
    /// Must satisfy the password complexity rule.
    /// </summary>
    [Required(ErrorMessage = "New password is required")]
    [StringLength(PasswordPolicy.MaxByteLength, ErrorMessage = "The {0} must be between {2} and {1} characters long.", MinimumLength = PasswordPolicy.MinLength)]
    [DataType(DataType.Password)]
    [RegularExpression(PasswordPolicy.Pattern, ErrorMessage = ValidationMessages.PasswordComplexity)]
    [PasswordMaxBytes(ErrorMessage = "Password must be at most 72 bytes (one Korean character counts as 3 bytes).")]
    public string? NewPassword { get; set; }

    /// <summary>IANA time-zone ID the user selects for date/time display (e.g. "Asia/Seoul").</summary>
    [Required(ErrorMessage = "Please select time zone")]
    [Display(Name = "Time zone")]
    public string? TimeZoneIanaId { get; set; }
}
