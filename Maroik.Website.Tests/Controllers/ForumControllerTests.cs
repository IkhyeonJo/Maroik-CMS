using System.Text.RegularExpressions;
using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Domain.Account;
using Maroik.Core.PostgreSQL.Data;
using Maroik.Core.PostgreSQL.Models;
using Maroik.Website.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Maroik.Website.Tests.Controllers;

/// <summary>
/// Integration smoke tests for <c>ForumController</c>.
/// The free-forum list view uses <c>GetAccount() ?? anonymous</c> so it works
/// without a session and must respond 200 OK.
/// </summary>
[Collection("Website Integration")]
public class ForumControllerTests(MaroikWebApplicationFactory factory)
{
    /// <summary>Client for the shared test host; redirects are not followed so they can be asserted.</summary>
    private readonly HttpClient _client = factory.CreateTestClient(followRedirects: false);
    
    // -- FreeForum list -------------------------------------------------------

    /// <summary>Verifies that <c>FreeForum</c> default list when returns200.</summary>
    [Fact]
    public async Task FreeForum_DefaultList_Returns200()
    {
        var response = await _client.GetAsync("/Forum/FreeForum", TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>Verifies that <c>FreeForum</c> list with title search when returns200.</summary>
    [Fact]
    public async Task FreeForum_ListWithTitleSearch_Returns200()
    {
        var response = await _client.GetAsync("/Forum/FreeForum?method=list&searchType=Title&searchText=hello", TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>Verifies that <c>FreeForum</c> list with writer search when returns200.</summary>
    [Fact]
    public async Task FreeForum_ListWithWriterSearch_Returns200()
    {
        var response = await _client.GetAsync("/Forum/FreeForum?method=list&searchType=Writer&searchText=alice", TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    // -- FreeForum write (no session) -----------------------------------------

    /// <summary>Verifies that <c>FreeForum</c> write mode without session when redirects to log in.</summary>
    [Fact]
    public async Task FreeForum_WriteModeWithoutSession_RedirectsToLogin()
    {
        var response = await _client.GetAsync("/Forum/FreeForum?method=write", TestContext.Current.CancellationToken);

        // Without a session, the controller redirects to Account/Login
        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("Login", response.Headers.Location?.ToString() ?? "");
    }

    // -- FreeForum detail (nonexistent board) ---------------------------------

    /// <summary>Verifies that <c>FreeForum</c> detail mode with missing board when redirects to list.</summary>
    [Fact]
    public async Task FreeForum_DetailModeWithMissingBoard_RedirectsToList()
    {
        var response = await _client.GetAsync("/Forum/FreeForum?method=detail&boardId=99999", TestContext.Current.CancellationToken);

        // Board does not exist → controller redirects back to the list
        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
    }

    /// <summary>Verifies that <c>FreeForum</c> detail mode with null board id when redirects to list.</summary>
    [Fact]
    public async Task FreeForum_DetailModeWithNullBoardId_RedirectsToList()
    {
        var response = await _client.GetAsync("/Forum/FreeForum?method=detail", TestContext.Current.CancellationToken);

        // boardId is null → RedirectToAction("FreeForum", "Forum")
        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
    }

    // -- IsBoardExists (IDOR regression) --------------------------------------

    /// <summary>
    /// Verifies the fix for an IDOR: <c>IsBoardExists</c> used to return a locked board's full
    /// title/content to ANY logged-in user, skipping the writer/admin-only check that the normal
    /// GET detail view enforces for <c>Locked</c> posts.
    /// </summary>
    [Fact]
    public async Task IsBoardExists_ForLockedBoardOwnedByAnotherUser_DoesNotLeakContent()
    {
        const string writerEmail = "forum-writer@example.com";
        const string writerNickname = "LockedWriter";
        const string otherEmail = "forum-other@example.com";
        const string otherNickname = "OtherReader";
        const string password = "TestPassword1!";
        const string secretContent = "TOP-SECRET-LOCKED-BOARD-CONTENT";

        long boardId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var passwordService = scope.ServiceProvider.GetRequiredService<IPasswordService>();

            db.Accounts.AddRange(
                NewAccount(writerEmail, writerNickname, passwordService.HashPassword(password)),
                NewAccount(otherEmail, otherNickname, passwordService.HashPassword(password)));

            var board = new Board
            {
                Type = "FreeForum",
                Title = "Locked post",
                Content = secretContent,
                Writer = writerNickname,
                Created = DateTime.UtcNow,
                Updated = DateTime.UtcNow,
                View = 0,
                Deleted = false,
                Locked = true
            };
            db.Boards.Add(board);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            boardId = board.Id;
        }

        var (sessionCookie, antiForgeryCookie, antiForgeryToken) = await LoginAsync(otherEmail, password);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/Forum/IsBoardExists");
        request.Headers.Add("Cookie", $"{SessionCookieName}={sessionCookie}; {AntiForgeryCookieName}={antiForgeryCookie}");
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["id"] = boardId.ToString(),
            ["__RequestVerificationToken"] = antiForgeryToken
        });
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.DoesNotContain(secretContent, body);
    }

    // -- WriteFreeComment -----------------------------------------------------------

    /// <summary>Write free comment valid payload persists comment.</summary>
    [Fact]
    public async Task WriteFreeComment_ValidPayload_PersistsComment()
    {
        long boardId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var board = new Board
            {
                Type = "FreeForum",
                Title = "Commentable post",
                Content = "Body",
                Writer = "SomeoneElse",
                Created = DateTime.UtcNow,
                Updated = DateTime.UtcNow,
                View = 0,
                Deleted = false,
                Locked = false
            };
            TestAccounts.EnsureNickname(db, board.Writer);
            db.Boards.Add(board);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            boardId = board.Id;
        }

        var session = await AuthenticatedSessionHelper.LoginAsync(factory, _client, "forum-writecomment-user@test.com", "UserPassword1!", Role.User, TestContext.Current.CancellationToken);
        using var request = session.BuildJsonPostRequest("/Forum/WriteFreeComment", new { BoardId = boardId, Content = "Nice post!", DetailCurrentPage = 1 });

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("\"result\":true", json);

        using var verifyScope = factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Contains(verifyDb.BoardComments, c => c.BoardId == boardId && c.Writer == session.Nickname && c.Content == "Nice post!");
    }

    /// <summary>Write free comment empty content returns failure result.</summary>
    [Fact]
    public async Task WriteFreeComment_EmptyContent_ReturnsFailureResult()
    {
        var session = await AuthenticatedSessionHelper.LoginAsync(factory, _client, "forum-writecomment-empty@test.com", "UserPassword1!", Role.User, TestContext.Current.CancellationToken);
        using var request = session.BuildJsonPostRequest("/Forum/WriteFreeComment", new { BoardId = 1, Content = "", DetailCurrentPage = 1 });

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"result\":false", json);
    }

    /// <summary>Write free comment anonymous session is forbidden.</summary>
    [Fact]
    public async Task WriteFreeComment_AnonymousSession_IsForbidden()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/Forum/WriteFreeComment");
        request.Content = System.Net.Http.Json.JsonContent.Create(new { BoardId = 1, Content = "x", DetailCurrentPage = 1 });

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.NotEqual(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    // -- UploadImageFile: validation only ------------------------------------------------
    // Success requires a live Maroik.FileStorage instance (AttachmentContentService.
    // UploadSummernoteImageAsync calls out to IFileClient with no fallback), which isn't
    // available in this test environment — only the pre-upload validation branches are covered.

    /// <summary>Upload image file no file attached returns failure result.</summary>
    [Fact]
    public async Task UploadImageFile_NoFileAttached_ReturnsFailureResult()
    {
        var session = await AuthenticatedSessionHelper.LoginAsync(factory, _client, "forum-uploadimage-nofile@test.com", "UserPassword1!", Role.User, TestContext.Current.CancellationToken);
        var form = new MultipartFormDataContent();
        using var request = session.BuildFormPostRequest("/Forum/UploadImageFile", form);

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"result\":false", json);
    }

    /// <summary>Upload image file disallowed extension returns failure result.</summary>
    [Fact]
    public async Task UploadImageFile_DisallowedExtension_ReturnsFailureResult()
    {
        var session = await AuthenticatedSessionHelper.LoginAsync(factory, _client, "forum-uploadimage-badext@test.com", "UserPassword1!", Role.User, TestContext.Current.CancellationToken);
        var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent([1, 2, 3, 4]);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
        form.Add(fileContent, "summernoteImageFile", "notanimage.txt");
        using var request = session.BuildFormPostRequest("/Forum/UploadImageFile", form);

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"result\":false", json);
    }

