using System.Text.Json;
using Maroik.Core.Contract.Dtos;
using Maroik.Website.Mappings;
using Maroik.Website.Models.ViewModels.Calendar;

namespace Maroik.Website.Tests.Mappings;

/// <summary>
/// Unit tests for <see cref="CalendarViewModelMapper"/> — UTC/local timezone conversion,
/// the FullCalendar exclusive-end-date grid convention for all-day events, and reminder
/// JSON (de)serialization.
/// </summary>
public class CalendarViewModelMapperTests
{
    /// <summary>An all-day event from <paramref name="start"/> to <paramref name="end"/>.</summary>
    private static CalendarEventResponse AllDayEvent(DateTime start, DateTime end) => new()
    {
        Id = 1,
        CalendarId = 1,
        Title = "Trip",
        AllDay = true,
        StartDate = start,
        EndDate = end
    };

    /// <summary>A timed event from <paramref name="start"/> to <paramref name="end"/> with optional per-end time zones.</summary>
    private static CalendarEventResponse TimedEvent(DateTime start, DateTime end, string? startTz = null, string? endTz = null) => new()
    {
        Id = 1,
        CalendarId = 1,
        Title = "Meeting",
        AllDay = false,
        StartDate = start,
        EndDate = end,
        StartDateTimeZoneIanaId = startTz,
        EndDateTimeZoneIanaId = endTz
    };

    // -- ToDisplayViewModel: an attachment ------------------------------------

    /// <summary>An attachment is described to the client by its name, extension and size; its id, event id and storage path are not carried over.</summary>
    [Fact]
    public void ToDisplayViewModel_DescribesAnAttachment_ByNameExtensionAndSizeOnly()
    {
        var dto = new CalendarEventAttachedFileDto { Id = 7, CalendarEventId = 9, Name = "report", Extension = ".zip", Size = 2048, Path = "upload/Calendar/x/report.zip" };

        CalendarEventAttachedFileOutputViewModel vm = dto.ToDisplayViewModel();

        Assert.Equal("report", vm.Name);
        Assert.Equal(".zip", vm.Extension);
        Assert.Equal(2048, vm.Size);
        Assert.Equal(["Extension", "Name", "Size"], typeof(CalendarEventAttachedFileOutputViewModel).GetProperties().Select(p => p.Name).Order());
    }

    // -- ToDisplayViewModel: all-day events -----------------------------------

    /// <summary>To display view model all day for grid end date is midnight of day after local end.</summary>
    [Fact]
    public void ToDisplayViewModel_AllDay_ForGrid_EndDateIsMidnightOfDayAfterLocalEnd()
    {
        var evt = AllDayEvent(
            new DateTime(2025, 6, 10, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2025, 6, 12, 0, 0, 0, DateTimeKind.Utc));

        var vm = evt.ToDisplayViewModel("UTC", "#3788d8");

        Assert.Equal(new DateTime(2025, 6, 10), vm.StartDate);
        Assert.Equal(new DateTime(2025, 6, 13), vm.EndDate); // exclusive end: day after the local end date
    }

    /// <summary>To display view model all day for grid sets display strings and empty time zone labels.</summary>
    [Fact]
    public void ToDisplayViewModel_AllDay_ForGrid_SetsDisplayStringsAndEmptyTimeZoneLabels()
    {
        var evt = AllDayEvent(
            new DateTime(2025, 6, 10, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2025, 6, 10, 0, 0, 0, DateTimeKind.Utc));

        var vm = evt.ToDisplayViewModel("UTC", "#3788d8");

        Assert.Equal("2025-06-10", vm.DisplayStartDate);
        Assert.Equal("2025-06-10", vm.DisplayEndDate);
        Assert.Equal("", vm.DisplayStartDateTimeZone);
        Assert.Equal("", vm.DisplayEndDateTimeZone);
        Assert.Equal("UTC", vm.StartDateTimeZoneIanaId);
        Assert.Equal("UTC", vm.EndDateTimeZoneIanaId);
    }

