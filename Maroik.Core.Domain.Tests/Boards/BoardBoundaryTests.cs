using Maroik.Core.Domain.Boards;
using Maroik.Core.Domain.Primitives;
using Maroik.Core.Domain.Tests.Fixtures;

namespace Maroik.Core.Domain.Tests.Boards;

/// <summary>
/// Exact limits and ownership branches of the board rules: a title, content or attachment AT its limit is accepted and
/// one more is refused, and who may delete a private note or a forum post.
/// </summary>
public class BoardBoundaryTests
{
    /// <summary>The fixed "current time" every domain call in this class receives.</summary>
    private static readonly DateTime Now = new(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>A string of exactly <paramref name="length"/> characters.</summary>
    private static string Text(int length) => new('x', length);

    /// <summary>Writer nickname used for boards and comments.</summary>
    private const string Writer = "Alice";

    /// <summary>Verifies that a forum post may be deleted by its writer or an admin, but a private note only by its writer.</summary>
    [Fact]
    public void Board_CanBeDeletedBy_FollowsThePrivateNoteAndForumRules()
    {
        Domain.Boards.Board forum = Domain.Boards.Board.Reconstitute(1, BoardTypes.FreeForum, "t", "c", "Alice", DateTime.UtcNow, DateTime.UtcNow, 0L, false, false, false);
        Domain.Boards.Board note = Domain.Boards.Board.Reconstitute(2, BoardTypes.PrivateNote, "t", "c", "Alice", DateTime.UtcNow, DateTime.UtcNow, 0L, false, false, false);

        Assert.True(forum.CanBeDeletedBy("Alice", isAdmin: false));
        Assert.True(forum.CanBeDeletedBy("Admin", isAdmin: true));
        Assert.False(forum.CanBeDeletedBy("Bob", isAdmin: false));

        Assert.True(note.CanBeDeletedBy("Alice", isAdmin: false));
        Assert.False(note.CanBeDeletedBy("Admin", isAdmin: true)); // a private note has no admin bypass
        Assert.False(note.CanBeDeletedBy("Bob", isAdmin: false));
    }

    /// <summary>Verifies that <c>Board.Create</c> accepts a title and content at the maximum length and refuses one character more.</summary>
    [Fact]
    public void Board_Create_AcceptsATitleAndContentAtTheLimit_AndRefusesOneMore()
    {
        Assert.False(Domain.Boards.Board.Create(BoardTypes.FreeForum, Text(TitledContentPolicy.MaxTitleLength), Text(TitledContentPolicy.MaxBodyLength), Writer, Now).IsError);

        ErrorAssert.Validation(Domain.Boards.Board.Create(BoardTypes.FreeForum, Text(TitledContentPolicy.MaxTitleLength + 1), "c", Writer, Now),
            "Board.TitleTooLong", "Post title must be {0} characters or fewer.", TitledContentPolicy.MaxTitleLength);
        ErrorAssert.Validation(Domain.Boards.Board.Create(BoardTypes.FreeForum, "t", Text(TitledContentPolicy.MaxBodyLength + 1), Writer, Now),
            "Board.ContentTooLong", "Post content must be {0} characters or fewer.", TitledContentPolicy.MaxBodyLength);
    }

    /// <summary>Verifies that <c>Board.Update</c> accepts a title and content at the maximum length and refuses one character more.</summary>
    [Fact]
    public void Board_Update_AcceptsATitleAndContentAtTheLimit_AndRefusesOneMore()
    {
        Domain.Boards.Board board = Domain.Boards.Board.Create(BoardTypes.FreeForum, "t", "c", Writer, Now).Value;

        Assert.False(board.Update(Text(TitledContentPolicy.MaxTitleLength), Text(TitledContentPolicy.MaxBodyLength), Now).IsError);

        ErrorAssert.Validation(board.Update(Text(TitledContentPolicy.MaxTitleLength + 1), "c", Now),
            "Board.TitleTooLong", "Post title must be {0} characters or fewer.", TitledContentPolicy.MaxTitleLength);
        ErrorAssert.Validation(board.Update("t", Text(TitledContentPolicy.MaxBodyLength + 1), Now),
            "Board.ContentTooLong", "Post content must be {0} characters or fewer.", TitledContentPolicy.MaxBodyLength);
    }

    /// <summary>Verifies the empty/unknown board type, empty title and empty writer errors.</summary>
    [Fact]
    public void Board_ValidationMessages_ForEmptyAndUnknownInput()
    {
        ErrorAssert.Validation(Domain.Boards.Board.Create("", "t", "c", Writer, Now), "Board.TypeEmpty", "Board type cannot be empty.");
        ErrorAssert.Validation(Domain.Boards.Board.Create("Nonsense", "t", "c", Writer, Now), "Board.TypeInvalid", "Board type is not a recognised value.");
        ErrorAssert.Validation(Domain.Boards.Board.Create(BoardTypes.FreeForum, " ", "c", Writer, Now), "Board.TitleEmpty", "Post title cannot be empty.");
        ErrorAssert.Validation(Domain.Boards.Board.Create(BoardTypes.FreeForum, "t", "c", " ", Now), "Board.WriterEmpty", "Post author (writer) cannot be empty.");
        ErrorAssert.Validation(Domain.Boards.Board.Create(BoardTypes.FreeForum, "t", "c", Writer, Now).Value.Update(" ", "c", Now),
            "Board.TitleEmpty", "Post title cannot be empty.");
    }

    /// <summary>Verifies the size, empty and length limits of a board attachment (a size of 0 is legal).</summary>
    [Fact]
    public void BoardAttachedFile_Create_AcceptsTheLimits_AndRefusesOneMore()
    {
        Assert.False(BoardAttachedFile.Create(1, 0, Text(ShortTextPolicy.MaxLength), Text(ShortTextPolicy.MaxLength), Text(ShortTextPolicy.MaxLength)).IsError); // size 0 is a legal (empty) size

        ErrorAssert.Validation(BoardAttachedFile.Create(1, -1, "n", ".e", "p"), "BoardAttachedFile.InvalidSize", "File size cannot be negative.");
        ErrorAssert.Validation(BoardAttachedFile.Create(1, 1, " ", ".e", "p"), "BoardAttachedFile.NameEmpty", "File name cannot be empty.");
        ErrorAssert.Validation(BoardAttachedFile.Create(1, 1, "n", ".e", " "), "BoardAttachedFile.PathEmpty", "File storage path cannot be empty.");
        ErrorAssert.Validation(BoardAttachedFile.Create(1, 1, Text(ShortTextPolicy.MaxLength + 1), ".e", "p"), "BoardAttachedFile.NameTooLong", "File name must be {0} characters or fewer.", ShortTextPolicy.MaxLength);
        ErrorAssert.Validation(BoardAttachedFile.Create(1, 1, "n", Text(ShortTextPolicy.MaxLength + 1), "p"), "BoardAttachedFile.ExtensionTooLong", "File extension must be {0} characters or fewer.", ShortTextPolicy.MaxLength);
        ErrorAssert.Validation(BoardAttachedFile.Create(1, 1, "n", ".e", Text(ShortTextPolicy.MaxLength + 1)), "BoardAttachedFile.PathTooLong", "File storage path must be {0} characters or fewer.", ShortTextPolicy.MaxLength);
    }
}
