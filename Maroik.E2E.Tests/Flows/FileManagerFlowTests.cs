using Maroik.E2E.Tests.Fixtures;
using Microsoft.Playwright;

namespace Maroik.E2E.Tests.Flows;

/// <summary>
/// End-to-end tests for the File Manager (<c>/FileManager/UserIndex</c>) as a signed-in User, in a real browser against the real website
/// (file storage is the in-memory <see cref="E2EFileStorage"/>): the folder tree, creating a folder, uploading through the picker in chunks,
/// renaming, previewing an image, deleting, and the quota refusal of the default 0 GB quota.
/// </summary>
[Collection("E2E")]
public class FileManagerFlowTests(E2ESharedFixture fixture) : E2ETestBase(fixture)
{
    /// <summary>One GB in bytes.</summary>
    private const long Gb = 1024L * 1024 * 1024;

    /// <summary>The leading bytes of a PNG (enough for the server to prove the format).</summary>
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, 0x49, 0x48, 0x44, 0x52];

    /// <summary>A signed-in User with <paramref name="quotaBytes"/> of quota, on the File Manager page.</summary>
    private async Task<(IPage Page, SeededAccount Account)> OpenAsync(long quotaBytes = Gb)
    {
        var (page, account) = await NewSignedInPageAsync();
        await Db.ExecuteAsync("""UPDATE "Account" SET "FileManagerQuotaBytes" = @quota WHERE "Email" = @email""", ("quota", quotaBytes), ("email", account.Email));
        await page.WaitForURLAsync(url => url.Contains("/Dashboard/", StringComparison.Ordinal), new PageWaitForURLOptions { Timeout = 30_000 });
        await GotoAsync(page, "/FileManager/UserIndex");
        await Assertions.Expect(page.Locator("#fileManagerTree .jstree-anchor").First).ToBeVisibleAsync();
        return (page, account);
    }

    /// <summary>The tile of the item named <paramref name="name"/>.</summary>
    private static ILocator Tile(IPage page, string name) =>
        page.Locator(".file-manager-tile", new PageLocatorOptions { Has = page.Locator(".file-manager-tile-name", new PageLocatorOptions { HasTextString = name }) });

    /// <summary>The tree shows the root with its Korean name (the E2E browser is ko-KR) and an empty drive says so.</summary>
    [Fact]
    public async Task TheEmptyDrive_ShowsTheRoot_AndTheUsage()
    {
        var (page, _) = await OpenAsync();

        await Assertions.Expect(page.Locator("#fileManagerTree .jstree-anchor").First).ToContainTextAsync("내 드라이브 (0)");
        await Assertions.Expect(page.Locator("#fileManagerUsage")).ToHaveTextAsync("0 B / 1 GB");
        await Assertions.Expect(page.Locator("#fileManagerItems")).ToHaveTextAsync("이 폴더는 비어 있습니다.");
    }

    /// <summary>A new folder appears in the tree and as a tile; opening it shows it in the breadcrumb.</summary>
    [Fact]
    public async Task NewFolder_AppearsInTheTreeAndTheItems()
    {
        var (page, _) = await OpenAsync();

        await page.Locator("#btnNewFolder").ClickAsync();
        await page.Locator("#newFolderName").FillAsync("Design");
        await page.Locator("#formNewFolder input[type='submit']").ClickAsync();

        await Assertions.Expect(Tile(page, "Design")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("#fileManagerTree")).ToContainTextAsync("Design (0)");
        await Tile(page, "Design").DblClickAsync();
        await Assertions.Expect(page.Locator("#fileManagerBreadcrumb li.active")).ToHaveTextAsync("Design");
    }

    /// <summary>Picked files upload in chunks into the open folder, are listed as uploaded, and count against the storage usage.</summary>
    [Fact]
    public async Task Upload_StoresThePickedFiles()
    {
        var (page, account) = await OpenAsync();

        await page.Locator("#fileManagerFileInput").SetInputFilesAsync(
        [
            new FilePayload { Name = "notes.txt", MimeType = "text/plain", Buffer = "hello"u8.ToArray() },
            new FilePayload { Name = "logo.png", MimeType = "image/png", Buffer = Png },
        ]);

        await Assertions.Expect(page.Locator("#uploadResults li")).ToHaveCountAsync(2);
        await Assertions.Expect(page.Locator("#btnUploadClose")).ToBeEnabledAsync();
        await page.Locator("#btnUploadClose").ClickAsync();
        await Assertions.Expect(Tile(page, "notes.txt")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("#fileManagerUsage")).ToHaveTextAsync("21 B / 1 GB");
        string? path = await Db.ScalarAsync<string>("""SELECT "StoragePath" FROM "FileManagerNode" WHERE "AccountEmail" = @email AND "Name" = 'notes.txt'""", ("email", account.Email));
        Assert.Equal("hello"u8.ToArray(), Files.BytesOf(path!));
    }

    /// <summary>Under the default quota of 0 GB an upload is refused before it starts, saying how much is missing.</summary>
    [Fact]
    public async Task Upload_IsRefused_UnderTheZeroQuota()
    {
        var (page, account) = await OpenAsync(quotaBytes: 0);

        await page.Locator("#fileManagerFileInput").SetInputFilesAsync(new FilePayload { Name = "a.txt", MimeType = "text/plain", Buffer = "x"u8.ToArray() });

        await Assertions.Expect(page.Locator(".toast-error")).ToContainTextAsync("0.1GB 부족");
        Assert.Equal(0L, await Db.ScalarAsync<long>("""SELECT count(*) FROM "FileManagerNode" WHERE "AccountEmail" = @email""", ("email", account.Email)));
    }

    /// <summary>In the list view a file is renamed, previewed (images only) and deleted.</summary>
    [Fact]
    public async Task ListView_RenamesPreviewsAndDeletes()
    {
        var (page, account) = await OpenAsync();
        await page.Locator("#fileManagerFileInput").SetInputFilesAsync(new FilePayload { Name = "logo.png", MimeType = "image/png", Buffer = Png });
        await Assertions.Expect(page.Locator("#btnUploadClose")).ToBeEnabledAsync();
        await page.Locator("#btnUploadClose").ClickAsync();
        await page.Locator("#btnListView").ClickAsync();
        ILocator row = page.Locator(".file-manager-list tbody tr").First;

        await row.Locator("[data-action='rename']").ClickAsync();
        await page.Locator("#renameName").FillAsync("brand.png");
        await page.Locator("#formRename input[type='submit']").ClickAsync();
        await Assertions.Expect(row).ToContainTextAsync("brand.png");

        await row.Locator("[data-action='preview']").ClickAsync();
        await Assertions.Expect(page.Locator("#previewModal")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("#previewImage")).ToHaveAttributeAsync("src", new System.Text.RegularExpressions.Regex("^blob:"));
        await page.Locator("#previewModal .close").ClickAsync();
        await Assertions.Expect(page.Locator("#previewModal")).ToBeHiddenAsync();

        await row.Locator("[data-action='delete']").ClickAsync();
        await page.Locator("#btnConfirmDelete").ClickAsync();
        await Assertions.Expect(page.Locator(".file-manager-list")).ToHaveCountAsync(0);
        Assert.Equal(0L, await Db.ScalarAsync<long>("""SELECT count(*) FROM "FileManagerNode" WHERE "AccountEmail" = @email""", ("email", account.Email)));
    }
}
