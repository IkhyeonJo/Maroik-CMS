using System.Text.Json;
using Maroik.Core.Domain.Accounts;
using Maroik.Core.PostgreSQL.Contexts;
using Maroik.Core.PostgreSQL.Models;
using Maroik.Website.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Maroik.Website.Tests.Controllers;

/// <summary>
/// Integration tests for <c>KanbanController</c> against a real PostgreSQL database (<see cref="MaroikWebApplicationFactory"/>):
/// the board page reached through the menu (<c>/Kanban/UserIndex</c>, User role only), its AJAX partial, and the three card writes
/// (create in a lane, edit without moving, delete), each owner-scoped so another user's card is "not found" (K2).
/// </summary>
[Collection("Website Integration")]
public class KanbanControllerTests(MaroikWebApplicationFactory factory)
{
    /// <summary>The account most tests sign in as.</summary>
    private const string Email = "kanban-user@test.com";

    /// <summary>A second account whose cards the first must never see or touch.</summary>
    private const string OtherEmail = "kanban-other@test.com";

    /// <summary>Client for the shared test host; redirects are not followed so they can be asserted.</summary>
    private readonly HttpClient _client = factory.CreateTestClient(followRedirects: false);

    /// <summary>Signs in as <paramref name="email"/> with the User role.</summary>
    private Task<AuthenticatedSession> LoginAsync(string email = Email) =>
        AuthenticatedSessionHelper.LoginAsync(factory, _client, email, "UserPassword1!", Role.User, TestContext.Current.CancellationToken);

    /// <summary>Makes sure the second account exists (signing in creates it), so its cards can be seeded.</summary>
    private Task EnsureOtherAccountAsync() => LoginAsync(OtherEmail);

    /// <summary>A title unique to the calling test, so assertions on the shared account cannot collide.</summary>
    private static string UniqueTitle([System.Runtime.CompilerServices.CallerMemberName] string testName = "") =>
        $"{testName[..Math.Min(testName.Length, 50)]}-{Guid.NewGuid():N}";

