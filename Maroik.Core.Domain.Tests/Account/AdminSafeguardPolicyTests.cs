using ErrorOr;
using Maroik.Core.Domain.Account;
using Maroik.Core.Domain.Errors;
using Maroik.Core.Domain.ValueObjects;

namespace Maroik.Core.Domain.Tests.Account;

/// <summary>
/// Unit tests for <see cref="AdminSafeguardPolicy"/> and <see cref="AdminStanding"/>: an administrator cannot lock,
/// delete or demote their own account, and nobody can lock, delete or demote the last active administrator.
/// </summary>
public class AdminSafeguardPolicyTests
{
    /// <summary>An unlocked, undeleted administrator.</summary>
    private static readonly AdminStanding _activeAdmin = new(IsAdmin: true, Locked: false, Deleted: false);

    /// <summary>An unlocked, undeleted ordinary user.</summary>
    private static readonly AdminStanding _activeUser = new(IsAdmin: false, Locked: false, Deleted: false);

    /// <summary>Asserts a refusal with the given code, type and English message template (the resx key).</summary>
    private static void AssertRefused(ErrorOr<Success> result, string code, ErrorType type, string template)
    {
        Assert.True(result.IsError);
        Assert.Equal(code, result.FirstError.Code);
        Assert.Equal(type, result.FirstError.Type);
        Assert.Equal(template, result.FirstError.Metadata![DomainError.MessageTemplateMetadataKey]);
    }

    // -- AdminStanding --------------------------------------------------------

    /// <summary>Only an administrator that is neither locked nor deleted counts as active.</summary>
    [Theory]
    [InlineData(true, false, false, true)]
    [InlineData(true, true, false, false)]
    [InlineData(true, false, true, false)]
    [InlineData(false, false, false, false)]
    public void IsActiveAdmin_RequiresAnUnlockedUndeletedAdmin(bool isAdmin, bool locked, bool deleted, bool expected)
        => Assert.Equal(expected, new AdminStanding(isAdmin, locked, deleted).IsActiveAdmin);

    /// <summary>Of reads the role, lock and delete flags off the account.</summary>
    [Fact]
    public void Of_ReadsRoleLockAndDeleteFromTheAccount()
    {
        DateTime now = new(2026, 10, 10, 0, 0, 0, DateTimeKind.Utc);
        var account = Domain.Account.Account.Create("admin@example.com", "hashed", "Boss", AccountRole.Admin, "UTC",
            CurrencyCode.Create("KRW").Value, registrationToken: null, agreedServiceTerms: true, now).Value;

        Assert.Equal(new AdminStanding(true, false, false), AdminStanding.Of(account));

        account.ChangeRole(AccountRole.User, now);
        account.Lock(now);
        account.SoftDelete(now);
        Assert.Equal(new AdminStanding(false, true, true), AdminStanding.Of(account));
    }

    // -- Own account ----------------------------------------------------------

    /// <summary>An administrator cannot delete their own account.</summary>
    [Fact]
    public void CheckChange_RefusesDeletingYourOwnAccount()
        => AssertRefused(
            AdminSafeguardPolicy.CheckChange(actingOnSelf: true, _activeAdmin, _activeAdmin with { Deleted = true }, otherActiveAdmins: 3),
            "Account.CannotDeleteSelf", ErrorType.Validation, "You cannot delete your own account.");

    /// <summary>An administrator cannot lock their own account.</summary>
    [Fact]
    public void CheckChange_RefusesLockingYourOwnAccount()
        => AssertRefused(
            AdminSafeguardPolicy.CheckChange(actingOnSelf: true, _activeAdmin, _activeAdmin with { Locked = true }, otherActiveAdmins: 3),
            "Account.CannotLockSelf", ErrorType.Validation, "You cannot lock your own account.");

    /// <summary>An administrator cannot take the Admin role off their own account.</summary>
    [Fact]
    public void CheckChange_RefusesDemotingYourOwnAccount()
        => AssertRefused(
            AdminSafeguardPolicy.CheckChange(actingOnSelf: true, _activeAdmin, _activeAdmin with { IsAdmin = false }, otherActiveAdmins: 3),
            "Account.CannotDemoteSelf", ErrorType.Validation, "You cannot remove the Admin role from your own account.");

