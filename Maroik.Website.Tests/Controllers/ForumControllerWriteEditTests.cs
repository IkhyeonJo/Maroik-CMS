using Maroik.Core.Domain.Account;
using Maroik.Core.PostgreSQL.Data;
using Maroik.Website.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Maroik.Website.Tests.Controllers;

/// <summary>
/// Integration tests for <c>WriteFreeBoard</c>/<c>EditFreeBoard</c> on <c>ForumController</c> —
/// the multipart-form counterparts to the JSON-bodied actions covered in
/// <c>ForumControllerBoardTests</c>. Runs against a real PostgreSQL instance (via
/// <see cref="MaroikWebApplicationFactory"/>'s Testcontainers setup), so
/// <c>BoardService.WriteBoardAsync</c>/<c>EditBoardAsync</c>'s <c>IUnitOfWork</c> transactions —
/// unusable under EF Core InMemory (see <c>AccountBookControllerAssetTests</c>) — are exercised
/// for real on the success paths, not just the ModelState validation branch.
/// </summary>
[Collection("Website Integration")]
public class ForumControllerWriteEditTests(MaroikWebApplicationFactory factory)
{
    /// <summary>Client for the shared test host; redirects are not followed so they can be asserted.</summary>
    private readonly HttpClient _client = factory.CreateTestClient(followRedirects: false);
    
    /// <summary>Seeds (if missing) a User account for <paramref name="email"/> and signs in as it.</summary>
    private Task<AuthenticatedSession> LoginAsUserAsync(string email = "forum-writeedit-user@test.com") =>
        AuthenticatedSessionHelper.LoginAsync(factory, _client, email, "UserPassword1!", Role.User, TestContext.Current.CancellationToken);

    /// <summary>A write/edit-post form (the title is omitted when <see langword="null"/>).</summary>
    private static MultipartFormDataContent BoardForm(string? title, string content = "Body", bool locked = false, bool noticed = false)
    {
        var form = new MultipartFormDataContent();
        if (title != null) form.Add(new StringContent(title), "Title");
        form.Add(new StringContent(content), "Content");
        form.Add(new StringContent(locked.ToString()), "Locked");
        form.Add(new StringContent(noticed.ToString()), "Noticed");
        return form;
    }

    /// <summary>A title unique to this call, prefixed with the calling test's name.</summary>
    private static string UniqueTitle([System.Runtime.CompilerServices.CallerMemberName] string testName = "") =>
        $"{testName}-{Guid.NewGuid():N}";

    /// <summary>Write free board anonymous session is forbidden.</summary>
    [Fact]
    public async Task WriteFreeBoard_AnonymousSession_IsForbidden()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/Forum/WriteFreeBoard");
        request.Content = BoardForm("Hello");

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.NotEqual(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>Write free board missing title returns input invalid.</summary>
    [Fact]
    public async Task WriteFreeBoard_MissingTitle_ReturnsInputInvalid()
    {
        var session = await LoginAsUserAsync();
        using var request = session.BuildFormPostRequest("/Forum/WriteFreeBoard", BoardForm(null));

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"result\":false", json);
    }

    /// <summary>Write free board title too long returns input invalid.</summary>
    [Fact]
    public async Task WriteFreeBoard_TitleTooLong_ReturnsInputInvalid()
    {
        var session = await LoginAsUserAsync();
        using var request = session.BuildFormPostRequest("/Forum/WriteFreeBoard", BoardForm(new string('a', 101)));

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"result\":false", json);
    }

    /// <summary>Edit free board missing title returns input invalid.</summary>
    [Fact]
    public async Task EditFreeBoard_MissingTitle_ReturnsInputInvalid()
    {
        var session = await LoginAsUserAsync();
        var form = BoardForm(null);
        form.Add(new StringContent("1"), "Id");
        using var request = session.BuildFormPostRequest("/Forum/EditFreeBoard", form);

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"result\":false", json);
    }

    // -- WriteFreeBoard: success path ---------------------------------------------

    /// <summary>Write free board valid payload persists board.</summary>
    [Fact]
    public async Task WriteFreeBoard_ValidPayload_PersistsBoard()
    {
        var session = await LoginAsUserAsync();
        string title = UniqueTitle();
        using var request = session.BuildFormPostRequest("/Forum/WriteFreeBoard", BoardForm(title, content: "Hello world", locked: true));

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("\"result\":true", json);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var created = db.Boards.Single(b => b.Title == title);
        Assert.Equal(session.Nickname, created.Writer);
        Assert.Contains("Hello world", created.Content);
        Assert.True(created.Locked);
        // Noticed is only honored for Admin writers, ignored here since this session is Role.User.
        Assert.False(created.Noticed);
    }

    /// <summary>Write free board noticed requested by non admin is ignored.</summary>
    [Fact]
    public async Task WriteFreeBoard_NoticedRequestedByNonAdmin_IsIgnored()
    {
        var session = await LoginAsUserAsync();
        string title = UniqueTitle();
        using var request = session.BuildFormPostRequest("/Forum/WriteFreeBoard", BoardForm(title, noticed: true));

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("\"result\":true", json);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var created = db.Boards.Single(b => b.Title == title);
        Assert.False(created.Noticed);
    }

    // -- EditFreeBoard: success path / ownership -----------------------------------

    /// <summary>Edit free board by owner updates board.</summary>
    [Fact]
    public async Task EditFreeBoard_ByOwner_UpdatesBoard()
    {
        var session = await LoginAsUserAsync();
        string title = UniqueTitle();
        using var createRequest = session.BuildFormPostRequest("/Forum/WriteFreeBoard", BoardForm(title, content: "Original"));
        await _client.SendAsync(createRequest, TestContext.Current.CancellationToken);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var created = db.Boards.Single(b => b.Title == title);

        string updatedTitle = UniqueTitle();
        var editForm = BoardForm(updatedTitle, content: "Edited", locked: true);
        editForm.Add(new StringContent(created.Id.ToString()), "Id");
        using var editRequest = session.BuildFormPostRequest("/Forum/EditFreeBoard", editForm);

        var response = await _client.SendAsync(editRequest, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("\"result\":true", json);

        using var verifyScope = factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var updated = verifyDb.Boards.Single(b => b.Id == created.Id);
        Assert.Equal(updatedTitle, updated.Title);
        Assert.Contains("Edited", updated.Content);
        Assert.True(updated.Locked);
    }

    /// <summary>Edit free board by non owner returns failure result.</summary>
    [Fact]
    public async Task EditFreeBoard_ByNonOwner_ReturnsFailureResult()
    {
        var session = await LoginAsUserAsync();
        long boardId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var board = new Maroik.Core.PostgreSQL.Models.Board
            {
                Type = "FreeForum",
                Title = "Someone Else's Post",
                Content = "Body",
                Writer = "SomeoneElse",
                Locked = false,
                Noticed = false,
                Deleted = false,
                View = 0,
                Created = DateTime.UtcNow,
                Updated = DateTime.UtcNow
            };
            TestAccounts.EnsureNickname(db, board.Writer);
            db.Boards.Add(board);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            boardId = board.Id;
        }

        var editForm = BoardForm(UniqueTitle());
        editForm.Add(new StringContent(boardId.ToString()), "Id");
        using var editRequest = session.BuildFormPostRequest("/Forum/EditFreeBoard", editForm);

        var response = await _client.SendAsync(editRequest, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"result\":false", json);
    }
}
