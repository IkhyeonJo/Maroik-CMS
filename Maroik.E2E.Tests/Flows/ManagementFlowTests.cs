using Maroik.E2E.Tests.Infrastructure;
using Microsoft.Playwright;

namespace Maroik.E2E.Tests.Flows;

/// <summary>
/// End-to-end tests for the Management area: the User's own profile page, and the Admin-only account
/// and menu administration — including the nickname rules (reserved names, case-insensitive
/// uniqueness) as they apply when an administrator creates an account.
/// </summary>
[Collection("E2E")]
public class ManagementFlowTests(E2ESharedFixture fixture) : E2ETestBase(fixture)
{
    // -- Profile (User) ------------------------------------------------------------

    /// <summary>The profile page shows the signed-in User's own e-mail and nickname.</summary>
    [Fact]
    public async Task Profile_ShowsTheSignedInUsersDetails()
    {
        var (page, account) = await NewSignedInPageAsync();

        await GotoAsync(page, "/Management/Profile");

        await Assertions.Expect(page.Locator(".profile-username")).ToHaveTextAsync(account.Nickname);
        // The e-mail is shown in a disabled input, so it is matched by value rather than by text.
        await Assertions.Expect(page.Locator($"input[value='{account.Email}']")).ToHaveCountAsync(1);
    }

    // -- Account administration (Admin) ------------------------------------------------

    /// <summary>The account grid lists accounts to an Admin.</summary>
    [Fact]
    public async Task AdminAccount_Grid_ListsAccounts()
    {
        var (page, _) = await NewSignedInPageAsync("Admin");
        SeededAccount user = await Db.SeedAccountAsync();

        await GotoAsync(page, "/Management/Account");

        // The grid is paged and the shared test database keeps accumulating accounts, so narrow it
        // with the free-text search rather than hoping the new row is on the first page.
        await page.Locator("#gridSearch").FillAsync(user.Email);

        await Assertions.Expect(page.Locator(".mvc-grid")).ToContainTextAsync(user.Email);
    }

    /// <summary>Fills and submits the admin "create account" dialog for a User in Asia/Seoul.</summary>
    private static async Task CreateAccountThroughTheUiAsync(IPage page, string email, string nickname)
    {
        await GotoAsync(page, "/Management/Account");
        await page.Locator("button[data-target='#createAccountDialogModal']").ClickAsync();
        await page.Locator("#createAccountEmail").FillAsync(email);
        await page.Locator("#createAccountPassword").FillAsync("Adm1n-Created!");
        await page.Locator("#createAccountNickname").FillAsync(nickname);
        await page.Locator("#createAccountRole").SelectOptionAsync("User");
        await page.Locator("#createAccountTimeZone").SelectOptionAsync("Asia/Seoul");
        await page.Locator("#formCreateAccount input[type='submit']").ClickAsync();
    }

    /// <summary>An Admin can create an account through the UI; the nickname is stored normalized.</summary>
    [Fact]
    public async Task AdminAccount_Create_PersistsTheAccount_WithANormalizedNickname()
    {
        var (page, _) = await NewSignedInPageAsync("Admin");
        string key = Guid.NewGuid().ToString("N")[..8];
        string email = $"created-{key}@test.com";

        // Full-width letters and stray spaces around the nickname.
        await CreateAccountThroughTheUiAsync(page, email, $"  Ｎｅｗ{key}  ");

        await Assertions.Expect(page.Locator(".toast-success")).ToBeVisibleAsync();
        string? stored = await Db.ScalarAsync<string>("""SELECT "Nickname" FROM "Account" WHERE "Email" = @email""", ("email", email));
        Assert.Equal($"New{key}", stored);
    }

    /// <summary>
    /// Unlike self-registration, an administrator may deliberately create an account under a reserved
    /// name (here a second "Moderator"); only ordinary users are barred from it.
    /// </summary>
    [Fact]
    public async Task AdminAccount_Create_MayUseAReservedNickname()
    {
        var (page, _) = await NewSignedInPageAsync("Admin");
        string email = $"mod-{Guid.NewGuid():N}"[..14] + "@test.com";

        await CreateAccountThroughTheUiAsync(page, email, "Moderator");

        await Assertions.Expect(page.Locator(".toast-success")).ToBeVisibleAsync();
        string? stored = await Db.ScalarAsync<string>("""SELECT "Nickname" FROM "Account" WHERE "Email" = @email""", ("email", email));
        Assert.Equal("Moderator", stored);
    }

    /// <summary>A nickname that differs from an existing one only by case is refused, and no account is created.</summary>
    [Fact]
    public async Task AdminAccount_Create_WithANicknameDifferingOnlyByCase_IsRefused()
    {
        var (page, _) = await NewSignedInPageAsync("Admin");
        string key = Guid.NewGuid().ToString("N")[..8];
        await Db.SeedAccountAsync(nickname: $"CaseTwin{key}");
        string email = $"twin-{key}@test.com";

        await CreateAccountThroughTheUiAsync(page, email, $"casetwin{key}");

        await Assertions.Expect(page.Locator(".toast-error")).ToBeVisibleAsync();
        long created = await Db.ScalarAsync<long>("""SELECT count(*) FROM "Account" WHERE "Email" = @email""", ("email", email));
        Assert.Equal(0, created);
    }

    // -- Menu administration (Admin) -----------------------------------------------------

    /// <summary>The menu grid lists the navigation menu to an Admin.</summary>
    [Fact]
    public async Task AdminMenu_Grid_ListsTheNavigationMenu()
    {
        var (page, _) = await NewSignedInPageAsync("Admin");

        await GotoAsync(page, "/Management/Menu");

        // The grid is paged (the newest rows come first), and menu names are localized; every seeded
        // menu row carries an icon class, which is shown as is.
        await Assertions.Expect(page.Locator(".mvc-grid tbody tr").First).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator(".mvc-grid")).ToContainTextAsync("nav-icon");
    }

    // -- Private note (User) ----------------------------------------------------------

    /// <summary>The private-note write page turns the body into a Summernote editor.</summary>
    [Fact]
    public async Task PrivateNoteWriteMode_ShowsTheRichTextEditor()
    {
        var (page, _) = await NewSignedInPageAsync();
        await GotoAsync(page, "/Management/PrivateNote?method=write");

        await Assertions.Expect(page.Locator(".note-editor .note-editable")).ToBeVisibleAsync();
    }

    /// <summary>The private-note edit page turns the body into a Summernote editor holding the note's content.</summary>
    [Fact]
    public async Task PrivateNoteEditMode_ShowsTheRichTextEditor_WithTheNoteBody()
    {
        var (page, account) = await NewSignedInPageAsync();
        long boardId = await Db.SeedPrivateNoteAsync(account.Nickname, "<p>editable note</p>");
        await GotoAsync(page, $"/Management/PrivateNote?method=edit&boardId={boardId}");

        await Assertions.Expect(page.Locator(".note-editor .note-editable")).ToContainTextAsync("editable note");
    }
}
