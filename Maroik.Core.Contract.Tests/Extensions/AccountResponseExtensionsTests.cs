using Maroik.Core.Contract.Dtos;
using Maroik.Core.Contract.Misc.Extensions;
using Maroik.Core.Domain.Account;

namespace Maroik.Core.Contract.Tests.Extensions;

/// <summary>
/// Unit tests for <see cref="AccountResponseExtensions"/> — the owner / owner-or-admin
/// authorization rules used to gate edit/delete UI affordances for board posts, comments,
/// and private notes.
/// </summary>
public class AccountResponseExtensionsTests
{
    // -- IsOwner ----------------------------------------------------------------

    /// <summary>Is owner same nickname true.</summary>
    [Fact]
    public void IsOwner_SameNickname_True()
    {
        var viewer = new AccountResponse { Nickname = "Alice", Role = Role.User };
        Assert.True(viewer.IsOwner("Alice"));
    }

    /// <summary>Is owner different nickname false.</summary>
    [Fact]
    public void IsOwner_DifferentNickname_False()
    {
        var viewer = new AccountResponse { Nickname = "Alice", Role = Role.User };
        Assert.False(viewer.IsOwner("Bob"));
    }

    /// <summary>Is owner admin but not writer false.</summary>
    [Fact]
    public void IsOwner_AdminButNotWriter_False()
    {
        // Unlike IsOwnerOrAdmin, IsOwner never grants access based on role alone.
        var admin = new AccountResponse { Nickname = "AdminUser", Role = Role.Admin };
        Assert.False(admin.IsOwner("Bob"));
    }

    /// <summary>Is owner null viewer nickname false.</summary>
    [Fact]
    public void IsOwner_NullViewerNickname_False()
    {
        var anonymous = new AccountResponse { Nickname = null, Role = Role.Anonymous };
        Assert.False(anonymous.IsOwner("Bob"));
    }

    // -- IsOwnerOrAdmin -----------------------------------------------------------

    /// <summary>Is owner or admin same nickname true.</summary>
    [Fact]
    public void IsOwnerOrAdmin_SameNickname_True()
    {
        var viewer = new AccountResponse { Nickname = "Alice", Role = Role.User };
        Assert.True(viewer.IsOwnerOrAdmin("Alice"));
    }

    /// <summary>Is owner or admin not writer true.</summary>
    [Fact]
    public void IsOwnerOrAdmin_AdminNotWriter_True()
    {
        var admin = new AccountResponse { Nickname = "AdminUser", Role = Role.Admin };
        Assert.True(admin.IsOwnerOrAdmin("Bob"));
    }

    /// <summary>Is owner or admin regular user not writer false.</summary>
    [Fact]
    public void IsOwnerOrAdmin_RegularUserNotWriter_False()
    {
        var viewer = new AccountResponse { Nickname = "Alice", Role = Role.User };
        Assert.False(viewer.IsOwnerOrAdmin("Bob"));
    }

    /// <summary>Is owner or admin anonymous not writer false.</summary>
    [Fact]
    public void IsOwnerOrAdmin_AnonymousNotWriter_False()
    {
        var anonymous = new AccountResponse { Nickname = null, Role = Role.Anonymous };
        Assert.False(anonymous.IsOwnerOrAdmin("Bob"));
    }

    // -- SeesLocalTime ------------------------------------------------------------

    /// <summary>An Admin viewer sees local time.</summary>
    [Fact]
    public void SeesLocalTime_Admin_True()
    {
        var viewer = new AccountResponse { Nickname = "AdminUser", Role = Role.Admin };
        Assert.True(viewer.SeesLocalTime());
    }

    /// <summary>A regular User viewer sees local time, same as Admin.</summary>
    [Fact]
    public void SeesLocalTime_User_True()
    {
        var viewer = new AccountResponse { Nickname = "Alice", Role = Role.User };
        Assert.True(viewer.SeesLocalTime());
    }

    /// <summary>An Anonymous viewer does not see local time (raw UTC instead).</summary>
    [Fact]
    public void SeesLocalTime_Anonymous_False()
    {
        var anonymous = new AccountResponse { Nickname = null, Role = Role.Anonymous };
        Assert.False(anonymous.SeesLocalTime());
    }

    /// <summary>A null role (unauthenticated default) does not see local time.</summary>
    [Fact]
    public void SeesLocalTime_NullRole_False()
    {
        var viewer = new AccountResponse { Nickname = null, Role = null };
        Assert.False(viewer.SeesLocalTime());
    }

    // -- ToAdminNicknameSet -------------------------------------------------------

    /// <summary>To admin nickname set returns only admin nicknames.</summary>
    [Fact]
    public void ToAdminNicknameSet_ReturnsOnlyAdminNicknames()
    {
        var accounts = new[]
        {
            new AccountResponse { Nickname = "Alice", Role = Role.Admin },
            new AccountResponse { Nickname = "Bob", Role = Role.User },
            new AccountResponse { Nickname = "Carol", Role = Role.Admin }
        };

        var result = accounts.ToAdminNicknameSet();

        Assert.Equal([
            "Alice", "Carol"
        ], result);
    }

    /// <summary>To admin nickname set excludes admins with null or empty nickname.</summary>
    [Fact]
    public void ToAdminNicknameSet_ExcludesAdminsWithNullOrEmptyNickname()
    {
        var accounts = new[]
        {
            new AccountResponse { Nickname = null, Role = Role.Admin },
            new AccountResponse { Nickname = "", Role = Role.Admin },
            new AccountResponse { Nickname = "Dave", Role = Role.Admin }
        };

        var result = accounts.ToAdminNicknameSet();

        Assert.Equal([
            "Dave"
        ], result);
    }

    /// <summary>To admin nickname set empty collection returns empty set.</summary>
    [Fact]
    public void ToAdminNicknameSet_EmptyCollection_ReturnsEmptySet()
    {
        var result = Array.Empty<AccountResponse>().ToAdminNicknameSet();

        Assert.Empty(result);
    }
}