    /// <summary>Inserts a card row directly and returns its id.</summary>
    private long SeedCard(string email, string lane, string title, string? description = null, DateOnly? dueDate = null, DateTime? created = null)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var row = new KanbanCard
        {
            AccountEmail = email, Lane = lane, Title = title, Description = description, DueDate = dueDate,
            Created = created ?? DateTime.UtcNow, Updated = created ?? DateTime.UtcNow,
        };
        db.KanbanCards.Add(row);
        db.SaveChanges();
        return row.Id;
    }

    /// <summary>The stored card with <paramref name="id"/>, or null.</summary>
    private KanbanCard? Card(long id)
    {
        using var scope = factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().KanbanCards.AsNoTracking().SingleOrDefault(c => c.Id == id);
    }

    /// <summary>GETs <paramref name="url"/> with the session cookie (and the AJAX header when <paramref name="ajax"/>).</summary>
    private async Task<HttpResponseMessage> GetAsync(string url, AuthenticatedSession? session, bool ajax = false)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (session != null) request.Headers.Add("Cookie", session.CookieHeader);
        if (ajax) request.Headers.Add("X-Requested-With", "XMLHttpRequest");
        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    /// <summary>POSTs a JSON body and returns the parsed reply.</summary>
    private async Task<JsonElement> PostAsync(AuthenticatedSession session, string url, object? body = null, string? culture = null)
    {
        using var request = session.BuildJsonPostRequest(url, body);
        if (culture != null) request.Headers.AcceptLanguage.ParseAdd(culture);
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return doc.RootElement.Clone();
    }

    // -- UserIndex ------------------------------------------------------------------

    /// <summary>The menu page shows the four lanes in order with their localized labels and an "Add card" button each.</summary>
    [Fact]
    public async Task UserIndex_ShowsTheFourLanes_InOrder()
    {
        var session = await LoginAsync("kanban-empty@test.com");

        var response = await GetAsync("/Kanban/UserIndex", session);
        string html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        int[] positions = new[] { "data-lane=\"Backlog\"", "data-lane=\"ToDo\"", "data-lane=\"InProgress\"", "data-lane=\"Done\"" }
            .Select(marker => html.IndexOf(marker, StringComparison.Ordinal)).ToArray();
        Assert.All(positions, p => Assert.True(p >= 0));
        Assert.Equal(positions.Order(), positions);
        Assert.Contains(">Backlog<", html, StringComparison.Ordinal);
        Assert.Contains(">To do<", html, StringComparison.Ordinal);
        Assert.Contains(">In progress<", html, StringComparison.Ordinal);
        Assert.Contains(">Done<", html, StringComparison.Ordinal);
        Assert.Equal(4, html.Split("kanban-add-card").Length - 1);
        Assert.DoesNotContain("kanban-card-item", html, StringComparison.Ordinal);
    }

    /// <summary>A Korean browser sees the Korean lane labels and menu name.</summary>
    [Fact]
    public async Task UserIndex_ShowsTheKoreanLabels_ToAKoreanBrowser()
    {
        var session = await LoginAsync("kanban-korean@test.com");
        using var request = new HttpRequestMessage(HttpMethod.Get, "/Kanban/UserIndex");
        request.Headers.Add("Cookie", $"{session.CookieHeader}; .AspNetCore.Culture=c=ko-KR|uic=ko-KR");

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        string html = System.Net.WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Assert.Contains(">나중에<", html, StringComparison.Ordinal);
        Assert.Contains(">예정<", html, StringComparison.Ordinal);
        Assert.Contains(">진행 중<", html, StringComparison.Ordinal);
        Assert.Contains(">완료<", html, StringComparison.Ordinal);
        Assert.Contains("할 일", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// The board lists only the signed-in user's cards, each in its own lane, oldest first, with its text HTML-encoded and the
    /// description's line breaks kept as text.
    /// </summary>
    [Fact]
    public async Task UserIndex_ShowsOnlyTheUsersCards_InTheirLanes_OldestFirst_Encoded()
    {
        await EnsureOtherAccountAsync();
        var session = await LoginAsync();
        string older = UniqueTitle() + "<b>";
        string newer = UniqueTitle();
        string others = UniqueTitle();
        SeedCard(Email, "InProgress", newer, created: DateTime.UtcNow);
        SeedCard(Email, "InProgress", older, "line 1\nline 2", created: DateTime.UtcNow.AddMinutes(-5));
        SeedCard(OtherEmail, "InProgress", others);

        var response = await GetAsync("/Kanban/UserIndex", session);
        string html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        string encodedOlder = older.Replace("<b>", "&lt;b&gt;", StringComparison.Ordinal);
        Assert.Contains(encodedOlder, html, StringComparison.Ordinal);
        Assert.DoesNotContain(older, html, StringComparison.Ordinal);
        Assert.True(html.IndexOf(encodedOlder, StringComparison.Ordinal) < html.IndexOf(newer, StringComparison.Ordinal));
        Assert.True(html.IndexOf("data-lane=\"InProgress\"", StringComparison.Ordinal) < html.IndexOf(newer, StringComparison.Ordinal));
        Assert.True(html.IndexOf(newer, StringComparison.Ordinal) < html.IndexOf("data-lane=\"Done\"", StringComparison.Ordinal));
        Assert.Contains("line 1\nline 2", System.Net.WebUtility.HtmlDecode(html), StringComparison.Ordinal);
        Assert.DoesNotContain(others, html, StringComparison.Ordinal);
    }

    /// <summary>
    /// The due date is shown as yyyy-MM-dd: grey for today or later, red once it has passed (decided by the server), and nothing for a
    /// card without one.
    /// </summary>
    [Fact]
    public async Task UserIndex_ShowsTheDueDate_GreyOrRed()
    {
        var session = await LoginAsync("kanban-due@test.com");
        SeedCard("kanban-due@test.com", "ToDo", "Future card", dueDate: new DateOnly(2999, 5, 28));
        SeedCard("kanban-due@test.com", "ToDo", "Past card", dueDate: new DateOnly(2000, 1, 2));
        SeedCard("kanban-due@test.com", "ToDo", "Undated card");

        string html = await (await GetAsync("/Kanban/UserIndex", session)).Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Matches("class=\"[^\"]*kanban-due[^\"]*text-muted[^\"]*\"[^>]*>\\s*<i class=\"far fa-calendar-alt[^\"]*\"></i>\\s*2999-05-28", html);
        Assert.Matches("class=\"[^\"]*kanban-due[^\"]*text-danger[^\"]*\"[^>]*>\\s*<i class=\"far fa-calendar-alt[^\"]*\"></i>\\s*2000-01-02", html);
        Assert.Equal(2, html.Split("kanban-due ").Length - 1);
    }

    /// <summary>The AJAX request for the page returns only the board (no layout), which the script swaps in after a write.</summary>
    [Fact]
    public async Task UserIndex_ReturnsOnlyTheBoard_ToAnAjaxRequest()
    {
        var session = await LoginAsync();
        string title = UniqueTitle();
        SeedCard(Email, "Backlog", title);

        var response = await GetAsync("/Kanban/UserIndex", session, ajax: true);
        string html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(title, html, StringComparison.Ordinal);
        Assert.Contains("data-lane=\"Done\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<html", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("kanbanCardModal", html, StringComparison.Ordinal);
    }

    /// <summary>The page is for the User role only: an administrator is denied (the menu has no Admin entry).</summary>
    [Fact]
    public async Task UserIndex_DeniesAnAdministrator()
    {
        var session = await AuthenticatedSessionHelper.LoginAsync(factory, _client, "kanban-admin@test.com", "UserPassword1!", Role.Admin, TestContext.Current.CancellationToken);

        var response = await GetAsync("/Kanban/UserIndex", session);

        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Dashboard/AnonymousIndex", response.Headers.Location?.OriginalString);
    }

    /// <summary>A signed-out visitor is sent to the public dashboard.</summary>
    [Fact]
    public async Task UserIndex_SendsAnAnonymousVisitorAway()
    {
        var response = await GetAsync("/Kanban/UserIndex", null);

        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Dashboard/AnonymousIndex", response.Headers.Location?.OriginalString);
    }

    // -- CreateKanbanCard -------------------------------------------------------------

    /// <summary>A valid card is created in the lane whose "Add card" was clicked, with its description and due date.</summary>
    [Fact]
    public async Task CreateKanbanCard_CreatesTheCard_InTheGivenLane()
    {
        var session = await LoginAsync();
        string title = UniqueTitle();

        JsonElement reply = await PostAsync(session, "/Kanban/CreateKanbanCard",
            new { Lane = "InProgress", Title = title, Description = "a\nb", DueDate = "2026-05-28" });

        Assert.True(reply.GetProperty("result").GetBoolean());
        Assert.Equal("The card has been successfully created.", reply.GetProperty("message").GetString());
        using var scope = factory.Services.CreateScope();
        var row = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().KanbanCards.AsNoTracking().Single(c => c.Title == title);
        Assert.Equal(Email, row.AccountEmail);
        Assert.Equal("InProgress", row.Lane);
        Assert.Equal("a\nb", row.Description);
        Assert.Equal(new DateOnly(2026, 5, 28), row.DueDate);
    }

    /// <summary>A card without a due date is created with none.</summary>
    [Fact]
    public async Task CreateKanbanCard_AcceptsNoDueDate()
    {
        var session = await LoginAsync();
        string title = UniqueTitle();

        JsonElement reply = await PostAsync(session, "/Kanban/CreateKanbanCard", new { Lane = "Done", Title = title, Description = "", DueDate = (string?)null });

        Assert.True(reply.GetProperty("result").GetBoolean());
        using var scope = factory.Services.CreateScope();
        var row = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().KanbanCards.AsNoTracking().Single(c => c.Title == title);
        Assert.Null(row.DueDate);
        Assert.Null(row.Description);
    }

    /// <summary>A refusal from the domain is answered with its specific, localized reason.</summary>
    [Theory]
    [InlineData("en-US", "Archive", "x", "'Archive' is not a Kanban lane.")]
    [InlineData("en-US", "ToDo", " ", "Card title cannot be empty.")]
    [InlineData("ko-KR", "ToDo", " ", "카드 제목을 입력해 주세요.")]
    public async Task CreateKanbanCard_AnswersTheDomainRefusal(string culture, string lane, string title, string expected)
    {
        var session = await LoginAsync();

        JsonElement reply = await PostAsync(session, "/Kanban/CreateKanbanCard", new { Lane = lane, Title = title }, culture);

        Assert.False(reply.GetProperty("result").GetBoolean());
        Assert.Equal(expected, reply.GetProperty("error").GetString());
    }

    /// <summary>A due date that is not a yyyy-MM-dd date is invalid input.</summary>
    [Theory]
    [InlineData("28/05/2026")]
    [InlineData("2026-13-01")]
    [InlineData("")]
    public async Task CreateKanbanCard_RefusesADueDateThatIsNotAnIsoDate(string dueDate)
    {
        var session = await LoginAsync();

        JsonElement reply = await PostAsync(session, "/Kanban/CreateKanbanCard", new { Lane = "ToDo", Title = UniqueTitle(), DueDate = dueDate });

        Assert.False(reply.GetProperty("result").GetBoolean());
        Assert.Equal("Input is invalid", reply.GetProperty("error").GetString());
    }

    /// <summary>A signed-out POST is refused.</summary>
    [Fact]
    public async Task CreateKanbanCard_RefusesAnAnonymousSession()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/Kanban/CreateKanbanCard");
        request.Content = System.Net.Http.Json.JsonContent.Create(new { Lane = "ToDo", Title = "x" });

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.NotEqual(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    // -- UpdateKanbanCard -------------------------------------------------------------

    /// <summary>An edit replaces the title, description and due date of the user's card.</summary>
    [Fact]
    public async Task UpdateKanbanCard_SavesTheNewContent()
    {
        var session = await LoginAsync();
        long id = SeedCard(Email, "ToDo", UniqueTitle(), "old", new DateOnly(2026, 1, 1));
        string title = UniqueTitle();

        JsonElement reply = await PostAsync(session, "/Kanban/UpdateKanbanCard", new { Id = id, Title = title, Description = "", DueDate = (string?)null });

        Assert.True(reply.GetProperty("result").GetBoolean());
        Assert.Equal("The card has been successfully updated.", reply.GetProperty("message").GetString());
        KanbanCard row = Card(id)!;
        Assert.Equal(title, row.Title);
        Assert.Null(row.Description);
        Assert.Null(row.DueDate);
    }

    /// <summary>An edit never moves a card: a lane sent along with the edit is ignored and the card stays where it was (K11).</summary>
    [Fact]
    public async Task UpdateKanbanCard_KeepsTheLane_EvenWhenTheRequestNamesAnother()
    {
        var session = await LoginAsync();
        long id = SeedCard(Email, "Backlog", UniqueTitle());

        JsonElement reply = await PostAsync(session, "/Kanban/UpdateKanbanCard", new { Id = id, Title = UniqueTitle(), Lane = "Done" });

        Assert.True(reply.GetProperty("result").GetBoolean());
        Assert.Equal("Backlog", Card(id)!.Lane);
    }

    /// <summary>Another user's card is "not found" and stays untouched.</summary>
    [Fact]
    public async Task UpdateKanbanCard_AnswersNotFound_ForAnotherUsersCard()
    {
        await EnsureOtherAccountAsync();
        var session = await LoginAsync();
        string title = UniqueTitle();
        long id = SeedCard(OtherEmail, "ToDo", title);

        JsonElement reply = await PostAsync(session, "/Kanban/UpdateKanbanCard", new { Id = id, Title = "Hijacked" });

        Assert.False(reply.GetProperty("result").GetBoolean());
        Assert.Equal("The card could not be found.", reply.GetProperty("error").GetString());
        Assert.Equal(title, Card(id)!.Title);
    }

    /// <summary>A due date that is not a yyyy-MM-dd date is invalid input on an edit too.</summary>
    [Fact]
    public async Task UpdateKanbanCard_RefusesADueDateThatIsNotAnIsoDate()
    {
        var session = await LoginAsync();
        long id = SeedCard(Email, "ToDo", UniqueTitle());

        JsonElement reply = await PostAsync(session, "/Kanban/UpdateKanbanCard", new { Id = id, Title = "x", DueDate = "tomorrow" });

        Assert.False(reply.GetProperty("result").GetBoolean());
        Assert.Equal("Input is invalid", reply.GetProperty("error").GetString());
    }

    // -- DeleteKanbanCard -------------------------------------------------------------

    /// <summary>The user's delete removes the card.</summary>
    [Fact]
    public async Task DeleteKanbanCard_RemovesTheUsersCard()
    {
        var session = await LoginAsync();
        long id = SeedCard(Email, "Done", UniqueTitle());

        JsonElement reply = await PostAsync(session, $"/Kanban/DeleteKanbanCard?id={id}");

        Assert.True(reply.GetProperty("result").GetBoolean());
        Assert.Equal("The card has been successfully deleted.", reply.GetProperty("message").GetString());
        Assert.Null(Card(id));
    }

    /// <summary>Another user's card is "not found" (in Korean for a Korean browser) and is not deleted.</summary>
    [Fact]
    public async Task DeleteKanbanCard_AnswersNotFound_ForAnotherUsersCard()
    {
        await EnsureOtherAccountAsync();
        var session = await LoginAsync();
        long id = SeedCard(OtherEmail, "Done", UniqueTitle());

        JsonElement reply = await PostAsync(session, $"/Kanban/DeleteKanbanCard?id={id}", culture: "ko-KR");

        Assert.False(reply.GetProperty("result").GetBoolean());
        Assert.Equal("카드를 찾을 수 없습니다.", reply.GetProperty("error").GetString());
        Assert.NotNull(Card(id));
    }
}
