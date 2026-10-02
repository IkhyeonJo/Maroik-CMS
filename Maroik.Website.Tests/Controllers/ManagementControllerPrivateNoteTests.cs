using System.Text.Json;
using Maroik.Core.Domain.Account;
using Maroik.Core.PostgreSQL.Data;
using Maroik.Core.PostgreSQL.Models;
using Maroik.Website.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Maroik.Website.Tests.Controllers;

/// <summary>
/// Integration tests for the PrivateNote action group of <c>ManagementController</c> —
/// <c>WritePrivateNoteBoard</c>/<c>EditPrivateNoteBoard</c>/<c>DeleteBoard</c>/
/// <c>DeleteComment</c>/<c>IsBoardExists</c>/<c>WritePrivateNoteComment</c> — previously entirely
/// uncovered. Mirrors <c>ForumControllerBoardTests</c>/<c>ForumControllerWriteEditTests</c>, with
/// one behavioral difference worth calling out: unlike Forum's <c>IsBoardExists</c> (any
/// logged-in user may check any non-deleted board), PrivateNote's <c>IsBoardExists</c> only
/// returns <c>true</c> when the caller is the board's own writer — a private note isn't visible
/// to anyone else, including via this existence check.
/// </summary>
[Collection("Website Integration")]
public class ManagementControllerPrivateNoteTests(MaroikWebApplicationFactory factory)
{
    /// <summary>Board type of the private note.</summary>
    private const string PrivateNoteType = "PrivateNote";

    /// <summary>Client for the shared test host; redirects are not followed so they can be asserted.</summary>
    private readonly HttpClient _client = factory.CreateTestClient(followRedirects: false);
    
    /// <summary>Seeds (if missing) a User account for <paramref name="email"/> and signs in as it.</summary>
    private Task<AuthenticatedSession> LoginAsUserAsync(string email = "management-privatenote-user@test.com") =>
        AuthenticatedSessionHelper.LoginAsync(factory, _client, email, "UserPassword1!", Role.User, TestContext.Current.CancellationToken);

    /// <summary>A write/edit-note form (the title is omitted when <see langword="null"/>).</summary>
    private static MultipartFormDataContent BoardForm(string? title, string content = "Body", bool noticed = false)
    {
        var form = new MultipartFormDataContent();
        if (title != null) form.Add(new StringContent(title), "Title");
        form.Add(new StringContent(content), "Content");
        form.Add(new StringContent(noticed.ToString()), "Noticed");
        return form;
    }

    /// <summary>A title unique to this call, prefixed with the calling test's name.</summary>
    private static string UniqueTitle([System.Runtime.CompilerServices.CallerMemberName] string testName = "") =>
        $"{testName}-{Guid.NewGuid():N}";

