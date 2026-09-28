using Maroik.Core.Contract.Dtos;

namespace Maroik.Core.Contract.Interfaces;

/// <summary>
/// Service interface for account-related business logic including login, registration, and password management.
/// </summary>
public interface IAccountService
{
    /// <summary>Returns all accounts (admin use).</summary>
    Task<List<AccountResponse>> GetAllAccountsAsync(CancellationToken ct = default);

    /// <summary>Returns accounts whose nickname matches any of the given values.</summary>
    Task<List<AccountResponse>> GetAccountsByNicknamesAsync(IEnumerable<string> nicknames, CancellationToken ct = default);

    /// <summary>Returns the account with the given email, or null if not found.</summary>
    Task<AccountResponse?> GetAccountByEmailAsync(string email, CancellationToken ct = default);

    /// <summary>Validates credentials and returns a <see cref="LoginResult"/> indicating success or failure.</summary>
    Task<LoginResult> LoginAsync(string email, string password, CancellationToken ct = default);

    /// <summary>Creates a new account and sends a confirmation email. Returns a <see cref="RegisterResult"/> with status details.</summary>
    Task<RegisterResult> RegisterAsync(AccountRequest newAccount, EmailTemplate emailTemplate, CancellationToken ct = default);

    /// <summary>Re-sends the confirmation email to an unverified account.</summary>
    Task<RegisterResult> ResendConfirmationEmailAsync(string email, EmailTemplate emailTemplate, CancellationToken ct = default);

    /// <summary>Checks the mailed confirmation link before the password form is shown; activates nothing.</summary>
    Task<ConfirmEmailResult> ValidateRegistrationTokenAsync(string registrationToken, CancellationToken ct = default);

    /// <summary>
    /// Activates an account only when the mailed confirmation link and the password chosen at
    /// registration are presented together — proof that one person owns both the mailbox and the
    /// credentials, so nobody can pre-register someone else's address and have its owner activate it.
    /// </summary>
    Task<ConfirmEmailResult> ConfirmEmailAsync(string registrationToken, string password, CancellationToken ct = default);

    /// <summary>Sends a password-reset email if the address belongs to a valid account.</summary>
    Task<bool> ForgotPasswordAsync(string email, EmailTemplate emailTemplate, CancellationToken ct = default);

    /// <summary>Checks whether the password-reset token is still valid before showing the reset form.</summary>
    Task<ResetPasswordValidationResult> ValidateResetPasswordTokenAsync(string resetPasswordToken, CancellationToken ct = default);

    /// <summary>Sets a new hashed password on the account identified by the reset token.</summary>
    Task<ServiceResult> ResetPasswordAsync(string resetPasswordToken, string newPassword, CancellationToken ct = default);
}
