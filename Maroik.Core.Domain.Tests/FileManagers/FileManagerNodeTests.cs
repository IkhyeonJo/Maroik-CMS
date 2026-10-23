using Maroik.Core.Domain.FileManagers;
namespace Maroik.Core.Domain.Tests.FileManagers;

/// <summary>
/// Unit tests for <see cref="FileManagerNode"/>: a folder or a file of a user's drive. Folders are logical (database only, P3); a file
/// is stored under a GUID name in <c>upload/FileManager</c> (P1) and is "uploading" until its upload completes (Q4). Names follow
/// <see cref="FileManagerNamePolicy"/>; a folder cannot be moved into itself or its own sub-folder (S5).
/// </summary>
public class FileManagerNodeTests
{
    /// <summary>The fixed "current time" every domain call in this class receives.</summary>
    private static readonly DateTime Now = new(2026, 5, 24, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>A storage id for the uploads these tests start.</summary>
    private static readonly Guid StorageId = Guid.Parse("8f1e9c0a-1b2c-4d5e-8f70-112233445566");

    /// <summary>A stored folder with the given id and parent.</summary>
    private static FileManagerNode Folder(long id, long? parentId = null, string name = "Design") =>
        FileManagerNode.Reconstitute(id, "user@example.com", parentId, name, isFolder: true, 0, null, uploading: false, Now, Now);

    /// <summary>A stored, completed file with the given id.</summary>
    private static FileManagerNode File(long id, long? parentId = null) =>
        FileManagerNode.Reconstitute(id, "user@example.com", parentId, "a.txt", isFolder: false, 10, "upload/FileManager/x", uploading: false, Now, Now);

    // -- CreateFolder -------------------------------------------------------------

    /// <summary>A folder is created with its normalized name under its parent (null = the drive root), with no size and no file.</summary>
    [Fact]
    public void CreateFolder_ReturnsAFolder()
    {
        var result = FileManagerNode.CreateFolder("User@Example.com", 7, "ＤＥＳＩＧＮ", Now);

        Assert.False(result.IsError);
        FileManagerNode folder = result.Value;
        Assert.Equal("user@example.com", folder.AccountEmail.Value);
        Assert.Equal(7, folder.ParentId);
        Assert.Equal("DESIGN", folder.Name);
        Assert.True(folder.IsFolder);
        Assert.Equal(0, folder.Size);
        Assert.Null(folder.StoragePath);
        Assert.False(folder.Uploading);
        Assert.Equal(Now, folder.Created);
        Assert.Equal(Now, folder.Updated);
    }

    /// <summary>A folder name that breaks the name rule is refused.</summary>
    [Fact]
    public void CreateFolder_RefusesAnInvalidName()
    {
        Assert.Equal("FileManager.NameReserved", FileManagerNode.CreateFolder("user@example.com", null, "..", Now).FirstError.Code);
    }

    // -- StartUpload / CompleteUpload ------------------------------------------------

    /// <summary>
    /// Starting an upload creates an "uploading" file: its declared size is reserved against the quota and it is stored under the
    /// GUID name in upload/FileManager, never under the name the user typed.
    /// </summary>
    [Fact]
    public void StartUpload_ReturnsAnUploadingFile_StoredUnderItsGuid()
    {
        var result = FileManagerNode.StartUpload("user@example.com", null, "photo.png", 1234, StorageId, Now);

        Assert.False(result.IsError);
        FileManagerNode file = result.Value;
        Assert.False(file.IsFolder);
        Assert.True(file.Uploading);
        Assert.Equal(1234, file.Size);
        Assert.Equal("photo.png", file.Name);
        Assert.Equal("upload/FileManager/8f1e9c0a-1b2c-4d5e-8f70-112233445566", file.StoragePath);
        Assert.Equal(StorageId, file.StorageId);
    }

    /// <summary>An upload over the per-file limit, or with an invalid name, is refused when it starts.</summary>
    [Fact]
    public void StartUpload_RefusesATooLargeFile_OrAnInvalidName()
    {
        Assert.Equal("FileManager.FileTooLarge", FileManagerNode.StartUpload("user@example.com", null, "big.iso", FileManagerUploadPolicy.MaxFileBytes + 1, StorageId, Now).FirstError.Code);
        Assert.Equal("FileManager.NameInvalidCharacter", FileManagerNode.StartUpload("user@example.com", null, "a:b", 1, StorageId, Now).FirstError.Code);
    }

    /// <summary>Completing an upload makes the file a normal one and stamps Updated.</summary>
    [Fact]
    public void CompleteUpload_EndsTheUpload()
    {
        FileManagerNode file = FileManagerNode.StartUpload("user@example.com", null, "photo.png", 1, StorageId, Now).Value;

        file.CompleteUpload(Now.AddMinutes(5));

        Assert.False(file.Uploading);
        Assert.Equal(Now.AddMinutes(5), file.Updated);
        Assert.Equal(Now, file.Created);
    }

    /// <summary>A folder has no storage id; a file stored under another name than a GUID has none either.</summary>
    [Fact]
    public void StorageId_IsNull_WithoutAGuidStorageName()
    {
        Assert.Null(Folder(1).StorageId);
        Assert.Null(File(2).StorageId);
    }

    /// <summary>An upload left unfinished for more than 24 hours is stale; a completed file or a folder never is.</summary>
    [Fact]
    public void IsStaleUpload_IsTrue_OnlyForAnOldUnfinishedUpload()
    {
        FileManagerNode upload = FileManagerNode.Reconstitute(1, "user@example.com", null, "a", false, 1, "upload/FileManager/x", true, Now, Now);

        Assert.True(upload.IsStaleUpload(Now.AddHours(25)));
        Assert.False(upload.IsStaleUpload(Now.AddHours(23)));
        Assert.False(File(2).IsStaleUpload(Now.AddDays(30)));
        Assert.False(Folder(3).IsStaleUpload(Now.AddDays(30)));
    }

    // -- Rename -------------------------------------------------------------------

    /// <summary>Rename stores the normalized new name and stamps Updated.</summary>
    [Fact]
    public void Rename_StoresTheNewName()
    {
        FileManagerNode folder = Folder(1);

        var result = folder.Rename("ｎｅｗ", Now.AddHours(1));

        Assert.False(result.IsError);
        Assert.Equal("new", folder.Name);
        Assert.Equal(Now.AddHours(1), folder.Updated);
    }

    /// <summary>A refused rename leaves the name as it was.</summary>
    [Fact]
    public void Rename_RefusesAnInvalidName_AndChangesNothing()
    {
        FileManagerNode folder = Folder(1);

        Assert.Equal("FileManager.NameTrailingDotOrSpace", folder.Rename("bad.", Now.AddHours(1)).FirstError.Code);
        Assert.Equal("Design", folder.Name);
        Assert.Equal(Now, folder.Updated);
    }

    // -- MoveTo -------------------------------------------------------------------

    /// <summary>A file or folder moves into another folder (or the root) and Updated is stamped.</summary>
    [Fact]
    public void MoveTo_ChangesTheParent()
    {
        FileManagerNode folder = Folder(5, parentId: 1);
        FileManagerNode file = File(6, parentId: 1);

        Assert.False(folder.MoveTo(2, [2, 1], Now.AddHours(1)).IsError);
        Assert.False(file.MoveTo(null, [], Now.AddHours(1)).IsError);

        Assert.Equal(2, folder.ParentId);
        Assert.Null(file.ParentId);
        Assert.Equal(Now.AddHours(1), folder.Updated);
    }

    /// <summary>
    /// A folder cannot be moved into itself or into one of its own sub-folders (a cycle): the target's ancestry (the target and
    /// every folder above it) must not contain the folder.
    /// </summary>
    [Theory]
    [InlineData(5L, new long[] { 5, 1 })]
    [InlineData(9L, new long[] { 9, 7, 5, 1 })]
    public void MoveTo_RefusesACycle(long targetId, long[] targetAncestry)
    {
        FileManagerNode folder = Folder(5, parentId: 1);

        var result = folder.MoveTo(targetId, targetAncestry, Now.AddHours(1));

        Assert.Equal("FileManager.MoveIntoItself", result.FirstError.Code);
        Assert.Equal(1, folder.ParentId);
        Assert.Equal(Now, folder.Updated);
    }

    /// <summary>Reconstitute assigns every stored field verbatim.</summary>
    [Fact]
    public void Reconstitute_AssignsEveryField()
    {
        DateTime created = new(2026, 1, 2);
        DateTime updated = new(2026, 2, 3);

        FileManagerNode node = FileManagerNode.Reconstitute(42, "user@example.com", 3, "n", false, 99, "upload/FileManager/g", true, created, updated);

        Assert.Equal((42L, "user@example.com", (long?)3, "n", false, 99L, "upload/FileManager/g", true, created, updated),
            (node.Id, node.AccountEmail.Value, node.ParentId, node.Name, node.IsFolder, node.Size, node.StoragePath, node.Uploading, node.Created, node.Updated));
    }
}
