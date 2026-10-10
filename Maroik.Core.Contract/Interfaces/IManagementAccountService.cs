using Maroik.Core.Contract.Dtos;

namespace Maroik.Core.Contract.Interfaces;

/// <summary>
/// Service interface for admin-level account management (Management > Account page).
/// Provides full CRUD over user accounts, including forced password changes. The write methods take
/// <c>actorEmail</c>, the signed-in administrator making the change, so the audit log records who did it.
/// </summary>
public interface IManagementAccountService
{
    /// <summary>
    /// Returns all accounts for display on the admin account list. The rows are plain
    /// <see cref="AccountResponse"/>s: the password hash and the registration / reset tokens never leave
    /// the service, so the admin screen cannot leak a live token or a hash.
    /// </summary>
    Task<List<AccountResponse>> GetAllAccountsAsync(CancellationToken ct = default);

    /// <summary>
    /// Returns accounts whose displayed fields contain <paramref name="search"/>, filtered in the database
    /// (the password hash and the tokens are not searched).
    /// </summary>
    Task<List<AccountResponse>> SearchAccountsAsync(string search, CancellationToken ct = default);

    /// <summary>
    /// Returns a single account by email for the admin edit form, or null.
    /// </summary>
    Task<AccountResponse?> GetAccountByEmailAsync(string email, CancellationToken ct = default);

    /// <summary>
    /// Admin-creates a new account. No confirmation mail is sent: the account is confirmed at once when
    /// the request's <c>EmailConfirmed</c> is set, and a reserved nickname is allowed.
    /// </summary>
    Task<ServiceResult> CreateAccountAsync(AdminCreateAccountRequest request, string actorEmail, CancellationToken ct = default);

    /// <summary>
    /// Updates an account's details. If <paramref name="newPassword"/> is provided (it must meet the
    /// password policy), hashes and sets it as the account's password and forces the account to choose
    /// its own new password at its next login.
    /// </summary>
    Task<ServiceResult> UpdateAccountAsync(AdminUpdateAccountRequest request, string? newPassword, string actorEmail, CancellationToken ct = default);

    /// <summary>Soft-deletes an account (sets its Deleted flag; the row and its data are kept).</summary>
    Task<ServiceResult> DeleteAccountAsync(string email, string actorEmail, CancellationToken ct = default);
}
