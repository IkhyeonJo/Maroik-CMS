using Maroik.Core.Contract.Dtos;

namespace Maroik.Core.Contract.Interfaces;

/// <summary>
/// Service interface for admin-level account management (Management > Account page).
/// Provides full CRUD over user accounts, including forced password changes. The write methods take
/// <c>actorEmail</c>, the signed-in administrator making the change, so the audit log records who did it.
/// </summary>
public interface IManagementAccountService
{
    /// <summary>Returns all accounts for display on the admin account list.</summary>
    Task<List<AdminAccountResponse>> GetAllAccountsAsync(CancellationToken ct = default);

    /// <summary>Returns accounts whose fields contain <paramref name="search"/>, filtered in the database.</summary>
    Task<List<AdminAccountResponse>> SearchAccountsAsync(string search, CancellationToken ct = default);

    /// <summary>
    /// Returns a single account by email for the admin edit form, or null. Returns the plain
    /// <see cref="AccountResponse"/>: the edit form never reads the password hash or the
    /// registration / reset tokens, so they are not sent to the browser for this lookup (the grid,
    /// search and export keep <see cref="AdminAccountResponse"/>, which shows them deliberately).
    /// </summary>
    Task<AccountResponse?> GetAccountByEmailAsync(string email, CancellationToken ct = default);

    /// <summary>
    /// Admin-creates a new account. No confirmation mail is sent: the account is confirmed at once when
    /// the request's <c>EmailConfirmed</c> is set, and a reserved nickname is allowed.
    /// </summary>
    Task<ServiceResult> CreateAccountAsync(AccountRequest request, string actorEmail, CancellationToken ct = default);

    /// <summary>
    /// Updates an account's details. If <paramref name="newPassword"/> is provided (it must meet the
    /// password policy), hashes and sets it as the account's password and forces the account to choose
    /// its own new password at its next login.
    /// </summary>
    Task<ServiceResult> UpdateAccountAsync(AccountRequest request, string? newPassword, string actorEmail, CancellationToken ct = default);

    /// <summary>Soft-deletes an account (sets its Deleted flag; the row and its data are kept).</summary>
    Task<ServiceResult> DeleteAccountAsync(string email, string actorEmail, CancellationToken ct = default);
}
