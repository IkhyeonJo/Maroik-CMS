using Maroik.Core.Contract.Dtos;
using Maroik.Core.Service.Services;
using H = Maroik.Core.Service.Tests.Fixtures.FileManagerServiceHarness;

namespace Maroik.Core.Service.Tests.Services;

/// <summary>
/// The read side of <see cref="FileManagerService"/>: the left panel's folder tree and usage (U1–U3, F7), an open folder's breadcrumb and
/// items (U4, U6), the drive-wide name search with locations (U5) and the file list a multi-download fetches (F8). Another account's items
/// are never shown, and an unfinished upload is never listed (though its size counts as used, Q4).
/// </summary>
public class FileManagerServiceBrowseTests
{
    /// <summary>The service, its collaborators and the seeding helpers.</summary>
    private readonly H _h = new();

    // -- GetDriveAsync -------------------------------------------------------------

    /// <summary>The drive lists every folder with its parent and item count, the root's count, the used bytes (uploads included) and the quota.</summary>
    [Fact]
    public async Task GetDriveAsync_ReturnsTheFolderTree_AndTheUsage()
    {
        long docs = _h.Folder("Documents");
        long work = _h.Folder("Work", docs);
        _h.File("a.txt", docs, size: 100);
        _h.File("b.txt", work, size: 50);
        _h.File("half.iso", docs, size: 1000, uploading: true);
        _h.Folder("Not mine", email: H.OtherEmail);
        _h.File("x.bin", size: 7777, email: H.OtherEmail);
        _h.QuotaBytes = 30 * H.Gb;

        FileManagerDriveResponse drive = await _h.Sut.GetDriveAsync(H.Email, TestContext.Current.CancellationToken);

        Assert.Equal(1150, drive.UsedBytes);
        Assert.Equal(30 * H.Gb, drive.QuotaBytes);
        Assert.Equal(1, drive.RootItemCount);
        Assert.Equal(["Documents", "Work"], drive.Folders.Select(f => f.Name));
        FileManagerItemResponse documents = drive.Folders[0];
        Assert.Equal((docs, (long?)null, true, 2), (documents.Id, documents.ParentId, documents.IsFolder, documents.ItemCount));
        Assert.Equal((long?)docs, drive.Folders[1].ParentId);
        Assert.Equal(1, drive.Folders[1].ItemCount);
    }

    // -- GetFolderAsync ------------------------------------------------------------

