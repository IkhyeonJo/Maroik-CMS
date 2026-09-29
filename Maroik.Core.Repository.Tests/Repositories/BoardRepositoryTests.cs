using Maroik.Core.Contract.Dtos;
using Maroik.Core.Domain.Account;
using Maroik.Core.Domain.Board;
using Maroik.Core.PostgreSQL.Data;
using Maroik.Core.Repository.Repositories;
using Microsoft.EntityFrameworkCore;
using OrmBoard = Maroik.Core.PostgreSQL.Models.Board;

namespace Maroik.Core.Repository.Tests.Repositories;

/// <summary>
/// Tests for <see cref="BoardRepository"/> against a real PostgreSQL database (Testcontainers)
/// preloaded with the <c>Init.sql</c> schema and seed data. <c>Board.Type</c>
/// is constrained by <c>Board_Type_check</c> to <c>FreeForum</c> / <c>PrivateNote</c>, so tests
/// cannot isolate by a private type — instead each test tags its boards with a unique
/// <see cref="RepositoryTestBase.Unique"/> <c>Writer</c> and scopes list assertions to it. Includes
/// <c>IncrementViewAsync</c> (<c>ExecuteUpdateAsync</c>) and <c>FindActiveByIdForUpdateAsync</c>
/// (<c>SELECT ... FOR UPDATE</c>), neither runnable on the EF Core InMemory provider.
/// </summary>
public sealed class BoardRepositoryTests(DatabaseFixture database) : RepositoryTestBase(database)
{
    private BoardRepository Sut => new(Context);

    /// <summary>An unsaved, unlocked post row of board type <paramref name="type"/>.</summary>
    private OrmBoard MakeBoard(string title, string? writer = null, string type = BoardTypes.FreeForum, bool deleted = false) => new()
    {
        Type = type,
        Title = title,
        Content = "Some content",
        Writer = writer ?? Unique("writer"),
        Created = DateTime.UtcNow,
        Updated = DateTime.UtcNow,
        View = 0,
        Deleted = deleted,
        Locked = false,
        Noticed = false
    };

    /// <summary>Inserts <paramref name="boards"/>, saves, clears the change tracker, and returns the rows with their generated ids.</summary>
    private async Task<OrmBoard[]> SeedAsync(params OrmBoard[] boards)
    {
        await Context.Boards.AddRangeAsync(boards);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();
        return boards;
    }

    // -- GetByTypeAsync -----------------------------------------------------------

    /// <summary>Verifies that <c>GetByTypeAsync</c> returns only non-deleted boards of the given type.</summary>
    [Fact]
    public async Task GetByTypeAsync_ReturnsOnlyBoardsOfGivenType()
    {
        string mine = Unique("writer");
        await SeedAsync(
            MakeBoard("Hello", mine),
            MakeBoard("World", mine),
            MakeBoard("Note1", mine, type: BoardTypes.PrivateNote));

        List<Board> result = await Sut.GetByTypeAsync(BoardTypes.FreeForum, TestContext.Current.CancellationToken);

        Assert.All(result, b => Assert.Equal(BoardTypes.FreeForum, b.Type));
        Assert.Equal(2, result.Count(b => b.Writer == mine));
    }

    /// <summary>Verifies that <c>GetByTypeAsync</c> returns empty for a type that matches no rows.</summary>
    [Fact]
    public async Task GetByTypeAsync_ReturnsEmpty_WhenNoMatchingType()
    {
        List<Board> result = await Sut.GetByTypeAsync("NonExistentType", TestContext.Current.CancellationToken);

        Assert.Empty(result);
    }

    /// <summary>Verifies that <c>GetByTypeAsync</c> excludes soft-deleted boards.</summary>
    [Fact]
    public async Task GetByTypeAsync_ExcludesDeletedBoards()
    {
        string mine = Unique("writer");
        await SeedAsync(
            MakeBoard("Visible", mine),
            MakeBoard("Removed", mine, deleted: true));

        List<Board> result = await Sut.GetByTypeAsync(BoardTypes.FreeForum, TestContext.Current.CancellationToken);

        Board single = Assert.Single(result, b => b.Writer == mine);
        Assert.Equal("Visible", single.Title);
    }

