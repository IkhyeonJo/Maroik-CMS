using Maroik.Core.Domain.Account;
using Maroik.Core.PostgreSQL.Data;
using Maroik.Core.PostgreSQL.Models;
using Maroik.Website.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Maroik.Website.Tests.Controllers;

/// <summary>
/// Integration tests for the free-forum PAGES of <c>ForumController.FreeForum</c> — the list (paging,
/// search, pinned posts, locked-post visibility), the post detail (who may read a locked post, the view
/// counter, comments, attachment metadata) and the edit page (owner only). The JSON write/delete actions
/// are covered by <c>ForumControllerBoardTests</c> / <c>ForumControllerWriteEditTests</c>; here the posts
/// are seeded straight into PostgreSQL and read back through the real HTTP pipeline.
/// </summary>
[Collection("Website Integration")]
public class ForumControllerPageTests(MaroikWebApplicationFactory factory)
{
    /// <summary>Board type of the free forum.</summary>
    private const string FreeForumType = "FreeForum";

    /// <summary>Client for the shared test host; redirects are not followed so they can be asserted.</summary>
    private readonly HttpClient _client = factory.CreateTestClient(followRedirects: false);

    /// <summary>Seeds (if missing) an account for <paramref name="email"/> with <paramref name="role"/> and signs in as it.</summary>
    private Task<AuthenticatedSession> LoginAsync(string email, string role = Role.User) =>
        AuthenticatedSessionHelper.LoginAsync(factory, _client, email, "UserPassword1!", role, TestContext.Current.CancellationToken);

