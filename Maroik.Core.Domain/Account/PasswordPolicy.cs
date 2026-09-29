using System.Text;
using System.Text.RegularExpressions;

namespace Maroik.Core.Domain.Account;

/// <summary>
/// Centralizes the password-complexity rule used across the application.
/// Rule: 8 to <see cref="MaxByteLength"/> bytes (UTF-8), meeting 3 of 4 criteria —
/// uppercase (A-Z), lowercase (a-z), digit (0-9), special character.
/// </summary>
public static partial class PasswordPolicy
{
    /// <summary>
    /// Regex pattern for the password complexity rule.
    /// Exposed as a <c>const</c> so it can be used directly in
    /// <see cref="System.ComponentModel.DataAnnotations.RegularExpressionAttribute"/> on ViewModels.
    /// </summary>
    public const string Pattern =
        @"^((?=.*?[A-Z])(?=.*?[a-z])(?=.*?[0-9])|(?=.*?[A-Z])(?=.*?[a-z])(?=.*?[^a-zA-Z0-9])|(?=.*?[A-Z])(?=.*?[0-9])(?=.*?[^a-zA-Z0-9])|(?=.*?[a-z])(?=.*?[0-9])(?=.*?[^a-zA-Z0-9])).{8,}$";

    /// <summary>
    /// Upper bound on the password length, in UTF-8 bytes. BCrypt silently ignores everything past
    /// the first 72 bytes of its input, so a longer password would hash as if truncated — two
    /// different long passwords sharing a 72-byte prefix would then verify against each other.
    /// Rejecting them up front keeps "the whole password matters" true. Usable as a
    /// <see cref="System.ComponentModel.DataAnnotations.StringLengthAttribute"/> ceiling on ViewModels
    /// (a conservative char count, since one char is 1–4 UTF-8 bytes).
    /// </summary>
    public const int MaxByteLength = 72;

    /// <summary>
    /// Human-readable statement of the rule, for a validation error message. Single source of truth:
    /// every service that rejects a weak password reports it, and the Website's view-model validation
    /// (<c>ValidationMessages.PasswordComplexity</c>) reuses it, so it is also the resx lookup key —
    /// changing the text means renaming that key in every resx pair that carries it.
    /// </summary>
    public const string ViolationMessage =
        "Password must be at least 8 characters (and at most 72) and contain at least 3 of 4 of the following: " +
        "upper case (A-Z), lower case (a-z), number (0-9) and special character (e.g. !@#$%^&*).";

    /// <summary>Cached instance of the source-generated <see cref="Pattern"/> regex (<see cref="MyRegex"/>).</summary>
    private static readonly Regex _compiledRegex = MyRegex();

    /// <summary>
    /// Returns <c>true</c> when <paramref name="password"/> fits within <see cref="MaxByteLength"/>
    /// UTF-8 bytes (<c>null</c>/empty trivially do). Split out from <see cref="IsValid"/> so a caller can tell
    /// "too long" apart from "not complex enough" when it reports the problem.
    /// </summary>
    public static bool IsWithinMaxByteLength(string? password) =>
        string.IsNullOrEmpty(password) || Encoding.UTF8.GetByteCount(password) <= MaxByteLength;

    /// <summary>Returns <c>true</c> when <paramref name="password"/> satisfies the complexity and length rule.</summary>
    public static bool IsValid(string? password) =>
        !string.IsNullOrEmpty(password)
        && IsWithinMaxByteLength(password)
        && _compiledRegex.IsMatch(password);
    // No RegexOptions.Compiled: the source generator already emits a purpose-built implementation,
    // so Compiled would only add redundant runtime IL emission on top.
    [GeneratedRegex(Pattern)]
    private static partial Regex MyRegex();
}
