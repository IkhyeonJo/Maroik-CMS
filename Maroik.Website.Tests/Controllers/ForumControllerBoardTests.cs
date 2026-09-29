using Maroik.Core.Domain.Account;
using Maroik.Core.PostgreSQL.Data;
using Maroik.Core.PostgreSQL.Models;
using Maroik.Website.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Maroik.Website.Tests.Controllers;

/// <summary>
/// Integration tests for the FreeForum board/comment action group of <c>ForumController</c>
/// (all <c>[RequiredHttpPostAccess]</c>-gated for both Admin and User).
///
/// <c>WriteFreeBoard</c>/<c>EditFreeBoard</c> are not covered here: <c>BoardService</c> wraps
/// both in an <c>IUnitOfWork</c> transaction, which the EF Core InMemory provider used by this
/// test factory cannot execute (same limitation as <c>AccountBookController</c>'s
/// <c>UpdateAsset</c> — see <c>AccountBookControllerAssetTests</c>). <c>IsBoardExists</c>,
/// <c>DeleteBoard</c>, and <c>DeleteComment</c> use plain repository calls with no transaction,
/// so those are covered here instead, including the owner-vs-non-owner permission check.
///
/// <see cref="AuthenticatedSessionHelper.LoginAsync"/> derives each seeded account's nickname
/// from its email (real Postgres enforces a unique index on Nickname), so board/comment
/// ownership tests seed <c>Writer = session.Nickname</c> (owner) or a fixed different string
/// (non-owner) rather than a hardcoded nickname.
/// </summary>
[Collection("Website Integration")]
public class ForumControllerBoardTests(MaroikWebApplicationFactory factory)
{
    /// <summary>Board type of the free forum.</summary>
    private const string FreeForumType = "FreeForum";

    /// <summary>Client for the shared test host; redirects are not followed so they can be asserted.</summary>
    private readonly HttpClient _client = factory.CreateTestClient(followRedirects: false);
    
    /// <summary>Seeds (if missing) a User account for <paramref name="email"/> and signs in as it.</summary>
    private Task<AuthenticatedSession> LoginAsUserAsync(string email = "forum-user@test.com") =>
        AuthenticatedSessionHelper.LoginAsync(factory, _client, email, "UserPassword1!", Role.User, TestContext.Current.CancellationToken);

    /// <summary>Seeds (if missing) an Admin account for <paramref name="email"/> and signs in as it.</summary>
    private Task<AuthenticatedSession> LoginAsAdminAsync(string email = "forum-admin@test.com") =>
        AuthenticatedSessionHelper.LoginAsync(factory, _client, email, "AdminPassword1!", Role.Admin, TestContext.Current.CancellationToken);

    /// <summary>Inserts a free-forum post by <paramref name="writer"/> directly into the database and returns its id.</summary>
    private long SeedBoard(string writer, bool locked = false, bool deleted = false)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var board = new Board
        {
            Type = FreeForumType,
            Title = "Test Post",
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

    /// <summary>Inserts a comment by <paramref name="writer"/> on board <paramref name="boardId"/> and returns its id.</summary>
    private long SeedComment(long boardId, string writer)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var comment = new BoardComment
        {
            BoardId = boardId,
            Writer = writer,
            AvatarImagePath = "/upload/Management/Profile/default-avatar.jpg",
            Content = "A comment",
            Deleted = false,
            Order = 0,
            Created = DateTime.UtcNow
        };
        db.BoardComments.Add(comment);
        db.SaveChanges();
        return comment.Id;
    }

    // -- IsBoardExists ------------------------------------------------------------

