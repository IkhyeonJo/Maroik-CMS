using Maroik.Core.Domain.Account;
namespace Maroik.Core.Domain.Tests.Account;

/// <summary>
/// Unit tests for <see cref="AccountViewPolicy"/> — the role-based rule deciding whether a viewer
/// sees timestamps converted to local time versus raw UTC.
/// </summary>
public class AccountViewPolicyTests
{
    /// <summary>Admin viewers see local time.</summary>
    [Fact]
    public void SeesLocalTime_Admin_True()
    {
        Assert.True(AccountViewPolicy.SeesLocalTime(Role.Admin));
    }

    /// <summary>User viewers see local time, same as Admin.</summary>
    [Fact]
    public void SeesLocalTime_User_True()
    {
        Assert.True(AccountViewPolicy.SeesLocalTime(Role.User));
    }

    /// <summary>Anonymous viewers do not see local time.</summary>
    [Fact]
    public void SeesLocalTime_Anonymous_False()
    {
        Assert.False(AccountViewPolicy.SeesLocalTime(Role.Anonymous));
    }

    /// <summary>A null role does not see local time.</summary>
    [Fact]
    public void SeesLocalTime_NullRole_False()
    {
        Assert.False(AccountViewPolicy.SeesLocalTime(null));
    }
}
