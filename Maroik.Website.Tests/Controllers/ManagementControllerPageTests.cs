using Maroik.Core.Domain.Account;
using Maroik.Core.PostgreSQL.Data;
using Maroik.Core.PostgreSQL.Models;
using Maroik.Website.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Maroik.Website.Tests.Controllers;

/// <summary>
/// Integration tests for the PAGES of <c>ManagementController</c>: the private-note list / detail / edit
/// (a private note belongs to exactly one account — no other user and not even an admin may list, read or
/// edit it), the admin account grid (with its whole-row search) and the admin menu grid. The JSON
/// write/delete actions are covered by <c>ManagementControllerPrivateNoteTests</c> and friends; here the
/// rows are seeded straight into PostgreSQL and read back through the real HTTP pipeline.
/// </summary>
[Collection("Website Integration")]
public class ManagementControllerPageTests(MaroikWebApplicationFactory factory)
{
    /// <summary>Board type of the private note.</summary>
    private const string PrivateNoteType = "PrivateNote";

    /// <summary>Client for the shared test host; redirects are not followed so they can be asserted.</summary>
    private readonly HttpClient _client = factory.CreateTestClient(followRedirects: false);
    
    /// <summary>Seeds (if missing) an account for <paramref name="email"/> with <paramref name="role"/> and signs in as it.</summary>
    private Task<AuthenticatedSession> LoginAsync(string email, string role = Role.User) =>
        AuthenticatedSessionHelper.LoginAsync(factory, _client, email, "UserPassword1!", role, TestContext.Current.CancellationToken);

