using Maroik.Core.Contract.Dtos;
using Maroik.Core.Contract.Misc.Extensions;
using Maroik.Core.Domain.Account;
using Maroik.Core.Domain.Board;

namespace Maroik.Core.Contract.Tests.Extensions;

/// <summary>
/// Unit tests for <see cref="BoardResponseExtensions"/> — the "can this viewer see a
/// locked post's title" rule used by the FreeForum and PrivateNote board list views.
/// </summary>
public class BoardResponseExtensionsTests
{
    // -- IsTitleVisibleToOwnerOrAdmin ---------------------------------------------

    /// <summary>Is title visible to owner or admin not locked any viewer true.</summary>
    [Fact]
    public void IsTitleVisibleToOwnerOrAdmin_NotLocked_AnyViewer_True()
    {
        var board = new BoardResponse { Locked = false, Writer = "Alice" };
        var viewer = new AccountResponse { Nickname = "Bob", Role = Role.User };

        Assert.True(board.IsTitleVisibleToOwnerOrAdmin(viewer));
    }

    /// <summary>Is title visible to owner or admin locked and writer true.</summary>
    [Fact]
    public void IsTitleVisibleToOwnerOrAdmin_LockedAndWriter_True()
    {
        var board = new BoardResponse { Locked = true, Writer = "Alice" };
        var viewer = new AccountResponse { Nickname = "Alice", Role = Role.User };

        Assert.True(board.IsTitleVisibleToOwnerOrAdmin(viewer));
    }

    /// <summary>Is title visible to owner or admin locked and admin not writer true.</summary>
    [Fact]
    public void IsTitleVisibleToOwnerOrAdmin_LockedAndAdminNotWriter_True()
    {
        var board = new BoardResponse { Locked = true, Writer = "Alice" };
        var admin = new AccountResponse { Nickname = "AdminUser", Role = Role.Admin };

        Assert.True(board.IsTitleVisibleToOwnerOrAdmin(admin));
    }

    /// <summary>Is title visible to owner or admin locked and regular user not writer false.</summary>
    [Fact]
    public void IsTitleVisibleToOwnerOrAdmin_LockedAndRegularUserNotWriter_False()
    {
        var board = new BoardResponse { Locked = true, Writer = "Alice" };
        var viewer = new AccountResponse { Nickname = "Bob", Role = Role.User };

        Assert.False(board.IsTitleVisibleToOwnerOrAdmin(viewer));
    }

    /// <summary>Is title visible to owner or admin locked and anonymous false.</summary>
    [Fact]
    public void IsTitleVisibleToOwnerOrAdmin_LockedAndAnonymous_False()
    {
        var board = new BoardResponse { Locked = true, Writer = "Alice" };
        var anonymous = new AccountResponse { Nickname = null, Role = Role.Anonymous };

        Assert.False(board.IsTitleVisibleToOwnerOrAdmin(anonymous));
    }

    // -- IsTitleVisibleToOwner ------------------------------------------------------

    /// <summary>Is title visible to owner not locked any viewer true.</summary>
    [Fact]
    public void IsTitleVisibleToOwner_NotLocked_AnyViewer_True()
    {
        var board = new BoardResponse { Locked = false, Writer = "Alice" };
        var viewer = new AccountResponse { Nickname = "Bob", Role = Role.User };

        Assert.True(board.IsTitleVisibleToOwner(viewer));
    }

    /// <summary>Is title visible to owner locked and writer true.</summary>
    [Fact]
    public void IsTitleVisibleToOwner_LockedAndWriter_True()
    {
        var board = new BoardResponse { Locked = true, Writer = "Alice" };
        var viewer = new AccountResponse { Nickname = "Alice", Role = Role.User };

        Assert.True(board.IsTitleVisibleToOwner(viewer));
    }

    /// <summary>Is title visible to owner locked and admin not writer false.</summary>
    [Fact]
    public void IsTitleVisibleToOwner_LockedAndAdminNotWriter_False()
    {
        // Unlike IsTitleVisibleToOwnerOrAdmin, admin status alone does not grant access here.
        var board = new BoardResponse { Locked = true, Writer = "Alice" };
        var admin = new AccountResponse { Nickname = "AdminUser", Role = Role.Admin };

        Assert.False(board.IsTitleVisibleToOwner(admin));
    }

    /// <summary>Is title visible to owner locked and regular user not writer false.</summary>
    [Fact]
    public void IsTitleVisibleToOwner_LockedAndRegularUserNotWriter_False()
    {
        var board = new BoardResponse { Locked = true, Writer = "Alice" };
        var viewer = new AccountResponse { Nickname = "Bob", Role = Role.User };

        Assert.False(board.IsTitleVisibleToOwner(viewer));
    }

    /// <summary>Is title visible to owner locked and anonymous false.</summary>
    [Fact]
    public void IsTitleVisibleToOwner_LockedAndAnonymous_False()
    {
        var board = new BoardResponse { Locked = true, Writer = "Alice" };
        var anonymous = new AccountResponse { Nickname = null, Role = Role.Anonymous };

        Assert.False(board.IsTitleVisibleToOwner(anonymous));
    }

    // -- Delegation to Board.CanBeViewedBy (PrivateNote is owner-only regardless of lock) ---------

    /// <summary>
    /// Regression test: an UNLOCKED PrivateNote's title must still be hidden from a non-owner.
    /// The old hand-rolled "!Locked || IsOwner" formula would have returned true here (a non-owner
    /// could see another account's unlocked private-note title); delegating to
    /// <see cref="Board.CanBeViewedBy(string?, bool, bool, string?, string?, bool)"/> makes
    /// PrivateNote owner-only unconditionally, matching the domain rule.
    /// </summary>
    [Fact]
    public void IsTitleVisibleToOwner_PrivateNoteNotLockedNotWriter_False()
    {
        var board = new BoardResponse { Type = BoardTypes.PrivateNote, Locked = false, Writer = "Alice" };
        var viewer = new AccountResponse { Nickname = "Bob", Role = Role.User };

        Assert.False(board.IsTitleVisibleToOwner(viewer));
    }

    /// <summary>An unlocked PrivateNote's title is still visible to its own writer.</summary>
    [Fact]
    public void IsTitleVisibleToOwner_PrivateNoteNotLockedIsWriter_True()
    {
        var board = new BoardResponse { Type = BoardTypes.PrivateNote, Locked = false, Writer = "Alice" };
        var viewer = new AccountResponse { Nickname = "Alice", Role = Role.User };

        Assert.True(board.IsTitleVisibleToOwner(viewer));
    }

    /// <summary>A soft-deleted post's title is never visible, even to its own writer.</summary>
    [Fact]
    public void IsTitleVisibleToOwnerOrAdmin_Deleted_False()
    {
        var board = new BoardResponse { Locked = false, Deleted = true, Writer = "Alice" };
        var viewer = new AccountResponse { Nickname = "Alice", Role = Role.Admin };

        Assert.False(board.IsTitleVisibleToOwnerOrAdmin(viewer));
    }
}
