using Maroik.Core.Domain.FileManagers;
using Maroik.Core.Repository.Repositories;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using OrmFileManagerNode = Maroik.Core.PostgreSQL.Models.FileManagerNode;

namespace Maroik.Core.Repository.Tests.Repositories;

/// <summary>
/// Tests for <see cref="FileManagerNodeRepository"/> against a real PostgreSQL database (Testcontainers) preloaded with the
/// <c>Init.sql</c> schema and seed data. <c>FileManagerNode</c> holds folders and files in one table: <c>FileManagerNode_fk_0</c> points
/// at <c>Account.Email</c>, <c>FileManagerNode_fk_1</c> at the parent folder (ON DELETE CASCADE), the unique index
/// <c>FileManagerNode_unique_index_0</c> keeps finished names unique per folder (root included, NULLS NOT DISTINCT) and the checks keep a
/// folder without a file and a file with one. Every lookup is owner-scoped (S5).
/// </summary>
public sealed class FileManagerNodeRepositoryTests(DatabaseFixture database) : RepositoryTestBase(database)
{
    /// <summary>The repository under test, over this test's database.</summary>
    private FileManagerNodeRepository Sut => new(Context);

    /// <summary>The fixed time of the nodes these tests create.</summary>
    private static readonly DateTime Now = new(2026, 5, 24, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>Saves <paramref name="node"/> through the repository and returns its stored id.</summary>
    private async Task<long> SaveAsync(FileManagerNode node)
    {
        await EnsureAccountsAsync(node.AccountEmail.Value);
        await Sut.CreateAsync(node, TestContext.Current.CancellationToken);
        Context.ChangeTracker.Clear();
        return await Context.FileManagerNodes.AsNoTracking()
            .Where(n => n.AccountEmail == node.AccountEmail.Value && n.ParentId == node.ParentId && n.Name == node.Name && n.Uploading == node.Uploading)
            .Select(n => n.Id).SingleAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>A folder of <paramref name="email"/>.</summary>
    private Task<long> FolderAsync(string email, string name, long? parentId = null) =>
        SaveAsync(FileManagerNode.CreateFolder(email, parentId, name, Now).Value);

    /// <summary>A file of <paramref name="email"/> (finished unless <paramref name="uploading"/>).</summary>
    private Task<long> FileAsync(string email, string name, long? parentId = null, bool uploading = false, long size = 10)
    {
        FileManagerNode file = FileManagerNode.StartUpload(email, parentId, name, size, Guid.NewGuid(), Now).Value;
        if (!uploading) file.CompleteUpload(Now);
        return SaveAsync(file);
    }

    // -- CreateAsync / GetByAccountEmailAsync ---------------------------------------

    /// <summary>A folder and a file read back with every field.</summary>
    [Fact]
    public async Task CreateAsync_ThenGetByAccountEmailAsync_ReadsBackEveryField()
    {
        string email = UniqueEmail();
        long folderId = await FolderAsync(email, "Design");
        FileManagerNode upload = FileManagerNode.StartUpload(email, folderId, "logo.png", 1234, Guid.Parse("8f1e9c0a-1b2c-4d5e-8f70-112233445566"), Now).Value;
        long fileId = await SaveAsync(upload);

        List<FileManagerNode> nodes = await Sut.GetByAccountEmailAsync(email, TestContext.Current.CancellationToken);

        FileManagerNode folder = Assert.Single(nodes, n => n.Id == folderId);
        Assert.Equal((email, (long?)null, "Design", true, 0L, (string?)null, false, Now, Now),
            (folder.AccountEmail.Value, folder.ParentId, folder.Name, folder.IsFolder, folder.Size, folder.StoragePath, folder.Uploading, folder.Created, folder.Updated));
        FileManagerNode file = Assert.Single(nodes, n => n.Id == fileId);
        Assert.Equal(((long?)folderId, "logo.png", false, 1234L, "upload/FileManager/8f1e9c0a-1b2c-4d5e-8f70-112233445566", true),
            (file.ParentId, file.Name, file.IsFolder, file.Size, file.StoragePath, file.Uploading));
        Assert.Equal(2, nodes.Count);
    }

    /// <summary>Each account sees only its own nodes.</summary>
    [Fact]
    public async Task GetByAccountEmailAsync_ReturnsOnlyTheAccountsNodes()
    {
        string alice = UniqueEmail("alice");
        string bob = UniqueEmail("bob");
        await FolderAsync(alice, "A");
        await FolderAsync(bob, "B");

        Assert.Equal(["A"], (await Sut.GetByAccountEmailAsync(alice, TestContext.Current.CancellationToken)).Select(n => n.Name));
    }

    // -- Find -------------------------------------------------------------------------

    /// <summary>The owner finds a node by id and a file by storage path; another account finds neither.</summary>
    [Fact]
    public async Task FindByEmailAndIdAsync_AndByStoragePath_FindOnlyTheOwnersNode()
    {
        string owner = UniqueEmail("owner");
        string other = UniqueEmail("other");
        await EnsureAccountsAsync(other);
        long id = await FileAsync(owner, "a.txt");
        string path = (await Context.FileManagerNodes.AsNoTracking().SingleAsync(n => n.Id == id, TestContext.Current.CancellationToken)).StoragePath!;

        Assert.Equal("a.txt", (await Sut.FindByEmailAndIdAsync(owner, id, TestContext.Current.CancellationToken))?.Name);
        Assert.Null(await Sut.FindByEmailAndIdAsync(other, id, TestContext.Current.CancellationToken));
        Assert.Equal(id, (await Sut.FindByEmailAndStoragePathAsync(owner, path, TestContext.Current.CancellationToken))?.Id);
        Assert.Null(await Sut.FindByEmailAndStoragePathAsync(other, path, TestContext.Current.CancellationToken));
    }

    // -- UpdateEntityAsync ------------------------------------------------------------

    /// <summary>A rename, a move and a completed upload are persisted.</summary>
    [Fact]
    public async Task UpdateEntityAsync_PersistsRenameMoveAndCompletion()
    {
        string email = UniqueEmail();
        long target = await FolderAsync(email, "Target");
        long fileId = await FileAsync(email, "old.txt", uploading: true);
        FileManagerNode file = (await Sut.FindByEmailAndIdAsync(email, fileId, TestContext.Current.CancellationToken))!;
        Assert.False(file.Rename("new.txt", Now.AddHours(1)).IsError);
        Assert.False(file.MoveTo(target, [target], Now.AddHours(1)).IsError);
        file.CompleteUpload(Now.AddHours(1));

        await Sut.UpdateEntityAsync(file, TestContext.Current.CancellationToken);
        Context.ChangeTracker.Clear();

        OrmFileManagerNode row = await Context.FileManagerNodes.AsNoTracking().SingleAsync(n => n.Id == fileId, TestContext.Current.CancellationToken);
        Assert.Equal(("new.txt", (long?)target, false, Now.AddHours(1)), (row.Name, row.ParentId, row.Uploading, row.Updated));
    }

    // -- DeleteByEmailAndIdsAsync -----------------------------------------------------

    /// <summary>The listed nodes of the owner are deleted; another account's id in the list is left alone.</summary>
    [Fact]
    public async Task DeleteByEmailAndIdsAsync_DeletesOnlyTheOwnersListedNodes()
    {
        string owner = UniqueEmail("owner");
        string other = UniqueEmail("other");
        long a = await FileAsync(owner, "a.txt");
        long b = await FileAsync(owner, "b.txt");
        long kept = await FileAsync(owner, "c.txt");
        long others = await FileAsync(other, "a.txt");

        await Sut.DeleteByEmailAndIdsAsync(owner, [a, b, others], TestContext.Current.CancellationToken);

        List<long> left = await Context.FileManagerNodes.AsNoTracking().Where(n => new[] { a, b, kept, others }.Contains(n.Id)).Select(n => n.Id).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal([kept, others], left.Order());
    }

    /// <summary>Deleting a folder row deletes everything under it (FileManagerNode_fk_1 ON DELETE CASCADE).</summary>
    [Fact]
    public async Task DeletingAFolder_CascadesToEverythingUnderIt()
    {
        string email = UniqueEmail();
        long folder = await FolderAsync(email, "Top");
        long sub = await FolderAsync(email, "Sub", folder);
        long file = await FileAsync(email, "deep.txt", sub);

        await Sut.DeleteByEmailAndIdsAsync(email, [folder], TestContext.Current.CancellationToken);

        Assert.False(await Context.FileManagerNodes.AsNoTracking().AnyAsync(n => n.Id == folder || n.Id == sub || n.Id == file, TestContext.Current.CancellationToken));
    }

    // -- Schema -----------------------------------------------------------------------

    /// <summary>Saving <paramref name="row"/> fails on <paramref name="constraint"/>.</summary>
    private async Task AssertRefusedAsync(OrmFileManagerNode row, string constraint)
    {
        await Context.FileManagerNodes.AddAsync(row, TestContext.Current.CancellationToken);
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => Context.SaveChangesAsync(TestContext.Current.CancellationToken));
        Assert.Equal(constraint, Assert.IsType<PostgresException>(error.InnerException).ConstraintName);
        Context.ChangeTracker.Clear();
    }

    /// <summary>An unsaved row for <paramref name="email"/>.</summary>
    private static OrmFileManagerNode Row(string email, string name, bool isFolder, string? storagePath, long? parentId = null, bool uploading = false, long size = 0) => new()
    {
        AccountEmail = email, Name = name, IsFolder = isFolder, StoragePath = storagePath, ParentId = parentId, Uploading = uploading, Size = size,
        Created = Now, Updated = Now,
    };

    /// <summary>Two finished nodes of the same name in the same folder are refused — at the root too (NULLS NOT DISTINCT).</summary>
    [Fact]
    public async Task TheDatabase_RefusesADuplicateFinishedName_InTheSameFolder()
    {
        string email = UniqueEmail();
        long folder = await FolderAsync(email, "Docs");
        await FileAsync(email, "a.txt", folder);
        await FolderAsync(email, "RootName");

        await AssertRefusedAsync(Row(email, "a.txt", false, $"upload/FileManager/{Guid.NewGuid()}", folder, size: 1), "FileManagerNode_unique_index_0");
        await AssertRefusedAsync(Row(email, "RootName", true, null), "FileManagerNode_unique_index_0");
    }

    /// <summary>An unfinished upload may share its name with a finished file (an overwrite in progress), and the same name may sit in another folder.</summary>
    [Fact]
    public async Task TheDatabase_AllowsAnUploadingTwin_AndTheSameNameElsewhere()
    {
        string email = UniqueEmail();
        long folder = await FolderAsync(email, "Docs");
        await FileAsync(email, "a.txt", folder);

        await FileAsync(email, "a.txt", folder, uploading: true);
        await FileAsync(email, "a.txt");

        Assert.Equal(3, await Context.FileManagerNodes.AsNoTracking().CountAsync(n => n.AccountEmail == email && n.Name == "a.txt", TestContext.Current.CancellationToken));
    }

    /// <summary>The kind check keeps a folder without a stored file and a file with one; the name check refuses a blank name; sizes cannot be negative.</summary>
    [Fact]
    public async Task TheDatabase_EnforcesTheKindNameAndSizeChecks()
    {
        string email = UniqueEmail();
        await EnsureAccountsAsync(email);

        await AssertRefusedAsync(Row(email, "f", true, "upload/FileManager/x"), "FileManagerNode_Kind_check");
        await AssertRefusedAsync(Row(email, "f", false, null), "FileManagerNode_Kind_check");
        await AssertRefusedAsync(Row(email, " ", true, null), "FileManagerNode_Name_check");
        await AssertRefusedAsync(Row(email, "f", false, "upload/FileManager/y", size: -1), "FileManagerNode_Size_check");
    }

    /// <summary>A node needs an existing account and an existing parent.</summary>
    [Fact]
    public async Task TheDatabase_RefusesAnUnknownAccountOrParent()
    {
        string email = UniqueEmail();
        await EnsureAccountsAsync(email);

        await AssertRefusedAsync(Row(UniqueEmail("nobody"), "f", true, null), "FileManagerNode_fk_0");
        await AssertRefusedAsync(Row(email, "f", true, null, parentId: long.MaxValue), "FileManagerNode_fk_1");
    }
}
