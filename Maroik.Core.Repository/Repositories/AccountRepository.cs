using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Domain.Account;
using Maroik.Core.PostgreSQL.Data;
using Microsoft.EntityFrameworkCore;
using OrmAccount = Maroik.Core.PostgreSQL.Models.Account;

namespace Maroik.Core.Repository.Repositories;

/// <summary>
/// EF Core repository for <see cref="Account"/> domain objects.
/// Maps between the <see cref="Account"/> domain aggregate and the <see cref="OrmAccount"/> PostgreSQL entity.
/// </summary>
public class AccountRepository(ApplicationDbContext context)
    : GenericRepository<Account, OrmAccount>(context), IAccountRepository
{
    /// <summary>The underlying <see cref="DbSet{TEntity}"/> for <see cref="OrmAccount"/> rows.</summary>
    protected override DbSet<OrmAccount> Set => Context.Accounts;

    /// <summary>Maps a persisted <see cref="OrmAccount"/> row to the <see cref="Account"/> domain aggregate.</summary>
    protected override Account ToDomain(OrmAccount e) => Account.Reconstitute(
        e.Email, e.HashedPassword, e.Nickname,
        e.AvatarImagePath, e.Role, e.TimeZoneIanaId,
        e.DefaultMonetaryUnit, e.Locked, e.LoginAttempt, e.EmailConfirmed,
        e.AgreedServiceTerms, e.RegistrationToken, e.ResetPasswordToken,
        e.Created, e.Updated, e.Message, e.Deleted,
        e.SecurityStamp, e.MustChangePassword);

    /// <summary>Maps an <see cref="Account"/> domain aggregate to its <see cref="OrmAccount"/> persistence representation.</summary>
    protected override OrmAccount ToEntity(Account a) => new()
    {
        Email = a.Email.Value,
        HashedPassword = a.HashedPassword,
        Nickname = a.Nickname,
        AvatarImagePath = a.AvatarImagePath ?? Account.DefaultAvatarImagePath,
        Role = a.Role.Value,
        TimeZoneIanaId = a.TimeZone.Value,
        DefaultMonetaryUnit = a.DefaultMonetaryUnit?.Value,
        Locked = a.Locked,
        LoginAttempt = a.LoginAttempt,
        EmailConfirmed = a.EmailConfirmed,
        AgreedServiceTerms = a.AgreedServiceTerms,
        RegistrationToken = a.RegistrationToken,
        ResetPasswordToken = a.ResetPasswordToken,
        Created = a.Created,
        Updated = a.Updated,
        Message = a.Message,
        Deleted = a.Deleted,
        SecurityStamp = a.SecurityStamp,
        MustChangePassword = a.MustChangePassword
    };

    /// <inheritdoc />
    public Task<List<Account>> GetAllAsync(CancellationToken ct = default)
        => QueryAllAsync(ct: ct);

    /// <inheritdoc />
    // See ExpenditureRepository.SearchByAccountEmailAsync for why this is raw SQL rather than LINQ
    // (ILIKE is case-insensitive natively, so it also covers the Locked/EmailConfirmed/
    // AgreedServiceTerms/Deleted bool matching). Every column the admin account grid renders is
    // searchable, HashedPassword / RegistrationToken / ResetPasswordToken included: they are shown
    // in that grid (and exported), so the whole-row search box must match what the admin sees.
    public async Task<List<Account>> SearchAsync(string search, CancellationToken ct = default)
    {
        string pattern = ToLikePattern(search);
        List<string> matchingEmails = await Context.Database.SqlQuery<string>($"""
            SELECT "Email" AS "Value" FROM "Account"
            WHERE
                  "Email" ILIKE {pattern} OR
                  "HashedPassword" ILIKE {pattern} OR
                  "Nickname" ILIKE {pattern} OR
                  "AvatarImagePath" ILIKE {pattern} OR
                  "Role" ILIKE {pattern} OR
                  "TimeZoneIanaId" ILIKE {pattern} OR
                  CAST("Locked" AS TEXT) ILIKE {pattern} OR
                  CAST("LoginAttempt" AS TEXT) ILIKE {pattern} OR
                  CAST("EmailConfirmed" AS TEXT) ILIKE {pattern} OR
                  CAST("AgreedServiceTerms" AS TEXT) ILIKE {pattern} OR
                  "RegistrationToken" ILIKE {pattern} OR
                  "ResetPasswordToken" ILIKE {pattern} OR
                  CAST("Created" AS TEXT) ILIKE {pattern} OR
                  CAST("Updated" AS TEXT) ILIKE {pattern} OR
                  "Message" ILIKE {pattern} OR
                  CAST("Deleted" AS TEXT) ILIKE {pattern}
            """).ToListAsync(ct);

        if (matchingEmails.Count == 0) return [];
        return await QueryAsync(e => matchingEmails.Contains(e.Email), ct: ct);
    }

    /// <inheritdoc />
    public Task<Account?> FindByEmailAsync(string email, CancellationToken ct = default)
    {
        // Email is always persisted lower-cased (see Email.Create), but callers here pass raw,
        // un-normalized user input (login form, forgot-password form, etc.) — normalize the
        // lookup key too, or a differently-cased-but-identical email never matches.
        string normalizedEmail = email.ToLowerInvariant();
        return QueryFirstAsync(e => e.Email == normalizedEmail, ct: ct);
    }

    /// <inheritdoc />
    public Task<Account?> FindByRegistrationTokenAsync(string token, CancellationToken ct = default)
        => QueryFirstAsync(e => e.RegistrationToken == token, ct: ct);

    /// <inheritdoc />
    public Task<Account?> FindByResetPasswordTokenAsync(string token, CancellationToken ct = default)
        => QueryFirstAsync(e => e.ResetPasswordToken == token, ct: ct);

    /// <inheritdoc />
    public Task<bool> NicknameExistsIgnoreCaseAsync(string nickname, CancellationToken ct = default)
    {
        string lowered = nickname.ToLowerInvariant();
        return Set.AnyAsync(e => e.Nickname.ToLower() == lowered, ct);
    }

    /// <inheritdoc />
    public Task<List<Account>> FindByNicknamesAsync(IEnumerable<string> nicknames, CancellationToken ct = default)
    {
        var list = nicknames.ToList();
        return QueryAsync(e => list.Contains(e.Nickname), ct: ct);
    }

    /// <inheritdoc />
    public async Task<Account?> FindByEmailForUpdateAsync(string email, CancellationToken ct = default)
    {
        // Normalize for the same reason as FindByEmailAsync above (this is the login lookup path).
        string normalizedEmail = email.ToLowerInvariant();

        // Raw SQL is required because EF Core LINQ cannot emit FOR UPDATE.
        // ToListAsync on the raw root executes the SQL verbatim (no composition),
        // keeping FOR UPDATE at the top level of the statement.
        // AsNoTracking is required too: if this Account's row was already tracked earlier in the
        // same DbContext scope, a tracking query performs identity resolution and hands back that
        // already-tracked (pre-lock) instance instead of the row this FOR UPDATE just (re-)read —
        // silently defeating the lock's whole purpose. AsNoTracking always materializes a fresh
        // instance from what was actually just selected, bypassing that snap-back.
        var entities = await Set.FromSqlInterpolated(
            $"""
             SELECT *
             FROM "Account"
             WHERE "Email" = {normalizedEmail}
             LIMIT 1
             FOR UPDATE
             """).AsNoTracking().ToListAsync(ct);

        var entity = entities.FirstOrDefault();
        return entity == null ? null : ToDomain(entity);
    }

    // -- Column-scoped updates (see IAccountRepository for the rationale) ---------------------------
    // ExecuteUpdateAsync emits one UPDATE that sets only the listed columns and runs immediately
    // (inside the ambient transaction if one is open), so it never overwrites a column a concurrent
    // writer just changed.

    /// <inheritdoc />
    public Task<int> UpdateAvatarPathAsync(string email, string? avatarImagePath, DateTime updated, CancellationToken ct = default)
    {
        string normalizedEmail = email.ToLowerInvariant();
        // Keep parity with ToEntity: a null avatar persists as the shared default-avatar path.
        string stored = avatarImagePath ?? Account.DefaultAvatarImagePath;
        return Set.Where(e => e.Email == normalizedEmail)
            .ExecuteUpdateAsync(s => s
                .SetProperty(e => e.AvatarImagePath, stored)
                .SetProperty(e => e.Updated, updated), ct);
    }

    /// <inheritdoc />
    public Task<int> UpdateTimeZoneAsync(string email, string timeZoneIanaId, DateTime updated, CancellationToken ct = default)
    {
        string normalizedEmail = email.ToLowerInvariant();
        return Set.Where(e => e.Email == normalizedEmail)
            .ExecuteUpdateAsync(s => s
                .SetProperty(e => e.TimeZoneIanaId, timeZoneIanaId)
                .SetProperty(e => e.Updated, updated), ct);
    }

    /// <inheritdoc />
    public Task<int> UpdatePasswordAsync(string email, string hashedPassword, string securityStamp, bool mustChangePassword, string? resetPasswordToken, DateTime updated, CancellationToken ct = default)
    {
        string normalizedEmail = email.ToLowerInvariant();
        return Set.Where(e => e.Email == normalizedEmail)
            .ExecuteUpdateAsync(s => s
                .SetProperty(e => e.HashedPassword, hashedPassword)
                .SetProperty(e => e.SecurityStamp, securityStamp)
                .SetProperty(e => e.MustChangePassword, mustChangePassword)
                .SetProperty(e => e.ResetPasswordToken, resetPasswordToken)
                .SetProperty(e => e.Updated, updated), ct);
    }

    /// <inheritdoc />
    public Task<int> UpdateDefaultMonetaryUnitAsync(string email, string? defaultMonetaryUnit, CancellationToken ct = default)
    {
        string normalizedEmail = email.ToLowerInvariant();
        return Set.Where(e => e.Email == normalizedEmail)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.DefaultMonetaryUnit, defaultMonetaryUnit), ct);
    }

    /// <inheritdoc />
    public Task<int> UpdateResetPasswordTokenAsync(string email, string? resetPasswordToken, DateTime updated, CancellationToken ct = default)
    {
        string normalizedEmail = email.ToLowerInvariant();
        return Set.Where(e => e.Email == normalizedEmail)
            .ExecuteUpdateAsync(s => s
                .SetProperty(e => e.ResetPasswordToken, resetPasswordToken)
                .SetProperty(e => e.Updated, updated), ct);
    }

    /// <inheritdoc />
    public Task<int> UpdateRegistrationTokenAsync(string email, string? registrationToken, DateTime updated, CancellationToken ct = default)
    {
        string normalizedEmail = email.ToLowerInvariant();
        return Set.Where(e => e.Email == normalizedEmail)
            .ExecuteUpdateAsync(s => s
                .SetProperty(e => e.RegistrationToken, registrationToken)
                .SetProperty(e => e.Updated, updated), ct);
    }

    /// <inheritdoc />
    public Task<int> UpdateAgreedServiceTermsAsync(string email, bool agreedServiceTerms, DateTime updated, CancellationToken ct = default)
    {
        string normalizedEmail = email.ToLowerInvariant();
        return Set.Where(e => e.Email == normalizedEmail)
            .ExecuteUpdateAsync(s => s
                .SetProperty(e => e.AgreedServiceTerms, agreedServiceTerms)
                .SetProperty(e => e.Updated, updated), ct);
    }

    /// <inheritdoc />
    public Task<int> UpdateMessageAsync(string email, string? message, DateTime updated, CancellationToken ct = default)
    {
        string normalizedEmail = email.ToLowerInvariant();
        return Set.Where(e => e.Email == normalizedEmail)
            .ExecuteUpdateAsync(s => s
                .SetProperty(e => e.Message, message)
                .SetProperty(e => e.Updated, updated), ct);
    }

    /// <inheritdoc />
    public Task<int> UpdateEmailConfirmationAsync(string email, string? registrationToken, string? message, DateTime updated, CancellationToken ct = default)
    {
        string normalizedEmail = email.ToLowerInvariant();
        return Set.Where(e => e.Email == normalizedEmail)
            .ExecuteUpdateAsync(s => s
                .SetProperty(e => e.EmailConfirmed, true)
                .SetProperty(e => e.RegistrationToken, registrationToken)
                .SetProperty(e => e.Message, message)
                .SetProperty(e => e.Updated, updated), ct);
    }
}
