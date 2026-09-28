using Maroik.Core.Domain.Board;
namespace Maroik.Core.Domain.Tests.Board;

/// <summary>
/// Unit tests for <see cref="Maroik.Core.Domain.Board.Board"/>.
/// Covers board creation, update, view incrementing, comment management, lock/pin, and soft-delete.
/// </summary>
public class BoardTests
{
    private static Domain.Board.Board ValidBoard(long id = 1) =>
        Domain.Board.Board.Reconstitute(id, "FreeForum", "Hello World", "content", "Alice", DateTime.UtcNow, DateTime.UtcNow, 0, false, false, false);

    private static BoardComment ValidComment(long boardId = 1) =>
        BoardComment.Reconstitute(1, boardId, 1, null, "Bob", "Nice post!", DateTime.UtcNow, false);

    // -- Create ---------------------------------------------------------------

    /// <summary>Create returns board, when valid.</summary>
    [Fact]
    public void Create_ReturnsBoard_WhenValid()
    {
        var result = Domain.Board.Board.Create("FreeForum", "Title", "body", "Alice");

        Assert.False(result.IsError);
        Assert.Equal("FreeForum", result.Value.Type);
        Assert.Equal("Alice", result.Value.Writer);
    }

    /// <summary>Create returns error, when type empty.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_ReturnsError_WhenTypeEmpty(string? type)
    {
        var result = Domain.Board.Board.Create(type, "Title", null, "Alice");

        Assert.True(result.IsError);
        Assert.Equal("Board.TypeEmpty", result.FirstError.Code);
    }

    /// <summary>Create rejects a type outside the known taxonomy (BuildAttachedFilePath puts it in a path).</summary>
    [Theory]
    [InlineData("NotARealType")]
    [InlineData("../../etc")]
    [InlineData("freeforum")]
    public void Create_ReturnsError_WhenTypeNotKnown(string type)
    {
        var result = Domain.Board.Board.Create(type, "Title", null, "Alice");

        Assert.True(result.IsError);
        Assert.Equal("Board.TypeInvalid", result.FirstError.Code);
    }

    /// <summary>Create accepts every value in the known board-type taxonomy.</summary>
    [Theory]
    [InlineData(BoardTypes.FreeForum)]
    [InlineData(BoardTypes.PrivateNote)]
    public void Create_Succeeds_ForKnownType(string type)
    {
        var result = Domain.Board.Board.Create(type, "Title", "body", "Alice");

        Assert.False(result.IsError);
        Assert.Equal(type, result.Value.Type);
    }

    /// <summary>Create returns error, when title empty.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_ReturnsError_WhenTitleEmpty(string? title)
    {
        var result = Domain.Board.Board.Create("FreeForum", title, null, "Alice");

        Assert.True(result.IsError);
        Assert.Equal("Board.TitleEmpty", result.FirstError.Code);
    }

    /// <summary>Create returns error, when title too long.</summary>
    [Fact]
    public void Create_ReturnsError_WhenTitleTooLong()
    {
        var result = Domain.Board.Board.Create("FreeForum", new string('x', 101), null, "Alice");

        Assert.True(result.IsError);
        Assert.Equal("Board.TitleTooLong", result.FirstError.Code);
    }

    /// <summary>Create returns error, when content too long.</summary>
    [Fact]
    public void Create_ReturnsError_WhenContentTooLong()
    {
        var result = Domain.Board.Board.Create("FreeForum", "Title", new string('x', 16385), "Alice");

        Assert.True(result.IsError);
        Assert.Equal("Board.ContentTooLong", result.FirstError.Code);
    }

    /// <summary>Create returns error, when writer empty.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_ReturnsError_WhenWriterEmpty(string? writer)
    {
        var result = Domain.Board.Board.Create("FreeForum", "Title", null, writer);

        Assert.True(result.IsError);
        Assert.Equal("Board.WriterEmpty", result.FirstError.Code);
    }

    // -- Update ---------------------------------------------------------------

    /// <summary>Update succeeds, when valid.</summary>
    [Fact]
    public void Update_Succeeds_WhenValid()
    {
        var board = ValidBoard();

        var result = board.Update("New Title", "New content");

        Assert.False(result.IsError);
        Assert.Equal("New Title", board.Title);
    }

    /// <summary>Update returns error, when deleted.</summary>
    [Fact]
    public void Update_ReturnsError_WhenDeleted()
    {
        var board = ValidBoard();
        board.SoftDelete();

        var result = board.Update("New Title", null);

        Assert.True(result.IsError);
        Assert.Equal("Board.Deleted", result.FirstError.Code);
    }

