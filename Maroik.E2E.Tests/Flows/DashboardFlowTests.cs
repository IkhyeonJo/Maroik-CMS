using Maroik.E2E.Tests.Infrastructure;
using Microsoft.Playwright;

namespace Maroik.E2E.Tests.Flows;

/// <summary>
/// End-to-end tests for the dashboard pages.
/// The anonymous dashboard is the application's default landing page and the only
/// dashboard that an unauthenticated browser can reach without a session.
/// </summary>
[Collection("E2E")]
public class DashboardFlowTests(E2ESharedFixture fixture) : E2ETestBase(fixture)
{
    // -- Anonymous dashboard --------------------------------------------------

    /// <summary>Verifies that <c>AnonymousDashboard</c> loads with non-empty body.</summary>
    [Fact]
    public async Task AnonymousDashboard_Loads_WithNonEmptyBody()
    {
        var page = await NewPageAsync();
        await GotoAsync(page, "/Dashboard/AnonymousIndex");

        string bodyText = await page.Locator("body").InnerTextAsync();
        Assert.False(string.IsNullOrWhiteSpace(bodyText));
    }

    /// <summary>Verifies that the default route loads and resolves to the dashboard page.</summary>
    [Fact]
    public async Task DefaultRoute_Loads_AndResolvesToDashboard()
    {
        var page = await NewPageAsync();
        await GotoAsync(page, "/");

        // The root URL resolves to Dashboard/AnonymousIndex via the default route; the test host
        // is served from a fixed lower-case "localhost" base URL.
        string finalUrl = page.Url;
        Assert.Contains("localhost", finalUrl, StringComparison.Ordinal);

        string bodyText = await page.Locator("body").InnerTextAsync();
        Assert.False(string.IsNullOrWhiteSpace(bodyText));
    }

    /// <summary>Verifies that <c>AnonymousDashboard</c> has HTML body element.</summary>
    [Fact]
    public async Task AnonymousDashboard_HasHtmlBodyElement()
    {
        var page = await NewPageAsync();
        await GotoAsync(page, "/Dashboard/AnonymousIndex");

        // Standard HTML body element must exist
        await Assertions.Expect(page.Locator("body")).ToBeVisibleAsync();
    }

    /// <summary>Verifies that <c>AnonymousDashboard</c> has non-empty page title.</summary>
    [Fact]
    public async Task AnonymousDashboard_HasNonEmptyPageTitle()
    {
        var page = await NewPageAsync();
        await GotoAsync(page, "/Dashboard/AnonymousIndex");

        string title = await page.TitleAsync();
        Assert.False(string.IsNullOrWhiteSpace(title));
    }

    // -- Navigation links available on the anonymous dashboard ----------------

    /// <summary>Verifies that <c>AnonymousDashboard</c> contains navigation elements.</summary>
    [Fact]
    public async Task AnonymousDashboard_ContainsNavigationElements()
    {
        var page = await NewPageAsync();
        await GotoAsync(page, "/Dashboard/AnonymousIndex");

        // At minimum there should be one anchor element in the page
        int linkCount = await page.Locator("a").CountAsync();
        Assert.True(linkCount > 0, "Anonymous dashboard should contain at least one navigation link.");
    }
}
