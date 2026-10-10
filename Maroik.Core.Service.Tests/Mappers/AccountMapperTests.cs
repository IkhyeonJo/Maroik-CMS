using Maroik.Core.Contract.Dtos;
using Maroik.Core.Domain.Account;
using Maroik.Core.Service.Mappers;

namespace Maroik.Core.Service.Tests.Mappers;

/// <summary>Unit tests for <see cref="AccountMapper"/>.</summary>
public class AccountMapperTests
{
    /// <summary>To response maps all fields.</summary>
    [Fact]
    public void ToResponse_MapsAllFields()
    {
        var created = new DateTime(2026, 1, 1, 1, 0, 0, DateTimeKind.Utc);
        var updated = new DateTime(2026, 1, 2, 2, 0, 0, DateTimeKind.Utc);

        Account account = Account.Reconstitute(
            email: "alice@example.com",
            hashedPassword: "hashed-pw",
            nickname: "Alice",
            avatarImagePath: "/avatars/alice.png",
            role: "Admin",
            timeZoneIanaId: "Asia/Seoul",
            defaultMonetaryUnit: "KRW",
            locked: true,
            loginAttempt: 3,
            emailConfirmed: true,
            agreedServiceTerms: true,
            registrationToken: "reg-token",
            resetPasswordToken: "reset-token",
            created: created,
            updated: updated,
            message: "locked for review",
            deleted: false,
            securityStamp: "stamp",
            mustChangePassword: false);

        AccountResponse response = AccountMapper.ToResponse(account);

        Assert.Equal("alice@example.com", response.Email);
        Assert.Equal("Alice", response.Nickname);
        Assert.Equal("/avatars/alice.png", response.AvatarImagePath);
        Assert.Equal("Admin", response.Role);
        Assert.Equal("Asia/Seoul", response.TimeZoneIanaId);
        Assert.Equal("KRW", response.DefaultMonetaryUnit);
        Assert.True(response.Locked);
        Assert.Equal(3, response.LoginAttempt);
        Assert.True(response.EmailConfirmed);
        Assert.True(response.AgreedServiceTerms);
        Assert.Equal(created, response.Created);
        Assert.Equal(updated, response.Updated);
        Assert.Equal("locked for review", response.Message);
        Assert.False(response.Deleted);
    }
}