    /// <summary>Update returns error, when title too long.</summary>
    [Fact]
    public void Update_ReturnsError_WhenTitleTooLong()
    {
        var board = ValidBoard();

        var result = board.Update(new string('x', 101), null);

        Assert.True(result.IsError);
        Assert.Equal("Board.TitleTooLong", result.FirstError.Code);
    }

    // -- IncrementView ---------------------------------------------------------

    /// <summary>Increment view increments counter.</summary>
    [Fact]
    public void IncrementView_IncrementsCounter()
    {
        var board = ValidBoard();

        board.IncrementView();
        board.IncrementView();

        Assert.Equal(2, board.View);
    }

    // -- AddComment ------------------------------------------------------------

    /// <summary>Add comment succeeds, when not locked or deleted.</summary>
    [Fact]
    public void AddComment_Succeeds_WhenNotLockedOrDeleted()
    {
        var board = ValidBoard(id: 1);
        var comment = ValidComment(boardId: 1);

        var result = board.AddComment(comment);

        Assert.False(result.IsError);
        Assert.Single(board.Comments);
    }

    /// <summary>Add comment returns error, when locked.</summary>
    [Fact]
    public void AddComment_ReturnsError_WhenLocked()
    {
        var board = ValidBoard(id: 1);
        board.Lock();

        var result = board.AddComment(ValidComment(boardId: 1));

        Assert.True(result.IsError);
        Assert.Equal("Board.Locked", result.FirstError.Code);
    }

    /// <summary>Add comment returns error, when locked and commenter is neither the owner nor an admin.</summary>
    [Fact]
    public void AddComment_ReturnsError_WhenLockedAndCommenterIsNotOwnerOrAdmin()
    {
        var board = ValidBoard(id: 1);
        board.Lock();

        var result = board.AddComment(ValidComment(boardId: 1), commenterNickname: "Bob", isAdmin: false);

        Assert.True(result.IsError);
        Assert.Equal("Board.Locked", result.FirstError.Code);
    }

    /// <summary>Add comment succeeds on a locked post, when the commenter is the post's owner.</summary>
    [Fact]
    public void AddComment_Succeeds_WhenLockedAndCommenterIsOwner()
    {
        var board = ValidBoard(id: 1);
        board.Lock();

        var result = board.AddComment(ValidComment(boardId: 1), commenterNickname: "Alice", isAdmin: false);

        Assert.False(result.IsError);
        Assert.Single(board.Comments);
    }

    /// <summary>Add comment succeeds on a locked post, when the commenter is an admin.</summary>
    [Fact]
    public void AddComment_Succeeds_WhenLockedAndCommenterIsAdmin()
    {
        var board = ValidBoard(id: 1);
        board.Lock();

        var result = board.AddComment(ValidComment(boardId: 1), commenterNickname: "Bob", isAdmin: true);

        Assert.False(result.IsError);
        Assert.Single(board.Comments);
    }

    /// <summary>Add comment returns error, when deleted.</summary>
    [Fact]
    public void AddComment_ReturnsError_WhenDeleted()
    {
        var board = ValidBoard(id: 1);
        board.SoftDelete();

        var result = board.AddComment(ValidComment(boardId: 1));

        Assert.True(result.IsError);
        Assert.Equal("Board.Deleted", result.FirstError.Code);
    }

    /// <summary>Add comment returns error, when comment board id mismatch.</summary>
    [Fact]
    public void AddComment_ReturnsError_WhenCommentBoardIdMismatch()
    {
        var board = ValidBoard(id: 1);
        var comment = ValidComment(boardId: 99);

        var result = board.AddComment(comment);

        Assert.True(result.IsError);
        Assert.Equal("Board.CommentMismatch", result.FirstError.Code);
    }

    // -- Lock / Unlock ---------------------------------------------------------

    /// <summary>Lock sets locked true.</summary>
    [Fact]
    public void Lock_SetsLockedTrue()
    {
        var board = ValidBoard();

        board.Lock();

        Assert.True(board.Locked);
    }

    /// <summary>Unlock sets locked false.</summary>
    [Fact]
    public void Unlock_SetsLockedFalse()
    {
        var board = ValidBoard();
        board.Lock();

        board.Unlock();

        Assert.False(board.Locked);
    }

    // -- Pin / Unpin -----------------------------------------------------------

    /// <summary>Pin sets noticed true.</summary>
    [Fact]
    public void Pin_SetsNoticedTrue()
    {
        var board = ValidBoard();

        board.Pin();

        Assert.True(board.Noticed);
    }

    /// <summary>Unpin sets noticed false.</summary>
    [Fact]
    public void Unpin_SetsNoticedFalse()
    {
        var board = ValidBoard();
        board.Pin();

        board.Unpin();

        Assert.False(board.Noticed);
    }

    // -- SoftDelete ------------------------------------------------------------

