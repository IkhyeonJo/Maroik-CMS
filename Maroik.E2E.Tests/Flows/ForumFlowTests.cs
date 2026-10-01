using Maroik.E2E.Tests.Infrastructure;
using Microsoft.Playwright;

namespace Maroik.E2E.Tests.Flows;

/// <summary>
/// End-to-end tests for the FreeForum user flow.
/// The forum list is accessible without authentication; write mode requires a login.
/// </summary>
[Collection("E2E")]
public class ForumFlowTests(E2ESharedFixture fixture) : E2ETestBase(fixture)
{
    // -- Forum list -----------------------------------------------------------

    /// <summary>Verifies that <c>FreeForumList</c> loads with non-empty body.</summary>
    [Fact]
    public async Task FreeForumList_Loads_WithNonEmptyBody()
    {
        var page = await NewPageAsync();
        await GotoAsync(page, "/Forum/FreeForum");

        string bodyText = await page.Locator("body").InnerTextAsync();
        Assert.False(string.IsNullOrWhiteSpace(bodyText));
    }

    /// <summary>Verifies that <c>FreeForumList</c> has free board heading.</summary>
    [Fact]
    public async Task FreeForumList_HasFreeBoardHeading()
    {
        var page = await NewPageAsync();
        await GotoAsync(page, "/Forum/FreeForum");

        // The forum list view renders an <h3 class="card-title"> with the localised "Free Forum" text
        var heading = page.Locator("h3.card-title");
        await Assertions.Expect(heading).ToBeVisibleAsync();
    }

    /// <summary>Verifies that <c>FreeForumList</c> contains search inputs.</summary>
    [Fact]
    public async Task FreeForumList_ContainsSearchForm()
    {
        var page = await NewPageAsync();
        await GotoAsync(page, "/Forum/FreeForum");

        // The forum list renders a JS-driven search bar (no <form> wrapper).
        // Verify the search text input and the search-type selector are present.
        await Assertions.Expect(page.Locator("#btnFreeForumSearchText")).ToBeVisibleAsync();
    }

    // -- Write mode: unauthenticated ------------------------------------------

    /// <summary>Verifies that <c>FreeForumWriteMode</c> without session when redirects to log in.</summary>
    [Fact]
    public async Task FreeForumWriteMode_WithoutSession_RedirectsToLogin()
    {
        var page = await NewPageAsync();

        // Navigate to write mode — without a session the controller redirects to Log in
        await GotoAsync(page, "/Forum/FreeForum?method=write");

        // AuthorizationFilter redirects with new RedirectResult("/Account/Login") — a literal,
        // fixed-case path, so match it exactly.
        string finalUrl = page.Url;
        Assert.Contains("/Account/Login", finalUrl, StringComparison.Ordinal);
    }

    /// <summary>Verifies that <c>FreeForumWriteMode</c> without session when shows login form.</summary>
    [Fact]
    public async Task FreeForumWriteMode_WithoutSession_ShowsLoginForm()
    {
        var page = await NewPageAsync();
        await GotoAsync(page, "/Forum/FreeForum?method=write");

        // After redirect to Log in, the login form must be present
        await page.WaitForSelectorAsync("#loginForm", new PageWaitForSelectorOptions { Timeout = 10_000 });
        await Assertions.Expect(page.Locator("#loginForm")).ToBeVisibleAsync();
    }

    // -- Detail mode: missing board --------------------------------------------

    /// <summary>Verifies that <c>FreeForumDetailMode</c> with missing board id when redirects to list.</summary>
    [Fact]
    public async Task FreeForumDetailMode_WithMissingBoardId_RedirectsToList()
    {
        var page = await NewPageAsync();

        // boardId 99999 does not exist → controller redirects back to the list
        await GotoAsync(page, "/Forum/FreeForum?method=detail&boardId=99999");

        // After following the redirect, the h3 "Free Forum" heading must be present on the list
        var heading = page.Locator("h3.card-title");
        await Assertions.Expect(heading).ToBeVisibleAsync();
    }

    // -- Title search ---------------------------------------------------------

    /// <summary>Verifies that <c>FreeForumList</c> with title search when renders page.</summary>
    [Fact]
    public async Task FreeForumList_WithTitleSearch_RendersPage()
    {
        var page = await NewPageAsync();
        await GotoAsync(page, "/Forum/FreeForum?method=list&searchType=Title&searchText=hello");

        string bodyText = await page.Locator("body").InnerTextAsync();
        Assert.False(string.IsNullOrWhiteSpace(bodyText));
    }

    // -- Detail mode: inline image and attachment -------------------------------

    /// <summary>A valid 1×1 PNG.</summary>
    private static readonly byte[] _png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    /// <summary>
    /// On a post's detail page the body image still arrives embedded and is shown, while the
    /// attachment is not embedded: clicking its link downloads it from the download endpoint,
    /// under its own name and with its stored bytes — here for a signed-out visitor.
    /// </summary>
    [Fact]
    public async Task FreeForumDetail_ShowsTheBodyImage_AndDownloadsTheAttachmentFromItsEndpoint()
    {
        SeededAccount author = await Db.SeedAccountAsync();
        string key = Guid.NewGuid().ToString("N");
        string imagePath = $"upload/FreeForum/e2e/{key}.png";
        string attachmentPath = $"upload/FreeForum/e2e/{key}.zip";
        byte[] attachment = [.. "e2e-attachment-bytes"u8];
        Files.Seed(imagePath, _png);
        Files.Seed(attachmentPath, attachment);
        long boardId = await Db.SeedFreeForumPostAsync(author.Nickname, $"<p>with image</p><img alt=\"{imagePath}\">", attachmentPath, attachment.Length);

        var page = await NewPageAsync();
        await GotoAsync(page, $"/Forum/FreeForum?method=detail&boardId={boardId}");

        var image = page.Locator("#detailBoardContent img");
        await Assertions.Expect(image).ToHaveAttributeAsync("src", new System.Text.RegularExpressions.Regex("^blob:"));
        await page.WaitForFunctionAsync("() => document.querySelector('#detailBoardContent img')?.naturalWidth === 1");
        Assert.DoesNotContain(Convert.ToBase64String(attachment), await page.ContentAsync());

        IDownload download = await page.RunAndWaitForDownloadAsync(() => page.Locator("#aDetailBoardAttachedFile").ClickAsync());

        Assert.Equal("payload.zip", download.SuggestedFilename);
        await using Stream content = await download.CreateReadStreamAsync();
        using var copy = new MemoryStream();
        await content.CopyToAsync(copy, TestContext.Current.CancellationToken);
        Assert.Equal(attachment, copy.ToArray());
    }
}