    /// <summary>To display view model all day not for grid does not set grid start end date.</summary>
    [Fact]
    public void ToDisplayViewModel_AllDay_NotForGrid_DoesNotSetGridStartEndDate()
    {
        var evt = AllDayEvent(
            new DateTime(2025, 6, 10, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2025, 6, 12, 0, 0, 0, DateTimeKind.Utc));

        var vm = evt.ToDisplayViewModel("UTC", "#3788d8", forGrid: false);

        Assert.Equal(default, vm.StartDate);
        Assert.Equal(default, vm.EndDate);
        Assert.Equal("2025-06-10", vm.DisplayStartDate);
    }

    // -- ToDisplayViewModel: timed events --------------------------------------

    /// <summary>
    /// A timed event's grid end is its real local end instant, NOT midnight of the next day —
    /// the exclusive-end-at-midnight rule is an all-day convention only. Applying it to timed
    /// events rendered every one as a block running to the following midnight.
    /// </summary>
    [Fact]
    public void ToDisplayViewModel_Timed_ForGrid_EndDateIsRealLocalEndInstant()
    {
        var evt = TimedEvent(
            new DateTime(2025, 6, 10, 9, 0, 0, DateTimeKind.Utc),
            new DateTime(2025, 6, 10, 17, 30, 0, DateTimeKind.Utc),
            "UTC", "UTC");

        var vm = evt.ToDisplayViewModel("UTC", "#3788d8");

        Assert.Equal(new DateTime(2025, 6, 10, 9, 0, 0), vm.StartDate);
        Assert.Equal(new DateTime(2025, 6, 10, 17, 30, 0), vm.EndDate);
        Assert.Equal("2025-06-10 09:00", vm.DisplayStartDate);
        Assert.Equal("2025-06-10 17:30", vm.DisplayEndDate);
    }

    /// <summary>
    /// An all-day event is stored as midnight UTC standing in for a bare calendar date; it must
    /// not be time-zone converted for display, or a viewer west of UTC sees the date a day early
    /// (and every open/save cycle walks it back further).
    /// </summary>
    [Fact]
    public void ToDisplayViewModel_AllDay_WestOfUtcViewer_DoesNotShiftDate()
    {
        var evt = AllDayEvent(
            new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc));

        var vm = evt.ToDisplayViewModel("America/New_York", "#3788d8");