    // -- FindActiveByIdAsync -------------------------------------------------

    /// <summary>Verifies that <c>FindActiveByIdAsync</c> returns the board when it exists and is not deleted.</summary>
    [Fact]
    public async Task FindActiveByIdAsync_ReturnsBoard_WhenFoundAndNotDeleted()
    {
        OrmBoard[] seeded = await SeedAsync(MakeBoard("My Post"));

        Board? result = await Sut.FindActiveByIdAsync(seeded[0].Id, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal("My Post", result.Title);
    }

    /// <summary>Verifies that <c>FindActiveByIdAsync</c> returns null when the board is deleted.</summary>
    [Fact]
    public async Task FindActiveByIdAsync_ReturnsNull_WhenBoardIsDeleted()
    {
        OrmBoard[] seeded = await SeedAsync(MakeBoard("Deleted Post", deleted: true));

        Board? result = await Sut.FindActiveByIdAsync(seeded[0].Id, TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    /// <summary>Verifies that <c>FindActiveByIdAsync</c> returns null when the id does not exist.</summary>
    [Fact]
    public async Task FindActiveByIdAsync_ReturnsNull_WhenIdDoesNotExist()
    {
        Board? result = await Sut.FindActiveByIdAsync(long.MaxValue, TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    // -- WriteBoardAsync ----------------------------------------------------------

    /// <summary>Verifies that <c>WriteBoardAsync</c> inserts a board and returns its DB-generated id.</summary>
    [Fact]
    public async Task WriteBoardAsync_AddsBoardToDatabase()
    {
        string writer = Unique("writer");
        var board = Board.Reconstitute(
            id: 0, type: BoardTypes.FreeForum, title: "New Post", content: "Post content",
            writer: writer, created: DateTime.UtcNow, updated: DateTime.UtcNow,
            view: 0, deleted: false, locked: false, noticed: false);

        long id = await Sut.WriteBoardAsync(board, TestContext.Current.CancellationToken);
        Context.ChangeTracker.Clear();

        Assert.True(id > 0);
        OrmBoard? saved = await Context.Boards.FirstOrDefaultAsync(b => b.Id == id, TestContext.Current.CancellationToken);
        Assert.NotNull(saved);
        Assert.Equal(writer, saved.Writer);
    }

    /// <summary>PostgreSQL enforces <c>Board_Type_check</c> — an unknown board type throws (InMemory did not).</summary>
    [Fact]
    public async Task WriteBoardAsync_Throws_WhenTypeIsNotAllowed()
    {
        var board = Board.Reconstitute(
            id: 0, type: "Announcement", title: "Nope", content: "x",
            writer: Unique("writer"), created: DateTime.UtcNow, updated: DateTime.UtcNow,
            view: 0, deleted: false, locked: false, noticed: false);

        await Assert.ThrowsAsync<DbUpdateException>(
            () => Sut.WriteBoardAsync(board, TestContext.Current.CancellationToken));
    }

    // -- UpdateEntityAsync ------------------------------------------------------

    /// <summary>Verifies that <c>UpdateEntityAsync</c> persists changes to an existing board.</summary>
    [Fact]
    public async Task UpdateEntityAsync_UpdatesExistingBoard()
    {
        OrmBoard[] seeded = await SeedAsync(MakeBoard("Original Title"));

        var board = Board.Reconstitute(
            id: seeded[0].Id, type: BoardTypes.FreeForum, title: "Updated Title", content: "Updated content",
            writer: seeded[0].Writer!, created: seeded[0].Created, updated: DateTime.UtcNow,
            view: seeded[0].View, deleted: false, locked: false, noticed: false);

        await Sut.UpdateEntityAsync(board, TestContext.Current.CancellationToken);
        Context.ChangeTracker.Clear();

        OrmBoard? updated = await Context.Boards.FirstOrDefaultAsync(b => b.Id == seeded[0].Id, TestContext.Current.CancellationToken);
        Assert.NotNull(updated);
        Assert.Equal("Updated Title", updated.Title);
    }

    /// <summary>Verifies that <c>UpdateEntityAsync</c> is how a board is soft-deleted (flag, not row removal).</summary>
    [Fact]
    public async Task UpdateEntityAsync_SetsDeletedFlag_OnBoard()
    {
        OrmBoard[] seeded = await SeedAsync(MakeBoard("Post to Delete"));

        var board = Board.Reconstitute(
            id: seeded[0].Id, type: seeded[0].Type!, title: seeded[0].Title!, content: seeded[0].Content,
            writer: seeded[0].Writer!, created: seeded[0].Created, updated: DateTime.UtcNow,
            view: seeded[0].View, deleted: true, locked: seeded[0].Locked, noticed: seeded[0].Noticed);

        await Sut.UpdateEntityAsync(board, TestContext.Current.CancellationToken);
        Context.ChangeTracker.Clear();

        OrmBoard? result = await Context.Boards.FirstOrDefaultAsync(b => b.Id == seeded[0].Id, TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.True(result.Deleted);
    }

    // -- IncrementViewAsync (ExecuteUpdateAsync) -----------------------------------

    /// <summary>Verifies that <c>IncrementViewAsync</c> bumps the view counter by one via <c>ExecuteUpdateAsync</c>.</summary>
    [Fact]
    public async Task IncrementViewAsync_IncrementsViewByOne()
    {
        var seed = MakeBoard("Viewed");
        seed.View = 5;
        OrmBoard[] seeded = await SeedAsync(seed);

        await Sut.IncrementViewAsync(seeded[0].Id, TestContext.Current.CancellationToken);
        Context.ChangeTracker.Clear();

        OrmBoard? result = await Context.Boards.FirstOrDefaultAsync(b => b.Id == seeded[0].Id, TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Equal(6, result.View);
    }

    // -- FindActiveByIdForUpdateAsync (SELECT ... FOR UPDATE) --------------------

    /// <summary>Verifies that the row-locking lookup returns the board when it exists.</summary>
    [Fact]
    public async Task FindActiveByIdForUpdateAsync_ReturnsBoard_WhenExists()
    {
        OrmBoard[] seeded = await SeedAsync(MakeBoard("Locked"));

        await using var unitOfWork = new UnitOfWork(Context);
        await unitOfWork.BeginAsync(TestContext.Current.CancellationToken);
        Board? result = await Sut.FindActiveByIdForUpdateAsync(seeded[0].Id, TestContext.Current.CancellationToken);
        await unitOfWork.CommitAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(seeded[0].Writer, result.Writer);
    }

    /// <summary>Verifies that the row-locking lookup returns null when the board does not exist.</summary>
    [Fact]
    public async Task FindActiveByIdForUpdateAsync_ReturnsNull_WhenNotFound()
    {
        await using var unitOfWork = new UnitOfWork(Context);
        await unitOfWork.BeginAsync(TestContext.Current.CancellationToken);
        Board? result = await Sut.FindActiveByIdForUpdateAsync(long.MaxValue, TestContext.Current.CancellationToken);
        await unitOfWork.CommitAsync(TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    /// <summary>Verifies that the row-locking lookup returns null when the board is soft-deleted.</summary>
    [Fact]
    public async Task FindActiveByIdForUpdateAsync_ReturnsNull_WhenDeleted()
    {
        OrmBoard[] seeded = await SeedAsync(MakeBoard("Gone", deleted: true));

        await using var unitOfWork = new UnitOfWork(Context);
        await unitOfWork.BeginAsync(TestContext.Current.CancellationToken);
        Board? result = await Sut.FindActiveByIdForUpdateAsync(seeded[0].Id, TestContext.Current.CancellationToken);
        await unitOfWork.CommitAsync(TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    /// <summary>
    /// Reproduces the race <c>BoardService.WriteCommentAsync</c> must avoid: several concurrent
    /// "read comment count, insert comment with Order = count" sequences against one post. Each
    /// sequence locks the post row via <c>FindActiveByIdForUpdateAsync</c> in its own transaction,
    /// so PostgreSQL serializes them and every comment gets a distinct <c>Order</c> 0..n-1 instead
    /// of colliding on a stale count.
    /// </summary>
    [Fact]
    public async Task FindActiveByIdForUpdateAsync_SerializesConcurrentCommentOrderAssignment()
    {
        const int writerCount = 5;
        OrmBoard[] seeded = await SeedAsync(MakeBoard("Hot post"));
        long boardId = seeded[0].Id;

        ApplicationDbContext[] contexts = [.. Enumerable.Range(0, writerCount).Select(_ => NewDbContext())];

        await Task.WhenAll(contexts.Select(async (context, i) =>
        {
            var boardRepo = new BoardRepository(context);
            var commentRepo = new BoardCommentRepository(context);
            await using var unitOfWork = new UnitOfWork(context);

            await unitOfWork.BeginAsync(TestContext.Current.CancellationToken);
            _ = (await boardRepo.FindActiveByIdForUpdateAsync(boardId, TestContext.Current.CancellationToken))!;
            List<BoardComment> existing = await commentRepo.GetByBoardIdOrderedAsync(boardId, TestContext.Current.CancellationToken);
            BoardComment comment = BoardComment.Create(boardId, existing.Count, "/upload/avatar.jpg", $"Writer{i}", "Hi").Value;
            await commentRepo.CreateAsync(comment, TestContext.Current.CancellationToken);
            await unitOfWork.CommitAsync(TestContext.Current.CancellationToken);
        }));

        Context.ChangeTracker.Clear();
        List<BoardComment> comments = await new BoardCommentRepository(Context)
            .GetByBoardIdOrderedAsync(boardId, TestContext.Current.CancellationToken);

        Assert.Equal(writerCount, comments.Count);
        Assert.Equal(Enumerable.Range(0, writerCount).Select(n => (long)n), comments.Select(c => c.Order).OrderBy(o => o));
    }

    // -- QueryPageAsync (page bounds) -------------------------------------------

    /// <summary>Anonymous free-forum "Writer" search for <paramref name="writer"/> at the given page and page size.</summary>
    private Task<(List<Board> Items, int TotalCount)> PageOf(string writer, int page, int pageSize) =>
        Sut.QueryPageAsync(
            new BoardPageQuery(
                Type: BoardTypes.FreeForum, OwnerNickname: null,
                SearchType: "Writer", SearchText: writer,
                IsLoggedIn: false, ViewerRole: null, ViewerNickname: null,
                Page: page, PageSize: pageSize),
            TestContext.Current.CancellationToken);

    /// <summary>
    /// Regression: <c>(Page - 1) * PageSize</c> used to be int arithmetic, so a large <c>?page=</c>
    /// (600000000 * 5) wrapped to a negative OFFSET and Postgres threw "OFFSET must not be negative".
    /// Any page past the last row must return an empty page instead, with the real total intact.
    /// </summary>
    [Theory]
    [InlineData(600_000_000)]
    [InlineData(1_000_000_000)]
    [InlineData(int.MaxValue)]
    public async Task QueryPageAsync_HugePageNumber_ReturnsEmptyPage_WithoutThrowing(int page)
    {
        string writer = Unique("hugepage");
        await SeedAsync(MakeBoard("t1", writer), MakeBoard("t2", writer));

        (List<Board> items, int totalCount) = await PageOf(writer, page, pageSize: 5);

        Assert.Empty(items);
        Assert.Equal(2, totalCount);
    }

    /// <summary>A page number below 1 is floored to the first page rather than producing a negative OFFSET.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task QueryPageAsync_PageBelowOne_ReturnsFirstPage(int page)
    {
        string writer = Unique("lowpage");
        await SeedAsync(MakeBoard("only", writer));

        (List<Board> items, int totalCount) = await PageOf(writer, page, pageSize: 5);

        Assert.Single(items);
        Assert.Equal(1, totalCount);
    }

    /// <summary>Ordinary paging is unchanged: page 2 of size 1 returns the second-newest post.</summary>
    [Fact]
    public async Task QueryPageAsync_OrdinaryPaging_IsUnchanged()
    {
        string writer = Unique("normalpage");
        await SeedAsync(MakeBoard("first", writer), MakeBoard("second", writer));

        (List<Board> items, int totalCount) = await PageOf(writer, page: 2, pageSize: 1);

        var item = Assert.Single(items);
        Assert.Equal("first", item.Title);
        Assert.Equal(2, totalCount);
    }

    // -- QueryPageAsync (locked-post visibility) --------------------------------

    /// <summary>Sets the locked flag of <paramref name="board"/> and returns it, for inline use in seeding.</summary>
    private static OrmBoard Locked(OrmBoard board, bool locked)
    {
        board.Locked = locked;
        return board;
    }

    /// <summary>First page (50 rows) of a free-forum "Writer" search for <paramref name="writer"/>, as seen by the given viewer.</summary>
    private Task<(List<Board> Items, int TotalCount)> WriterSearch(
        string writer, bool isLoggedIn, string? viewerRole, string? viewerNickname) =>
        Sut.QueryPageAsync(
            new BoardPageQuery(
                Type: BoardTypes.FreeForum, OwnerNickname: null,
                SearchType: "Writer", SearchText: writer,
                IsLoggedIn: isLoggedIn, ViewerRole: viewerRole, ViewerNickname: viewerNickname,
                Page: 1, PageSize: 50),
            TestContext.Current.CancellationToken);

    /// <summary>
    /// Regression: a "Writer" search must apply the same locked-post visibility split the "Title"
    /// search does — an anonymous or cross-account viewer must never get another account's locked
    /// (private) posts back, while the owner still sees their own locked posts and an admin sees all.
    /// </summary>
    [Fact]
    public async Task QueryPageAsync_WriterSearch_AppliesLockedPostVisibilitySplit()
    {
        string owner = Unique("owner");
        string other = Unique("other");

        await SeedAsync(
            Locked(MakeBoard("owner-open", owner), locked: false),
            Locked(MakeBoard("owner-locked", owner), locked: true),
            Locked(MakeBoard("other-open", other), locked: false),
            Locked(MakeBoard("other-locked", other), locked: true));

        // Anonymous searching "other": only their non-locked post.
        var (items, _) = await WriterSearch(other, isLoggedIn: false, viewerRole: null, viewerNickname: null);
        Assert.Equal(["other-open"], items.Select(b => b.Title));

        // User "owner" searching their own writer tag: their own locked post is included.
        var (boards, totalCount) = await WriterSearch(owner, isLoggedIn: true, viewerRole: Role.User, viewerNickname: owner);
        Assert.Equal(2, totalCount);
        Assert.Contains("owner-locked", boards.Select(b => b.Title));

        // User "owner" searching a different writer: still no cross-account locked post.
        var (list, _) = await WriterSearch(other, isLoggedIn: true, viewerRole: Role.User, viewerNickname: owner);
        Assert.Equal(["other-open"], list.Select(b => b.Title));

        // Admin searching "other": the locked post is visible.
        var (boards1, i) = await WriterSearch(other, isLoggedIn: true, viewerRole: Role.Admin, viewerNickname: Unique("admin"));
        Assert.Equal(2, i);
        Assert.Contains("other-locked", boards1.Select(b => b.Title));
    }

    // -- QueryPageAsync (search text is literal) ------------------------------------

    /// <summary>
    /// <c>%</c> and <c>_</c> in a search term are literals, not LIKE wildcards — for the Title search and
    /// for the Writer search (nicknames legitimately contain underscores, e.g. <c>john_doe</c>).
    /// </summary>
    [Fact]
    public async Task QueryPageAsync_TreatsLikeWildcardsInTheSearchTextLiterally()
    {
        string writerWithUnderscore = $"a_b-{Token}";
        string writerLookalike = $"axb-{Token}";
        await SeedAsync(
            MakeBoard($"100%-{Token}", writerWithUnderscore),
            MakeBoard($"1000-{Token}", writerLookalike));

        var (items, _) = await WriterSearch(writerWithUnderscore, isLoggedIn: false, viewerRole: null, viewerNickname: null);
        var (boards, _) = await Sut.QueryPageAsync(
            new BoardPageQuery(BoardTypes.FreeForum, OwnerNickname: null, SearchType: "Title", SearchText: $"0%-{Token}",
                IsLoggedIn: false, ViewerRole: null, ViewerNickname: null, Page: 1, PageSize: 50),
            TestContext.Current.CancellationToken);

        Assert.Equal([writerWithUnderscore], items.Select(b => b.Writer));
        Assert.Equal([$"100%-{Token}"], boards.Select(b => b.Title));
    }

    /// <summary>The owner-scoped list (private notes) matches literally as well.</summary>
    [Fact]
    public async Task QueryPageAsync_OwnerScopedSearch_TreatsLikeWildcardsInTheSearchTextLiterally()
    {
        string owner = Unique("note_owner");
        await SeedAsync(MakeBoard($"50%_off-{Token}", owner), MakeBoard($"50xxoff-{Token}", owner));

        var (items, _) = await Sut.QueryPageAsync(
            new BoardPageQuery(BoardTypes.FreeForum, OwnerNickname: owner, SearchType: "Title", SearchText: $"50%_off-{Token}",
                IsLoggedIn: true, ViewerRole: Role.User, ViewerNickname: owner, Page: 1, PageSize: 50),
            TestContext.Current.CancellationToken);

        Assert.Equal([$"50%_off-{Token}"], items.Select(b => b.Title));
    }

    // -- QueryPageAsync (owner-scoped Writer search; unknown viewer role) ------------------

    /// <summary>The owner-scoped list (private notes) can be searched by writer as well as by title.</summary>
    [Fact]
    public async Task QueryPageAsync_OwnerScopedWriterSearch_FindsOnlyThatWriter()
    {
        string owner = Unique("note_owner");
        await SeedAsync(MakeBoard($"Mine-{Token}", owner), MakeBoard($"Theirs-{Token}", Unique("someone_else")));

        var (items, _) = await Sut.QueryPageAsync(
            new BoardPageQuery(BoardTypes.FreeForum, OwnerNickname: owner, SearchType: "Writer", SearchText: owner,
                IsLoggedIn: true, ViewerRole: Role.User, ViewerNickname: owner, Page: 1, PageSize: 50),
            TestContext.Current.CancellationToken);

        Assert.Equal([$"Mine-{Token}"], items.Select(b => b.Title));
    }

    /// <summary>
    /// A logged-in viewer whose role is neither Admin nor User (impossible for a real session) is not restricted at all: the search
    /// is left as the plain, unfiltered, unsearched list — the behavior the original code had — rather than an error.
    /// </summary>
    [Fact]
    public async Task QueryPageAsync_ASearchByALoggedInViewerWithAnUnknownRole_GetsTheUnfilteredList()
    {
        OrmBoard open = MakeBoard($"Open-{Token}");
        OrmBoard locked = MakeBoard($"Locked-{Token}");
        locked.Locked = true;
        await SeedAsync(open, locked);

        var (items, _) = await Sut.QueryPageAsync(
            new BoardPageQuery(BoardTypes.FreeForum, OwnerNickname: null, SearchType: "Title", SearchText: $"Open-{Token}",
                IsLoggedIn: true, ViewerRole: "Ghost", ViewerNickname: Unique("ghost"), Page: 1, PageSize: 500),
            TestContext.Current.CancellationToken);

        Assert.Contains(items, b => b.Title == $"Open-{Token}");
        Assert.Contains(items, b => b.Title == $"Locked-{Token}"); // neither searched nor lock-filtered
    }
}
