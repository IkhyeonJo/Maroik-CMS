using Maroik.E2E.Tests.Infrastructure;
using Microsoft.Playwright;

namespace Maroik.E2E.Tests.Flows;

/// <summary>
/// End-to-end tests for the Calendar anonymous flow.
/// The anonymous calendar view uses a hard-coded anonymous <c>AccountResponse</c>
/// so it is reachable without a session.
/// </summary>
[Collection("E2E")]
public class CalendarFlowTests(E2ESharedFixture fixture) : E2ETestBase(fixture)
{
    /// <summary>Verifies that <c>AnonymousCalendar</c> loads with non-empty body.</summary>
    [Fact]
    public async Task AnonymousCalendar_Loads_WithNonEmptyBody()
    {
        var page = await NewPageAsync();
        await GotoAsync(page, "/Calendar/AnonymousIndex");

        string bodyText = await page.Locator("body").InnerTextAsync();
        Assert.False(string.IsNullOrWhiteSpace(bodyText));
    }

    /// <summary>Verifies that <c>AnonymousCalendar</c> has HTML body element.</summary>
    [Fact]
    public async Task AnonymousCalendar_HasHtmlBodyElement()
    {
        var page = await NewPageAsync();
        await GotoAsync(page, "/Calendar/AnonymousIndex");

        await Assertions.Expect(page.Locator("body")).ToBeVisibleAsync();
    }

    /// <summary>Verifies that <c>AnonymousCalendar</c> has non-empty title.</summary>
    [Fact]
    public async Task AnonymousCalendar_HasNonEmptyTitle()
    {
        var page = await NewPageAsync();
        await GotoAsync(page, "/Calendar/AnonymousIndex");

        string title = await page.TitleAsync();
        Assert.False(string.IsNullOrWhiteSpace(title));
    }

    /// <summary>Verifies that <c>AnonymousCalendar</c> contains at least one element.</summary>
    [Fact]
    public async Task AnonymousCalendar_ContainsAtLeastOneElement()
    {
        var page = await NewPageAsync();
        await GotoAsync(page, "/Calendar/AnonymousIndex");

        // The page must render at least one <div> ?? confirming the layout loaded
        int divCount = await page.Locator("div").CountAsync();
        Assert.True(divCount > 0);
    }
}
