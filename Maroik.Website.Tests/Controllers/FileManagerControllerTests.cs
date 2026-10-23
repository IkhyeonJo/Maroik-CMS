using System.Text.Json;
using Maroik.Core.Contract.Enums;
using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Domain.Accounts;
using Maroik.Core.PostgreSQL.Contexts;
using Maroik.Website.Tests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Maroik.Website.Tests.Controllers;

/// <summary>
/// Integration tests for <c>FileManagerController</c> against a real PostgreSQL database, with file storage replaced by the in-memory
/// <see cref="FakeFileClient"/>: the menu page (User role only), the drive / folder / search reads, the folder writes, the chunked upload
/// end to end (quota, name conflicts, virus refusals), download and preview. Every item is scoped to the signed-in account.
/// </summary>
[Collection("Website Integration")]
public sealed class FileManagerControllerTests(MaroikWebApplicationFactory factory) : IDisposable
{
    /// <summary>One GB in bytes.</summary>
    private const long Gb = 1024L * 1024 * 1024;

    /// <summary>
    /// The in-memory file storage of <see cref="_sharedHost"/>. Its stored files and upload ids are unique per test; the one outcome a
    /// test changes (<see cref="FakeFileClient.FileManagerOutcome"/>) is set back after each test (the collection runs one test at a time).
    /// </summary>
    private static readonly FakeFileClient _files = new();

    /// <summary>The one derived host the tests of this class share (each derived host is a full start-up).</summary>
    private static WebApplicationFactory<Program>? _sharedHost;

    /// <summary>Guards the one-time creation of <see cref="_sharedHost"/>.</summary>
    private static readonly Lock _sharedHostLock = new();

    /// <summary>The host whose file client is <see cref="_files"/> (shared, so not disposed by a test).</summary>
    private WebApplicationFactory<Program> Host => SharedHost(factory);

    /// <summary>Returns <see cref="_sharedHost"/>, deriving it from <paramref name="factory"/> on first use.</summary>
    private static WebApplicationFactory<Program> SharedHost(MaroikWebApplicationFactory factory)
    {
        lock (_sharedHostLock)
        {
            return _sharedHost ??= factory.WithWebHostBuilder(b => b.ConfigureTestServices(services =>
            {
                services.RemoveAll<IFileClient>();
                services.AddSingleton<IFileClient>(_files);
            }));
        }
    }

    /// <summary>The host's client (one per test; the host stays shared).</summary>
    private HttpClient? _client;

    /// <summary>Disposes this test's client (not the shared host) and sets the file client's outcome back.</summary>
    public void Dispose()
    {
        _client?.Dispose();
        _files.FileManagerOutcome = FileUploadResult.Stored;
    }

    /// <summary>The host's client, created on first use.</summary>
    private HttpClient Client => _client ??= Host.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://www.localhost/"), AllowAutoRedirect = false, HandleCookies = false });

    /// <summary>A fresh account (unique per test) signed in as a User, with <paramref name="quotaBytes"/> of File Manager quota.</summary>
    private async Task<(AuthenticatedSession Session, string Email)> LoginAsync(long quotaBytes = 10 * Gb, [System.Runtime.CompilerServices.CallerMemberName] string test = "")
    {
        string email = $"fm-{Math.Abs(test.GetHashCode(StringComparison.Ordinal))}-{Guid.NewGuid():N}"[..40].ToLowerInvariant() + "@test.com";
        AuthenticatedSession session = await AuthenticatedSessionHelper.LoginAsync(Host, Client, email, "UserPassword1!", Role.User, TestContext.Current.CancellationToken);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Accounts.Single(a => a.Email == email).FileManagerQuotaBytes = quotaBytes;
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return (session, email);
    }

    /// <summary>POSTs a JSON body and returns the parsed reply (HTTP 200 required).</summary>
    private async Task<JsonElement> PostAsync(AuthenticatedSession session, string url, object? body = null, string? culture = null)
    {
        using var request = session.BuildJsonPostRequest(url, body);
        if (culture != null) request.Headers.AcceptLanguage.ParseAdd(culture);
        using var response = await Client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return doc.RootElement.Clone();
    }