    /// <summary>Soft delete succeeds, when not deleted.</summary>
    [Fact]
    public void SoftDelete_Succeeds_WhenNotDeleted()
    {
        var board = ValidBoard();

        var result = board.SoftDelete();

        Assert.False(result.IsError);
        Assert.True(board.Deleted);
    }

    /// <summary>Soft delete returns error, when already deleted.</summary>
    [Fact]
    public void SoftDelete_ReturnsError_WhenAlreadyDeleted()
    {
        var board = ValidBoard();
        board.SoftDelete();

        var result = board.SoftDelete();

        Assert.True(result.IsError);
        Assert.Equal("Board.AlreadyDeleted", result.FirstError.Code);
    }

    // -- IsOwnedBy / CanBeEditedBy / CanBeDeletedBy -----------------------------

    /// <summary>Is owned by returns true for writer.</summary>
    [Fact]
    public void IsOwnedBy_ReturnsTrue_ForWriter()
    {
        var board = ValidBoard();

        Assert.True(board.IsOwnedBy("Alice"));
    }

    /// <summary>Is owned by returns false for non writer.</summary>
    [Fact]
    public void IsOwnedBy_ReturnsFalse_ForNonWriter()
    {
        var board = ValidBoard();

        Assert.False(board.IsOwnedBy("Bob"));
    }

    /// <summary>Can be edited by returns true for writer.</summary>
    [Fact]
    public void CanBeEditedBy_ReturnsTrue_ForWriter()
    {
        var board = ValidBoard();

        Assert.True(board.CanBeEditedBy("Alice"));
    }

    /// <summary>Can be edited by returns false for admin non writer.</summary>
    [Fact]
    public void CanBeEditedBy_ReturnsFalse_ForAdminNonWriter()
    {
        // Editing is owner-only, unlike deleting — no admin bypass.
        var board = ValidBoard();

        Assert.False(board.CanBeEditedBy("Bob"));
    }

    /// <summary>Can be deleted by returns true for writer.</summary>
    [Fact]
    public void CanBeDeletedBy_ReturnsTrue_ForWriter()
    {
        var board = ValidBoard();

        Assert.True(board.CanBeDeletedBy("Alice", isAdmin: false));
    }

    /// <summary>Can be deleted by returns true for non writer admin.</summary>
    [Fact]
    public void CanBeDeletedBy_ReturnsTrue_ForNonWriterAdmin()
    {
        var board = ValidBoard();

        Assert.True(board.CanBeDeletedBy("Bob", isAdmin: true));
    }

    /// <summary>Can be deleted by returns false for non writer non admin.</summary>
    [Fact]
    public void CanBeDeletedBy_ReturnsFalse_ForNonWriterNonAdmin()
    {
        var board = ValidBoard();

        Assert.False(board.CanBeDeletedBy("Bob", isAdmin: false));
    }

    // -- CanLockBeClearedBy -------------------------------------------------------

    /// <summary>An admin who is not the writer may clear the lock on a locked free-forum post.</summary>
    [Fact]
    public void CanLockBeClearedBy_ReturnsTrue_ForNonWriterAdmin_OnLockedFreeForumPost()
    {
        var board = ViewBoard(BoardTypes.FreeForum, writer: "Alice", locked: true);

        Assert.True(board.CanLockBeClearedBy("Bob", isAdmin: true));
    }

    /// <summary>A private note is single-owner: even an admin who is not the writer may not clear its lock.</summary>
    [Fact]
    public void CanLockBeClearedBy_ReturnsFalse_ForNonWriterAdmin_OnLockedPrivateNote()
    {
        var board = ViewBoard(BoardTypes.PrivateNote, writer: "Alice", locked: true);

        Assert.False(board.CanLockBeClearedBy("Bob", isAdmin: true));
    }

    /// <summary>A non-admin never clears someone else's lock.</summary>
    [Fact]
    public void CanLockBeClearedBy_ReturnsFalse_ForNonAdmin()
    {
        var board = ViewBoard(BoardTypes.FreeForum, writer: "Alice", locked: true);

        Assert.False(board.CanLockBeClearedBy("Bob", isAdmin: false));
    }

    /// <summary>The writer is not "someone else" — clearing their own lock goes through the normal edit path.</summary>
    [Fact]
    public void CanLockBeClearedBy_ReturnsFalse_ForWriter_EvenIfAdmin()
    {
        var board = ViewBoard(BoardTypes.FreeForum, writer: "Alice", locked: true);

        Assert.False(board.CanLockBeClearedBy("Alice", isAdmin: true));
    }

    /// <summary>There is nothing to clear on an unlocked post.</summary>
    [Fact]
    public void CanLockBeClearedBy_ReturnsFalse_WhenPostIsNotLocked()
    {
        var board = ViewBoard(BoardTypes.FreeForum, writer: "Alice", locked: false);

        Assert.False(board.CanLockBeClearedBy("Bob", isAdmin: true));
    }

