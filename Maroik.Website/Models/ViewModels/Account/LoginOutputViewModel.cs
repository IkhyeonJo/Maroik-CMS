// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable PropertyCanBeMadeInitOnly.Global
using Maroik.Website.Constants;
using System.ComponentModel.DataAnnotations;
using Maroik.Core.Domain.Account;

namespace Maroik.Website.Models.ViewModels.Account;

/// <summary>
/// Form model for the self-service registration form.
/// Contains the fields the new user must supply to create an account,
/// including a password confirmation field to prevent typos.
/// </summary>
public class LoginOutputViewModel
{
    /// <summary>The email address that will become the new account's primary key / login ID.</summary>
    [Required(ErrorMessage = "Email is required")]
    [DataType(DataType.EmailAddress)]
    [EmailAddress(ErrorMessage = "Invalid Email Address")]
    public string? Email { get; set; }

    /// <summary>
    /// Desired password for the new account.
    /// Must be at least 8 characters and satisfy 3 of 4 complexity rules
    /// (uppercase, lowercase, digit, special character).
    /// </summary>
    [Required(ErrorMessage = "Password is required")]
    [StringLength(100, ErrorMessage = "The {0} must be at least {2} characters long.", MinimumLength = 8)]
    [DataType(DataType.Password)]
    [RegularExpression(PasswordPolicy.Pattern, ErrorMessage = ValidationMessages.PasswordComplexity)]
    public string? Password { get; set; }


    /// <summary>
    /// Password confirmation field — must match <see cref="Password"/> exactly.
    /// Validated with a <see cref="CompareAttribute"/> against <see cref="Password"/>.
    /// </summary>
    [Required(ErrorMessage = "Confirm Password is required")]
    [StringLength(100, ErrorMessage = "The {0} must be at least {2} characters long.", MinimumLength = 8)]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "The password do not match")]
    public string? ConfirmPassword { get; set; }

    /// <summary>The display name (nickname) chosen by the registering user.</summary>
    [Required(ErrorMessage = "Nickname is required")]
    [StringLength(255, ErrorMessage = "Must be between 1 and 255 characters", MinimumLength = 1)]
    public string? Nickname { get; set; }

    /// <summary>Whether the user agreed to the service terms of use — must be <see langword="true"/> for registration to succeed.</summary>
    public bool AgreedServiceTerms { get; set; }

    /// <summary>IANA time-zone ID (e.g. "Asia/Seoul") selected during registration; stored on the new account.</summary>
    public string? TimeZoneIanaId { get; set; }
}
