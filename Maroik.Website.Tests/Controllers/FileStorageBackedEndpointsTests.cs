using System.Net.Http.Headers;
using System.Text.Json;
using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Contract.Misc.Enums;
using Maroik.Core.Domain.Account;
using Maroik.Core.PostgreSQL.Data;
using Maroik.Core.PostgreSQL.Models;
using Maroik.Website.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Maroik.Website.Tests.Controllers;

/// <summary>
/// The endpoints that talk to the file-storage service — avatar upload, the editor's inline-image upload,
/// post / event attachments and their download on the detail / edit pages — run against an in-memory
/// <see cref="FakeFileClient"/> in a derived host (sharing the same PostgreSQL database), so the success paths
/// and every storage / virus-scan outcome are reachable without a real FileStorage or ClamAV.
/// </summary>
[Collection("Website Integration")]
public class FileStorageBackedEndpointsTests(MaroikWebApplicationFactory factory)
{
    /// <summary>A valid 1×1 PNG.</summary>
    private static readonly byte[] _png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    /// <summary>A derived test host whose file client is <see cref="Files"/>, with its HTTP client.</summary>
    private sealed record Host(WebApplicationFactory<Program> Factory, HttpClient Client, FakeFileClient Files) : IDisposable
    {
        /// <summary>Disposes the derived host (and with it the client it created).</summary>
        public void Dispose() => Factory.Dispose();
    }

