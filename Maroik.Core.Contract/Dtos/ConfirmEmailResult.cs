namespace Maroik.Core.Contract.Dtos;

/// <summary>
/// Result of the email-confirmation flow: validating the mailed link (GET) and activating the
/// account once the registration password is presented with it (POST).
/// </summary>
public class ConfirmEmailResult
{
    /// <summary>True when the token was invalid or expired.</summary>
    public bool InvalidToken { get; init; }

    /// <summary>True when the account was successfully activated.</summary>
    public bool AccountCreated { get; init; }

    /// <summary>Localization key describing the error (empty string on success).</summary>
    public string ErrorKey { get; init; } = "";

    /// <summary>True when the link was valid but the password did not match the one chosen at registration.</summary>
    public bool WrongPassword { get; init; }

    /// <summary>
    /// The still-encrypted token to embed in the password form. Set only when that form should be
    /// shown: a live link for an unconfirmed account, or a retry after a wrong password.
    /// </summary>
    public string? RegistrationToken { get; init; }
}
