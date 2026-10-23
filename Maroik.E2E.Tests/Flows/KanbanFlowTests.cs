using Maroik.E2E.Tests.Fixtures;
using Microsoft.Playwright;

namespace Maroik.E2E.Tests.Flows;

/// <summary>
/// End-to-end tests for the personal Kanban board (<c>/Kanban/UserIndex</c>) as a signed-in User: the four lanes with only the
/// user's own cards, adding a card from a lane's "+ Add card" button, editing it (it stays in its lane) and deleting it, and the
/// red due date of an overdue card.
/// </summary>
[Collection("E2E")]
public class KanbanFlowTests(E2ESharedFixture fixture) : E2ETestBase(fixture)
{
    /// <summary>The lane column whose key is <paramref name="lane"/>.</summary>
    private static ILocator Lane(IPage page, string lane) => page.Locator($".kanban-lane[data-lane='{lane}']");

    /// <summary>The board shows the four lanes in order, with their Korean labels, and the signed-in user's cards only.</summary>
    [Fact]
    public async Task Board_ShowsTheFourLanes_AndOnlyTheUsersCards()
    {
        var (page, account) = await NewSignedInPageAsync();
        SeededAccount other = await Db.SeedAccountAsync();
        await Db.SeedKanbanCardAsync(account.Email, "ToDo", "My own card");
        await Db.SeedKanbanCardAsync(other.Email, "ToDo", "Someone elses card");

        await GotoAsync(page, "/Kanban/UserIndex");

        await Assertions.Expect(page.Locator(".kanban-lane")).ToHaveCountAsync(4);
        // The E2E browser is ko-KR (E2ETestBase): the lanes carry their Korean labels.
        Assert.Equal(["나중에", "예정", "진행 중", "완료"],
            (await page.Locator(".kanban-lane > .card-header .card-title").AllInnerTextsAsync()).Select(t => t.Trim()));
        await Assertions.Expect(Lane(page, "ToDo")).ToContainTextAsync("My own card");
        await Assertions.Expect(page.Locator("#kanbanBoard")).Not.ToContainTextAsync("Someone elses card");
    }

    /// <summary>"+ Add card" in a lane, then saving, creates the card in that lane and shows it there without a page reload.</summary>
    [Fact]
    public async Task AddCard_CreatesTheCard_InTheLaneWhoseButtonWasClicked()
    {
        var (page, account) = await NewSignedInPageAsync();
        await GotoAsync(page, "/Kanban/UserIndex");

        await Lane(page, "InProgress").Locator(".kanban-add-card").ClickAsync();
        await Assertions.Expect(page.Locator("#kanbanCardModal")).ToBeVisibleAsync();
        await page.Locator("#kanbanCardTitle").FillAsync("Draft the report");
        await page.Locator("#kanbanCardDescription").FillAsync("first line\nsecond line");
        await page.Locator("#kanbanCardDueDate").FillAsync("2999-05-28");
        await page.Locator("#formKanbanCard input[type='submit']").ClickAsync();

        await Assertions.Expect(page.Locator(".toast-success")).ToBeVisibleAsync();
        ILocator card = Lane(page, "InProgress").Locator(".kanban-card-item", new LocatorLocatorOptions { HasTextString = "Draft the report" });
        await Assertions.Expect(card).ToBeVisibleAsync();
        await Assertions.Expect(card.Locator(".kanban-due")).ToHaveTextAsync("2999-05-28");
        Assert.Equal("InProgress", await Db.ScalarAsync<string>(
            """SELECT "Lane" FROM "KanbanCard" WHERE "AccountEmail" = @email AND "Title" = 'Draft the report'""", ("email", account.Email)));
    }