        Assert.Equal("2026-01-15", vm.DisplayStartDate);
        Assert.Equal("2026-01-15", vm.DisplayEndDate);
        Assert.Equal(new DateTime(2026, 1, 15), vm.StartDate);
        Assert.Equal(new DateTime(2026, 1, 16), vm.EndDate); // exclusive end, no tz shift
    }

    /// <summary>To display view model timed not for grid returns before setting grid dates.</summary>
    [Fact]
    public void ToDisplayViewModel_Timed_NotForGrid_ReturnsBeforeSettingGridDates()
    {
        var evt = TimedEvent(
            new DateTime(2025, 6, 10, 9, 0, 0, DateTimeKind.Utc),
            new DateTime(2025, 6, 10, 17, 30, 0, DateTimeKind.Utc),
            "UTC", "UTC");

        var vm = evt.ToDisplayViewModel("UTC", "#3788d8", forGrid: false);

        Assert.Equal(default, vm.StartDate);
        Assert.Equal(default, vm.EndDate);
    }

    /// <summary>To display view model timed missing event time zone falls back to viewer time zone.</summary>
    [Fact]
    public void ToDisplayViewModel_Timed_MissingEventTimeZone_FallsBackToViewerTimeZone()
    {
        var evt = TimedEvent(
            new DateTime(2025, 6, 10, 9, 0, 0, DateTimeKind.Utc),
            new DateTime(2025, 6, 10, 17, 0, 0, DateTimeKind.Utc),
            startTz: null, endTz: null);

        var vm = evt.ToDisplayViewModel("UTC", "#3788d8");

        Assert.Equal("UTC", vm.StartDateTimeZoneIanaId);
        Assert.Equal("UTC", vm.EndDateTimeZoneIanaId);
    }

    /// <summary>To display view model unrecognized time zone display time zone falls back to raw iana id.</summary>
    [Fact]
    public void ToDisplayViewModel_UnrecognizedTimeZone_DisplayTimeZoneFallsBackToRawIanaId()
    {
        var evt = TimedEvent(
            new DateTime(2025, 6, 10, 9, 0, 0, DateTimeKind.Utc),
            new DateTime(2025, 6, 10, 17, 0, 0, DateTimeKind.Utc),
            "Not/ARealZone", "Not/ARealZone");

        var vm = evt.ToDisplayViewModel("UTC", "#3788d8");

        Assert.Equal("Not/ARealZone", vm.DisplayStartDateTimeZone);
        Assert.Equal("Not/ARealZone", vm.DisplayEndDateTimeZone);
    }

    /// <summary>To display view model copies id and color and calendar type.</summary>
    [Fact]
    public void ToDisplayViewModel_CopiesIdAndColorAndCalendarType()
    {
        var evt = TimedEvent(
            new DateTime(2025, 6, 10, 9, 0, 0, DateTimeKind.Utc),
            new DateTime(2025, 6, 10, 17, 0, 0, DateTimeKind.Utc),
            "UTC", "UTC");
        evt.Id = 42;
        evt.CalendarId = 7;

        var vm = evt.ToDisplayViewModel("UTC", "#abcdef", calendarType: "Other");

        Assert.Equal(42, vm.Id);
        Assert.Equal(7, vm.CalendarId);
        Assert.Equal("#abcdef", vm.HtmlColorCode);
        Assert.Equal("Other", vm.CalendarType);
    }

    // -- ToSerializedReminders --------------------------------------------------

    /// <summary>To serialized reminders serializes all fields.</summary>
    [Fact]
    public void ToSerializedReminders_SerializesAllFields()
    {
        List<CalendarEventReminderDto> reminders =
        [
            new()
            {
                Method = "Email",
                MinutesBeforeEvent = 30,
                HoursBeforeEvent = 1,
                DaysBeforeEvent = 2,
                WeeksBeforeEvent = 1,
                TimesBeforeEvent = new TimeOnly(9, 0)
            }
        ];

        string json = reminders.ToSerializedReminders();
        var roundTripped = JsonSerializer.Deserialize<List<CalendarReminderDto>>(json)!;

        Assert.Equal("Email", roundTripped[0].Method);
        Assert.Equal(30, roundTripped[0].MinutesBeforeEvent);
        Assert.Equal(1, roundTripped[0].HoursBeforeEvent);
        Assert.Equal(2, roundTripped[0].DaysBeforeEvent);
        Assert.Equal(1, roundTripped[0].WeeksBeforeEvent);
        Assert.Equal(new TimeOnly(9, 0), roundTripped[0].TimesBeforeEvent);
    }

    /// <summary>To serialized reminders null method defaults to empty string.</summary>
    [Fact]
    public void ToSerializedReminders_NullMethod_DefaultsToEmptyString()
    {
        List<CalendarEventReminderDto> reminders = [new() { Method = null }];

        string json = reminders.ToSerializedReminders();
        var roundTripped = JsonSerializer.Deserialize<List<CalendarReminderDto>>(json)!;

        Assert.Equal("", roundTripped[0].Method);
    }

    // -- PopulateTimeData ---------------------------------------------------------

    /// <summary>Populate time data generates A24 hour grid in fifteen minute increments.</summary>
    [Fact]
    public void PopulateTimeData_GeneratesA24HourGridInFifteenMinuteIncrements()
    {
        var vm = new CalendarOutputViewModel();

        vm.PopulateTimeData("UTC");

        Assert.Equal(96, vm.TimeIntervals.Count); // 24h * 4 (15-min slots)
        Assert.Equal("00:00", vm.TimeIntervals[0]);
        Assert.Equal("23:45", vm.TimeIntervals[^1]);
    }

    /// <summary>Populate time data current interval is consistent with current hour and minute.</summary>
    [Fact]
    public void PopulateTimeData_CurrentIntervalIsConsistentWithCurrentHourAndMinute()
    {
        var vm = new CalendarOutputViewModel();

        vm.PopulateTimeData("UTC");

        int roundedMinute = (int)Math.Round(vm.CurrentMinute / 15.0) * 15;
        int expectedHour = vm.CurrentHour;
        if (roundedMinute == 60) { expectedHour = (expectedHour + 1) % 24; roundedMinute = 0; }
        string expectedInterval = $"{expectedHour:D2}:{roundedMinute:D2}";

        Assert.Equal(expectedInterval, vm.CurrentInterval);
    }

    // -- ToCalendarEventRequest: all-day ------------------------------------------

    /// <summary>To calendar event request all day parses dates as utc midnight and clears time zones.</summary>
    [Fact]
    public void ToCalendarEventRequest_AllDay_ParsesDatesAsUtcMidnightAndClearsTimeZones()
    {
        var vm = new CalendarEventInputViewModel
        {
            AllDay = true,
            StartDate = "2025-06-10",
            EndDate = "2025-06-12",
            StartDateTimeZoneIanaId = "Asia/Seoul",
            EndDateTimeZoneIanaId = "Asia/Seoul"
        };

        var req = vm.ToCalendarEventRequest();

        Assert.Equal(new DateTime(2025, 6, 10, 0, 0, 0, DateTimeKind.Utc), req.StartDate);
        Assert.Equal(new DateTime(2025, 6, 12, 0, 0, 0, DateTimeKind.Utc), req.EndDate);
        Assert.Null(req.StartDateTimeZoneIanaId);
        Assert.Null(req.EndDateTimeZoneIanaId);
    }

    // -- ToCalendarEventRequest: timed --------------------------------------------

    /// <summary>To calendar event request timed converts local date time to utc using supplied time zone.</summary>
    [Fact]
    public void ToCalendarEventRequest_Timed_ConvertsLocalDateTimeToUtcUsingSuppliedTimeZone()
    {
        var vm = new CalendarEventInputViewModel
        {
            AllDay = false,
            StartDate = "2025-06-10 09:00",
            EndDate = "2025-06-10 17:00",
            StartDateTimeZoneIanaId = "UTC",
            EndDateTimeZoneIanaId = "UTC"
        };

        var req = vm.ToCalendarEventRequest();

        Assert.Equal(new DateTime(2025, 6, 10, 9, 0, 0, DateTimeKind.Utc), req.StartDate);
        Assert.Equal(new DateTime(2025, 6, 10, 17, 0, 0, DateTimeKind.Utc), req.EndDate);
        Assert.Equal("UTC", req.StartDateTimeZoneIanaId);
    }

    /// <summary>To calendar event request timed unrecognized time zone returns parsed local value unchanged.</summary>
    [Fact]
    public void ToCalendarEventRequest_Timed_UnrecognizedTimeZone_ReturnsParsedLocalValueUnchanged()
    {
        var vm = new CalendarEventInputViewModel
        {
            AllDay = false,
            StartDate = "2025-06-10 09:00",
            EndDate = "2025-06-10 17:00",
            StartDateTimeZoneIanaId = "Not/ARealZone",
            EndDateTimeZoneIanaId = "Not/ARealZone"
        };

        var req = vm.ToCalendarEventRequest();

        Assert.Equal(new DateTime(2025, 6, 10, 9, 0, 0), req.StartDate);
    }

    /// <summary>To calendar event request copies title description location status.</summary>
    [Fact]
    public void ToCalendarEventRequest_CopiesTitleDescriptionLocationStatus()
    {
        var vm = new CalendarEventInputViewModel
        {
            Id = 5,
            CalendarId = 2,
            AllDay = true,
            StartDate = "2025-06-10",
            EndDate = "2025-06-10",
            Title = "Trip",
            Description = "Beach",
            Location = "Busan",
            Status = "Confirmed"
        };

        var req = vm.ToCalendarEventRequest();

        Assert.Equal(5, req.Id);
        Assert.Equal(2, req.CalendarId);
        Assert.Equal("Trip", req.Title);
        Assert.Equal("Beach", req.Description);
        Assert.Equal("Busan", req.Location);
        Assert.Equal("Confirmed", req.Status);
    }

    // -- ToReminderInfoList --------------------------------------------------------

    /// <summary>To reminder info list null or empty returns empty list.</summary>
    [Fact]
    public void ToReminderInfoList_NullOrEmpty_ReturnsEmptyList()
    {
        Assert.Empty(((string?)null).ToReminderInfoList());
        Assert.Empty("".ToReminderInfoList());
    }

    /// <summary>To reminder info list parses fields and time of day.</summary>
    [Fact]
    public void ToReminderInfoList_ParsesFieldsAndTimeOfDay()
    {
        string json = JsonSerializer.Serialize(new[]
        {
            new
            {
                Method = "Push",
                MinutesBeforeEvent = 15,
                HoursBeforeEvent = 0,
                DaysBeforeEvent = 1,
                WeeksBeforeEvent = 0,
                TimesBeforeEvent = "09:30"
            }
        });

        var reminders = json.ToReminderInfoList();

        var item = Assert.Single(reminders);
        Assert.Equal("Push", item.Method);
        Assert.Equal(15, reminders[0].MinutesBeforeEvent);
        Assert.Equal(1, reminders[0].DaysBeforeEvent);
        Assert.Equal(new TimeOnly(9, 30), reminders[0].TimesBeforeEvent);
    }

    /// <summary>To reminder info list empty times before event leaves times before event null.</summary>
    [Fact]
    public void ToReminderInfoList_EmptyTimesBeforeEvent_LeavesTimesBeforeEventNull()
    {
        string json = JsonSerializer.Serialize(new[]
        {
            new
            {
                Method = "Push",
                MinutesBeforeEvent = 15,
                HoursBeforeEvent = 0,
                DaysBeforeEvent = 0,
                WeeksBeforeEvent = 0,
                TimesBeforeEvent = ""
            }
        });

        var reminders = json.ToReminderInfoList();

        Assert.Null(reminders[0].TimesBeforeEvent);
    }

    /// <summary>
    /// Regression test: an out-of-range or malformed "HH:mm" string (never expected from the real
    /// client, but this reads directly from client-supplied JSON) must parse to null instead of
    /// throwing. Before <see cref="CalendarReminderDto.TimesBeforeEvent"/> was <see cref="TimeOnly"/>,
    /// this kind of value could reach the service layer as an out-of-range <see cref="TimeSpan"/> and
    /// crash on the unchecked <c>TimeOnly.FromTimeSpan</c> conversion instead of failing gracefully.
    /// </summary>
    [Theory]
    [InlineData("25:99")]
    [InlineData("-1:30")]
    [InlineData("not-a-time")]
    public void ToReminderInfoList_OutOfRangeOrMalformedTimeString_YieldsNullTimesBeforeEvent(string timesBeforeEvent)
    {
        string json = JsonSerializer.Serialize(new[]
        {
            new
            {
                Method = "Push",
                MinutesBeforeEvent = (int?)null,
                HoursBeforeEvent = (int?)null,
                DaysBeforeEvent = (int?)null,
                WeeksBeforeEvent = (int?)null,
                TimesBeforeEvent = timesBeforeEvent
            }
        });

        var reminders = json.ToReminderInfoList();

        Assert.Null(reminders[0].TimesBeforeEvent);
    }

    /// <summary>
    /// The client only ever fills in the one lead-time field the user picked and sends the other
    /// three (plus TimesBeforeEvent, for a non-all-day event) as JSON null. Every unselected field
    /// must round-trip as null rather than 0, or the reminder's "exactly one lead-time field set"
    /// domain invariant is violated on every save.
    /// </summary>
    [Fact]
    public void ToReminderInfoList_UnselectedFieldsAsJsonNull_RemainNull()
    {
        const string json = """
            [{"Method":"Push","MinutesBeforeEvent":15,"HoursBeforeEvent":null,"DaysBeforeEvent":null,"WeeksBeforeEvent":null,"TimesBeforeEvent":null}]
            """;

        var reminders = json.ToReminderInfoList();

        var item = Assert.Single(reminders);
        Assert.Equal(15, item.MinutesBeforeEvent);
        Assert.Null(reminders[0].HoursBeforeEvent);
        Assert.Null(reminders[0].DaysBeforeEvent);
        Assert.Null(reminders[0].WeeksBeforeEvent);
        Assert.Null(reminders[0].TimesBeforeEvent);
    }
}