    /// <summary>A host whose <c>IFileClient</c> is a fresh in-memory fake.</summary>
    private Host CreateHost()
    {
        var files = new FakeFileClient();
        var factory1 = factory.WithWebHostBuilder(b => b.ConfigureTestServices(services =>
        {
            services.RemoveAll<IFileClient>();
            services.AddSingleton<IFileClient>(files);
        }));
        var client = factory1.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://www.localhost/"), AllowAutoRedirect = false, HandleCookies = false
        });
        return new Host(factory1, client, files);
    }

    /// <summary>Seeds (if missing) an account for <paramref name="email"/> with <paramref name="role"/> and signs in as it on <paramref name="host"/>.</summary>
    private static Task<AuthenticatedSession> LoginAsync(Host host, string email, string role = Role.User) =>
        AuthenticatedSessionHelper.LoginAsync(host.Factory, host.Client, email, "UserPassword1!", role, TestContext.Current.CancellationToken);

    /// <summary>A multipart form with one file part (<paramref name="field"/>, typed <paramref name="contentType"/>) plus text fields.</summary>
    private static MultipartFormDataContent FileForm(string field, string fileName, byte[] bytes, string contentType, params (string Name, string Value)[] fields)
    {
        var form = new MultipartFormDataContent();
        var part = new ByteArrayContent(bytes);
        part.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(part, field, fileName);
        foreach (var (name, value) in fields) form.Add(new StringContent(value), name);
        return form;
    }

    /// <summary>Sends <paramref name="request"/>, asserts 200, and parses the JSON body.</summary>
    private static async Task<JsonDocument> PostAsync(Host host, HttpRequestMessage request)
    {
        var response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>The <c>errorMessage</c> of a JSON result.</summary>
    private static string Error(JsonDocument doc) => doc.RootElement.GetProperty("errorMessage").GetString()!;

    // -- avatar ---------------------------------------------------------------------------------------

    /// <summary>A real PNG is stored under the avatar folder with its MIME type and becomes the account's avatar.</summary>
    [Fact]
    public async Task UpdateProfileAvatar_StoresAValidPng_AndPointsTheAccountAtIt()
    {
        using var host = CreateHost();
        var session = await LoginAsync(host, "storage-avatar-ok@test.com");
        using var request = session.BuildFormPostRequest("/Management/UpdateProfileAvatar", FileForm("ProfileAvatarFiles", "me.png", _png, "image/png"));

        using JsonDocument doc = await PostAsync(host, request);

        Assert.True(doc.RootElement.GetProperty("result").GetBoolean());
        string stored = Assert.Single(host.Files.UploadedPaths);
        Assert.StartsWith("upload/Management/Profile/Avatar/", stored);
        Assert.EndsWith(".png", stored);
        Assert.Equal("image/png", host.Files.ContentTypeOf(stored));
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal("/" + stored, await db.Accounts.AsNoTracking().Where(a => a.Email == "storage-avatar-ok@test.com").Select(a => a.AvatarImagePath).SingleAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>Each way an avatar can be refused is reported with its own message, and nothing is stored.</summary>
    [Fact]
    public async Task UpdateProfileAvatar_ReportsEachRefusalWithItsOwnMessage()
    {
        using var baseHost = CreateHost();
        var session = await LoginAsync(baseHost, "storage-avatar-bad@test.com");

        Assert.Equal("Please attach a file", await Send(new MultipartFormDataContent { { new StringContent("x"), "Nickname" } }, baseHost));
        Assert.Equal("Please attach a file", await Send([], baseHost)); // no fields at all: the model is not even bound
        Assert.Equal("Only .jpg or jpeg or .png file allowed", await Send(FileForm("ProfileAvatarFiles", "me.gif", _png, "image/gif"), baseHost));
        Assert.Equal("Invalid image file", await Send(FileForm("ProfileAvatarFiles", "me.png", [.. "definitely not an image"u8], "image/png"), baseHost));
        Assert.Equal("SVG format is not allowed", await Send(FileForm("ProfileAvatarFiles", "me.png", [.. "<svg xmlns=\"http://www.w3.org/2000/svg\"/>"u8], "image/png"), baseHost));

        baseHost.Files.Outcome = FileUploadResult.Infected;
        Assert.Equal("File may be infected with a virus.", await Send(FileForm("ProfileAvatarFiles", "me.png", _png, "image/png"), baseHost));
        baseHost.Files.Outcome = FileUploadResult.ScanUnavailable;
        Assert.Equal("The file could not be scanned for viruses. Please try again later.", await Send(FileForm("ProfileAvatarFiles", "me.png", _png, "image/png"), baseHost));
        baseHost.Files.Outcome = FileUploadResult.Failed;
        Assert.Equal("Input is invalid", await Send(FileForm("ProfileAvatarFiles", "me.png", _png, "image/png"), baseHost));

        Assert.Empty(baseHost.Files.UploadedPaths);
        return;

        // Posts the avatar form, asserts the result is a failure, and returns its error message.
        async Task<string> Send(MultipartFormDataContent form, Host host)
        {
            using var request = session.BuildFormPostRequest("/Management/UpdateProfileAvatar", form);
            using JsonDocument doc = await PostAsync(host, request);
            Assert.False(doc.RootElement.GetProperty("result").GetBoolean());
            return Error(doc);
        }
    }

    // -- the editor's inline images --------------------------------------------------------------------

    /// <summary>Each editor endpoint stores a real PNG under its own area and returns the bytes plus an encrypted (not raw) storage path.</summary>
    [Theory]
    [InlineData("/Forum/UploadImageFile", "upload/Forum/FreeForum/summernote/images/", Role.User)]
    [InlineData("/Management/UploadImageFile", "upload/Management/PrivateNote/summernote/images/", Role.User)]
    [InlineData("/Calendar/UploadImageFile", "upload/Calendar/UserIndex/summernote/images/", Role.User)]
    [InlineData("/Calendar/UploadImageFile", "upload/Calendar/AdminIndex/summernote/images/", Role.Admin)]
    public async Task UploadImageFile_StoresARealImage_AndReturnsItsBytesWithAnEncryptedPath(string url, string expectedFolder, string role)
    {
        using var host = CreateHost();
        var session = await LoginAsync(host, $"storage-editor-{Math.Abs(HashCode.Combine(url, role))}@test.com", role);
        using var request = session.BuildFormPostRequest(url, FileForm("summernoteImageFile", "pic.png", _png, "image/png"));

        using JsonDocument doc = await PostAsync(host, request);

        Assert.True(doc.RootElement.GetProperty("result").GetBoolean());
        string stored = Assert.Single(host.Files.UploadedPaths);
        Assert.StartsWith(expectedFolder, stored);
        var file = doc.RootElement.GetProperty("file");
        Assert.Equal(_png, Convert.FromBase64String(file.GetProperty("fileContents").GetString()!));
        Assert.Equal("image/png", file.GetProperty("contentType").GetString());
        string token = doc.RootElement.GetProperty("filePath").GetString()!;
        Assert.NotEmpty(token);
        Assert.DoesNotContain("upload/", token); // the client only ever sees an opaque RSA token
    }

    /// <summary>A file that is not a real image, or a failed storage write, is refused and reported without a path.</summary>
    [Fact]
    public async Task UploadImageFile_RefusesFakeImages_AndStorageFailures()
    {
        using var host = CreateHost();
        var session = await LoginAsync(host, "storage-editor-bad@test.com");

        using var fake = session.BuildFormPostRequest("/Forum/UploadImageFile", FileForm("summernoteImageFile", "pic.png", [.. "not an image"u8], "image/png"));
        using JsonDocument fakeDoc = await PostAsync(host, fake);
        Assert.False(fakeDoc.RootElement.GetProperty("result").GetBoolean());

        host.Files.Outcome = FileUploadResult.Failed;
        using var failing = session.BuildFormPostRequest("/Forum/UploadImageFile", FileForm("summernoteImageFile", "pic.png", _png, "image/png"));
        using JsonDocument failDoc = await PostAsync(host, failing);
        Assert.False(failDoc.RootElement.GetProperty("result").GetBoolean());
        Assert.False(failDoc.RootElement.TryGetProperty("filePath", out _));
    }

    // -- attachments -------------------------------------------------------------------------------------

    /// <summary>Write with a .zip attachment uploads it under the post's folder and records its metadata; a failed upload persists nothing.</summary>
    [Fact]
    public async Task WriteFreeBoard_WithAnAttachment_UploadsItAndRecordsIt()
    {
        using var host = CreateHost();
        var session = await LoginAsync(host, "storage-attach-write@test.com");
        string title = "AttachedPost-" + Guid.NewGuid().ToString("N")[..8];
        byte[] zip = [0x50, 0x4B, 3, 4, 1, 2, 3];
        using var request = session.BuildFormPostRequest("/Forum/WriteFreeBoard",
            FileForm("UploadedFile", "docs.zip", zip, "application/zip", ("Title", title), ("Content", "body"), ("Locked", "false"), ("Noticed", "false")));

        using JsonDocument doc = await PostAsync(host, request);

        Assert.True(doc.RootElement.GetProperty("result").GetBoolean());
        string stored = Assert.Single(host.Files.UploadedPaths);
        Assert.Contains("/boardAttachedFiles/", stored);
        Assert.EndsWith(".zip", stored);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        BoardAttachedFile row = await db.BoardAttachedFiles.AsNoTracking()
            .Join(db.Boards, f => f.BoardId, b => b.Id, (f, b) => new { f, b.Title }).Where(x => x.Title == title).Select(x => x.f).SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal("docs", row.Name);
        Assert.Equal(".zip", row.Extension);
        Assert.Equal(zip.Length, row.Size);
        Assert.Equal(stored, row.Path);

        host.Files.Outcome = FileUploadResult.Failed;
        string title2 = title + "-2";
        using var failing = session.BuildFormPostRequest("/Forum/WriteFreeBoard",
            FileForm("UploadedFile", "docs.zip", zip, "application/zip", ("Title", title2), ("Content", "body"), ("Locked", "false"), ("Noticed", "false")));
        using JsonDocument failDoc = await PostAsync(host, failing);
        Assert.False(failDoc.RootElement.GetProperty("result").GetBoolean());
        Assert.False(await db.Boards.AsNoTracking().AnyAsync(b => b.Title == title2, TestContext.Current.CancellationToken));
    }

    /// <summary>An attachment that is not a .zip is refused before anything is uploaded.</summary>
    [Fact]
    public async Task WriteFreeBoard_RefusesANonZipAttachment()
    {
        using var host = CreateHost();
        var session = await LoginAsync(host, "storage-attach-exe@test.com");
        using var request = session.BuildFormPostRequest("/Forum/WriteFreeBoard",
            FileForm("UploadedFile", "run.exe", [1, 2, 3], "application/octet-stream", ("Title", "x-" + Guid.NewGuid().ToString("N")[..8]), ("Content", "b"), ("Locked", "false"), ("Noticed", "false")));

        using JsonDocument doc = await PostAsync(host, request);

        Assert.False(doc.RootElement.GetProperty("result").GetBoolean());
        Assert.Empty(host.Files.UploadedPaths);
    }

    /// <summary>Adds a post of <paramref name="type"/> by <paramref name="writer"/> (optionally locked) with an attachment "payload.zip" stored at <paramref name="path"/>; returns its id.</summary>
    private async Task<long> SeedPostWithAttachmentAsync(string type, string writer, string path, long size, bool locked = false)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var board = new Board { Type = type, Title = "WithFile", Content = "b", Writer = writer, Locked = locked, Noticed = false, Deleted = false, View = 0, Created = DateTime.UtcNow, Updated = DateTime.UtcNow };
        db.Boards.Add(board);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.BoardAttachedFiles.Add(new BoardAttachedFile { BoardId = board.Id, Size = size, Name = "payload", Extension = ".zip", Path = path });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return board.Id;
    }

    /// <summary>
    /// The detail and edit pages show the attachment's name and a download link for it, but no longer
    /// fetch the file itself: nothing is read from file storage and no file bytes are embedded.
    /// </summary>
    [Theory]
    [InlineData("FreeForum", "/Forum/FreeForum?method={0}&boardId={1}")]
    [InlineData("PrivateNote", "/Management/PrivateNote?method={0}&boardId={1}")]
    public async Task DetailAndEditPages_LinkTheAttachment_WithoutFetchingIt(string type, string urlFormat)
    {
        using var host = CreateHost();
        var session = await LoginAsync(host, $"storage-attach-read-{type.ToLowerInvariant()}@test.com");
        string path = $"upload/{type}/attachments/{Guid.NewGuid():N}.zip";
        byte[] bytes = [.. "attachment-payload-bytes"u8];
        host.Files.Seed(path, bytes);
        long boardId = await SeedPostWithAttachmentAsync(type, session.Nickname, path, bytes.Length);

        foreach (string method in new[] { "detail", "edit" })
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, string.Format(urlFormat, method, boardId));
            request.Headers.Add("Cookie", session.CookieHeader);
            var response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);
            string html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

            Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("payload.zip", html);
            Assert.Contains($"data-boardid=\"{boardId}\"", html);
            Assert.DoesNotContain(Convert.ToBase64String(bytes), html);
        }
        Assert.DoesNotContain(path, host.Files.FetchedPaths);
    }

    /// <summary>The download action streams the stored attachment to a viewer of the post, named for the browser.</summary>
    [Theory]
    [InlineData("FreeForum", "/Forum/DownloadFreeBoardAttachedFile")]
    [InlineData("PrivateNote", "/Management/DownloadPrivateNoteAttachedFile")]
    public async Task DownloadAction_StreamsTheAttachment_ToAViewerOfThePost(string type, string url)
    {
        using var host = CreateHost();
        var session = await LoginAsync(host, $"storage-attach-dl-{type.ToLowerInvariant()}@test.com");
        string path = $"upload/{type}/attachments/{Guid.NewGuid():N}.zip";
        byte[] bytes = [.. "attachment-payload-bytes"u8];
        host.Files.Seed(path, bytes);
        long boardId = await SeedPostWithAttachmentAsync(type, session.Nickname, path, bytes.Length);

        using var request = session.BuildFormPostRequest(url, new MultipartFormDataContent { { new StringContent(boardId.ToString()), "boardId" } });
        var response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("payload.zip", response.Content.Headers.ContentDisposition?.FileNameStar ?? response.Content.Headers.ContentDisposition?.FileName);
        Assert.Equal(bytes, await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>An anonymous visitor may download the attachment of an unlocked forum post (the forum is in the anonymous menu).</summary>
    [Fact]
    public async Task DownloadFreeBoardAttachedFile_IsAllowedForAnAnonymousVisitor_OnAnUnlockedPost()
    {
        using var host = CreateHost();
        var author = await LoginAsync(host, "storage-attach-anon-author@test.com");
        string path = $"upload/FreeForum/attachments/{Guid.NewGuid():N}.zip";
        host.Files.Seed(path, [1, 2, 3]);
        long boardId = await SeedPostWithAttachmentAsync("FreeForum", author.Nickname, path, 3);
        var anonymous = await AuthenticatedSessionHelper.AnonymousAsync(host.Client, TestContext.Current.CancellationToken);

        using var request = anonymous.BuildFormPostRequest("/Forum/DownloadFreeBoardAttachedFile", new MultipartFormDataContent { { new StringContent(boardId.ToString()), "boardId" } });
        var response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Equal([1, 2, 3], await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// The post's visibility is checked again on download: another account cannot fetch a private
    /// note's attachment, nor an anonymous visitor a locked post's; nothing is read from storage.
    /// </summary>
    [Fact]
    public async Task DownloadAction_RefusesAViewerWhoMayNotSeeThePost()
    {
        using var host = CreateHost();
        var owner = await LoginAsync(host, "storage-attach-owner@test.com");
        var stranger = await LoginAsync(host, "storage-attach-stranger@test.com");
        string notePath = $"upload/PrivateNote/attachments/{Guid.NewGuid():N}.zip";
        string lockedPath = $"upload/FreeForum/attachments/{Guid.NewGuid():N}.zip";
        host.Files.Seed(notePath, [1]);
        host.Files.Seed(lockedPath, [2]);
        long noteId = await SeedPostWithAttachmentAsync("PrivateNote", owner.Nickname, notePath, 1);
        long lockedId = await SeedPostWithAttachmentAsync("FreeForum", owner.Nickname, lockedPath, 1, locked: true);
        var anonymous = await AuthenticatedSessionHelper.AnonymousAsync(host.Client, TestContext.Current.CancellationToken);

        using var noteRequest = stranger.BuildFormPostRequest("/Management/DownloadPrivateNoteAttachedFile", new MultipartFormDataContent { { new StringContent(noteId.ToString()), "boardId" } });
        using JsonDocument noteDoc = await PostAsync(host, noteRequest);
        using var lockedRequest = anonymous.BuildFormPostRequest("/Forum/DownloadFreeBoardAttachedFile", new MultipartFormDataContent { { new StringContent(lockedId.ToString()), "boardId" } });
        using JsonDocument lockedDoc = await PostAsync(host, lockedRequest);

        foreach (JsonDocument doc in new[] { noteDoc, lockedDoc })
        {
            Assert.False(doc.RootElement.GetProperty("result").GetBoolean());
            Assert.Equal("The post could not be found.", doc.RootElement.GetProperty("error").GetString());
        }
        Assert.Empty(host.Files.FetchedPaths);
    }

    /// <summary>A stored attachment that file storage cannot hand out is reported as a failed download, not served.</summary>
    [Fact]
    public async Task DownloadAction_ReportsAFailure_WhenStorageCannotHandOutTheFile()
    {
        using var host = CreateHost();
        var session = await LoginAsync(host, "storage-attach-down@test.com");
        host.Files.ThrowOnDownload = true;
        long boardId = await SeedPostWithAttachmentAsync("FreeForum", session.Nickname, $"upload/FreeForum/attachments/{Guid.NewGuid():N}.zip", 10);

        using var request = session.BuildFormPostRequest("/Forum/DownloadFreeBoardAttachedFile", new MultipartFormDataContent { { new StringContent(boardId.ToString()), "boardId" } });
        using JsonDocument doc = await PostAsync(host, request);

        Assert.False(doc.RootElement.GetProperty("result").GetBoolean());
        Assert.Equal("Input is invalid", doc.RootElement.GetProperty("error").GetString());
    }

    /// <summary>An avatar / editor image over the configured size limit is refused with the size message, and nothing is stored.</summary>
    [Theory]
    [InlineData("/Management/UpdateProfileAvatar", "ProfileAvatarFiles")]
    [InlineData("/Forum/UploadImageFile", "summernoteImageFile")]
    [InlineData("/Management/UploadImageFile", "summernoteImageFile")]
    public async Task AnUpload_OverTheSizeLimit_IsRefusedWithTheSizeMessage(string url, string field)
    {
        using var host = CreateHost();
        var session = await LoginAsync(host, $"storage-toobig-{url.Replace("/", "-").ToLowerInvariant()}@test.com");
        long limit = factory.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<Core.Contract.Misc.Settings.ServerSetting>>().Value.MaxAttachedFileSizeBytes;
        var form = FileForm(field, "big.png", new byte[limit + 1], "image/png", ("Nickname", "x"));

        using var request = session.BuildFormPostRequest(url, form);
        using JsonDocument doc = await PostAsync(host, request);

        Assert.False(doc.RootElement.GetProperty("result").GetBoolean());
        Assert.Contains("File Size must be smaller than", Error(doc));
        Assert.Empty(host.Files.UploadedPaths);
    }

    /// <summary>The private-note editor's image endpoint refuses a request with no file.</summary>
    [Fact]
    public async Task ManagementUploadImageFile_WithoutAFile_AsksForOne()
    {
        using var host = CreateHost();
        var session = await LoginAsync(host, "storage-nofile-note@test.com");

        using var request = session.BuildFormPostRequest("/Management/UploadImageFile", new MultipartFormDataContent { { new StringContent("x"), "unrelated" } });
        using JsonDocument doc = await PostAsync(host, request);

        Assert.False(doc.RootElement.GetProperty("result").GetBoolean());
        Assert.Contains("Please attach a file", Error(doc));
    }
}
