namespace Maroik.Core.Contract.Dtos;

/// <summary>
/// Result returned after the user clicks the email-confirmation link.
/// </summary>
public class ConfirmEmailResult
{
    /// <summary>True when the token was invalid or expired.</summary>
    public bool InvalidToken { get; init; }

    /// <summary>True when the account was successfully activated.</summary>
    public bool AccountCreated { get; init; }

    /// <summary>Localization key describing the error (empty string on success).</summary>
    public string ErrorKey { get; init; } = "";
}
