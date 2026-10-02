using Maroik.Core.Domain.Board;
using Maroik.Core.Repository.Repositories;
using Microsoft.EntityFrameworkCore;
using OrmBoard = Maroik.Core.PostgreSQL.Models.Board;
using OrmBoardAttachedFile = Maroik.Core.PostgreSQL.Models.BoardAttachedFile;

namespace Maroik.Core.Repository.Tests.Repositories;

/// <summary>
/// Tests for <see cref="BoardAttachedFileRepository"/> against a real PostgreSQL database
/// (Testcontainers) preloaded with the <c>Init.sql</c> schema and seed data.
/// <c>BoardAttachedFile</c> carries the FK <c>BoardAttachedFile_fk_0</c> to <c>Board.ID</c>
/// (which the EF Core InMemory provider did not enforce), so every test creates real parent
/// boards and scopes to their ids.
/// </summary>
public sealed class BoardAttachedFileRepositoryTests(DatabaseFixture database) : RepositoryTestBase(database)
{
    private BoardAttachedFileRepository Sut => new(Context);

    /// <summary>An unsaved attachment row for board <paramref name="boardId"/>.</summary>
    private static OrmBoardAttachedFile MakeFile(long boardId, string name = "file.pdf", long size = 1024) => new()
    {
        BoardId = boardId,
        Name = name,
        Extension = ".pdf",
        Path = $"/upload/boards/{name}",
        Size = size
    };

    /// <summary>Creates <paramref name="count"/> real parent boards and returns their ids.</summary>
    private async Task<long[]> SeedBoardsAsync(int count)
    {
        OrmBoard[] boards = [.. Enumerable.Range(0, count).Select(i => new OrmBoard
        {
            Type = BoardTypes.FreeForum,
            Title = Unique($"Parent {i}"),
            Content = "Content",
            Writer = Unique("writer"),
            Created = DateTime.UtcNow,
            Updated = DateTime.UtcNow,
            View = 0,
            Deleted = false,
            Locked = false,
            Noticed = false
        })];
        await EnsureNicknamesAsync(Unique("writer"));
        await Context.Boards.AddRangeAsync(boards);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();
        return [.. boards.Select(b => b.Id)];
    }

    /// <summary>Inserts <paramref name="files"/>, saves, and clears the change tracker so later reads hit the database.</summary>
    private async Task SeedAsync(params OrmBoardAttachedFile[] files)
    {
        await Context.BoardAttachedFiles.AddRangeAsync(files);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();
    }

    // -- FindByBoardIdAsync -------------------------------------------------

    /// <summary>Verifies that <c>FindByBoardIdAsync</c> returns the file when found.</summary>
    [Fact]
    public async Task FindByBoardIdAsync_ReturnsFile_WhenFound()
    {
        long[] boards = await SeedBoardsAsync(1);
        await SeedAsync(MakeFile(boards[0], "report.pdf"));

        BoardAttachedFile? result = await Sut.FindByBoardIdAsync(boards[0], TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(boards[0], result.BoardId);
        Assert.Equal("report.pdf", result.Name);
    }

    /// <summary>Verifies that <c>FindByBoardIdAsync</c> returns null when not found.</summary>
    [Fact]
    public async Task FindByBoardIdAsync_ReturnsNull_WhenNotFound()
    {
        BoardAttachedFile? result = await Sut.FindByBoardIdAsync(long.MaxValue, TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    // -- GetByBoardIdsAsync ---------------------------------------------------

    /// <summary>Verifies that <c>GetByBoardIdsAsync</c> returns a dictionary keyed by every requested id.</summary>
    [Fact]
    public async Task GetByBoardIdsAsync_ReturnsMappedDictionary()
    {
        long[] boards = await SeedBoardsAsync(3);
        await SeedAsync(MakeFile(boards[0], "a.pdf"), MakeFile(boards[2], "c.pdf"));

        Dictionary<long, BoardAttachedFile?> result = await Sut.GetByBoardIdsAsync(boards, TestContext.Current.CancellationToken);

        Assert.Equal(3, result.Count);
        Assert.NotNull(result[boards[0]]);
        Assert.Null(result[boards[1]]);
        Assert.NotNull(result[boards[2]]);
    }

    /// <summary>Verifies that <c>GetByBoardIdsAsync</c> yields null values when no board has a file.</summary>
    [Fact]
    public async Task GetByBoardIdsAsync_ReturnsNullValues_WhenNoneMatch()
    {
        long[] boards = await SeedBoardsAsync(2);

        Dictionary<long, BoardAttachedFile?> result = await Sut.GetByBoardIdsAsync(boards, TestContext.Current.CancellationToken);

        Assert.All(result.Values, Assert.Null);
    }

    // -- CreateAsync -------------------------------------------

    /// <summary>Verifies that <c>CreateAsync</c> persists an attached-file row.</summary>
    [Fact]
    public async Task CreateAsync_PersistsFile()
    {
        long[] boards = await SeedBoardsAsync(1);

        var file = BoardAttachedFile.Reconstitute(
            id: 0, boardId: boards[0], size: 2048, name: "new.pdf",
            extension: ".pdf", path: "/upload/boards/new.pdf");

        await Sut.CreateAsync(file, TestContext.Current.CancellationToken);
        Context.ChangeTracker.Clear();

        OrmBoardAttachedFile? saved = await Context.BoardAttachedFiles.FirstOrDefaultAsync(
            f => f.BoardId == boards[0], TestContext.Current.CancellationToken);
        Assert.NotNull(saved);
        Assert.Equal("new.pdf", saved.Name);
        Assert.Equal(2048, saved.Size);
    }

    /// <summary>PostgreSQL enforces <c>BoardAttachedFile_fk_0</c> — a file for an unknown board throws (InMemory did not).</summary>
    [Fact]
    public async Task CreateAsync_Throws_WhenParentBoardDoesNotExist()
    {
        var file = BoardAttachedFile.Reconstitute(
            id: 0, boardId: long.MaxValue, size: 1, name: "orphan.pdf",
            extension: ".pdf", path: "/x");

        await Assert.ThrowsAsync<DbUpdateException>(
            () => Sut.CreateAsync(file, TestContext.Current.CancellationToken));
    }

    // -- UpdateEntityAsync -----------------------------------------

    /// <summary>Verifies that <c>UpdateEntityAsync</c> persists changes to an existing attached file.</summary>
    [Fact]
    public async Task UpdateEntityAsync_UpdatesExistingFile()
    {
        long[] boards = await SeedBoardsAsync(1);
        var seed = MakeFile(boards[0], "old.pdf", 512);
        await SeedAsync(seed);

        var file = BoardAttachedFile.Reconstitute(
            id: seed.Id, boardId: boards[0], size: 9999, name: "updated.pdf",
            extension: ".pdf", path: "/upload/boards/updated.pdf");

        await Sut.UpdateEntityAsync(file, TestContext.Current.CancellationToken);
        Context.ChangeTracker.Clear();

        OrmBoardAttachedFile? updated = await Context.BoardAttachedFiles.FirstOrDefaultAsync(
            f => f.Id == seed.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(updated);
        Assert.Equal("updated.pdf", updated.Name);
        Assert.Equal(9999, updated.Size);
    }
}