    /// <summary>Inserts a private note by <paramref name="writer"/> directly into the database and returns its id.</summary>
    private long SeedNote(string writer, string title, string content = "Body", bool deleted = false)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var board = new Board
        {
            Type = PrivateNoteType, Title = title, Content = content, Writer = writer,
            Locked = false, Noticed = false, Deleted = deleted, View = 0, Created = DateTime.UtcNow, Updated = DateTime.UtcNow
        };
        TestAccounts.EnsureNickname(db, board.Writer);
        db.Boards.Add(board);
        db.SaveChanges();
        return board.Id;
    }

    /// <summary>Inserts a comment by <paramref name="writer"/> on board <paramref name="boardId"/>.</summary>
    private void SeedComment(long boardId, string writer, string content)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        TestAccounts.EnsureNickname(db, writer);
        db.BoardComments.Add(new BoardComment
        {
            BoardId = boardId, Writer = writer, AvatarImagePath = "/upload/Management/Profile/default-avatar.jpg",
            Content = content, Deleted = false, Order = 0, Created = DateTime.UtcNow
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

    /// <summary>Sends a GET to <paramref name="url"/>, with the session cookie when given and marked as an AJAX request when <paramref name="ajax"/> is set.</summary>
    private async Task<HttpResponseMessage> GetAsync(string url, AuthenticatedSession? session, bool ajax = false)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (session != null) request.Headers.Add("Cookie", session.CookieHeader);
        if (ajax) request.Headers.Add("X-Requested-With", "XMLHttpRequest");
        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    /// <summary>The response body as a string.</summary>
    private static async Task<string> BodyAsync(HttpResponseMessage response) =>
        await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

    /// <summary>URL of the private-note detail page for note <paramref name="id"/>.</summary>
    private static string Detail(long id) => $"/Management/PrivateNote?method=detail&boardId={id}";
    /// <summary>URL of the private-note edit page for note <paramref name="id"/>.</summary>
    private static string Edit(long id) => $"/Management/PrivateNote?method=edit&boardId={id}";

    /// <summary>On a private note's detail page an administrator's comment is marked, and the note's owner gets a delete link on their own comments.</summary>
    [Fact]
    public async Task Detail_Comments_MarkAnAdministratorsName_AndOfferDeleteOnTheOwnersOwn()
    {
        var admin = await LoginAsync("mgmt-page-cmt-admin@test.com", Role.Admin);
        var owner = await LoginAsync("mgmt-page-cmt-owner@test.com");
        long id = SeedNote(owner.Nickname, "CommentStyles-note");
        SeedComment(id, admin.Nickname, "from-the-admin");
        SeedComment(id, owner.Nickname, "from-the-owner");

        string html = await BodyAsync(await GetAsync(Detail(id), owner));

        Assert.Contains("font-weight:bold;", html);
 #pragma warning disable SYSLIB1045
        int deletable = System.Text.RegularExpressions.Regex
 #pragma warning restore SYSLIB1045
            .Matches(html, "aPrivateNoteDeleteComment\" href=\"#\" data-commentId=\"(\\d+)\"").Select(m => m.Groups[1].Value).Distinct().Count();
        Assert.Equal(1, deletable);
    }

    // -- PrivateNote: list ---------------------------------------------------------------------------

    /// <summary>The list shows only the caller's own notes — another user's notes never appear, and neither do an admin's.</summary>
    [Fact]
    public async Task PrivateNoteList_ShowsOnlyTheCallersOwnNotes()
    {
        var owner = await LoginAsync("mgmt-page-owner1@test.com");
        var other = await LoginAsync("mgmt-page-other1@test.com");
        var admin = await LoginAsync("mgmt-page-admin1@test.com", Role.Admin);
        SeedNote(owner.Nickname, "OwnerNote-1");
        SeedNote(other.Nickname, "OtherNote-1");
        SeedNote(admin.Nickname, "AdminNote-1");

        string ownerHtml = await BodyAsync(await GetAsync("/Management/PrivateNote", owner));
        string adminHtml = await BodyAsync(await GetAsync("/Management/PrivateNote", admin));

        Assert.Contains("OwnerNote-1", ownerHtml);
        Assert.DoesNotContain("OtherNote-1", ownerHtml);
        Assert.DoesNotContain("AdminNote-1", ownerHtml);
        Assert.Contains("AdminNote-1", adminHtml);
        Assert.DoesNotContain("OwnerNote-1", adminHtml);
        Assert.DoesNotContain("OtherNote-1", adminHtml);
    }

    /// <summary>A private-note title search is literal (a term with an underscore or percent matches only itself) and scoped to the owner.</summary>
    [Fact]
    public async Task PrivateNoteList_SearchIsLiteral_AndScopedToTheOwner()
    {
        var owner = await LoginAsync("mgmt-page-owner2@test.com");
        SeedNote(owner.Nickname, "Budget_50%-2");
        SeedNote(owner.Nickname, "BudgetX50Y-2");

        string html = await BodyAsync(await GetAsync("/Management/PrivateNote?searchType=Title&searchText=Budget_50%25-2", owner));

        Assert.Contains("Budget_50%-2", html);
        Assert.DoesNotContain("BudgetX50Y-2", html);
    }

    // -- PrivateNote: detail --------------------------------------------------------------------------

    /// <summary>The owner reads their note (with comments) and the view is counted.</summary>
    [Fact]
    public async Task PrivateNoteDetail_Owner_RendersTheNoteAndItsComments_AndCountsTheView()
    {
        var owner = await LoginAsync("mgmt-page-owner3@test.com");
        long id = SeedNote(owner.Nickname, "SecretTitle-3", content: "<p>SecretBody-3</p>");
        SeedComment(id, owner.Nickname, "CommentText-3");

        var response = await GetAsync(Detail(id), owner);
        string html = await BodyAsync(response);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("SecretTitle-3", html);
        Assert.Contains("SecretBody-3", html);
        Assert.Contains("CommentText-3", html);
        Assert.Equal(1, ViewCount(id));
    }

    /// <summary>Nobody but the owner can read a private note: another user, an admin, and an anonymous visitor are all redirected and no view is counted.</summary>
    [Fact]
    public async Task PrivateNoteDetail_IsHiddenFromEveryoneButTheOwner_EvenAdmins()
    {
        var owner = await LoginAsync("mgmt-page-owner4@test.com");
        var other = await LoginAsync("mgmt-page-other4@test.com");
        var admin = await LoginAsync("mgmt-page-admin4@test.com", Role.Admin);
        long id = SeedNote(owner.Nickname, "SecretTitle-4", content: "<p>SecretBody-4</p>");

        foreach (var session in new[] { other, admin })
        {
            var response = await GetAsync(Detail(id), session);
            Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
        }
        Assert.Equal(0, ViewCount(id));
    }

    /// <summary>A missing or deleted note, or a detail request without an id, goes back to the list.</summary>
    [Fact]
    public async Task PrivateNoteDetail_MissingDeletedOrIDLess_RedirectsToTheList()
    {
        var owner = await LoginAsync("mgmt-page-owner5@test.com");
        long deleted = SeedNote(owner.Nickname, "GoneTitle-5", deleted: true);

        Assert.Equal(System.Net.HttpStatusCode.Redirect, (await GetAsync(Detail(deleted), owner)).StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.Redirect, (await GetAsync(Detail(999_999_999), owner)).StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.Redirect, (await GetAsync("/Management/PrivateNote?method=detail", owner)).StatusCode);
    }

    // -- PrivateNote: edit / write ---------------------------------------------------------------------

    /// <summary>Only the owner opens the edit page; anyone else (including an admin) is redirected.</summary>
    [Fact]
    public async Task PrivateNoteEdit_IsAvailableOnlyToTheOwner()
    {
        var owner = await LoginAsync("mgmt-page-owner6@test.com");
        var other = await LoginAsync("mgmt-page-other6@test.com");
        var admin = await LoginAsync("mgmt-page-admin6@test.com", Role.Admin);
        long id = SeedNote(owner.Nickname, "EditTitle-6", content: "<p>EditBody-6</p>");

        var ownerResponse = await GetAsync(Edit(id), owner);
        Assert.Equal(System.Net.HttpStatusCode.OK, ownerResponse.StatusCode);
        Assert.Contains("EditTitle-6", await BodyAsync(ownerResponse));

        foreach (var session in new[] { other, admin })
            Assert.Equal(System.Net.HttpStatusCode.Redirect, (await GetAsync(Edit(id), session)).StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.Redirect, (await GetAsync("/Management/PrivateNote?method=edit", owner)).StatusCode);
    }

    /// <summary>The write page opens for a signed-in user.</summary>
    [Fact]
    public async Task PrivateNoteWrite_LoggedInUser_Returns200()
    {
        var owner = await LoginAsync("mgmt-page-owner7@test.com");

        var response = await GetAsync("/Management/PrivateNote?method=write", owner);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    // -- Account grid (admin) ----------------------------------------------------------------------------

    /// <summary>The admin account page opens and its grid's whole-row search finds exactly the matching account (the unfiltered grid is paged, so it is asserted through the search).</summary>
    [Fact]
    public async Task AccountGrid_ListsAccounts_AndTheSearchNarrowsThem()
    {
        var admin = await LoginAsync("mgmt-page-acctadmin8@test.com", Role.Admin);
        await LoginAsync("mgmt-page-findme8@test.com");
        await LoginAsync("mgmt-page-other8@test.com");

        var page = await GetAsync("/Management/Account", admin);
        string findMe = await BodyAsync(await GetAsync("/Management/Account?wholeSearch=mgmt-page-findme8", admin, ajax: true));
        string other = await BodyAsync(await GetAsync("/Management/Account?wholeSearch=mgmt-page-other8", admin, ajax: true));

        Assert.Equal(System.Net.HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("mgmt-page-findme8@test.com", findMe);
        Assert.DoesNotContain("mgmt-page-other8@test.com", findMe);
        Assert.Contains("mgmt-page-other8@test.com", other);
        Assert.DoesNotContain("mgmt-page-findme8@test.com", other);
    }

    /// <summary>Several matching accounts are listed ordered by e-mail address.</summary>
    [Fact]
    public async Task AccountGrid_ListsSeveralMatches_OrderedByEmail()
    {
        var admin = await LoginAsync("mgmt-page-acctorder@test.com", Role.Admin);
        await LoginAsync("zz-mgmt-order9@test.com");
        await LoginAsync("mm-mgmt-order9@test.com");
        await LoginAsync("aa-mgmt-order9@test.com");

        string body = await BodyAsync(await GetAsync("/Management/Account?wholeSearch=mgmt-order9", admin, ajax: true));

        int aa = body.IndexOf("aa-mgmt-order9@test.com", StringComparison.Ordinal);
        int mm = body.IndexOf("mm-mgmt-order9@test.com", StringComparison.Ordinal);
        int zz = body.IndexOf("zz-mgmt-order9@test.com", StringComparison.Ordinal);
        Assert.True(aa >= 0 && aa < mm && mm < zz, $"expected aa < mm < zz, got {aa}, {mm}, {zz}");
    }

    // -- Menu grid (admin) ---------------------------------------------------------------------------------

    /// <summary>The admin menu page opens and its grid's whole-row search finds exactly the matching categories.</summary>
    [Fact]
    public async Task MenuGrid_ListsCategories_AndTheSearchNarrowsThem()
    {
        var admin = await LoginAsync("mgmt-page-menuadmin9@test.com", Role.Admin);

        var page = await GetAsync("/Management/Menu", admin);
        string accountBook = await BodyAsync(await GetAsync("/Management/Menu?wholeSearch=AccountBook", admin, ajax: true));
        string drive = await BodyAsync(await GetAsync("/Management/Menu?wholeSearch=DriveAdmin", admin, ajax: true));

        Assert.Equal(System.Net.HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("AccountBook", accountBook);
        Assert.DoesNotContain("DriveAdmin", accountBook);
        Assert.Contains("DriveAdmin", drive);
        Assert.DoesNotContain("AccountBook", drive);
    }

    /// <summary>Listing the whole menu (no search) puts categories and sub-categories in one list, newest id first — a category and a sub-category may share an id, and the sort then falls back to the parent id.</summary>
    [Fact]
    public async Task MenuGrid_WithoutASearch_ListsCategoriesAndSubCategories_OrderedByIdDescending()
    {
        var admin = await LoginAsync("mgmt-page-menuall@test.com", Role.Admin);

        string body = await BodyAsync(await GetAsync("/Management/Menu", admin, ajax: true));

        Assert.Contains("mvc-grid", body); // the (paged) grid renders the merged, id-ordered list
    }

    /// <summary>The menu grid's search also finds sub-categories (by their own name), and lists them with no controller.</summary>
    [Fact]
    public async Task MenuGrid_Search_FindsSubCategories()
    {
        var admin = await LoginAsync("mgmt-page-menusub@test.com", Role.Admin);
        string name = $"SubFindMe{Guid.NewGuid():N}";
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            long categoryId = db.Categories.First().Id;
            db.SubCategories.Add(new SubCategory { CategoryId = categoryId, Name = name, DisplayName = name, IconPath = "/i.png", Action = "SubAction", Role = Role.User, Order = 5 });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        string body = await BodyAsync(await GetAsync($"/Management/Menu?wholeSearch={name}", admin, ajax: true));

        Assert.Contains(name, body);
        Assert.Contains("SubAction", body);
    }

    /// <summary>A page number below 1 (0, negative) is treated as the first page rather than failing.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task PrivateNoteList_APageBelowOne_ShowsTheFirstPage(int page)
    {
        string email = $"mgmt-page-below{(page < 0 ? "neg" : "zero")}@test.com";
        var session = await LoginAsync(email);
        string title = $"BelowOne-{Guid.NewGuid():N}";
        SeedNote(session.Nickname, title);

        var response = await GetAsync($"/Management/PrivateNote?method=list&page={page}", session);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(title, await BodyAsync(response));
    }

    // -- UploadImageFile: pre-upload validation -------------------------------------------------------------

    /// <summary>Uploads an editor image to <c>/Management/UploadImageFile</c>, asserts 200, and returns the response body.</summary>
    private async Task<string> UploadAsync(AuthenticatedSession session, string fileName, byte[] bytes)
    {
        var form = new MultipartFormDataContent { { new ByteArrayContent(bytes), "summernoteImageFile", fileName } };
        using var request = session.BuildFormPostRequest("/Management/UploadImageFile", form);
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        return await BodyAsync(response);
    }

    /// <summary>An empty upload is refused with the size message.</summary>
    [Fact]
    public async Task UploadImageFile_RefusesAnEmptyFile()
    {
        var session = await LoginAsync("mgmt-page-upload10@test.com");

        string json = await UploadAsync(session, "empty.png", []);

        Assert.Contains("\"result\":false", json);
        Assert.Contains("File Size must be smaller than", json);
    }

    /// <summary>Only .jpg/.jpeg/.png are accepted; anything else is refused before any decoding or storage.</summary>
    [Theory]
    [InlineData("anim.gif")]
    [InlineData("vector.svg")]
    [InlineData("shell.png.exe")]
    public async Task UploadImageFile_RefusesADisallowedExtension(string fileName)
    {
        var session = await LoginAsync("mgmt-page-upload11@test.com");

        string json = await UploadAsync(session, fileName, [1, 2, 3]);

        Assert.Contains("\"result\":false", json);
        Assert.Contains("Only .jpg or jpeg or .png file allowed.", json);
    }

    /// <summary>Bytes that merely claim to be a PNG are refused by content validation and nothing is stored.</summary>
    [Fact]
    public async Task UploadImageFile_RefusesBytesThatAreNotARealImage()
    {
        var session = await LoginAsync("mgmt-page-upload12@test.com");

        string json = await UploadAsync(session, "fake.png", [.. "this is not an image"u8]);

        Assert.Contains("\"result\":false", json);
        Assert.DoesNotContain("filePath", json);
    }
}
