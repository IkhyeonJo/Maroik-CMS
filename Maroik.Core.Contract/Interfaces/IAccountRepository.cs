using Maroik.Core.Domain.Account;
namespace Maroik.Core.Contract.Interfaces;

/// <summary>
/// Repository abstraction over <see cref="Account"/> persistence.
/// </summary>
public interface IAccountRepository : IGenericRepository<Account>
{
    /// <summary>Returns all accounts.</summary>
    Task<List<Account>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Returns accounts whose fields contain <paramref name="search"/>, filtered in the database.</summary>
    Task<List<Account>> SearchAsync(string search, CancellationToken ct = default);

    /// <summary>Returns the account with the given email, or null if not found.</summary>
    Task<Account?> FindByEmailAsync(string email, CancellationToken ct = default);

    /// <summary>Returns the account with the given registration token, or null if not found.</summary>
    Task<Account?> FindByRegistrationTokenAsync(string token, CancellationToken ct = default);

    /// <summary>Returns the account with the given password-reset token, or null if not found.</summary>
    Task<Account?> FindByResetPasswordTokenAsync(string token, CancellationToken ct = default);

    /// <summary>
    /// Whether any account already uses <paramref name="nickname"/>, ignoring case. The database's
    /// unique constraint on Nickname is case-sensitive, so it alone would accept "bob" next to "Bob".
    /// </summary>
    Task<bool> NicknameExistsIgnoreCaseAsync(string nickname, CancellationToken ct = default);

    /// <summary>Returns accounts whose nickname matches any of the given values.</summary>
    Task<List<Account>> FindByNicknamesAsync(IEnumerable<string> nicknames, CancellationToken ct = default);

    /// <summary>
    /// Returns the account with the given email, locked with SELECT ... FOR UPDATE. Must be called
    /// inside an active unit-of-work transaction; the row lock is held until commit/rollback, so
    /// concurrent calls for the same email (e.g. parallel login attempts) queue up and each sees the
    /// latest committed state instead of racing on a stale read.
    /// </summary>
    Task<Account?> FindByEmailForUpdateAsync(string email, CancellationToken ct = default);

    // ---------------------------------------------------------------------------------------------
    // Column-scoped updates. UpdateEntityAsync rewrites EVERY column from the domain snapshot, so
    // two unrelated writers (a profile edit, a mail-status write from the worker, the dashboard's
    // default-unit self-correction) racing on the same account silently clobber each other's field.
    // These issue a single UPDATE touching only the columns the caller actually changed, so writes
    // to different fields no longer collide. Each returns the affected row count (0 = no such
    // account). The caller still runs the domain method first so the entity stays authoritative for
    // the computed values (SecurityStamp, Updated, …).
    // ---------------------------------------------------------------------------------------------

    /// <summary>Writes only <c>AvatarImagePath</c> and <c>Updated</c>.</summary>
    Task<int> UpdateAvatarPathAsync(string email, string? avatarImagePath, DateTime updated, CancellationToken ct = default);

    /// <summary>Writes only <c>TimeZoneIanaId</c> and <c>Updated</c>.</summary>
    Task<int> UpdateTimeZoneAsync(string email, string timeZoneIanaId, DateTime updated, CancellationToken ct = default);

    /// <summary>
    /// Writes only <c>HashedPassword</c>, <c>SecurityStamp</c>, <c>MustChangePassword</c>, <c>ResetPasswordToken</c>,
    /// <c>Locked</c>, <c>LoginAttempt</c>, <c>Message</c> and <c>Updated</c> (a self-service change also lifts a lock).
    /// </summary>
    Task<int> UpdatePasswordAsync(string email, string hashedPassword, string securityStamp, bool mustChangePassword, string? resetPasswordToken, bool locked, long loginAttempt, string? message, DateTime updated, CancellationToken ct = default);

    /// <summary>Writes only <c>DefaultMonetaryUnit</c>. Does not touch <c>Updated</c> — this is a background self-correction, not a user edit.</summary>
    Task<int> UpdateDefaultMonetaryUnitAsync(string email, string? defaultMonetaryUnit, CancellationToken ct = default);

    /// <summary>Writes only <c>ResetPasswordToken</c> and <c>Updated</c>.</summary>
    Task<int> UpdateResetPasswordTokenAsync(string email, string? resetPasswordToken, DateTime updated, CancellationToken ct = default);

    /// <summary>Writes only <c>RegistrationToken</c> and <c>Updated</c>.</summary>
    Task<int> UpdateRegistrationTokenAsync(string email, string? registrationToken, DateTime updated, CancellationToken ct = default);

    /// <summary>Writes only <c>AgreedServiceTerms</c> and <c>Updated</c>.</summary>
    Task<int> UpdateAgreedServiceTermsAsync(string email, bool agreedServiceTerms, DateTime updated, CancellationToken ct = default);

    /// <summary>Writes only <c>Message</c> and <c>Updated</c>.</summary>
    Task<int> UpdateMessageAsync(string email, string? message, DateTime updated, CancellationToken ct = default);

    /// <summary>Writes only <c>EmailConfirmed</c> (to <c>true</c>), <c>RegistrationToken</c>, <c>Message</c> and <c>Updated</c>.</summary>
    Task<int> UpdateEmailConfirmationAsync(string email, string? registrationToken, string? message, DateTime updated, CancellationToken ct = default);
}