    // -- Test helpers -----------------------------------------------------------

    // Matches Program.cs: const cookiePrefix = "__Secure-" + SessionDefaults.CookieName / AntiforgeryOptions.DefaultCookiePrefix.
    private const string SessionCookieName = "__Secure-.AspNetCore.Session";
    /// <summary>Name prefix of the antiforgery cookie the site issues.</summary>
    private const string AntiForgeryCookieName = "__Secure-.AspNetCore.Antiforgery.";

    /// <summary>An unsaved, confirmed User account row.</summary>
    private static Maroik.Core.PostgreSQL.Models.Account NewAccount(string email, string nickname, string hashedPassword) => new()
    {
        Email = email,
        HashedPassword = hashedPassword,
        Nickname = nickname,
        AvatarImagePath = "/upload/Management/Profile/default-avatar.jpg",
        Role = Role.User,
        TimeZoneIanaId = "UTC",
        Locked = false,
        LoginAttempt = 0,
        EmailConfirmed = true,
        AgreedServiceTerms = true,
        Deleted = false,
        Created = DateTime.UtcNow,
        Updated = DateTime.UtcNow
    };

    /// <summary>Logs in via the real HTTP login flow and returns the resulting session cookie plus the antiforgery cookie/token pair (still valid for further authenticated POSTs).</summary>
    private async Task<(string sessionCookie, string antiForgeryCookie, string antiForgeryToken)> LoginAsync(string email, string password)
    {
        using var getRequest = new HttpRequestMessage(HttpMethod.Get, "/Account/Login");
        var getResponse = await _client.SendAsync(getRequest, TestContext.Current.CancellationToken);
        string html = await getResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        string antiForgeryToken = ExtractAntiForgeryToken(html);
        string? antiForgeryCookie = ExtractCookieValue(getResponse, AntiForgeryCookieName);
        Assert.False(string.IsNullOrEmpty(antiForgeryCookie));

        using var postRequest = new HttpRequestMessage(HttpMethod.Post, "/Account/Login");
        postRequest.Headers.Add("Cookie", $"{AntiForgeryCookieName}={antiForgeryCookie}");
        postRequest.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Password"] = password,
            ["__RequestVerificationToken"] = antiForgeryToken
        });
        var postResponse = await _client.SendAsync(postRequest, TestContext.Current.CancellationToken);
        string? sessionCookie = ExtractCookieValue(postResponse, SessionCookieName);
        Assert.False(string.IsNullOrEmpty(sessionCookie), "Login did not issue a session cookie.");

        return (sessionCookie, antiForgeryCookie, antiForgeryToken);
    }

    /// <summary>Extracts the value of a specific cookie from a response's Set-Cookie headers, or null if absent.</summary>
    private static string? ExtractCookieValue(HttpResponseMessage response, string cookieName)
    {
        return !response.Headers.TryGetValues("Set-Cookie", out var setCookieHeaders) ? null : (from header in setCookieHeaders select header.Split(';', 2)[0] into namePart let eq = namePart.IndexOf('=') where eq > 0 where namePart[..eq] == cookieName select namePart[(eq + 1)..]).FirstOrDefault();
    }

    /// <summary>Extracts the antiforgery hidden-input value from a rendered form's HTML.</summary>
    private static string ExtractAntiForgeryToken(string html)
    {
        Match match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(match.Success, "Could not find __RequestVerificationToken in the login page HTML.");
        return match.Groups[1].Value;
    }
}