    /// <summary>Inserts a free-forum post by <paramref name="writer"/> directly into the database and returns its id.</summary>
    private long SeedBoard(string writer, string title, string content = "Body", bool locked = false, bool noticed = false, bool deleted = false, DateTime? created = null)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var board = new Board
        {
            Type = FreeForumType, Title = title, Content = content, Writer = writer,
            Locked = locked, Noticed = noticed, Deleted = deleted, View = 0,
            Created = created ?? DateTime.UtcNow, Updated = created ?? DateTime.UtcNow
        };
        db.Boards.Add(board);
        db.SaveChanges();
        return board.Id;
    }

    /// <summary>Inserts a comment by <paramref name="writer"/> on board <paramref name="boardId"/>.</summary>
    private void SeedComment(long boardId, string writer, string content, long order = 0)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.BoardComments.Add(new BoardComment
        {
            BoardId = boardId, Writer = writer, AvatarImagePath = "/upload/Management/Profile/default-avatar.jpg",
            Content = content, Deleted = false, Order = order, Created = DateTime.UtcNow
        });
        db.SaveChanges();
    }

    /// <summary>The stored view count of board <paramref name="boardId"/>.</summary>
    private long ViewCount(long boardId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return db.Boards.AsNoTracking().Single(b => b.Id == boardId).View;
    }

    /// <summary>Sends a GET to <paramref name="url"/>, with the session cookie when <paramref name="session"/> is given.</summary>
    private async Task<HttpResponseMessage> GetAsync(string url, AuthenticatedSession? session = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (session != null) request.Headers.Add("Cookie", session.CookieHeader);
        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    /// <summary>URL of the free-forum detail page for post <paramref name="id"/>.</summary>
    private static string Detail(long id) => $"/Forum/FreeForum?method=detail&boardId={id}";
    /// <summary>URL of the free-forum edit page for post <paramref name="id"/>.</summary>
    private static string Edit(long id) => $"/Forum/FreeForum?method=edit&boardId={id}";

    /// <summary>The response body as a string.</summary>
    private static async Task<string> BodyAsync(HttpResponseMessage response) =>
        await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

    // -- Detail ------------------------------------------------------------------------------------

    /// <summary>
    /// On a post's detail page an administrator's comment is marked (bold green name), a reader sees a delete link on their own
    /// comment — and an administrator on every comment — but a plain reader not on someone else's.
    /// </summary>
    [Fact]
    public async Task Detail_Comments_MarkAnAdministratorsName_AndOfferDeleteToTheirOwnerOrAnAdmin()
    {
        var admin = await LoginAsync("forum-page-cmt-admin@test.com", Role.Admin);
        var owner = await LoginAsync("forum-page-cmt-owner@test.com");
        var other = await LoginAsync("forum-page-cmt-other@test.com");
        long id = SeedBoard(owner.Nickname, "CommentStyles");
        SeedComment(id, admin.Nickname, "from-the-admin", order: 1);
        SeedComment(id, owner.Nickname, "from-the-owner", order: 2);

        string ownerHtml = await BodyAsync(await GetAsync(Detail(id), owner));
        string adminHtml = await BodyAsync(await GetAsync(Detail(id), admin));
        string otherHtml = await BodyAsync(await GetAsync(Detail(id), other));

        Assert.Contains("font-weight:bold; color:green;", ownerHtml);   // the administrator's name stands out
        Assert.Equal(1, DeletableComments(ownerHtml));                  // only the owner's own comment
        Assert.Equal(2, DeletableComments(adminHtml));                  // an admin may delete both
        Assert.Equal(0, DeletableComments(otherHtml));                  // a bystander gets none
        return;

 #pragma warning disable SYSLIB1045
        // Number of distinct comments the page offers a delete link for.
        static int DeletableComments(string html) => System.Text.RegularExpressions.Regex
 #pragma warning restore SYSLIB1045
            .Matches(html, "aFreeForumDeleteComment\" href=\"#\" data-commentId=\"(\\d+)\"").Select(m => m.Groups[1].Value).Distinct().Count();
    }


    /// <summary>A public post renders for an anonymous visitor with its title, content and comments, and counts the view.</summary>
    [Fact]
    public async Task Detail_PublicPost_RendersTitleContentAndComments_AndCountsTheView()
    {
        long id = SeedBoard("forum-page-writer1", "PublicTitle-1", content: "<p>PublicBody-1</p>");
        SeedComment(id, "forum-page-commenter1", "CommentText-1");

        var response = await GetAsync(Detail(id));
        string html = await BodyAsync(response);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("PublicTitle-1", html);
        Assert.Contains("PublicBody-1", html);
        Assert.Contains("CommentText-1", html);
        Assert.Equal(1, ViewCount(id));
    }

    /// <summary>A locked post is not readable by a stranger (redirect to the list, no view counted) but is by its author and by an admin.</summary>
    [Fact]
    public async Task Detail_LockedPost_IsHiddenFromStrangers_ButReadableByTheAuthorAndAnAdmin()
    {
        var author = await LoginAsync("forum-page-author2@test.com");
        var stranger = await LoginAsync("forum-page-stranger2@test.com");
        var admin = await LoginAsync("forum-page-admin2@test.com", Role.Admin);
        long id = SeedBoard(author.Nickname, "LockedTitle-2", content: "<p>LockedBody-2</p>", locked: true);

        var anonymousResponse = await GetAsync(Detail(id));
        var strangerResponse = await GetAsync(Detail(id), stranger);
        Assert.Equal(System.Net.HttpStatusCode.Redirect, anonymousResponse.StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.Redirect, strangerResponse.StatusCode);
        Assert.Equal(0, ViewCount(id));

        var authorResponse = await GetAsync(Detail(id), author);
        var adminResponse = await GetAsync(Detail(id), admin);
        Assert.Equal(System.Net.HttpStatusCode.OK, authorResponse.StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.OK, adminResponse.StatusCode);
        Assert.Contains("LockedBody-2", await BodyAsync(authorResponse));
    }

    /// <summary>A soft-deleted post cannot be opened by anyone; the visitor is sent back to the list.</summary>
    [Fact]
    public async Task Detail_DeletedPost_RedirectsToTheList()
    {
        long id = SeedBoard("forum-page-writer3", "DeletedTitle-3", deleted: true);

        var response = await GetAsync(Detail(id));

        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("FreeForum", response.Headers.Location!.ToString());
    }

    /// <summary>The attachment's name is shown on the detail page even when the stored file itself cannot be fetched.</summary>
    [Fact]
    public async Task Detail_ShowsTheAttachmentName_EvenWhenTheStoredFileCannotBeFetched()
    {
        long id = SeedBoard("forum-page-writer4", "AttachTitle-4");
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.BoardAttachedFiles.Add(new BoardAttachedFile { BoardId = id, Size = 12, Name = "quarterly-report-4", Extension = ".zip", Path = "upload/Forum/FreeForum/boardAttachedFiles/none/absent.zip" });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var response = await GetAsync(Detail(id));

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("quarterly-report-4", await BodyAsync(response));
    }

    // -- Edit ---------------------------------------------------------------------------------------

    /// <summary>Only the author reaches the edit page; anonymous visitors and even admins are sent back to the list.</summary>
    [Fact]
    public async Task Edit_IsAvailableOnlyToTheAuthor()
    {
        var author = await LoginAsync("forum-page-author5@test.com");
        var other = await LoginAsync("forum-page-other5@test.com");
        var admin = await LoginAsync("forum-page-admin5@test.com", Role.Admin);
        long id = SeedBoard(author.Nickname, "EditTitle-5", content: "<p>EditBody-5</p>");

        var authorResponse = await GetAsync(Edit(id), author);
        Assert.Equal(System.Net.HttpStatusCode.OK, authorResponse.StatusCode);
        string html = await BodyAsync(authorResponse);
        Assert.Contains("EditTitle-5", html);

        foreach (var session in new[] { null, other, admin })
        {
            var response = await GetAsync(Edit(id), session);
            Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
        }
    }

    /// <summary>Editing a missing or deleted post, or one without a board id, goes back to the list.</summary>
    [Fact]
    public async Task Edit_MissingOrDeletedPost_RedirectsToTheList()
    {
        var author = await LoginAsync("forum-page-author6@test.com");
        long deleted = SeedBoard(author.Nickname, "GoneTitle-6", deleted: true);

        Assert.Equal(System.Net.HttpStatusCode.Redirect, (await GetAsync(Edit(deleted), author)).StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.Redirect, (await GetAsync(Edit(999_999_999), author)).StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.Redirect, (await GetAsync("/Forum/FreeForum?method=edit", author)).StatusCode);
    }

    /// <summary>The write page opens for a signed-in user.</summary>
    [Fact]
    public async Task Write_LoggedInUser_Returns200()
    {
        var user = await LoginAsync("forum-page-writer7@test.com");

        var response = await GetAsync("/Forum/FreeForum?method=write", user);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    // -- List -----------------------------------------------------------------------------------------

    /// <summary>The list shows a writer's posts, hides their locked ones from strangers and shows them to the writer, pinned posts included.</summary>
    [Fact]
    public async Task List_HidesLockedPostsFromStrangers_AndShowsThemToTheirWriter()
    {
        var writer = await LoginAsync("forum-page-writer8@test.com");
        SeedBoard(writer.Nickname, "OpenPost-8");
        SeedBoard(writer.Nickname, "LockedPost-8", locked: true);
        string url = $"/Forum/FreeForum?searchType=Writer&searchText={Uri.EscapeDataString(writer.Nickname)}";

        string anonymousHtml = await BodyAsync(await GetAsync(url));
        string writerHtml = await BodyAsync(await GetAsync(url, writer));

        Assert.Contains("OpenPost-8", anonymousHtml);
        Assert.DoesNotContain("LockedPost-8", anonymousHtml);
        Assert.Contains("OpenPost-8", writerHtml);
        Assert.Contains("LockedPost-8", writerHtml);
    }

    /// <summary>Five posts per page, newest first: the sixth-newest post appears only on page 2, and page 0 behaves like page 1.</summary>
    [Fact]
    public async Task List_PagesFivePostsAtATime_NewestFirst()
    {
        const string writer = "forum-page-pager9";
        DateTime start = DateTime.UtcNow.AddMinutes(-30);
        for (int i = 1; i <= 7; i++)
            SeedBoard(writer, $"PagedPost-9-{i}", created: start.AddMinutes(i));
        const string url = $"/Forum/FreeForum?searchType=Writer&searchText={writer}";

        string page1 = await BodyAsync(await GetAsync(url + "&page=1"));
        string page2 = await BodyAsync(await GetAsync(url + "&page=2"));
        string page0 = await BodyAsync(await GetAsync(url + "&page=0"));

        Assert.Contains("PagedPost-9-7", page1);
        Assert.Contains("PagedPost-9-3", page1);
        Assert.DoesNotContain("PagedPost-9-2", page1);
        Assert.Contains("PagedPost-9-2", page2);
        Assert.Contains("PagedPost-9-1", page2);
        Assert.DoesNotContain("PagedPost-9-7", page2);
        Assert.Contains("PagedPost-9-7", page0);
    }

    /// <summary>A title search narrows the list to matching posts (the search text is a literal, not a wildcard).</summary>
    [Fact]
    public async Task List_TitleSearch_ReturnsOnlyMatchingPosts()
    {
        SeedBoard("forum-page-writer10", "Zebra-Match-10");
        SeedBoard("forum-page-writer10", "Other-Title-10");

        string html = await BodyAsync(await GetAsync("/Forum/FreeForum?searchType=Title&searchText=Zebra-Match-10"));

        Assert.Contains("Zebra-Match-10", html);
        Assert.DoesNotContain("Other-Title-10", html);
    }

    /// <summary>A pinned (noticed) post appears on every page of the list, and its comment count is shown.</summary>
    [Fact]
    public async Task List_ShowsPinnedPosts_WithTheirCommentCount()
    {
        long pinned = SeedBoard("forum-page-writer11", "PinnedTitle-11", noticed: true);
        SeedComment(pinned, "forum-page-commenter11", "c1");
        SeedComment(pinned, "forum-page-commenter11", "c2", 1);

        string html = await BodyAsync(await GetAsync("/Forum/FreeForum?searchType=Writer&searchText=nobody-matches-11"));

        Assert.Contains("PinnedTitle-11", html);
    }
}
