using Maroik.E2E.Tests.Fixtures;
using Microsoft.Playwright;

namespace Maroik.E2E.Tests.Flows;

/// <summary>
/// The calendar pages laid out in a real browser: the event popup stays inside the window (it used to open past the right
/// edge for an event in the last columns and pushed the whole page sideways), every week of the month shows on a phone (the
/// grid used to keep a fixed height and hide the last weeks behind an inner scroll), the "all day" and selection checkboxes
/// are plain checkboxes next to their labels (they carried <c>form-control</c> and were drawn as wide centred boxes), and the
/// calendar dialogs line up their fields and keep a usable width.
/// </summary>
[Collection("E2E")]
public class CalendarLayoutFlowTests(E2ESharedFixture fixture) : E2ETestBase(fixture)
{
    /// <summary>Opens the role's calendar page at <paramref name="width"/> × 900.</summary>
    private async Task<IPage> OpenAsync(string role, int width)
    {
        var (page, _) = await NewSignedInPageAsync(role);
        await page.SetViewportSizeAsync(width, 900);
        await GotoAsync(page, $"/Calendar/{role}Index");
        await page.WaitForSelectorAsync(".fc-daygrid-day");
        return page;
    }

    /// <summary>The bounding box of <paramref name="selector"/>, which must be on screen.</summary>
    private static async Task<LocatorBoundingBoxResult> BoxAsync(IPage page, string selector) =>
        await page.Locator(selector).First.BoundingBoxAsync() ?? throw new InvalidOperationException($"{selector} is not displayed");

    /// <summary>
    /// Creates a timed event on the last Saturday of the shown month (the rightmost column) through the create dialog, then
    /// clicks it, so its popup opens next to the right edge of the grid.
    /// </summary>
    private static async Task OpenThePopupOfAnEventInTheLastColumnAsync(IPage page)
    {
        await page.Locator(".fc-day-sat:not(.fc-day-other)").Last.ClickAsync();
        await page.WaitForSelectorAsync("#createCalendarEventTaskDialogModal.show");
        await page.Locator("#createCalendarEventName").FillAsync("Layout check");
        await page.Locator("#createCalendarEventLocation").FillAsync("Seoul");
        await page.Locator("#createCalendarEventTaskDialogModal [type=submit]").ClickAsync();
        await page.Locator(".fc-event").First.ClickAsync();
        await page.WaitForSelectorAsync("#calendarEventPopup", new() { State = WaitForSelectorState.Visible });
    }

