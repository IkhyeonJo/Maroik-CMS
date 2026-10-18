using Maroik.Core.Contract.Dtos;

namespace Maroik.Website.Interfaces;

/// <summary>
/// Service interface for reading and writing the current user's account data
/// in the server-side session store.
/// </summary>
public interface ISessionService
{
    /// <summary>
    /// Reads the currently logged-in account from the session, loading the session from its store asynchronously
    /// first. Returns null if not authenticated; throws when the session store cannot be read.
    /// </summary>
    Task<AccountResponse?> GetAccountAsync(CancellationToken ct = default);

    /// <summary>Serializes the account into the session (called after a successful login).</summary>
    void SetAccount(AccountResponse account);

    /// <summary>Clears the account from the session (called on logout).</summary>
    void RemoveAccount();
}
