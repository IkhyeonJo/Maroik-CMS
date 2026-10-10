using ErrorOr;
using Maroik.Core.Domain.Errors;

namespace Maroik.Core.Domain.Account;

/// <summary>
/// The part of an account's state that decides whether it keeps the site administrable: its role and its
/// lock / delete flags, captured before and after an administrator's edit for <see cref="AdminSafeguardPolicy"/>.
/// </summary>
/// <param name="IsAdmin">Whether the account has the Admin role.</param>
/// <param name="Locked">Whether the account is locked.</param>
/// <param name="Deleted">Whether the account is soft-deleted.</param>
public readonly record struct AdminStanding(bool IsAdmin, bool Locked, bool Deleted)
{
    /// <summary>
    /// An administrator who can still sign in and reach the admin pages: Admin role, not locked, not deleted.
    /// </summary>
    public bool IsActiveAdmin => IsAdmin && !Locked && !Deleted;

    /// <summary>Reads the standing off <paramref name="account"/> as it is now.</summary>
    public static AdminStanding Of(Account account) => new(account.Role.IsAdmin, account.Locked, account.Deleted);
}

/// <summary>
/// Keeps an administrator from shutting the site's administration out by mistake. Checked on every
/// administrator edit or delete of an account (Management/Account):
/// <list type="bullet">
///   <item>an administrator cannot lock, delete or demote their own account — re-saving it in the state it is
///   already in, unlocking it and restoring it stay allowed;</item>
///   <item>nobody can lock, delete or demote the last active administrator (<see cref="AdminStanding.IsActiveAdmin"/>).</item>
/// </list>
/// A lockout from failed logins is not an administrator's edit and is not covered: anyone can still lock any
/// account, an administrator's included, by guessing wrong (see <see cref="Account.RecordLoginFailure"/>).
/// </summary>
public static class AdminSafeguardPolicy
{
    /// <summary>
    /// Refuses the change from <paramref name="before"/> to <paramref name="after"/> when it is one of the
    /// self-lockouts above, or when it turns the last active administrator into an inactive one.
    /// <paramref name="otherActiveAdmins"/> counts the active administrators other than the edited account.
    /// The own-account rules are reported first, as they tell the administrator more.
    /// </summary>
    public static ErrorOr<Success> CheckChange(bool actingOnSelf, AdminStanding before, AdminStanding after, int otherActiveAdmins)
    {
        if (actingOnSelf)
        {
            if (!before.Deleted && after.Deleted)
                return DomainError.Validation("Account.CannotDeleteSelf", "You cannot delete your own account.");
            if (!before.Locked && after.Locked)
                return DomainError.Validation("Account.CannotLockSelf", "You cannot lock your own account.");
            if (before.IsAdmin && !after.IsAdmin)
                return DomainError.Validation("Account.CannotDemoteSelf", "You cannot remove the Admin role from your own account.");
        }

        if (before.IsActiveAdmin && !after.IsActiveAdmin && otherActiveAdmins == 0)
            return DomainError.Conflict("Account.LastActiveAdmin", "The last active administrator cannot be locked, deleted or demoted.");

        return Result.Success;
    }
}