    /// <summary>Creates a folder and returns its id.</summary>
    private async Task<long> FolderAsync(AuthenticatedSession session, string name, long? parentId = null)
    {
        JsonElement reply = await PostAsync(session, "/FileManager/CreateFolder", new { ParentId = parentId, Name = name, Merge = false });
        Assert.True(reply.GetProperty("result").GetBoolean(), reply.ToString());
        return reply.GetProperty("folderId").GetInt64();
    }

    /// <summary>Uploads <paramref name="bytes"/> as <paramref name="name"/> through begin / chunk(s) / complete; returns the completion reply.</summary>
    private async Task<JsonElement> UploadAsync(AuthenticatedSession session, string name, byte[] bytes, long? parentId = null, string conflict = "Ask", bool overwrite = false)
    {
        JsonElement begin = await PostAsync(session, "/FileManager/BeginUpload", new { ParentId = parentId, Name = name, Size = bytes.Length, Conflict = conflict });
        if (!begin.GetProperty("result").GetBoolean() || begin.GetProperty("conflict").GetBoolean()) return begin;
        string uploadId = begin.GetProperty("uploadId").GetString()!;

        if (bytes.Length > 0)
        {
            using var form = MultipartForm.Of(("UploadId", uploadId), ("Offset", "0"));
            MultipartForm.AddFile(form, "Chunk", "blob", bytes, "application/octet-stream");
            using var chunk = session.BuildFormPostRequest("/FileManager/UploadChunk", form);
            using var chunkResponse = await Client.SendAsync(chunk, TestContext.Current.CancellationToken);
            using JsonDocument chunkReply = JsonDocument.Parse(await chunkResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            Assert.Equal(bytes.Length, chunkReply.RootElement.GetProperty("length").GetInt64());
        }

        return await PostAsync(session, "/FileManager/CompleteUpload", new { UploadId = uploadId, Overwrite = overwrite });
    }

    /// <summary>The names of the items of <paramref name="folderId"/>.</summary>
    private async Task<string[]> ItemNamesAsync(AuthenticatedSession session, long? folderId = null)
    {
        JsonElement reply = await PostAsync(session, "/FileManager/GetFolder", new { FolderId = folderId });
        return [.. reply.GetProperty("folder").GetProperty("items").EnumerateArray().Select(i => i.GetProperty("name").GetString()!)];
    }

    /// <summary>The id of the item named <paramref name="name"/> in <paramref name="folderId"/>.</summary>
    private async Task<long> IdOfAsync(AuthenticatedSession session, string name, long? folderId = null)
    {
        JsonElement reply = await PostAsync(session, "/FileManager/GetFolder", new { FolderId = folderId });
        return reply.GetProperty("folder").GetProperty("items").EnumerateArray().Single(i => i.GetProperty("name").GetString() == name).GetProperty("id").GetInt64();
    }

    // -- UserIndex ------------------------------------------------------------------

    /// <summary>The menu page renders for a User, with the server's limits for the upload code and the localized root name.</summary>
    [Fact]
    public async Task UserIndex_RendersThePage()
    {
        var (session, _) = await LoginAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/FileManager/UserIndex");
        request.Headers.Add("Cookie", session.CookieHeader);

        using var response = await Client.SendAsync(request, TestContext.Current.CancellationToken);
        string html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("id=\"fileManagerTree\"", html, StringComparison.Ordinal);
        Assert.Contains($"value=\"{2147483647}\"", html, StringComparison.Ordinal);
        Assert.Contains("My Drive", html, StringComparison.Ordinal);
        Assert.Contains("/user/plugins/jstree/jstree.min.js", html, StringComparison.Ordinal);
    }

    /// <summary>An administrator is denied the page (the menu has no Admin entry).</summary>
    [Fact]
    public async Task UserIndex_DeniesAnAdministrator()
    {
        _ = Client;
        var session = await AuthenticatedSessionHelper.LoginAsync(Host, Client, "fm-admin@test.com", "UserPassword1!", Role.Admin, TestContext.Current.CancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/FileManager/UserIndex");
        request.Headers.Add("Cookie", session.CookieHeader);

        using var response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Dashboard/AnonymousIndex", response.Headers.Location?.OriginalString);
    }

    // -- Reads ----------------------------------------------------------------------

    /// <summary>The drive answers the folder tree, the root count and the usage label / bar fill computed by the server.</summary>
    [Fact]
    public async Task GetDrive_AnswersTheTreeAndTheUsage()
    {
        var (session, _) = await LoginAsync(quotaBytes: 1024);
        long docs = await FolderAsync(session, "Documents");
        await FolderAsync(session, "Work", docs);
        await UploadAsync(session, "a.bin", new byte[512]);

        JsonElement drive = (await PostAsync(session, "/FileManager/GetDrive")).GetProperty("drive");

        Assert.Equal("512 B / 1 KB", drive.GetProperty("usage").GetString());
        Assert.Equal(50, drive.GetProperty("usedPercent").GetInt32());
        Assert.Equal(2, drive.GetProperty("rootItemCount").GetInt32());
        Assert.Equal(["Documents", "Work"], drive.GetProperty("folders").EnumerateArray().Select(f => f.GetProperty("name").GetString()));
        Assert.Equal(1, drive.GetProperty("folders")[0].GetProperty("itemCount").GetInt32());
    }

    /// <summary>A folder answers its breadcrumb and items, each with the server's size label, local time, icon and preview flag.</summary>
    [Fact]
    public async Task GetFolder_AnswersTheBreadcrumbAndItems()
    {
        var (session, _) = await LoginAsync();
        long docs = await FolderAsync(session, "Documents");
        await UploadAsync(session, "photo.png", new byte[1536], docs);

        JsonElement folder = (await PostAsync(session, "/FileManager/GetFolder", new { FolderId = docs })).GetProperty("folder");

        Assert.Equal("Documents", folder.GetProperty("breadcrumb")[0].GetProperty("name").GetString());
        JsonElement item = folder.GetProperty("items")[0];
        Assert.Equal("photo.png", item.GetProperty("name").GetString());
        Assert.Equal("1.5 KB", item.GetProperty("size").GetString());
        Assert.Equal("fas fa-file-image text-info", item.GetProperty("icon").GetString());
        Assert.True(item.GetProperty("previewable").GetBoolean());
        Assert.Matches(@"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}$", item.GetProperty("modified").GetString());
    }

    /// <summary>Another account's folder is "not found".</summary>
    [Fact]
    public async Task GetFolder_AnswersNotFound_ForAnotherAccountsFolder()
    {
        var (other, _) = await LoginAsync();
        long theirs = await FolderAsync(other, "Theirs");
        var (session, _) = await LoginAsync();

        JsonElement reply = await PostAsync(session, "/FileManager/GetFolder", new { FolderId = theirs });

        Assert.False(reply.GetProperty("result").GetBoolean());
        Assert.Equal("The file or folder could not be found.", reply.GetProperty("error").GetString());
    }

    /// <summary>The search matches names across the drive and gives each result its location from "My Drive".</summary>
    [Fact]
    public async Task Search_FindsByName_WithLocations()
    {
        var (session, _) = await LoginAsync();
        long docs = await FolderAsync(session, "Documents");
        await UploadAsync(session, "Report.txt", [1], docs);

        JsonElement items = (await PostAsync(session, "/FileManager/Search", new { Query = "report" })).GetProperty("items");

        JsonElement hit = Assert.Single(items.EnumerateArray());
        Assert.Equal("My Drive / Documents", hit.GetProperty("location").GetString());
    }

    // -- Folder writes ---------------------------------------------------------------

    /// <summary>Create, rename, move and delete round trip; a taken name is refused with its name in the message.</summary>
    [Fact]
    public async Task FolderWrites_RoundTrip()
    {
        var (session, _) = await LoginAsync();
        long a = await FolderAsync(session, "A");
        long b = await FolderAsync(session, "B");

        JsonElement taken = await PostAsync(session, "/FileManager/CreateFolder", new { ParentId = (long?)null, Name = "A", Merge = false });
        JsonElement renamed = await PostAsync(session, "/FileManager/Rename", new { Id = b, Name = "C" });
        JsonElement moved = await PostAsync(session, "/FileManager/Move", new { Ids = new[] { b }, TargetFolderId = a });
        string[] inA = await ItemNamesAsync(session, a);
        JsonElement deleted = await PostAsync(session, "/FileManager/Delete", new { Ids = new[] { a } });

        Assert.Equal("A file or folder named 'A' already exists here.", taken.GetProperty("error").GetString());
        Assert.True(renamed.GetProperty("result").GetBoolean());
        Assert.True(moved.GetProperty("result").GetBoolean());
        Assert.Equal(["C"], inA);
        Assert.True(deleted.GetProperty("result").GetBoolean());
        Assert.Empty(await ItemNamesAsync(session));
    }

    /// <summary>A cycle is refused with the domain's translated reason.</summary>
    [Theory]
    [InlineData("en-US", "A folder cannot be moved into itself or one of its sub-folders.")]
    [InlineData("ko-KR", "폴더를 자기 자신이나 그 하위 폴더로 옮길 수 없습니다.")]
    public async Task Move_RefusesACycle(string culture, string expected)
    {
        var (session, _) = await LoginAsync();
        long top = await FolderAsync(session, "Top");
        long sub = await FolderAsync(session, "Sub", top);

        JsonElement reply = await PostAsync(session, "/FileManager/Move", new { Ids = new[] { top }, TargetFolderId = sub }, culture);

        Assert.Equal(expected, reply.GetProperty("error").GetString());
    }

    // -- Upload -----------------------------------------------------------------------

    /// <summary>A file uploads through begin / chunk / complete and is stored under upload/FileManager/{uploadId}.</summary>
    [Fact]
    public async Task Upload_StoresTheFile()
    {
        var (session, email) = await LoginAsync();

        JsonElement done = await UploadAsync(session, "notes.txt", [1, 2, 3]);

        Assert.True(done.GetProperty("result").GetBoolean(), done.ToString());
        Assert.Equal("The file has been uploaded.", done.GetProperty("message").GetString());
        Assert.Equal(["notes.txt"], await ItemNamesAsync(session));
        using var scope = factory.Services.CreateScope();
        string path = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().FileManagerNodes.AsNoTracking().Single(n => n.AccountEmail == email).StoragePath!;
        Assert.Equal([1, 2, 3], _files.BytesOf(path));
    }

    /// <summary>Under the default 0 GB quota the upload is refused before it starts, saying how much is missing (in Korean too).</summary>
    [Theory]
    [InlineData("en-US", "Not enough storage: 0.1 GB short.")]
    [InlineData("ko-KR", "저장 공간이 0.1GB 부족합니다.")]
    public async Task Upload_IsRefused_OverTheQuota(string culture, string expected)
    {
        var (session, _) = await LoginAsync(quotaBytes: 0);

        JsonElement check = await PostAsync(session, "/FileManager/CheckQuota", new { TotalBytes = 10 }, culture);
        JsonElement begin = await PostAsync(session, "/FileManager/BeginUpload", new { Name = "a.txt", Size = 10, Conflict = "Ask" }, culture);

        Assert.Equal(expected, check.GetProperty("error").GetString());
        Assert.Equal(expected, begin.GetProperty("error").GetString());
    }

    /// <summary>A name conflict is reported to the client; "keep both" stores "a (1).txt"; "overwrite" replaces the old file.</summary>
    [Fact]
    public async Task Upload_ResolvesANameConflict()
    {
        var (session, _) = await LoginAsync();
        await UploadAsync(session, "a.txt", [1]);

        JsonElement ask = await UploadAsync(session, "a.txt", [2]);
        await UploadAsync(session, "a.txt", [3], conflict: "KeepBoth");
        JsonElement overwrite = await UploadAsync(session, "a.txt", [4, 4], conflict: "Overwrite", overwrite: true);

        Assert.True(ask.GetProperty("conflict").GetBoolean());
        Assert.True(overwrite.GetProperty("result").GetBoolean());
        Assert.Equal(["a (1).txt", "a.txt"], await ItemNamesAsync(session));
        JsonElement items = (await PostAsync(session, "/FileManager/GetFolder", new { FolderId = (long?)null })).GetProperty("folder").GetProperty("items");
        Assert.Equal("2 B", items.EnumerateArray().Single(i => i.GetProperty("name").GetString() == "a.txt").GetProperty("size").GetString());
    }

    /// <summary>An unknown conflict choice is invalid input.</summary>
    [Fact]
    public async Task BeginUpload_RefusesAnUnknownConflictChoice()
    {
        var (session, _) = await LoginAsync();

        JsonElement reply = await PostAsync(session, "/FileManager/BeginUpload", new { Name = "a.txt", Size = 1, Conflict = "Replace" });

        Assert.Equal("Input is invalid", reply.GetProperty("error").GetString());
    }

    /// <summary>An infected file is refused and not kept.</summary>
    [Fact]
    public async Task Upload_RefusesAnInfectedFile()
    {
        var (session, _) = await LoginAsync();
        _files.FileManagerOutcome = FileUploadResult.Infected;

        JsonElement done = await UploadAsync(session, "virus.exe", [6, 6]);

        Assert.Equal("The file may be infected with a virus.", done.GetProperty("error").GetString());
        Assert.Empty(await ItemNamesAsync(session));
    }

    /// <summary>An abort releases the upload.</summary>
    [Fact]
    public async Task AbortUpload_ReleasesTheUpload()
    {
        var (session, email) = await LoginAsync();
        JsonElement begin = await PostAsync(session, "/FileManager/BeginUpload", new { Name = "a.bin", Size = 10, Conflict = "Ask" });

        JsonElement abort = await PostAsync(session, "/FileManager/AbortUpload", new { UploadId = begin.GetProperty("uploadId").GetString() });

        Assert.True(abort.GetProperty("result").GetBoolean());
        using var scope = factory.Services.CreateScope();
        Assert.False(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().FileManagerNodes.Any(n => n.AccountEmail == email));
    }

    // -- Download / preview ------------------------------------------------------------

    /// <summary>A download is the file as an octet-stream attachment under its (RFC 5987-encoded) name.</summary>
    [Fact]
    public async Task Download_SendsTheFileAsAnAttachment()
    {
        var (session, _) = await LoginAsync();
        await UploadAsync(session, "보고서 1.txt", [7, 8]);
        long id = await IdOfAsync(session, "보고서 1.txt");

        using var request = session.BuildJsonPostRequest($"/FileManager/Download?id={id}");
        using var response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal("application/octet-stream", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Equal("보고서 1.txt", response.Content.Headers.ContentDisposition?.FileNameStar);
        Assert.Equal([7, 8], await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>A preview answers the image with the Content-Type its bytes prove; a file of another type is refused as JSON.</summary>
    [Fact]
    public async Task Preview_AnswersOnlyAProvenImage()
    {
        var (session, _) = await LoginAsync();
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0];
        await UploadAsync(session, "logo.png", png);
        await UploadAsync(session, "fake.png", "<html>"u8.ToArray());
        long logo = await IdOfAsync(session, "logo.png");
        long fake = await IdOfAsync(session, "fake.png");

        using var ok = session.BuildJsonPostRequest($"/FileManager/Preview?id={logo}");
        using var okResponse = await Client.SendAsync(ok, TestContext.Current.CancellationToken);
        JsonElement refused = await PostAsync(session, $"/FileManager/Preview?id={fake}");

        Assert.Equal("image/png", okResponse.Content.Headers.ContentType?.MediaType);
        Assert.Equal(png, await okResponse.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        Assert.Equal("This file cannot be previewed.", refused.GetProperty("error").GetString());
    }

    /// <summary>A multi-download lists each selected file and every file under each selected folder.</summary>
    [Fact]
    public async Task ListDownloadFiles_ExpandsFolders()
    {
        var (session, _) = await LoginAsync();
        long docs = await FolderAsync(session, "Docs");
        await UploadAsync(session, "a.txt", [1], docs);
        await UploadAsync(session, "b.txt", [1]);
        long b = await IdOfAsync(session, "b.txt");

        JsonElement files = (await PostAsync(session, "/FileManager/ListDownloadFiles", new { Ids = new[] { docs, b } })).GetProperty("files");

        Assert.Equal(["a.txt", "b.txt"], files.EnumerateArray().Select(f => f.GetProperty("name").GetString()).Order(StringComparer.Ordinal));
        Assert.All(files.EnumerateArray(), f => Assert.True(f.GetProperty("id").GetInt64() > 0));
    }
}
