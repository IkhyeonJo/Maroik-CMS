using Maroik.Core.Domain.Board;
using Maroik.Core.PostgreSQL.Data;
using Maroik.Core.Repository.Repositories;
using Microsoft.EntityFrameworkCore;
using OrmBoard = Maroik.Core.PostgreSQL.Models.Board;
using OrmBoardComment = Maroik.Core.PostgreSQL.Models.BoardComment;

namespace Maroik.Core.Repository.Tests.Repositories;

/// <summary>
/// Tests for <see cref="BoardCommentRepository"/> against a real PostgreSQL database (Testcontainers)
/// preloaded with the <c>Init.sql</c> schema and seed data. <c>BoardComment</c> carries
/// the FK <c>BoardComment_fk_0</c> to <c>Board.ID</c> and the check <c>BoardComment_Order_check</c>
/// (<c>Order &gt;= 0</c>), so every test creates a real parent board and scopes to its id.
/// </summary>
public sealed class BoardCommentRepositoryTests(DatabaseFixture database) : RepositoryTestBase(database)
{
    private BoardCommentRepository Sut => new(Context);

    private OrmBoardComment MakeComment(long boardId, long order, string? writer = null, bool deleted = false) => new()
    {
        BoardId = boardId,
        Order = order,
        AvatarImagePath = "/upload/default.jpg",
        Writer = writer ?? Unique("writer"),
        Content = "Comment content",
        Created = DateTime.UtcNow,
        Deleted = deleted
    };

    private async Task<long> SeedBoardAsync()
    {
        var board = new OrmBoard
        {
            Type = BoardTypes.FreeForum,
            Title = Unique("Parent Board"),
            Content = "Content",
            Writer = Unique("writer"),
            Created = DateTime.UtcNow,
            Updated = DateTime.UtcNow,
            View = 0,
            Deleted = false,
            Locked = false,
            Noticed = false
        };
        await Context.Boards.AddAsync(board);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();
        return board.Id;
    }

    private async Task SeedAsync(params OrmBoardComment[] comments)
    {
        await Context.BoardComments.AddRangeAsync(comments);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();
    }

    // -- GetByBoardIdOrderedAsync -------------------------------------------------

    /// <summary>Verifies that <c>GetByBoardIdOrderedAsync</c> returns only comments for the given board.</summary>
    [Fact]
    public async Task GetByBoardIdOrderedAsync_ReturnsOnlyCommentsForGivenBoard()
    {
        long board1 = await SeedBoardAsync();
        long board2 = await SeedBoardAsync();
        await SeedAsync(
            MakeComment(board1, 1),
            MakeComment(board1, 2),
            MakeComment(board2, 1));

        List<BoardComment> result = await Sut.GetByBoardIdOrderedAsync(board1, TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Count);
        Assert.All(result, c => Assert.Equal(board1, c.BoardId));
    }

    /// <summary>Verifies that <c>GetByBoardIdOrderedAsync</c> returns comments ordered by <c>Order</c>.</summary>
    [Fact]
    public async Task GetByBoardIdOrderedAsync_ReturnsOrderedByOrder()
    {
        long boardId = await SeedBoardAsync();
        await SeedAsync(
            MakeComment(boardId, 3),
            MakeComment(boardId, 1),
            MakeComment(boardId, 2));

        List<BoardComment> result = await Sut.GetByBoardIdOrderedAsync(boardId, TestContext.Current.CancellationToken);

        Assert.Equal([1L, 2L, 3L], result.Select(c => c.Order));
    }

    /// <summary>Verifies that <c>GetByBoardIdOrderedAsync</c> returns empty when the board has no comments.</summary>
    [Fact]
    public async Task GetByBoardIdOrderedAsync_ReturnsEmpty_WhenNoBoardComments()
    {
        long boardId = await SeedBoardAsync();

        List<BoardComment> result = await Sut.GetByBoardIdOrderedAsync(boardId, TestContext.Current.CancellationToken);

        Assert.Empty(result);
    }

    /// <summary>Verifies that <c>GetByBoardIdOrderedAsync</c> excludes soft-deleted comments.</summary>
    [Fact]
    public async Task GetByBoardIdOrderedAsync_ExcludesDeletedComments()
    {
        long boardId = await SeedBoardAsync();
        await SeedAsync(
            MakeComment(boardId, 1, writer: "Visible" + Token),
            MakeComment(boardId, 2, writer: "Removed" + Token, deleted: true));

        List<BoardComment> result = await Sut.GetByBoardIdOrderedAsync(boardId, TestContext.Current.CancellationToken);

        Assert.Equal("Visible" + Token, Assert.Single(result).Writer);
    }