    /// <summary>The root lists its finished items, folders first and then by name ignoring case, with an empty breadcrumb.</summary>
    [Fact]
    public async Task GetFolderAsync_ListsTheRoot_FoldersFirst_ThenByName()
    {
        _h.File("b.txt");
        _h.File("A.txt");
        _h.Folder("zeta");
        _h.Folder("Alpha");
        _h.File("upload.iso", uploading: true);

        var (result, folder) = await _h.Sut.GetFolderAsync(H.Email, null, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Null(folder!.FolderId);
        Assert.Empty(folder.Breadcrumb);
        Assert.Equal(["Alpha", "zeta", "A.txt", "b.txt"], folder.Items.Select(i => i.Name));
    }

    /// <summary>
    /// A sub-folder answers its breadcrumb from the top down, and each item its size, update time, item count and whether it can be
    /// previewed (an image within the limit, judged by the server).
    /// </summary>
    [Fact]
    public async Task GetFolderAsync_ListsASubFolder_WithItsBreadcrumbAndItemDetails()
    {
        long docs = _h.Folder("Documents");
        long design = _h.Folder("Design", docs);
        _h.Folder("Icons", design);
        _h.File("logo.png", design, size: 2048);
        _h.File("readme.svg", design, size: 10);

        var (_, folder) = await _h.Sut.GetFolderAsync(H.Email, design, TestContext.Current.CancellationToken);

        Assert.Equal((long?)design, folder!.FolderId);
        Assert.Equal([(docs, "Documents"), (design, "Design")], folder.Breadcrumb.Select(c => (c.Id, c.Name)));
        FileManagerItemResponse logo = Assert.Single(folder.Items, i => i.Name == "logo.png");
        Assert.Equal((2048L, H.Now, true, (long?)design), (logo.Size, logo.Updated, logo.Previewable, logo.ParentId));
        Assert.False(Assert.Single(folder.Items, i => i.Name == "readme.svg").Previewable);
        Assert.Equal(0, Assert.Single(folder.Items, i => i.Name == "Icons").ItemCount);
    }

    /// <summary>Another account's folder, a missing folder and a file are "not found".</summary>
    [Fact]
    public async Task GetFolderAsync_AnswersNotFound_ForAFolderTheAccountDoesNotOwn()
    {
        long others = _h.Folder("Theirs", email: H.OtherEmail);
        long file = _h.File("a.txt");

        foreach (long id in new[] { others, file, 999L })
        {
            var (result, folder) = await _h.Sut.GetFolderAsync(H.Email, id, TestContext.Current.CancellationToken);
            Assert.Equal(ServiceErrorType.NotFound, result.ErrorType);
            Assert.Equal(ServiceErrorKeys.FileManagerItemNotFound, result.ErrorKey);
            Assert.Null(folder);
        }
    }

    // -- SearchAsync ---------------------------------------------------------------

    /// <summary>The search matches names across the whole drive ignoring case, never an upload or another account's item, each with its location.</summary>
    [Fact]
    public async Task SearchAsync_MatchesNamesAcrossTheDrive_WithLocations()
    {
        long docs = _h.Folder("Documents");
        long work = _h.Folder("Work", docs);
        _h.File("Report-2026.pdf", work);
        _h.Folder("Reports", docs);
        _h.File("report-draft.iso", uploading: true);
        _h.File("report.txt", email: H.OtherEmail);
        _h.File("notes.txt");

        List<FileManagerItemResponse> results = await _h.Sut.SearchAsync(H.Email, "REPORT", TestContext.Current.CancellationToken);

        Assert.Equal(["Reports", "Report-2026.pdf"], results.Select(r => r.Name));
        Assert.Equal(["Documents"], results[0].Location);
        Assert.Equal(["Documents", "Work"], results[1].Location);
    }

    /// <summary>A blank query finds nothing.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public async Task SearchAsync_FindsNothing_ForABlankQuery(string? query)
    {
        _h.File("a.txt");

        Assert.Empty(await _h.Sut.SearchAsync(H.Email, query, TestContext.Current.CancellationToken));
    }

    // -- ListFilesForDownloadAsync -------------------------------------------------

    /// <summary>A selected file is listed as itself, a selected folder as every finished file under it at any depth — once each.</summary>
    [Fact]
    public async Task ListFilesForDownloadAsync_ExpandsFoldersIntoTheirFiles()
    {
        long docs = _h.Folder("Documents");
        long work = _h.Folder("Work", docs);
        long a = _h.File("a.txt", docs);
        long b = _h.File("b.txt", work);
        _h.File("half.iso", work, uploading: true);
        long c = _h.File("c.txt");

        var (result, files) = await _h.Sut.ListFilesForDownloadAsync(H.Email, [docs, c, a], TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal([a, b, c], files.Select(f => f.Id).Order());
    }

    /// <summary>A selection naming an item the account does not own is "not found" as a whole.</summary>
    [Fact]
    public async Task ListFilesForDownloadAsync_AnswersNotFound_ForAnotherAccountsItem()
    {
        long mine = _h.File("a.txt");
        long theirs = _h.File("b.txt", email: H.OtherEmail);

        var (result, files) = await _h.Sut.ListFilesForDownloadAsync(H.Email, [mine, theirs], TestContext.Current.CancellationToken);

        Assert.Equal(ServiceErrorType.NotFound, result.ErrorType);
        Assert.Empty(files);
    }
}