    /// <summary>Clicking a card opens it filled in; saving a new title updates it in place, in the same lane.</summary>
    [Fact]
    public async Task EditCard_ChangesTheTitle_AndKeepsTheLane()
    {
        var (page, account) = await NewSignedInPageAsync();
        long id = await Db.SeedKanbanCardAsync(account.Email, "Backlog", "Old title", "Some notes");
        await GotoAsync(page, "/Kanban/UserIndex");

        await page.Locator($".kanban-card-item[data-id='{id}']").ClickAsync();
        await Assertions.Expect(page.Locator("#kanbanCardModal")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("#kanbanCardTitle")).ToHaveValueAsync("Old title");
        await Assertions.Expect(page.Locator("#kanbanCardDescription")).ToHaveValueAsync("Some notes");
        await page.Locator("#kanbanCardTitle").FillAsync("New title");
        await page.Locator("#formKanbanCard input[type='submit']").ClickAsync();

        await Assertions.Expect(page.Locator(".toast-success")).ToBeVisibleAsync();
        await Assertions.Expect(Lane(page, "Backlog").Locator($".kanban-card-item[data-id='{id}']")).ToContainTextAsync("New title");
        Assert.Equal("Backlog", await Db.ScalarAsync<string>("""SELECT "Lane" FROM "KanbanCard" WHERE "Id" = @id""", ("id", id)));
    }

    /// <summary>Delete, then confirming, removes the card from the board and the database.</summary>
    [Fact]
    public async Task DeleteCard_RemovesIt_AfterConfirmation()
    {
        var (page, account) = await NewSignedInPageAsync();
        long id = await Db.SeedKanbanCardAsync(account.Email, "Done", "Finished work");
        await GotoAsync(page, "/Kanban/UserIndex");

        await page.Locator($".kanban-card-item[data-id='{id}']").ClickAsync();
        await page.Locator("#btnDeleteKanbanCard").ClickAsync();
        await Assertions.Expect(page.Locator("#confirmDeleteKanbanCardModal")).ToBeVisibleAsync();
        await page.Locator("#btnConfirmDeleteKanbanCard").ClickAsync();

        await Assertions.Expect(page.Locator(".toast-success")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator($".kanban-card-item[data-id='{id}']")).ToHaveCountAsync(0);
        Assert.Equal(0L, await Db.ScalarAsync<long>("""SELECT count(*) FROM "KanbanCard" WHERE "Id" = @id""", ("id", id)));
    }

    /// <summary>
    /// An overdue card's date is red and a future one's grey, while every card keeps the same default card colour — undated, upcoming
    /// or overdue (the site is AdminLTE dark mode, so that colour is the dark card background, not white).
    /// </summary>
    [Fact]
    public async Task DueDate_IsRed_OnlyOnceOverdue_AndTheCardColourNeverChanges()
    {
        var (page, account) = await NewSignedInPageAsync();
        long overdue = await Db.SeedKanbanCardAsync(account.Email, "ToDo", "Late", dueDate: new DateOnly(2000, 1, 2));
        long upcoming = await Db.SeedKanbanCardAsync(account.Email, "ToDo", "Later", dueDate: new DateOnly(2999, 1, 2));
        long undated = await Db.SeedKanbanCardAsync(account.Email, "ToDo", "Whenever");
        await GotoAsync(page, "/Kanban/UserIndex");

        ILocator late = page.Locator($".kanban-card-item[data-id='{overdue}']");
        ILocator later = page.Locator($".kanban-card-item[data-id='{upcoming}']");
        ILocator plain = page.Locator($".kanban-card-item[data-id='{undated}']");
        await Assertions.Expect(late.Locator(".kanban-due")).ToHaveCSSAsync("color", "rgb(220, 53, 69)");
        await Assertions.Expect(later.Locator(".kanban-due")).ToHaveCSSAsync("color", "rgb(173, 181, 189)");
        await Assertions.Expect(plain.Locator(".kanban-due")).ToHaveCountAsync(0);

        string defaultCardColour = await page.EvaluateAsync<string>(
            "() => { const c = document.createElement('div'); c.className = 'card'; document.querySelector('.kanban-lane .card-body').appendChild(c); return getComputedStyle(c).backgroundColor; }");
        await Assertions.Expect(late).ToHaveCSSAsync("background-color", defaultCardColour);
        await Assertions.Expect(later).ToHaveCSSAsync("background-color", defaultCardColour);
        await Assertions.Expect(plain).ToHaveCSSAsync("background-color", defaultCardColour);
    }
}
