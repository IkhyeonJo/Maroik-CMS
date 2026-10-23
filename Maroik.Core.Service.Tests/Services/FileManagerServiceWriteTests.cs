using Maroik.Core.Contract.Dtos;
using Maroik.Core.Service.Services;
using Microsoft.Extensions.Logging;
using Moq;
using H = Maroik.Core.Service.Tests.Fixtures.FileManagerServiceHarness;

namespace Maroik.Core.Service.Tests.Services;

/// <summary>
/// The structural writes of <see cref="FileManagerService"/>: create a folder (F1, and the "merge" of a folder upload, F2-C), rename (F4),
/// move (F5) and delete (F6, P4). Each runs in its own transaction after locking the account row, answers another account's item as
/// "not found" (S5), refuses a taken name or a cycle, and a delete removes the stored files only after the commit — a failed file delete is
/// logged and left as an orphan.
/// </summary>
public class FileManagerServiceWriteTests
{
    /// <summary>The service, its collaborators and the seeding helpers.</summary>
    private readonly H _h = new();

    // -- CreateFolderAsync ---------------------------------------------------------

    /// <summary>A new folder is created under its parent (or the root) and its id returned.</summary>
    [Fact]
    public async Task CreateFolderAsync_CreatesTheFolder()
    {
        long docs = _h.Folder("Documents");

        var (result, id) = await _h.Sut.CreateFolderAsync(H.Email, docs, "Design", merge: false, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(("Design", (long?)docs, true), (_h.Node(id!.Value).Name, _h.Node(id.Value).ParentId, _h.Node(id.Value).IsFolder));
        _h.AssertAccountLocked();
        _h.AssertCommitted();
    }

    /// <summary>A name already used in the folder (by a folder or a file) is refused, naming it.</summary>
    [Fact]
    public async Task CreateFolderAsync_RefusesATakenName()
    {
        _h.File("Design");

        var (result, id) = await _h.Sut.CreateFolderAsync(H.Email, null, "Design", merge: false, TestContext.Current.CancellationToken);

        Assert.Equal(ServiceErrorType.Conflict, result.ErrorType);
        Assert.Equal(ServiceErrorKeys.FileManagerNameTaken, result.ErrorKey);
        Assert.Equal(["Design"], result.ErrorArgs);
        Assert.Null(id);
        _h.AssertRolledBack();
    }

    /// <summary>A folder upload merges into an existing folder of the same name (only its contents are added).</summary>
    [Fact]
    public async Task CreateFolderAsync_WithMerge_ReusesAnExistingFolder()
    {
        long existing = _h.Folder("Design");

        var (result, id) = await _h.Sut.CreateFolderAsync(H.Email, null, "Design", merge: true, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(existing, id);
        Assert.Single(_h.Nodes.All);
    }

    /// <summary>A merge still refuses to reuse a file of that name as a folder.</summary>
    [Fact]
    public async Task CreateFolderAsync_WithMerge_RefusesAFileOfThatName()
    {
        _h.File("Design");

        var (result, _) = await _h.Sut.CreateFolderAsync(H.Email, null, "Design", merge: true, TestContext.Current.CancellationToken);

        Assert.Equal(ServiceErrorKeys.FileManagerNameTaken, result.ErrorKey);
    }

    /// <summary>An invalid name is refused with the domain's reason; a parent the account does not own is "not found".</summary>
    [Fact]
    public async Task CreateFolderAsync_RefusesAnInvalidNameOrParent()
    {
        long theirs = _h.Folder("Theirs", email: H.OtherEmail);

        var (invalid, _) = await _h.Sut.CreateFolderAsync(H.Email, null, "a/b", false, TestContext.Current.CancellationToken);
        var (notFound, _) = await _h.Sut.CreateFolderAsync(H.Email, theirs, "x", false, TestContext.Current.CancellationToken);

        Assert.Equal("FileManager.NameInvalidCharacter", invalid.ErrorCode);
        Assert.Equal(ServiceErrorKeys.FileManagerItemNotFound, notFound.ErrorKey);
    }

    // -- RenameAsync ---------------------------------------------------------------

    /// <summary>A rename stores the new name.</summary>
    [Fact]
    public async Task RenameAsync_StoresTheNewName()
    {
        long id = _h.File("old.txt");

        ServiceResult result = await _h.Sut.RenameAsync(H.Email, id, "new.txt", TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal("new.txt", _h.Node(id).Name);
        _h.AssertCommitted();
    }

    /// <summary>A name taken by another item of the same folder is refused; renaming to its own name is fine.</summary>
    [Fact]
    public async Task RenameAsync_RefusesANameTakenInTheFolder()
    {
        long id = _h.File("a.txt");
        _h.File("b.txt");

        ServiceResult taken = await _h.Sut.RenameAsync(H.Email, id, "b.txt", TestContext.Current.CancellationToken);
        ServiceResult same = await _h.Sut.RenameAsync(H.Email, id, "a.txt", TestContext.Current.CancellationToken);

        Assert.Equal(ServiceErrorKeys.FileManagerNameTaken, taken.ErrorKey);
        Assert.Equal(["b.txt"], taken.ErrorArgs);
        Assert.Equal("a.txt", _h.Node(id).Name);
        Assert.True(same.Success);
    }

    /// <summary>Another account's item and an unfinished upload cannot be renamed ("not found").</summary>
    [Fact]
    public async Task RenameAsync_AnswersNotFound_ForAnotherAccountsItemOrAnUpload()
    {
        long theirs = _h.File("a.txt", email: H.OtherEmail);
        long upload = _h.File("b.txt", uploading: true);

        Assert.Equal(ServiceErrorKeys.FileManagerItemNotFound, (await _h.Sut.RenameAsync(H.Email, theirs, "x", TestContext.Current.CancellationToken)).ErrorKey);
        Assert.Equal(ServiceErrorKeys.FileManagerItemNotFound, (await _h.Sut.RenameAsync(H.Email, upload, "x", TestContext.Current.CancellationToken)).ErrorKey);
        Assert.Equal("a.txt", _h.Node(theirs).Name);
    }

    // -- MoveAsync -----------------------------------------------------------------

    /// <summary>Items move into the target folder (or back to the root) together.</summary>
    [Fact]
    public async Task MoveAsync_MovesEveryItem()
    {
        long target = _h.Folder("Target");
        long folder = _h.Folder("Sub");
        long file = _h.File("a.txt");

        ServiceResult result = await _h.Sut.MoveAsync(H.Email, [folder, file], target, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal((long?)target, _h.Node(folder).ParentId);
        Assert.Equal((long?)target, _h.Node(file).ParentId);
        _h.AssertAccountLocked();
        _h.AssertCommitted();
    }

    /// <summary>A folder cannot move into itself or one of its sub-folders; nothing moves.</summary>
    [Fact]
    public async Task MoveAsync_RefusesACycle_AndMovesNothing()
    {
        long top = _h.Folder("Top");
        long sub = _h.Folder("Sub", top);
        long file = _h.File("a.txt");

        ServiceResult result = await _h.Sut.MoveAsync(H.Email, [file, top], sub, TestContext.Current.CancellationToken);

        Assert.Equal("FileManager.MoveIntoItself", result.ErrorCode);
        Assert.Null(_h.Node(file).ParentId);
        Assert.Null(_h.Node(top).ParentId);
        _h.AssertRolledBack();
    }

    /// <summary>A name taken in the target folder — or two moved items of the same name — is refused and nothing moves.</summary>
    [Fact]
    public async Task MoveAsync_RefusesANameConflict()
    {
        long target = _h.Folder("Target");
        _h.File("a.txt", target);
        long file = _h.File("a.txt");
        long other = _h.Folder("Other");
        long twin = _h.File("b.txt", other);
        long twin2 = _h.File("b.txt");

        ServiceResult taken = await _h.Sut.MoveAsync(H.Email, [file], target, TestContext.Current.CancellationToken);
        ServiceResult together = await _h.Sut.MoveAsync(H.Email, [twin, twin2], target, TestContext.Current.CancellationToken);

        Assert.Equal(ServiceErrorKeys.FileManagerNameTaken, taken.ErrorKey);
        Assert.Equal(["a.txt"], taken.ErrorArgs);
        Assert.Equal(ServiceErrorKeys.FileManagerNameTaken, together.ErrorKey);
        Assert.Null(_h.Node(file).ParentId);
        Assert.Equal((long?)other, _h.Node(twin).ParentId);
    }

    /// <summary>An item already in the target stays where it is (no conflict with itself).</summary>
    [Fact]
    public async Task MoveAsync_IntoItsOwnFolder_IsANoOp()
    {
        long target = _h.Folder("Target");
        long file = _h.File("a.txt", target);

        Assert.True((await _h.Sut.MoveAsync(H.Email, [file], target, TestContext.Current.CancellationToken)).Success);
        Assert.Equal((long?)target, _h.Node(file).ParentId);
    }

    /// <summary>A target that is not the account's folder (another account's, a file, missing) and an item the account does not own are "not found".</summary>
    [Fact]
    public async Task MoveAsync_AnswersNotFound_ForAnUnknownTargetOrItem()
    {
        long file = _h.File("a.txt");
        long notAFolder = _h.File("b.txt");
        long theirs = _h.Folder("Theirs", email: H.OtherEmail);
        long theirFile = _h.File("c.txt", email: H.OtherEmail);

        Assert.Equal(ServiceErrorKeys.FileManagerItemNotFound, (await _h.Sut.MoveAsync(H.Email, [file], notAFolder, TestContext.Current.CancellationToken)).ErrorKey);
        Assert.Equal(ServiceErrorKeys.FileManagerItemNotFound, (await _h.Sut.MoveAsync(H.Email, [file], theirs, TestContext.Current.CancellationToken)).ErrorKey);
        Assert.Equal(ServiceErrorKeys.FileManagerItemNotFound, (await _h.Sut.MoveAsync(H.Email, [theirFile], null, TestContext.Current.CancellationToken)).ErrorKey);
        Assert.Equal((long?)null, _h.Node(theirFile).ParentId);
    }

    // -- DeleteAsync ---------------------------------------------------------------

    /// <summary>
    /// A delete removes the selected items — a folder with everything under it — commits, and only then asks file storage to delete each
    /// stored file (an unfinished upload's is aborted); it is logged with who deleted how much.
    /// </summary>
    [Fact]
    public async Task DeleteAsync_DeletesTheRows_ThenTheStoredFiles()
    {
        long docs = _h.Folder("Documents");
        long a = _h.File("a.txt", docs);
        long upload = _h.File("b.iso", docs, uploading: true);
        long c = _h.File("c.txt");
        long kept = _h.File("kept.txt");
        string aPath = _h.Node(a).StoragePath!;
        string cPath = _h.Node(c).StoragePath!;
        Guid uploadId = _h.Node(upload).StorageId!.Value;
        List<string> order = [];
        _h.UnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).Callback(() => order.Add("commit")).Returns(Task.CompletedTask);
        _h.Files.Setup(f => f.DeleteFileManagerFileAsync(It.IsAny<string>(), H.BaseUrl, It.IsAny<CancellationToken>()))
            .Callback((string p, string _, CancellationToken _) => order.Add(p)).ReturnsAsync(true);

        ServiceResult result = await _h.Sut.DeleteAsync(H.Email, [docs, c], TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal([kept], _h.Nodes.All.Select(n => n.Id));
        Assert.Equal("commit", order[0]);
        Assert.Equal(new[] { aPath, cPath }.Order(StringComparer.Ordinal), order.Skip(1).Order(StringComparer.Ordinal));
        _h.Files.Verify(f => f.AbortFileManagerUploadAsync(uploadId, H.BaseUrl, It.IsAny<CancellationToken>()), Times.Once);
        _h.AssertAccountLocked();
        Assert.Contains(_h.Logger.Collector.GetSnapshot(), r => r.Level == LogLevel.Information && r.Message == $"File Manager: {H.Email} deleted 2 item(s) (4 node(s), 3 stored file(s))");
    }

    /// <summary>A stored file that cannot be deleted is left as an orphan: the delete still succeeds for the user and a warning names it.</summary>
    [Fact]
    public async Task DeleteAsync_LeavesAnOrphan_WhenTheStoredFileCannotBeDeleted()
    {
        long a = _h.File("a.txt");
        string path = _h.Node(a).StoragePath!;
        _h.Files.Setup(f => f.DeleteFileManagerFileAsync(path, H.BaseUrl, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        ServiceResult result = await _h.Sut.DeleteAsync(H.Email, [a], TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Empty(_h.Nodes.All);
        Assert.Contains(_h.Logger.Collector.GetSnapshot(), r => r.Level == LogLevel.Warning && r.Message == $"File Manager: stored file {path} of {H.Email} could not be deleted; left as an orphan");
    }

    /// <summary>A selection naming an item the account does not own is "not found" and deletes nothing.</summary>
    [Fact]
    public async Task DeleteAsync_AnswersNotFound_AndDeletesNothing_ForAnotherAccountsItem()
    {
        long mine = _h.File("a.txt");
        long theirs = _h.File("b.txt", email: H.OtherEmail);

        ServiceResult result = await _h.Sut.DeleteAsync(H.Email, [mine, theirs], TestContext.Current.CancellationToken);

        Assert.Equal(ServiceErrorType.NotFound, result.ErrorType);
        Assert.Equal(2, _h.Nodes.All.Count);
        _h.AssertRolledBack();
        _h.Files.Verify(f => f.DeleteFileManagerFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>A database failure inside a write is rolled back, logged at Error with the exception and answered as a temporary error.</summary>
    [Fact]
    public async Task AWriteFailure_IsRolledBack_LoggedAndAnsweredAsTemporary()
    {
        long a = _h.File("a.txt");
        _h.UnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("db down"));

        ServiceResult result = await _h.Sut.DeleteAsync(H.Email, [a], TestContext.Current.CancellationToken);

        Assert.Equal(ServiceErrorType.Failure, result.ErrorType);
        Assert.Equal(ServiceResult.TemporaryErrorKey, result.ErrorKey);
        _h.UnitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        Assert.Contains(_h.Logger.Collector.GetSnapshot(), r => r.Level == LogLevel.Error && r.Exception?.Message == "db down");
        _h.Files.Verify(f => f.DeleteFileManagerFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