    /// <summary>The popup of an event in the rightmost column opens inside the window, and the page does not scroll sideways.</summary>
    [Theory]
    [InlineData("User", 1400)]
    [InlineData("Admin", 1400)]
    [InlineData("User", 390)]
    public async Task TheEventPopup_StaysInsideTheWindow(string role, int width)
    {
        IPage page = await OpenAsync(role, width);

        await OpenThePopupOfAnEventInTheLastColumnAsync(page);

        LocatorBoundingBoxResult popup = await BoxAsync(page, "#calendarEventPopup");
        Assert.True(popup.X >= 0 && popup.X + popup.Width <= width, $"popup spans {popup.X}..{popup.X + popup.Width} in a {width}px window");
        Assert.True(await page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= window.innerWidth"));
    }

    /// <summary>On a phone every week of the month is shown in full: the grid grows with its weeks instead of scrolling inside.</summary>
    [Theory]
    [InlineData("User")]
    [InlineData("Admin")]
    public async Task EveryWeek_IsShown_OnAPhone(string role)
    {
        IPage page = await OpenAsync(role, 390);

        bool scrollsInside = await page.EvaluateAsync<bool>(
            "() => [...document.querySelectorAll('#calendar .fc-scroller')].some(s => s.scrollHeight > s.clientHeight + 1)");

        Assert.False(scrollsInside);
        Assert.Equal(6, await page.Locator("#calendar .fc-daygrid-body tr").CountAsync());
    }

    /// <summary>
    /// The weekday header keeps the dark theme: with a grid that grows with its weeks FullCalendar makes the header sticky by
    /// default and paints it white (<c>--fc-page-bg-color</c>), which hid the white weekday names.
    /// </summary>
    [Theory]
    [InlineData("User")]
    [InlineData("Admin")]
    [InlineData("Anonymous")]
    public async Task TheWeekdayHeader_IsNotPaintedWhite(string role)
    {
        IPage page = role == "Anonymous" ? await NewPageAsync() : (await NewSignedInPageAsync(role)).Page;
        await GotoAsync(page, $"/Calendar/{role}Index");
        await page.WaitForSelectorAsync(".fc-col-header-cell");

        string background = await page.EvaluateAsync<string>(
            "() => getComputedStyle(document.querySelector('#calendar .fc-scrollgrid-section-header > *')).backgroundColor");

        Assert.NotEqual("rgb(255, 255, 255)", background);
    }

    /// <summary>The "all day" checkbox of the event dialogs is a checkbox-sized box under the start of its label.</summary>
    [Theory]
    [InlineData("User")]
    [InlineData("Admin")]
    public async Task TheAllDayCheckbox_IsAPlainCheckbox(string role)
    {
        IPage page = await OpenAsync(role, 1400);
        await page.Locator(".fc-day-today, .fc-daygrid-day:not(.fc-day-other)").First.ClickAsync();
        await page.WaitForSelectorAsync("#createCalendarEventTaskDialogModal.show");

        LocatorBoundingBoxResult box = await BoxAsync(page, "#createCalendarEventAllDay");
        LocatorBoundingBoxResult label = await BoxAsync(page, "label[for=createCalendarEventAllDay]");

        Assert.True(box.Width <= 24, $"the checkbox is {box.Width}px wide");
        Assert.True(Math.Abs(box.X - label.X) <= 4, $"the checkbox starts at {box.X}, its label at {label.X}");
    }

    /// <summary>In the calendar dialog the time-zone select lines up with the name field (it sat outside the field grid).</summary>
    [Theory]
    [InlineData("User")]
    [InlineData("Admin")]
    public async Task TheCalendarDialog_LinesUpItsFields(string role)
    {
        IPage page = await OpenAsync(role, 1400);
        await page.EvaluateAsync("() => $('#createCalendarDialogModal').modal('show')");
        await page.WaitForSelectorAsync("#createCalendarDialogModal.show");

        LocatorBoundingBoxResult name = await BoxAsync(page, "#createCalendarName");
        LocatorBoundingBoxResult zone = await BoxAsync(page, "#createCalendarTimeZone");

        Assert.True(Math.Abs(zone.X - name.X) <= 1 && Math.Abs(zone.Width - name.Width) <= 1, $"name {name.X}+{name.Width}, zone {zone.X}+{zone.Width}");
    }

    /// <summary>The calendar selection dialog (browse calendars of interest / share calendars) keeps a usable width and small checkboxes.</summary>
    [Theory]
    [InlineData("User", "#browseCalendarsOfInterestDialogModal", "#allCheckBrowseCalendarsOfInterest")]
    [InlineData("Admin", "#setCalendarSharedDialogModal", null)]
    public async Task TheCalendarSelectionDialog_KeepsAUsableWidth(string role, string modal, string? checkbox)
    {
        IPage page = await OpenAsync(role, 1400);
        await page.EvaluateAsync("m => $(m).modal('show')", modal);
        await page.WaitForSelectorAsync($"{modal}.show");

        LocatorBoundingBoxResult dialog = await BoxAsync(page, $"{modal} .modal-content");
        Assert.True(dialog.Width >= 480, $"the dialog is {dialog.Width}px wide");
        if (checkbox is not null)
            Assert.True((await BoxAsync(page, checkbox)).Width <= 24);
    }
}
