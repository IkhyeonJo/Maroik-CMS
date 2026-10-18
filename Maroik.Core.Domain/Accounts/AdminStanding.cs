
namespace Maroik.Core.Domain.Accounts;

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
