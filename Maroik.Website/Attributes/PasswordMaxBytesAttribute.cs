using System.ComponentModel.DataAnnotations;
using Maroik.Core.Domain.Account;

namespace Maroik.Website.Attributes;

/// <summary>
/// ViewModel mirror of <see cref="PasswordPolicy.IsWithinMaxByteLength"/>: a password may be at most
/// <see cref="PasswordPolicy.MaxByteLength"/> UTF-8 bytes (a character count cannot express that — one
/// Korean character is 3 bytes). Authoritative in <see cref="PasswordPolicy"/> (the services re-validate);
/// having it on the ViewModel lets the form report "too long" instead of a generic failure.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class PasswordMaxBytesAttribute : ValidationAttribute
{
    /// <inheritdoc />
    public override bool IsValid(object? value) =>
        value is not string password || PasswordPolicy.IsWithinMaxByteLength(password);
}