    /// <summary>Is board exists active unlocked board returns success result.</summary>
    [Fact]
    public async Task IsBoardExists_ActiveUnlockedBoard_ReturnsSuccessResult()
    {
        var session = await LoginAsUserAsync();
        long boardId = SeedBoard(writer: "SomeoneElse");

        using var request = session.BuildJsonPostRequest($"/Forum/IsBoardExists?id={boardId}");
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"result\":true", json);
    }

    /// <summary>Is board exists unknown id returns failure result.</summary>
    [Fact]
    public async Task IsBoardExists_UnknownId_ReturnsFailureResult()
    {
        var session = await LoginAsUserAsync();
        using var request = session.BuildJsonPostRequest("/Forum/IsBoardExists?id=999999");

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"result\":false", json);
    }

    /// <summary>Is board exists deleted board returns failure result.</summary>
    [Fact]
    public async Task IsBoardExists_DeletedBoard_ReturnsFailureResult()
    {
        var session = await LoginAsUserAsync();
        long boardId = SeedBoard(writer: "SomeoneElse", deleted: true);

        using var request = session.BuildJsonPostRequest($"/Forum/IsBoardExists?id={boardId}");
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"result\":false", json);
    }

    /// <summary>Is board exists locked board non owner non admin returns failure result.</summary>
    [Fact]
    public async Task IsBoardExists_LockedBoard_NonOwnerNonAdmin_ReturnsFailureResult()
    {
        var session = await LoginAsUserAsync();
        long boardId = SeedBoard(writer: "SomeoneElse", locked: true);

        using var request = session.BuildJsonPostRequest($"/Forum/IsBoardExists?id={boardId}");
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"result\":false", json);
    }

    /// <summary>Is board exists locked board owner returns success result.</summary>
    [Fact]
    public async Task IsBoardExists_LockedBoard_Owner_ReturnsSuccessResult()
    {
        var session = await LoginAsUserAsync();
        long boardId = SeedBoard(writer: session.Nickname, locked: true);

        using var request = session.BuildJsonPostRequest($"/Forum/IsBoardExists?id={boardId}");
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"result\":true", json);
    }

    /// <summary>Is board exists locked board admin returns success result regardless of ownership.</summary>
    [Fact]
    public async Task IsBoardExists_LockedBoard_Admin_ReturnsSuccessResultRegardlessOfOwnership()
    {
        var session = await LoginAsAdminAsync();
        long boardId = SeedBoard(writer: "SomeoneElse", locked: true);

        using var request = session.BuildJsonPostRequest($"/Forum/IsBoardExists?id={boardId}");
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"result\":true", json);
    }

    // -- DeleteBoard ----------------------------------------------------------------

    /// <summary>Delete board owner soft deletes successfully.</summary>
    [Fact]
    public async Task DeleteBoard_Owner_SoftDeletesSuccessfully()
    {
        var session = await LoginAsUserAsync();
        long boardId = SeedBoard(writer: session.Nickname);

        using var request = session.BuildJsonPostRequest("/Forum/DeleteBoard", new { Id = boardId });
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"result\":true", json);
    }

    /// <summary>Delete board non owner non admin is denied.</summary>
    [Fact]
    public async Task DeleteBoard_NonOwnerNonAdmin_IsDenied()
    {
        var session = await LoginAsUserAsync();
        long boardId = SeedBoard(writer: "SomeoneElse");

        using var request = session.BuildJsonPostRequest("/Forum/DeleteBoard", new { Id = boardId });
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"result\":false", json);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(db.Boards.Single(b => b.Id == boardId).Deleted);
    }

    /// <summary>Delete board admin can delete anyones board.</summary>
    [Fact]
    public async Task DeleteBoard_Admin_CanDeleteAnyonesBoard()
    {
        var session = await LoginAsAdminAsync();
        long boardId = SeedBoard(writer: "SomeoneElse");

        using var request = session.BuildJsonPostRequest("/Forum/DeleteBoard", new { Id = boardId });
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"result\":true", json);
    }

    /// <summary>Delete board unknown id returns failure result.</summary>
    [Fact]
    public async Task DeleteBoard_UnknownId_ReturnsFailureResult()
    {
        var session = await LoginAsUserAsync();
        using var request = session.BuildJsonPostRequest("/Forum/DeleteBoard", new { Id = 999999 });

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"result\":false", json);
    }

    // -- DeleteComment ----------------------------------------------------------------

    /// <summary>Delete comment owner soft deletes successfully.</summary>
    [Fact]
    public async Task DeleteComment_Owner_SoftDeletesSuccessfully()
    {
        var session = await LoginAsUserAsync();
        long boardId = SeedBoard(writer: "SomeoneElse");
        long commentId = SeedComment(boardId, writer: session.Nickname);

        using var request = session.BuildJsonPostRequest($"/Forum/DeleteComment?id={commentId}");
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"result\":true", json);
    }

    /// <summary>Delete comment non owner non admin is denied.</summary>
    [Fact]
    public async Task DeleteComment_NonOwnerNonAdmin_IsDenied()
    {
        var session = await LoginAsUserAsync();
        long boardId = SeedBoard(writer: "SomeoneElse");
        long commentId = SeedComment(boardId, writer: "SomeoneElse");

        using var request = session.BuildJsonPostRequest($"/Forum/DeleteComment?id={commentId}");
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("\"result\":false", json);
    }
}