    // -- FindByIdAsync -------------------------------------------------------

    /// <summary>Verifies that <c>FindByIdAsync</c> returns the comment when found.</summary>
    [Fact]
    public async Task FindByIdAsync_ReturnsComment_WhenFound()
    {
        long boardId = await SeedBoardAsync();
        var orm = MakeComment(boardId, 1, writer: "Bob" + Token);
        await SeedAsync(orm);

        BoardComment? result = await Sut.FindByIdAsync(orm.Id, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal("Bob" + Token, result.Writer);
    }

    /// <summary>Verifies that <c>FindByIdAsync</c> returns null when not found.</summary>
    [Fact]
    public async Task FindByIdAsync_ReturnsNull_WhenNotFound()
    {
        BoardComment? result = await Sut.FindByIdAsync(long.MaxValue, TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    /// <summary>Verifies that <c>FindByIdAsync</c> returns null for a soft-deleted comment.</summary>
    [Fact]
    public async Task FindByIdAsync_ReturnsNull_WhenCommentIsDeleted()
    {
        long boardId = await SeedBoardAsync();
        var orm = MakeComment(boardId, 1, deleted: true);
        await SeedAsync(orm);

        BoardComment? result = await Sut.FindByIdAsync(orm.Id, TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    // -- FindByIdForUpdateAsync (SELECT ... FOR UPDATE) ---------------------------

    /// <summary>Verifies that <c>FindByIdForUpdateAsync</c> returns the comment when found.</summary>
    [Fact]
    public async Task FindByIdForUpdateAsync_ReturnsComment_WhenFound()
    {
        long boardId = await SeedBoardAsync();
        var orm = MakeComment(boardId, 1, writer: "Bob" + Token);
        await SeedAsync(orm);

        BoardComment? result = await Sut.FindByIdForUpdateAsync(orm.Id, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal("Bob" + Token, result.Writer);
    }

    /// <summary>Verifies that <c>FindByIdForUpdateAsync</c> returns null when not found.</summary>
    [Fact]
    public async Task FindByIdForUpdateAsync_ReturnsNull_WhenNotFound()
    {
        BoardComment? result = await Sut.FindByIdForUpdateAsync(long.MaxValue, TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    /// <summary>Verifies that <c>FindByIdForUpdateAsync</c> returns null for a soft-deleted comment.</summary>
    [Fact]
    public async Task FindByIdForUpdateAsync_ReturnsNull_WhenCommentIsDeleted()
    {
        long boardId = await SeedBoardAsync();
        var orm = MakeComment(boardId, 1, deleted: true);
        await SeedAsync(orm);

        BoardComment? result = await Sut.FindByIdForUpdateAsync(orm.Id, TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    /// <summary>
    /// Reproduces the race <c>BoardService.DeleteCommentAsync</c> must avoid: several concurrent
    /// "lock comment, soft-delete" sequences against the same comment. PostgreSQL serializes them
    /// via <c>FindByIdForUpdateAsync</c>'s row lock, so exactly one sequence observes the
    /// not-yet-deleted row and succeeds; every other sequence's own locked re-read (after the
    /// winner commits) sees it already gone, matching this repository's <c>!Deleted</c> filter.
    /// </summary>
    [Fact]
    public async Task FindByIdForUpdateAsync_SerializesConcurrentSoftDelete()
    {
        const int attempterCount = 5;
        long boardId = await SeedBoardAsync();
        var orm = MakeComment(boardId, 1);
        await SeedAsync(orm);

        ApplicationDbContext[] contexts = [.. Enumerable.Range(0, attempterCount).Select(_ => NewDbContext())];

        bool[] succeeded = await Task.WhenAll(contexts.Select(async context =>
        {
            var commentRepo = new BoardCommentRepository(context);
            await using var unitOfWork = new UnitOfWork(context);

            await unitOfWork.BeginAsync(TestContext.Current.CancellationToken);
            BoardComment? locked = await commentRepo.FindByIdForUpdateAsync(orm.Id, TestContext.Current.CancellationToken);
            if (locked == null)
            {
                await unitOfWork.RollbackAsync(TestContext.Current.CancellationToken);
                return false;
            }

            var deleteResult = locked.SoftDelete();
            if (deleteResult.IsError)
            {
                await unitOfWork.RollbackAsync(TestContext.Current.CancellationToken);
                return false;
            }

            await commentRepo.UpdateEntityAsync(locked, TestContext.Current.CancellationToken);
            await unitOfWork.CommitAsync(TestContext.Current.CancellationToken);
            return true;
        }));

        Assert.Equal(1, succeeded.Count(s => s));

        Context.ChangeTracker.Clear();
        BoardComment? afterAll = await new BoardCommentRepository(Context)
            .FindByIdAsync(orm.Id, TestContext.Current.CancellationToken);
        Assert.Null(afterAll); // soft-deleted
    }

    // -- GetCommentCountsByBoardIdsAsync ------------------------------------------

    /// <summary>Verifies that <c>GetCommentCountsByBoardIdsAsync</c> counts only non-deleted comments.</summary>
    [Fact]
    public async Task GetCommentCountsByBoardIdsAsync_ReturnsCorrectCounts()
    {
        long board1 = await SeedBoardAsync();
        long board2 = await SeedBoardAsync();
        await SeedAsync(
            MakeComment(board1, 1),
            MakeComment(board1, 2),
            MakeComment(board1, 3, deleted: true),
            MakeComment(board2, 1));

        Dictionary<long, int> counts = await Sut.GetCommentCountsByBoardIdsAsync([board1, board2], TestContext.Current.CancellationToken);

        Assert.Equal(2, counts[board1]);
        Assert.Equal(1, counts[board2]);
    }

    /// <summary>Verifies that <c>GetCommentCountsByBoardIdsAsync</c> returns zero for boards with no comments.</summary>
    [Fact]
    public async Task GetCommentCountsByBoardIdsAsync_ReturnsZero_ForBoardsWithNoComments()
    {
        long boardId = await SeedBoardAsync();

        Dictionary<long, int> counts = await Sut.GetCommentCountsByBoardIdsAsync([boardId], TestContext.Current.CancellationToken);

        Assert.Equal(0, counts[boardId]);
    }

    // -- CreateAsync -----------------------------------------------

    /// <summary>Verifies that <c>CreateAsync</c> inserts a comment row.</summary>
    [Fact]
    public async Task CreateAsync_AddsCommentToDatabase()
    {
        long boardId = await SeedBoardAsync();

        var comment = BoardComment.Reconstitute(
            id: 0, boardId: boardId, order: 1, avatarImagePath: "/upload/alice.jpg",
            writer: "Alice" + Token, content: "Great post!", created: DateTime.UtcNow, deleted: false);

        await Sut.CreateAsync(comment, TestContext.Current.CancellationToken);
        Context.ChangeTracker.Clear();

        OrmBoardComment? saved = await Context.BoardComments.FirstOrDefaultAsync(
            c => c.BoardId == boardId && c.Content == "Great post!", TestContext.Current.CancellationToken);
        Assert.NotNull(saved);
        Assert.Equal("Alice" + Token, saved.Writer);
    }

    /// <summary>PostgreSQL enforces <c>BoardComment_fk_0</c> — a comment on a non-existent board throws (InMemory did not).</summary>
    [Fact]
    public async Task CreateAsync_Throws_WhenParentBoardDoesNotExist()
    {
        var comment = BoardComment.Reconstitute(
            id: 0, boardId: long.MaxValue, order: 0, avatarImagePath: "/x",
            writer: "Ghost" + Token, content: "orphan", created: DateTime.UtcNow, deleted: false);

        await Assert.ThrowsAsync<DbUpdateException>(
            () => Sut.CreateAsync(comment, TestContext.Current.CancellationToken));
    }

    // -- UpdateEntityAsync ----------------------------------------------

    /// <summary>Verifies that <c>UpdateEntityAsync</c> is how a comment is soft-deleted.</summary>
    [Fact]
    public async Task UpdateEntityAsync_SoftDeletesComment()
    {
        long boardId = await SeedBoardAsync();
        var orm = MakeComment(boardId, 1);
        await SeedAsync(orm);

        var comment = BoardComment.Reconstitute(
            id: orm.Id, boardId: orm.BoardId, order: orm.Order, avatarImagePath: orm.AvatarImagePath,
            writer: orm.Writer ?? "", content: orm.Content, created: orm.Created, deleted: true);

        await Sut.UpdateEntityAsync(comment, TestContext.Current.CancellationToken);
        Context.ChangeTracker.Clear();

        OrmBoardComment? result = await Context.BoardComments.FirstOrDefaultAsync(c => c.Id == orm.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.True(result.Deleted);
    }
}