    // -- CanBeViewedBy ------------------------------------------------------------

    private static Domain.Board.Board ViewBoard(string type, string writer = "Alice", bool locked = false, bool deleted = false) =>
        Domain.Board.Board.Reconstitute(1, type, "Title", "body", writer, DateTime.UtcNow, DateTime.UtcNow, 0, deleted, locked, false);

    /// <summary>An unlocked free-forum post is viewable by anyone, including anonymous visitors.</summary>
    [Fact]
    public void CanBeViewedBy_UnlockedFreeForum_ViewableByAnonymous()
    {
        var board = ViewBoard(BoardTypes.FreeForum);

        Assert.True(board.CanBeViewedBy(viewerNickname: null, viewerIsAdmin: false));
    }

    /// <summary>A locked free-forum post is hidden from an unrelated viewer.</summary>
    [Fact]
    public void CanBeViewedBy_LockedFreeForum_HiddenFromUnrelatedViewer()
    {
        var board = ViewBoard(BoardTypes.FreeForum, locked: true);

        Assert.False(board.CanBeViewedBy("Bob", viewerIsAdmin: false));
        Assert.False(board.CanBeViewedBy(viewerNickname: null, viewerIsAdmin: false));
    }

    /// <summary>A locked free-forum post stays visible to its author and to any admin.</summary>
    [Fact]
    public void CanBeViewedBy_LockedFreeForum_VisibleToOwnerOrAdmin()
    {
        var board = ViewBoard(BoardTypes.FreeForum, locked: true);

        Assert.True(board.CanBeViewedBy("Alice", viewerIsAdmin: false));
        Assert.True(board.CanBeViewedBy("Bob", viewerIsAdmin: true));
    }

    /// <summary>A private note is visible only to its author — no admin bypass.</summary>
    [Fact]
    public void CanBeViewedBy_PrivateNote_AuthorOnly_NoAdminBypass()
    {
        var board = ViewBoard(BoardTypes.PrivateNote);

        Assert.True(board.CanBeViewedBy("Alice", viewerIsAdmin: false));
        Assert.False(board.CanBeViewedBy("Bob", viewerIsAdmin: true));
        Assert.False(board.CanBeViewedBy(viewerNickname: null, viewerIsAdmin: false));
    }

    /// <summary>A soft-deleted post is never viewable, not even by its author.</summary>
    [Fact]
    public void CanBeViewedBy_DeletedPost_NeverViewable()
    {
        var board = ViewBoard(BoardTypes.FreeForum, deleted: true);

        Assert.False(board.CanBeViewedBy("Alice", viewerIsAdmin: true));
    }

    /// <summary>The instance rule and the field-level overload agree.</summary>
    [Fact]
    public void CanBeViewedBy_StaticOverload_MatchesInstanceRule()
    {
        var board = ViewBoard(BoardTypes.FreeForum, locked: true);

        Assert.Equal(
            board.CanBeViewedBy("Alice", viewerIsAdmin: false),
            Domain.Board.Board.CanBeViewedBy(board.Type, board.Deleted, board.Locked, board.Writer, "Alice", viewerIsAdmin: false));
    }

    // -- Update: content length ----------------------------------------------------

    /// <summary>Update refuses over-long content.</summary>
    [Fact]
    public void Update_ReturnsError_WhenContentTooLong()
    {
        var board = Domain.Board.Board.Create("FreeForum", "Title", "body", "Alice").Value;

        var result = board.Update("Title", new string('x', 16385));

        Assert.True(result.IsError);
        Assert.Equal("Board.ContentTooLong", result.FirstError.Code);
    }

    // -- IsOwnedBy (static): a missing name never owns anything ----------------------------

    /// <summary>Two missing names are not "the same owner": ownership needs an actual nickname on both sides.</summary>
    [Theory]
    [InlineData(null, null)]
    [InlineData("Alice", null)]
    [InlineData(null, "Alice")]
    [InlineData("", "")]
    public void IsOwnedBy_Static_IsFalse_WhenEitherNameIsMissing(string? writer, string? viewer)
    {
        Assert.False(Domain.Board.Board.IsOwnedBy(writer, viewer));
    }

    /// <summary>The static ownership check is case-sensitive and exact.</summary>
    [Theory]
    [InlineData("Alice", "Alice", true)]
    [InlineData("Alice", "alice", false)]
    public void IsOwnedBy_Static_ComparesNicknamesExactly(string writer, string viewer, bool expected)
    {
        Assert.Equal(expected, Domain.Board.Board.IsOwnedBy(writer, viewer));
    }
}
