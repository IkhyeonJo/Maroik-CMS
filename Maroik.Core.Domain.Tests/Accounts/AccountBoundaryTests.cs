using System.Text.RegularExpressions;
using Maroik.Core.Domain.Accounts;
using DomainAccount = Maroik.Core.Domain.Accounts.Account;

namespace Maroik.Core.Domain.Tests.Accounts;

/// <summary>
/// Boundary and round-trip rules of the account aggregate: the lock / unlock attempt count, the security stamp of a new
/// account, and a reconstituted account keeping every field.
/// </summary>
public class AccountBoundaryTests
{
    /// <summary>The fixed "current time" every domain call in this class receives.</summary>
    private static readonly DateTime _now = new(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>Owner e-mail used by every aggregate built in these tests.</summary>
    private const string Email = "user@example.com";

    /// <summary>A persisted, confirmed account with the given lockout state.</summary>
    private static DomainAccount Account(bool locked = false, long loginAttempt = 0) =>
        DomainAccount.Reconstitute(Email, "$2a$hash", "Nick", null, Role.User, "UTC", null, locked, loginAttempt, true, true,
            null, null, DateTime.UtcNow, DateTime.UtcNow, null, false, "stamp", false);

    /// <summary>Verifies that locking keeps the login attempt count and unlocking clears it.</summary>
    [Fact]
    public void Lock_KeepsTheAttemptCount_AndUnlockClearsIt()
    {
        DomainAccount account = Account(locked: false, loginAttempt: 3);
        account.Lock(_now);
        Assert.Equal(3, account.LoginAttempt);

        account.Unlock(_now);
        Assert.Equal(0, account.LoginAttempt);
    }

    /// <summary>Verifies that each new account gets a different 32-hex-digit security stamp.</summary>
    [Fact]
    public void NewAccounts_GetADistinctHyphenlessSecurityStamp()
    {
        string stamp1 = DomainAccount.Create(Email, "$2a$h", "Nick", AccountRole.User, "UTC", null, GuidToken.Generate(_now), true, _now).Value.SecurityStamp;
        string stamp2 = DomainAccount.Create(Email, "$2a$h", "Nick", AccountRole.User, "UTC", null, GuidToken.Generate(_now), true, _now).Value.SecurityStamp;

        Assert.Matches(new Regex("^[0-9a-f]{32}$"), stamp1);
        Assert.NotEqual(stamp1, stamp2);
    }

    /// <summary>Verifies that <c>Account.Reconstitute</c> restores every field (the email lower-cased).</summary>
    [Fact]
    public void Account_Reconstitute_RestoresEveryFieldVerbatim()
    {
        DateTime created = new(2025, 1, 2, 3, 4, 5), updated = new(2025, 6, 7, 8, 9, 10);

        DomainAccount account = DomainAccount.Reconstitute("Mixed@Case.com", "$2a$hash", "Nick", "/a.png", Role.Admin, "Asia/Seoul", "KRW",
            locked: true, loginAttempt: 4, emailConfirmed: true, agreedServiceTerms: true, registrationToken: "reg", resetPasswordToken: "reset",
            created, updated, "msg", deleted: true, securityStamp: "stamp", mustChangePassword: true);

        Assert.Equal("mixed@case.com", account.Email.Value);
        Assert.Equal("$2a$hash", account.HashedPassword);
        Assert.Equal("Nick", account.Nickname);
        Assert.Equal("/a.png", account.AvatarImagePath);
        Assert.Equal(Role.Admin, account.Role.Value);
        Assert.Equal("Asia/Seoul", account.TimeZone.Value);
        Assert.Equal("KRW", account.DefaultMonetaryUnit?.Value);
        Assert.True(account.Locked);
        Assert.Equal(4, account.LoginAttempt);
        Assert.True(account.EmailConfirmed);
        Assert.True(account.AgreedServiceTerms);
        Assert.Equal("reg", account.RegistrationToken);
        Assert.Equal("reset", account.ResetPasswordToken);
        Assert.Equal(created, account.Created);
        Assert.Equal(updated, account.Updated);
        Assert.Equal("msg", account.Message);
        Assert.True(account.Deleted);
        Assert.Equal("stamp", account.SecurityStamp);
        Assert.True(account.MustChangePassword);
    }
}