    /// <summary>Re-saving your own account that is already locked or deleted changes nothing, so it is allowed.</summary>
    [Fact]
    public void CheckChange_AllowsResavingYourOwnAccountInItsCurrentState()
    {
        var lockedAdmin = _activeAdmin with { Locked = true };
        Assert.False(AdminSafeguardPolicy.CheckChange(true, lockedAdmin, lockedAdmin, otherActiveAdmins: 0).IsError);

        var deletedAdmin = _activeAdmin with { Deleted = true };
        Assert.False(AdminSafeguardPolicy.CheckChange(true, deletedAdmin, deletedAdmin, otherActiveAdmins: 0).IsError);
    }

    /// <summary>Unlocking or restoring your own account is allowed.</summary>
    [Fact]
    public void CheckChange_AllowsUnlockingAndRestoringYourOwnAccount()
    {
        Assert.False(AdminSafeguardPolicy.CheckChange(true, _activeAdmin with { Locked = true }, _activeAdmin, otherActiveAdmins: 0).IsError);
        Assert.False(AdminSafeguardPolicy.CheckChange(true, _activeAdmin with { Deleted = true }, _activeAdmin, otherActiveAdmins: 0).IsError);
    }

    /// <summary>The self rule is reported ahead of the last-admin rule when both apply.</summary>
    [Fact]
    public void CheckChange_ReportsTheSelfRule_WhenYouAreAlsoTheLastActiveAdmin()
        => AssertRefused(
            AdminSafeguardPolicy.CheckChange(actingOnSelf: true, _activeAdmin, _activeAdmin with { Locked = true }, otherActiveAdmins: 0),
            "Account.CannotLockSelf", ErrorType.Validation, "You cannot lock your own account.");

    // -- Last active administrator --------------------------------------------

    /// <summary>Locking, deleting or demoting the last active administrator is refused, whoever does it.</summary>
    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void CheckChange_RefusesRemovingTheLastActiveAdmin(bool demote, bool locked, bool deleted)
        => AssertRefused(
            AdminSafeguardPolicy.CheckChange(actingOnSelf: false, _activeAdmin, new AdminStanding(!demote, locked, deleted), otherActiveAdmins: 0),
            "Account.LastActiveAdmin", ErrorType.Conflict, "The last active administrator cannot be locked, deleted or demoted.");

    /// <summary>Another administrator may be locked, deleted or demoted while another active one remains.</summary>
    [Fact]
    public void CheckChange_AllowsRemovingAnAdmin_WhileAnotherActiveAdminRemains()
        => Assert.False(AdminSafeguardPolicy.CheckChange(false, _activeAdmin, _activeAdmin with { Locked = true }, otherActiveAdmins: 1).IsError);

    /// <summary>An administrator who is already inactive is not the last active one; changing them is allowed.</summary>
    [Fact]
    public void CheckChange_AllowsChangingAnAlreadyInactiveAdmin()
        => Assert.False(AdminSafeguardPolicy.CheckChange(false, _activeAdmin with { Locked = true }, _activeAdmin with { Deleted = true, Locked = true }, otherActiveAdmins: 0).IsError);

    /// <summary>Changes that keep the last administrator active, and changes to ordinary users, are allowed.</summary>
    [Fact]
    public void CheckChange_AllowsChangesThatKeepTheAdminActive_AndChangesToUsers()
    {
        Assert.False(AdminSafeguardPolicy.CheckChange(false, _activeAdmin, _activeAdmin, otherActiveAdmins: 0).IsError);
        Assert.False(AdminSafeguardPolicy.CheckChange(false, _activeUser, _activeUser with { Locked = true, Deleted = true }, otherActiveAdmins: 0).IsError);
        Assert.False(AdminSafeguardPolicy.CheckChange(false, _activeUser, _activeAdmin, otherActiveAdmins: 0).IsError);
    }
}
