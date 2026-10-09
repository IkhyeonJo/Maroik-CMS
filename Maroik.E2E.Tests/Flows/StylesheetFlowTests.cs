using Maroik.E2E.Tests.Infrastructure;
using Microsoft.Playwright;

namespace Maroik.E2E.Tests.Flows;

/// <summary>
/// The site's own stylesheets (<c>wwwroot/{role}/custom/**/css/site.css</c>), checked as a browser applies them: on the
/// real pages, after the AdminLTE / Bootstrap / FullCalendar stylesheets they override. Each test reads the computed
/// style of an element the rule targets, so it fails when the rule is lost — removed, mistyped, outranked by a vendor
/// rule, or no longer loaded by the page.
/// </summary>
[Collection("E2E")]
public class StylesheetFlowTests(E2ESharedFixture fixture) : E2ETestBase(fixture)
{
    /// <summary>The computed <paramref name="property"/> of the first element matching <paramref name="selector"/>.</summary>
    private static async Task<string> StyleAsync(IPage page, string selector, string property, string? pseudo = null)
    {
        await page.Locator(selector).First.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached, Timeout = 15_000 });
        return await page.EvaluateAsync<string>(
            "([selector, property, pseudo]) => getComputedStyle(document.querySelector(selector), pseudo).getPropertyValue(property)",
            new object?[] { selector, property, pseudo });
    }

    /// <summary>Opens <paramref name="path"/> as a visitor, a User or an Admin.</summary>
    private async Task<IPage> OpenAsync(string role, string path)
    {
        IPage page;
        if (role == "Anonymous")
        {
            page = await NewPageAsync();
        }
        else
        {
            (page, _) = await NewSignedInPageAsync(role);
            await page.WaitForURLAsync(url => url.Contains("/Dashboard/", StringComparison.Ordinal), new PageWaitForURLOptions { Timeout = 30_000 });
        }
        await GotoAsync(page, path);
        return page;
    }

    /// <summary>
    /// Every role's layout centres its Bootstrap modals vertically: an inline-block dialog next to a full-height
    /// inline-block <c>::before</c> (<c>{role}/custom/_Layout/css/site.css</c>).
    /// </summary>
    [Theory]
    [InlineData("Anonymous", "/Calendar/AnonymousIndex")]
    [InlineData("User", "/Calendar/UserIndex")]
    [InlineData("Admin", "/Calendar/AdminIndex")]
    public async Task Layout_CentresModalsVertically(string role, string path)
    {
        IPage page = await OpenAsync(role, path);

        Assert.Equal("center", await StyleAsync(page, ".modal", "text-align"));
        Assert.Equal("0px", await StyleAsync(page, ".modal", "padding-right"));
        Assert.Equal("inline-block", await StyleAsync(page, ".modal", "display", "::before"));
        Assert.Equal("middle", await StyleAsync(page, ".modal", "vertical-align", "::before"));
        Assert.Equal("-4px", await StyleAsync(page, ".modal", "margin-right", "::before"));
        Assert.Equal("inline-block", await StyleAsync(page, ".modal-dialog", "display"));
        Assert.Equal("middle", await StyleAsync(page, ".modal-dialog", "vertical-align"));
        Assert.Equal("left", await StyleAsync(page, ".modal-dialog", "text-align"));
    }

    /// <summary>The account pages' own layout centres the modal box the same way (<c>anonymous/custom/_AdminlteBlank/css/site.css</c>).</summary>
    [Fact]
    public async Task AccountPages_CentreTheirModals()
    {
        IPage page = await OpenAsync("Anonymous", "/Account/Login");

        Assert.Equal("center", await StyleAsync(page, ".modal", "text-align"));
        Assert.Equal("inline-block", await StyleAsync(page, ".modal", "display", "::before"));
        Assert.Equal("middle", await StyleAsync(page, ".modal", "vertical-align", "::before"));
    }

    /// <summary>
    /// The calendar pages tint weekends, hide the month-view switcher, show a pointer over events and draw the event
    /// popup as a small white card (<c>{role}/custom/Calendar/{Page}/css/site.css</c>).
    /// </summary>
    [Theory]
    [InlineData("Anonymous", "/Calendar/AnonymousIndex", "#otherCalendarEventPopup", "#closeOtherCalendarEventPopup")]
    [InlineData("User", "/Calendar/UserIndex", "#calendarEventPopup", "#closeCalendarEventPopup")]
    [InlineData("User", "/Calendar/UserIndex", "#otherCalendarEventPopup", "#closeOtherCalendarEventPopup")]
    [InlineData("Admin", "/Calendar/AdminIndex", "#calendarEventPopup", "#closeCalendarEventPopup")]
    public async Task Calendar_StylesTheGridAndTheEventPopup(string role, string path, string popup, string closeButton)
    {
        IPage page = await OpenAsync(role, path);

        Assert.Equal("rgba(80, 180, 255, 0.15)", await StyleAsync(page, ".fc-daygrid-day.fc-day-sun", "background-color"));
        Assert.Equal("rgba(80, 180, 255, 0.15)", await StyleAsync(page, ".fc-daygrid-day.fc-day-sat", "background-color"));
        Assert.Equal("none", await StyleAsync(page, ".fc-dayGridMonth-button", "display"));

        Assert.Equal("rgb(255, 255, 255)", await StyleAsync(page, popup, "background-color"));
        Assert.Equal("rgb(204, 204, 204)", await StyleAsync(page, popup, "border-top-color"));
        Assert.Equal("rgba(0, 0, 0, 0.2) 0px 0px 15px 0px", await StyleAsync(page, popup, "box-shadow"));
        Assert.Equal("170px", await StyleAsync(page, popup, "width"));
        Assert.Equal("10000", await StyleAsync(page, popup, "z-index"));
        Assert.Equal("flex", await StyleAsync(page, $"{popup} .popup-header", "display"));
        Assert.Equal("0px", await StyleAsync(page, $"{popup} .popup-header h5", "margin-top"));
        Assert.Equal("16px", await StyleAsync(page, $"{popup} .popup-header h5", "font-size"));
        Assert.Equal("rgba(0, 0, 0, 0)", await StyleAsync(page, closeButton, "background-color"));
        Assert.Equal("18px", await StyleAsync(page, closeButton, "font-size"));
        Assert.Equal("pointer", await StyleAsync(page, closeButton, "cursor"));
        Assert.Equal("200px", await StyleAsync(page, $"{popup} .popup-body", "max-height"));
        Assert.Equal("14px", await StyleAsync(page, $"{popup} .popup-body p", "font-size"));
        Assert.Equal("flex-end", await StyleAsync(page, $"{popup} .popup-footer", "justify-content"));

        // An event of the page's own markup (the seed data may have none in view): its title shows the pointer too.
        await page.EvaluateAsync("""() => document.body.insertAdjacentHTML("beforeend", '<a class="fc-event" id="styleProbe"><span class="fc-event-title">e</span></a>')""");
        Assert.Equal("pointer", await StyleAsync(page, "#styleProbe", "cursor"));
        Assert.Equal("pointer", await StyleAsync(page, "#styleProbe .fc-event-title", "cursor"));
    }
}
