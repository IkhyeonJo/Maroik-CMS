using Maroik.Core.Domain.Calendar;
namespace Maroik.Core.Domain.Tests.Calendar;

/// <summary>
/// Unit tests for <see cref="Maroik.Core.Domain.Calendar.CalendarEvent"/>.
/// Covers event creation and update.
/// </summary>
public class CalendarEventTests
{
    /// <summary>The fixed "current time" every domain call in this class receives.</summary>
    private static readonly DateTime Now = new(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>Start of the event built by <see cref="ValidEvent"/>.</summary>
    private static readonly DateTime _start = new(2025, 6, 1, 9, 0, 0, DateTimeKind.Utc);
    /// <summary>End of the event built by <see cref="ValidEvent"/> (one hour after <see cref="_start"/>).</summary>
    private static readonly DateTime _end = new(2025, 6, 1, 10, 0, 0, DateTimeKind.Utc);

    /// <summary>A persisted one-hour UTC event titled "Team Meeting".</summary>
    private static CalendarEvent ValidEvent(long id = 1, long calendarId = 10) =>
        CalendarEvent.Reconstitute(id, calendarId, "Team Meeting", null, false, _start, _end, "UTC", "UTC", null, null, null, DateTime.UtcNow, DateTime.UtcNow);

    // -- Create ---------------------------------------------------------------

    /// <summary>Create returns calendar event, when valid.</summary>
    [Fact]
    public void Create_ReturnsCalendarEvent_WhenValid()
    {
        var result = CalendarEvent.Create(10, "Stand-up", null, false, _start, _end, "UTC", "UTC", null, null, Now);

        Assert.False(result.IsError);
        Assert.Equal("Stand-up", result.Value.Title);
        Assert.Equal(10, result.Value.CalendarId);
    }

    /// <summary>A location exactly at the persisted 255-character limit is accepted on create and update.</summary>
    [Fact]
    public void Location_AtTheMaximumLength_IsAccepted()
    {
        string location = new('l', 255);

        Assert.False(CalendarEvent.Create(10, "Stand-up", null, false, _start, _end, null, null, location, null, Now).IsError);
        Assert.False(ValidEvent().Update("Team Meeting", null, false, _start, _end, null, null, location, null, Now).IsError);
    }

    /// <summary>A location over 255 characters is a clean validation error on create and update.</summary>
    [Fact]
    public void Location_LongerThanTheColumn_IsRejected()
    {
        string location = new('l', 256);

        var created = CalendarEvent.Create(10, "Stand-up", null, false, _start, _end, null, null, location, null, Now);
        var updated = ValidEvent().Update("Team Meeting", null, false, _start, _end, null, null, location, null, Now);

        Assert.Equal("CalendarEvent.LocationTooLong", created.FirstError.Code);
        Assert.Equal("CalendarEvent.LocationTooLong", updated.FirstError.Code);
    }

    /// <summary>Create returns error, when title empty.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_ReturnsError_WhenTitleEmpty(string? title)
    {
        var result = CalendarEvent.Create(10, title, null, false, _start, _end, null, null, null, null, Now);

        Assert.True(result.IsError);
        Assert.Equal("CalendarEvent.TitleEmpty", result.FirstError.Code);
    }

    /// <summary>Create returns error, when title too long.</summary>
    [Fact]
    public void Create_ReturnsError_WhenTitleTooLong()
    {
        var result = CalendarEvent.Create(10, new string('x', 101), null, false, _start, _end, null, null, null, null, Now);

        Assert.True(result.IsError);
        Assert.Equal("CalendarEvent.TitleTooLong", result.FirstError.Code);
    }

    /// <summary>Create returns error, when description too long.</summary>
    [Fact]
    public void Create_ReturnsError_WhenDescriptionTooLong()
    {
        var result = CalendarEvent.Create(10, "Title", new string('x', 16385), false, _start, _end, null, null, null, null, Now);

        Assert.True(result.IsError);
        Assert.Equal("CalendarEvent.DescriptionTooLong", result.FirstError.Code);
    }

    /// <summary>Create returns error when end before start and not all day.</summary>
    [Fact]
    public void Create_ReturnsError_WhenEndBeforeStart_AndNotAllDay()
    {
        var result = CalendarEvent.Create(10, "Title", null, false, _end, _start, null, null, null, null, Now);

        Assert.True(result.IsError);
        Assert.Equal("CalendarEvent.InvalidDateRange", result.FirstError.Code);
    }

    /// <summary>Create succeeds when the end time is before the start time on the same day and the event is all-day.</summary>
    [Fact]
    public void Create_Succeeds_WhenEndBeforeStart_AndAllDay()
    {
        var result = CalendarEvent.Create(10, "All Day", null, true, _end, _start, null, null, null, null, Now);

        Assert.False(result.IsError);
    }

    /// <summary>Create rejects an all-day event whose end date is on an earlier day than the start date.</summary>
    [Fact]
    public void Create_ReturnsError_WhenEndDateBeforeStartDate_AndAllDay()
    {
        var result = CalendarEvent.Create(10, "All Day", null, true, _start, _start.AddDays(-1), null, null, null, null, Now);

        Assert.True(result.IsError);
        Assert.Equal("CalendarEvent.InvalidDateRange", result.FirstError.Code);
    }

    /// <summary>Create rejects an unrecognized per-endpoint time-zone ID.</summary>
    [Fact]
    public void Create_ReturnsError_WhenTimeZoneIdInvalid()
    {
        var result = CalendarEvent.Create(10, "Title", null, false, _start, _end, "Not/AZone", "UTC", null, null, Now);

        Assert.True(result.IsError);
        Assert.Equal("CalendarEvent.InvalidTimeZone", result.FirstError.Code);
    }

    /// <summary>
    /// The rejection is built via <c>LocalizableError</c>: its Metadata carries the composite-format
    /// resource template and the raw offending zone separately, so <c>ServiceResult.FromError</c> can
    /// hand the UI something resx-localizable instead of the already-baked, value-embedding sentence
    /// in <see cref="ErrorOr.Error.Description"/>.
    /// </summary>
    [Fact]
    public void Create_ReturnsError_WithLocalizableMetadata_WhenTimeZoneIdInvalid()
    {
        var result = CalendarEvent.Create(10, "Title", null, false, _start, _end, "Not/AZone", "UTC", null, null, Now);

        Assert.Equal("'Not/AZone' is not a recognised time-zone ID.", result.FirstError.Description);
        Assert.Equal("'{0}' is not a recognised time-zone ID.", result.FirstError.Metadata!["ResourceKey"]);
        Assert.Equal(["Not/AZone"], (object[])result.FirstError.Metadata["ResourceArgs"]);
    }

    /// <summary>
    /// Regression: Create rejects a <c>Status</c> value outside <see cref="CalendarEventStatuses"/>'s
    /// known members — previously any non-empty string was accepted here and would only fail later
    /// with a raw DB <c>CalendarEvent_Status_check</c> exception on save. A null/blank status is
    /// still allowed (the repository maps it to the "Busy" default before persisting).
    /// </summary>
    [Theory]
    [InlineData("Tentative")]
    [InlineData("busy")]
    public void Create_ReturnsError_WhenStatusNotRecognised(string status)
    {
        var result = CalendarEvent.Create(10, "Title", null, false, _start, _end, null, null, null, status, Now);

        Assert.True(result.IsError);
        Assert.Equal("CalendarEvent.StatusInvalid", result.FirstError.Code);
    }

    /// <summary>Create still succeeds with a null or blank status -- the repository defaults it before persisting.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_Succeeds_WhenStatusNullOrBlank(string? status)
    {
        var result = CalendarEvent.Create(10, "Title", null, false, _start, _end, null, null, null, status, Now);

        Assert.False(result.IsError);
    }

    // -- Update ---------------------------------------------------------------

    /// <summary>Update succeeds, when valid.</summary>
    [Fact]
    public void Update_Succeeds_WhenValid()
    {
        var ev = ValidEvent();
        var newEnd = _end.AddHours(1);

        var result = ev.Update("Updated Title", "desc", false, _start, newEnd, "Asia/Seoul", "Asia/Seoul", "Room 1", "Busy", Now);

        Assert.False(result.IsError);
        Assert.Equal("Updated Title", ev.Title);
        Assert.Equal("Room 1", ev.Location);
    }

    /// <summary>Update returns error, when end before start.</summary>
    [Fact]
    public void Update_ReturnsError_WhenEndBeforeStart()
    {
        var ev = ValidEvent();

        var result = ev.Update("Title", null, false, _end, _start, null, null, null, null, Now);

        Assert.True(result.IsError);
        Assert.Equal("CalendarEvent.InvalidDateRange", result.FirstError.Code);
    }

    /// <summary>Regression: Update rejects a <c>Status</c> value outside <see cref="CalendarEventStatuses"/>'s known members (see <see cref="Create_ReturnsError_WhenStatusNotRecognised"/>).</summary>
    [Fact]
    public void Update_ReturnsError_WhenStatusNotRecognised()
    {
        var ev = ValidEvent();

        var result = ev.Update("Title", null, false, _start, _end, null, null, null, "Tentative", Now);

        Assert.True(result.IsError);
        Assert.Equal("CalendarEvent.StatusInvalid", result.FirstError.Code);
    }

    // -- Update: length limits (the shared rules of Create and Update) -----------------

    /// <summary>Update refuses an over-long title.</summary>
    [Fact]
    public void Update_ReturnsError_WhenTitleTooLong()
    {
        var result = ValidEvent().Update(new string('x', 101), null, false, _start, _end, null, null, null, null, Now);

        Assert.True(result.IsError);
        Assert.Equal("CalendarEvent.TitleTooLong", result.FirstError.Code);
    }

    /// <summary>Update refuses an over-long description.</summary>
    [Fact]
    public void Update_ReturnsError_WhenDescriptionTooLong()
    {
        var result = ValidEvent().Update("Title", new string('x', 16385), false, _start, _end, null, null, null, null, Now);

        Assert.True(result.IsError);
        Assert.Equal("CalendarEvent.DescriptionTooLong", result.FirstError.Code);
    }
}
