using Maroik.Core.Domain.Boards;
namespace Maroik.Core.Domain.Tests.Boards;

/// <summary>Unit tests for <see cref="Domain.Boards.Board.EditBy"/>: who may change what on a post.</summary>
public class BoardEditTests
{
    /// <summary>The fixed "current time" every domain call in this class receives.</summary>
    private static readonly DateTime Now = new(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>The instant the stored post was last updated.</summary>
    private static readonly DateTime Before = Now.AddDays(-1);

    /// <summary>A stored post by "Alice" of the given type, locked or not.</summary>
    private static Domain.Boards.Board PostOf(bool locked = false, string type = BoardTypes.FreeForum) =>
        Domain.Boards.Board.Reconstitute(1, type, "Title", "Body", "Alice", Before, Before, 0, false, locked, false);

    /// <summary>The author edits the title and content and sets the lock as asked.</summary>
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void EditBy_TheAuthor_EditsTheContent_AndSetsTheLock(bool wasLocked, bool locked)
    {
        var post = PostOf(wasLocked);

        var result = post.EditBy("Alice", isAdmin: false, "New title", "New body", locked, Now);

        Assert.Equal(BoardEdit.ContentEdited, result.Value);
        Assert.Equal("New title", post.Title);
        Assert.Equal("New body", post.Content);
        Assert.Equal(locked, post.Locked);
        Assert.Equal(Now, post.Updated);
    }

    /// <summary>An author's edit the post rejects is returned as that error, leaving the lock as it was.</summary>
    [Fact]
    public void EditBy_TheAuthor_ReturnsTheUpdateError_AndKeepsTheLock()
    {
        var post = PostOf(locked: true);

        var result = post.EditBy("Alice", isAdmin: false, " ", "New body", locked: false, Now);

        Assert.Equal("Board.TitleEmpty", result.FirstError.Code);
        Assert.True(post.Locked);
    }

    /// <summary>An administrator who is not the author clears a lock and leaves title and content alone.</summary>
    [Fact]
    public void EditBy_AnAdminWhoIsNotTheAuthor_OnlyClearsTheLock()
    {
        var post = PostOf(locked: true);

        var result = post.EditBy("Admin", isAdmin: true, "New title", "New body", locked: false, Now);

        Assert.Equal(BoardEdit.LockCleared, result.Value);
        Assert.False(post.Locked);
        Assert.Equal("Title", post.Title);
        Assert.Equal("Body", post.Content);
        Assert.Equal(Now, post.Updated);
    }

    /// <summary>
    /// Everyone else is refused without a change: a non-author non-admin, an admin asking to keep or raise a lock,
    /// an admin on an unlocked post, and an admin on someone's private note.
    /// </summary>
    [Theory]
    [InlineData("Bob", false, true, false, BoardTypes.FreeForum)]
    [InlineData("Admin", true, true, true, BoardTypes.FreeForum)]
    [InlineData("Admin", true, false, false, BoardTypes.FreeForum)]
    [InlineData("Admin", true, true, false, BoardTypes.PrivateNote)]
    public void EditBy_RefusesAnyoneElse_WithoutAChange(string nickname, bool isAdmin, bool wasLocked, bool locked, string type)
    {
        var post = PostOf(wasLocked, type);

        var result = post.EditBy(nickname, isAdmin, "New title", "New body", locked, Now);

        Assert.Equal(BoardEdit.Refused, result.Value);
        Assert.Equal("Title", post.Title);
        Assert.Equal(wasLocked, post.Locked);
        Assert.Equal(Before, post.Updated);
    }
}
