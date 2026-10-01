using Maroik.Core.Domain.Account;
namespace Maroik.Core.Domain.Tests.Account;

/// <summary>
/// Unit tests for <see cref="AccountRole"/>: an account's role is Admin or User — never the
/// menu-only Anonymous role or any other string.
/// </summary>
public class AccountRoleTests
{
    /// <summary>The two account roles parse to their shared instances.</summary>
    [Fact]
    public void Create_ReturnsTheRole_ForAdminAndUser()
    {
        Assert.Equal(AccountRole.Admin, AccountRole.Create(Role.Admin).Value);
        Assert.Equal(AccountRole.User, AccountRole.Create(Role.User).Value);
        Assert.Equal(Role.Admin, AccountRole.Admin.Value);
        Assert.Equal(Role.User, AccountRole.User.Value);
    }

    /// <summary>Anything else — including the menu's Anonymous role — is refused.</summary>
    [Theory]
    [InlineData(Role.Anonymous)]
    [InlineData("Root")]
    [InlineData("admin")]
    [InlineData("")]
    [InlineData(null)]
    public void Create_RejectsAnythingButAdminOrUser(string? value)
    {
        var result = AccountRole.Create(value);

        Assert.True(result.IsError);
        Assert.Equal("Account.RoleInvalid", result.FirstError.Code);
        Assert.Equal("Role must be either Admin or User.", result.FirstError.Description);
    }

    /// <summary>Only the Admin role is an administrator.</summary>
    [Fact]
    public void IsAdmin_IsTrueForAdminOnly()
    {
        Assert.True(AccountRole.Admin.IsAdmin);
        Assert.False(AccountRole.User.IsAdmin);
    }

    /// <summary>The role renders as its stored value.</summary>
    [Fact]
    public void ToString_ReturnsTheStoredValue() => Assert.Equal("User", AccountRole.User.ToString());
}
