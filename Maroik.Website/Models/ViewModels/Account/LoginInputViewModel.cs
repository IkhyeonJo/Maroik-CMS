// ReSharper disable UnusedAutoPropertyAccessor.Global
using Maroik.Website.Constants;
using System.ComponentModel.DataAnnotations;
using Maroik.Core.Domain.Account;
using Maroik.Website.Attributes;

namespace Maroik.Website.Models.ViewModels.Account;

/// <summary>
/// Form model bound from the login / account-action POST requests.
/// Covers login credentials, registration confirmation, and password-reset flows,
/// because all three share the same Account page with different partial views.
/// </summary>
public class LoginInputViewModel
{
    /// <summary>The user's email address — used as both the login identifier and the registration email.</summary>
    [Required(ErrorMessage = "Email is required")]
    [DataType(DataType.EmailAddress)]
    [EmailAddress(ErrorMessage = "Invalid Email Address")]
    public string? Email { get; set; }

    /// <summary>
    /// Plain-text password submitted by the user.
    /// Must be at least 8 characters and satisfy 3 of 4 complexity rules
    /// (uppercase, lowercase, digit, special character).
    /// </summary>
    [Required(ErrorMessage = "Password is required")]
    [StringLength(100, ErrorMessage = "The {0} must be at least {2} characters long.", MinimumLength = 8)]
    [DataType(DataType.Password)]
    [RegularExpression(PasswordPolicy.Pattern, ErrorMessage = ValidationMessages.PasswordComplexity)]
    [PasswordMaxBytes(ErrorMessage = "Password must be at most 72 bytes (one Korean character counts as 3 bytes).")]
    public string? Password { get; set; }

    /// <summary>The display name chosen by the user during registration.</summary>
    [Required(ErrorMessage = "Nickname is required")]
    [StringLength(255, ErrorMessage = "Must be between 1 and 255 characters", MinimumLength = 1)]
    public string? Nickname { get; set; }

    /// <summary>Whether the user has agreed to the service terms of use (required for registration).</summary>
    public bool AgreedServiceTerms { get; set; }

    /// <summary>One-time token sent via email for the password-reset flow.</summary>
    public string? ResetPasswordToken { get; set; }

    /// <summary>Encrypted token from the confirmation link, posted back with the registration password to activate the account.</summary>
    public string? RegistrationToken { get; set; }

    /// <summary>Locale/culture string (e.g. "en-US") selected by the user on the login page.</summary>
    public string? Culture { get; set; }

    /// <summary>IANA time-zone ID (e.g. "Asia/Seoul") chosen by the user; persisted to the account.</summary>
    public string? TimeZoneIanaId { get; set; }
}
