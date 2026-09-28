namespace Maroik.Website.Constants;

/// <summary>
/// Validation messages shared by several view models. The text doubles as the resx lookup key, so it is kept in one place —
/// a change here and in the resx pair is then a single edit rather than one per view model.
/// </summary>
public static class ValidationMessages
{
    /// <summary>The password-complexity rule's message (the rule itself is <c>PasswordPolicy.Pattern</c> in the Domain).</summary>
    public const string PasswordComplexity =
        "Password must be at least 8 characters and contain at 3 of 4 of the following: upper case (A-Z), lower case (a-z), number (0-9) and special character (e.g. !@#$%^&*)";
}