    /// <summary>Inserts a private note by <paramref name="writer"/> directly into the database and returns its id.</summary>
    private long SeedBoard(string writer, bool locked = false, bool deleted = false)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var board = new Board
        {
            Type = PrivateNoteType,
            Title = "Seed Note",
            Content = "Body",
            Writer = writer,
            Locked = locked,
            Noticed = false,
            Deleted = deleted,
            View = 0,
            Created = DateTime.UtcNow,
            Updated = DateTime.UtcNow
        };
        db.Boards.Add(board);
        db.SaveChanges();
        return board.Id;
    }

    // -- WritePrivateNoteBoard: success path ----------------------------------------

    /// <summary>Write private note board valid payload persists board.</summary>
    [Fact]
    public async Task WritePrivateNoteBoard_ValidPayload_PersistsBoard()
    {
        var session = await LoginAsUserAsync();
        string title = UniqueTitle();
        using var request = session.BuildFormPostRequest("/Management/WritePrivateNoteBoard", BoardForm(title, content: "My private thoughts"));

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("\"result\":true", json);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var created = db.Boards.Single(b => b.Title == title);
        Assert.Equal(PrivateNoteType, created.Type);
        Assert.Equal(session.Nickname, created.Writer);
        Assert.Contains("My private thoughts", created.Content);
    }

    /// <summary>Write private note board missing title returns input invalid.</summary>
    [Fact]
    public async Task WritePrivateNoteBoard_MissingTitle_ReturnsInputInvalid()
    {
        var session = await LoginAsUserAsync();
        using var request = session.BuildFormPostRequest("/Management/WritePrivateNoteBoard", BoardForm(null));

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"result\":false", json);
    }

    // -- IsBoardExists: owner-only visibility ----------------------------------------

    /// <summary>Is board exists own board returns success result.</summary>
    [Fact]
    public async Task IsBoardExists_OwnBoard_ReturnsSuccessResult()
    {
        var session = await LoginAsUserAsync();
        long boardId = SeedBoard(writer: session.Nickname);

        using var request = session.BuildJsonPostRequest($"/Management/IsBoardExists?id={boardId}");
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"result\":true", json);
    }

    /// <summary>Is board exists others board returns failure result.</summary>
    [Fact]
    public async Task IsBoardExists_OthersBoard_ReturnsFailureResult()
    {
        var session = await LoginAsUserAsync();
        long boardId = SeedBoard(writer: "SomeoneElse");

        using var request = session.BuildJsonPostRequest($"/Management/IsBoardExists?id={boardId}");
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Unlike Forum's IsBoardExists, a PrivateNote is only visible to its own writer.
        Assert.Contains("\"result\":false", json);
    }

    /// <summary>
    /// Verifies a successful reply carries only the id the delete flow needs — never the stored
    /// content, whose <c>&lt;img alt&gt;</c> holds the decrypted plain storage path.
    /// </summary>
    [Fact]
    public async Task IsBoardExists_OwnBoard_RepliesWithOnlyTheBoardId()
    {
        var session = await LoginAsUserAsync();
        long boardId = SeedBoard(writer: session.Nickname);

        using var request = session.BuildJsonPostRequest($"/Management/IsBoardExists?id={boardId}");
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.GetProperty("result").GetBoolean());
        JsonElement board = doc.RootElement.GetProperty("privateNoteBoard");
        Assert.Equal(["id"], board.EnumerateObject().Select(p => p.Name));
        Assert.Equal(boardId, board.GetProperty("id").GetInt64());
        Assert.DoesNotContain("content", json, StringComparison.OrdinalIgnoreCase);
    }

    // -- EditPrivateNoteBoard ------------------------------------------------------------

    /// <summary>Edit private note board by owner updates board.</summary>
    [Fact]
    public async Task EditPrivateNoteBoard_ByOwner_UpdatesBoard()
    {
        var session = await LoginAsUserAsync();
        long boardId = SeedBoard(writer: session.Nickname);

        string updatedTitle = UniqueTitle();
        var form = BoardForm(updatedTitle, content: "Edited");
        form.Add(new StringContent(boardId.ToString()), "Id");
        using var request = session.BuildFormPostRequest("/Management/EditPrivateNoteBoard", form);

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("\"result\":true", json);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var updated = db.Boards.Single(b => b.Id == boardId);
        Assert.Equal(updatedTitle, updated.Title);
        Assert.Contains("Edited", updated.Content);
    }

    /// <summary>Edit private note board by non owner returns failure result.</summary>
    [Fact]
    public async Task EditPrivateNoteBoard_ByNonOwner_ReturnsFailureResult()
    {
        var session = await LoginAsUserAsync();
        long boardId = SeedBoard(writer: "SomeoneElse");

        var form = BoardForm(UniqueTitle());
        form.Add(new StringContent(boardId.ToString()), "Id");
        using var request = session.BuildFormPostRequest("/Management/EditPrivateNoteBoard", form);

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"result\":false", json);
    }

    // -- DeleteBoard ----------------------------------------------------------------

    /// <summary>Delete board owner soft deletes successfully.</summary>
    [Fact]
    public async Task DeleteBoard_Owner_SoftDeletesSuccessfully()
    {
        var session = await LoginAsUserAsync();
        long boardId = SeedBoard(writer: session.Nickname);

        using var request = session.BuildJsonPostRequest("/Management/DeleteBoard", new { Id = boardId });
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"result\":true", json);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.True(db.Boards.Single(b => b.Id == boardId).Deleted);
    }

    /// <summary>Delete board non owner non admin is denied.</summary>
    [Fact]
    public async Task DeleteBoard_NonOwnerNonAdmin_IsDenied()
    {
        var session = await LoginAsUserAsync();
        long boardId = SeedBoard(writer: "SomeoneElse");

        using var request = session.BuildJsonPostRequest("/Management/DeleteBoard", new { Id = boardId });
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"result\":false", json);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(db.Boards.Single(b => b.Id == boardId).Deleted);
    }

    // -- WritePrivateNoteComment / DeleteComment ----------------------------------------

    /// <summary>Write private note comment then delete comment by owner succeeds.</summary>
    [Fact]
    public async Task WritePrivateNoteComment_ThenDeleteComment_ByOwner_Succeeds()
    {
        var session = await LoginAsUserAsync();
        long boardId = SeedBoard(writer: session.Nickname);

        using var writeRequest = session.BuildJsonPostRequest("/Management/WritePrivateNoteComment",
            new { BoardId = boardId, Content = "A comment", DetailCurrentPage = 1 });
        var writeResponse = await _client.SendAsync(writeRequest, TestContext.Current.CancellationToken);
        string writeJson = await writeResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("\"result\":true", writeJson);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var comment = db.BoardComments.Single(c => c.BoardId == boardId && c.Writer == session.Nickname);

        using var deleteRequest = session.BuildJsonPostRequest($"/Management/DeleteComment?id={comment.Id}");
        var deleteResponse = await _client.SendAsync(deleteRequest, TestContext.Current.CancellationToken);
        string deleteJson = await deleteResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"result\":true", deleteJson);
    }

    /// <summary>Write private note comment empty content returns failure result.</summary>
    [Fact]
    public async Task WritePrivateNoteComment_EmptyContent_ReturnsFailureResult()
    {
        var session = await LoginAsUserAsync();
        long boardId = SeedBoard(writer: session.Nickname);

        using var request = session.BuildJsonPostRequest("/Management/WritePrivateNoteComment",
            new { BoardId = boardId, Content = "", DetailCurrentPage = 1 });
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"result\":false", json);
    }
    // -- PrivateNote detail page ------------------------------------------------------

    /// <summary>Inserts a comment by <paramref name="writer"/> on board <paramref name="boardId"/>.</summary>
    private void SeedComment(long boardId, string writer, string content)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.BoardComments.Add(new BoardComment
        {
            BoardId = boardId,
            Order = 0,
            AvatarImagePath = "/upload/Management/Profile/default-avatar.jpg",
            Writer = writer,
            Content = content,
            Created = DateTime.UtcNow,
            Deleted = false
        });
        db.SaveChanges();
    }

    /// <summary>
    /// Regression: a note WITHOUT an attached file used to return its detail view before the comments
    /// (and the viewer data) were loaded, so its existing comments silently never rendered.
    /// </summary>
    [Fact]
    public async Task PrivateNoteDetail_NoteWithoutAttachment_RendersItsComments()
    {
        var session = await LoginAsUserAsync("management-privatenote-detail-user@test.com");
        long boardId = SeedBoard(writer: session.Nickname);
        string commentText = $"Comment-{Guid.NewGuid():N}";
        SeedComment(boardId, session.Nickname, commentText);

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/Management/PrivateNote?method=detail&boardId={boardId}");
        request.Headers.Add("Cookie", session.CookieHeader);
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        string html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(commentText, html);
    }

    /// <summary>A private note is invisible to anyone but its author, so another user's detail request redirects away.</summary>
    [Fact]
    public async Task PrivateNoteDetail_OtherUsersNote_RedirectsToList()
    {
        var session = await LoginAsUserAsync("management-privatenote-detail-other@test.com");
        long boardId = SeedBoard(writer: "SomeoneElse_" + Guid.NewGuid().ToString("N"));

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/Management/PrivateNote?method=detail&boardId={boardId}");
        request.Headers.Add("Cookie", session.CookieHeader);
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
    }
}
