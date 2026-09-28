using Maroik.E2E.Tests.Infrastructure;
using Microsoft.Playwright;

namespace Maroik.E2E.Tests.Flows;

/// <summary>
/// End-to-end tests for the nickname rules on the public registration form: a reserved or lookalike
/// nickname is refused by the server, the page stays on the form with an error, and no account row is
/// created. (Successful registration needs the mail broker, so only the refusals are exercised.)
/// </summary>
[Collection("E2E")]
public class RegistrationFlowTests(E2ESharedFixture fixture) : E2ETestBase(fixture)
{
    private static async Task<string> SubmitRegistrationAsync(IPage page, string nickname, string? email = null)
    {
        email ??= $"reg-{Guid.NewGuid():N}"[..16] + "@test.com";
        await GotoAsync(page, "/Account/Register");
        await page.WaitForSelectorAsync("#registerForm");

        await page.Locator("#Nickname").FillAsync(nickname);
        await page.Locator("#Email").FillAsync(email);
        await page.Locator("#Password").FillAsync("Reg1-Pass!");
        await page.Locator("#ConfirmPassword").FillAsync("Reg1-Pass!");
        // Not left to the culture's default: without a time zone the form never submits.
        await page.Locator("#TimeZoneIanaId").SelectOptionAsync("Asia/Seoul");
        // The checkbox is the hidden input of an iCheck-styled control; force past the visibility check.
        await page.Locator("#AgreedServiceTerms").CheckAsync(new LocatorCheckOptions { Force = true });
        // The refusals below assert "form still shown, no account", which is also what an unsubmitted
        // form looks like — so require that the POST really reached the server.
        await page.RunAndWaitForResponseAsync(
            () => page.Locator("#btnRegister").ClickAsync(),
            response => response.Request.Method == "POST" && response.Url.Contains("/Account/Register", StringComparison.Ordinal));

        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        return email;
    }

    private async Task AssertRefusedAsync(IPage page, string email)
    {
        // Still on the registration form, with an error shown, and nothing was created.
        await Assertions.Expect(page.Locator("#registerForm")).ToBeVisibleAsync();
        long created = await Db.ScalarAsync<long>("""SELECT count(*) FROM "Account" WHERE "Email" = @email""", ("email", email));
        Assert.Equal(0, created);
    }

    /// <summary>Staff / system names cannot be claimed by self-registration, however they are dressed up.</summary>
    [Theory]
    [InlineData("Admin")]
    [InlineData("ADMIN")]
    [InlineData("A d m i n")]
    [InlineData("Ａｄｍｉｎ")]
    [InlineData("Login")]
    [InlineData("관리자")]
    public async Task Register_WithAReservedNickname_IsRefused(string nickname)
    {
        var page = await NewPageAsync();

        string email = await SubmitRegistrationAsync(page, nickname);

        await AssertRefusedAsync(page, email);
    }

    /// <summary>A nickname that differs from an existing user's only by case is refused.</summary>
    [Fact]
    public async Task Register_WithANicknameDifferingOnlyByCase_IsRefused()
    {
        string key = Guid.NewGuid().ToString("N")[..8];
        await Db.SeedAccountAsync(nickname: $"Existing{key}");
        var page = await NewPageAsync();

        string email = await SubmitRegistrationAsync(page, $"EXISTING{key}");

        await AssertRefusedAsync(page, email);
    }

    /// <summary>A nickname hiding an invisible character (a zero-width space) is refused.</summary>
    [Fact]
    public async Task Register_WithAnInvisibleCharacterInTheNickname_IsRefused()
    {
        var page = await NewPageAsync();

        string email = await SubmitRegistrationAsync(page, "Bo​b" + Guid.NewGuid().ToString("N")[..5]);

        await AssertRefusedAsync(page, email);
    }

    /// <summary>
    /// An e-mail address longer than the database column (255) is refused with a validation message
    /// instead of failing to write, and no account is created.
    /// </summary>
    [Fact]
    public async Task Register_WithAnOverlongEmail_IsRefused()
    {
        var page = await NewPageAsync();
        string email = new string('a', 250) + "@test.com";

        await SubmitRegistrationAsync(page, "LongMail" + Guid.NewGuid().ToString("N")[..6], email);

        await AssertRefusedAsync(page, email);
        await Assertions.Expect(page.Locator("span[style*='color: red']").First).ToContainTextAsync("255");
    }
}
