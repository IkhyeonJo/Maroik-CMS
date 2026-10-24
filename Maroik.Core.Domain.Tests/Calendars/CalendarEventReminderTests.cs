using ErrorOr;
using Maroik.Core.Domain.Calendars;
namespace Maroik.Core.Domain.Tests.Calendars;

/// <summary>
/// Unit tests for <see cref="CalendarEventReminder"/>: creation validation (exactly one lead time, within range), the fire time it
/// is scheduled for (<see cref="ReminderSchedule"/>), a reminder whose time has already passed being handled at once (it would
/// arrive late), marking a reminder handled, and reconstitution from trusted data. A reminder is always an e-mail.
/// </summary>
public class CalendarEventReminderTests
{
    /// <summary>"Now" in these tests.</summary>
    private static readonly DateTime _now = new(2026, 10, 24, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>Event 1, timed, starting 2026-11-02 09:00 UTC.</summary>
    private static readonly CalendarEvent _event = CalendarEvent.Reconstitute(1, 7, "Dentist", null, false,
        new DateTime(2026, 11, 2, 9, 0, 0), new DateTime(2026, 11, 2, 10, 0, 0), "UTC", "UTC", null, "Busy", null, _now, _now);

    /// <summary>Creates a reminder for <see cref="_event"/> in a UTC calendar.</summary>
    private static ErrorOr<CalendarEventReminder> Create(long? minutes, long? hours, long? days, long? weeks, TimeOnly? time = null) =>
        CalendarEventReminder.Create(1, _event, "UTC", minutes, hours, days, weeks, time, _now);

    // -- Create ---------------------------------------------------------------

    /// <summary>A valid reminder is an e-mail for its event, scheduled by <see cref="ReminderSchedule"/>, not yet handled.</summary>
    [Fact]
    public void Create_ReturnsAnEmailReminder_ScheduledBeforeTheEvent()
    {
        var result = Create(30, null, null, null);

        Assert.False(result.IsError);
        Assert.Equal(1, result.Value.CalendarEventId);
        Assert.Equal(ReminderMethods.Email, result.Value.Method);
        Assert.Equal(30, result.Value.MinutesBeforeEvent);
        Assert.Equal(new DateTime(2026, 11, 2, 8, 30, 0, DateTimeKind.Utc), result.Value.FireAtUtc);
        Assert.Null(result.Value.HandledAt);
    }

    /// <summary>A reminder whose time has already passed when it is saved is handled at once: sending it now would arrive late.</summary>
    [Fact]
    public void Create_HandlesAtOnce_AReminderWhoseTimeHasPassed()
    {
        var result = Create(null, null, null, 2); // 2026-10-19, before "now"

        Assert.Equal(_now, result.Value.HandledAt);
    }

    /// <summary>A reminder due exactly now is still sent.</summary>
    [Fact]
    public void Create_DoesNotHandle_AReminderDueNow()
    {
        var result = Create(12_780, null, null, null); // 8 days 21 hours before: 2026-10-24 12:00

        Assert.Equal(_now, result.Value.FireAtUtc);
        Assert.Null(result.Value.HandledAt);
    }

    /// <summary>Create returns error, when no lead time set.</summary>
    [Fact]
    public void Create_ReturnsError_WhenNoLeadTimeSet()
    {
        var result = Create(null, null, null, null);

        Assert.True(result.IsError);
        Assert.Equal("Reminder.InvalidLeadTime", result.FirstError.Code);
    }

    /// <summary>Create returns error, when more than one lead-time field is set.</summary>
    [Fact]
    public void Create_ReturnsError_WhenMultipleLeadTimeFieldsSet()
    {
        var result = Create(30, 2, null, null);

        Assert.True(result.IsError);
        Assert.Equal("Reminder.InvalidLeadTime", result.FirstError.Code);
    }

    /// <summary>Create returns error, when the lead time is past the allowed maximum for its unit.</summary>
    [Theory]
    [InlineData(40321L, null, null, null)]
    [InlineData(null, 673L, null, null)]
    [InlineData(null, null, 29L, null)]
    [InlineData(null, null, null, 5L)]
    public void Create_ReturnsError_WhenLeadTimeOutOfRange(long? minutes, long? hours, long? days, long? weeks)
    {
        var result = Create(minutes, hours, days, weeks);

        Assert.True(result.IsError);
        Assert.Equal("Reminder.LeadTimeOutOfRange", result.FirstError.Code);
    }

    /// <summary>Create returns error, when the lead time is negative.</summary>
    [Fact]
    public void Create_ReturnsError_WhenLeadTimeNegative()
    {
        var result = Create(-1, null, null, null);

        Assert.True(result.IsError);
        Assert.Equal("Reminder.LeadTimeOutOfRange", result.FirstError.Code);
    }

    /// <summary>Create succeeds, when the lead time sits exactly on the allowed maximum for its unit.</summary>
    [Theory]
    [InlineData(40320L, null, null, null)]
    [InlineData(null, 672L, null, null)]
    [InlineData(null, null, 28L, null)]
    [InlineData(null, null, null, 4L)]
    public void Create_Succeeds_WhenLeadTimeAtMaxBoundary(long? minutes, long? hours, long? days, long? weeks)
    {
        Assert.False(Create(minutes, hours, days, weeks).IsError);
    }

    /// <summary>Create keeps each unit's value.</summary>
    [Fact]
    public void Create_KeepsTheLeadTimeAndTheTimeOfDay()
    {
        var time = new TimeOnly(9, 0);

        Assert.Equal(2, Create(null, 2, null, null).Value.HoursBeforeEvent);
        Assert.Equal(1, Create(null, null, 1, null).Value.DaysBeforeEvent);
        Assert.Equal(1, Create(null, null, null, 1).Value.WeeksBeforeEvent);
        Assert.Equal(time, Create(30, null, null, null, time).Value.TimesBeforeEvent);
    }

    /// <summary>An all-day event's reminder is scheduled in the calendar's time zone.</summary>
    [Fact]
    public void Create_SchedulesAnAllDayEvent_InTheCalendarsTimeZone()
    {
        var allDay = CalendarEvent.Reconstitute(2, 7, "Trip", null, true,
            new DateTime(2026, 11, 2), new DateTime(2026, 11, 3), null, null, null, "Busy", null, _now, _now);

        var result = CalendarEventReminder.Create(2, allDay, "Asia/Seoul", null, null, 1, null, new TimeOnly(9, 0), _now);

        Assert.Equal(new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc), result.Value.FireAtUtc);
    }

    // -- MarkHandled -------------------------------------------------------------

    /// <summary>MarkHandled records when the reminder was handled.</summary>
    [Fact]
    public void MarkHandled_RecordsTheTime()
    {
        var reminder = Create(30, null, null, null).Value;

        reminder.MarkHandled(_now.AddDays(9));

        Assert.Equal(_now.AddDays(9), reminder.HandledAt);
    }

    // -- Reconstitute -----------------------------------------------------------

    /// <summary>Reconstitute rebuilds reminder without validation.</summary>
    [Fact]
    public void Reconstitute_RebuildsReminder_WithoutValidation()
    {
        DateTime fire = new(2026, 11, 2, 8, 30, 0), handled = new(2026, 11, 2, 8, 31, 0);

        var reminder = CalendarEventReminder.Reconstitute(5, 1, "Email", 30, null, null, null, null, fire, handled);

        Assert.Equal((5L, 1L, "Email", (long?)30), (reminder.Id, reminder.CalendarEventId, reminder.Method, reminder.MinutesBeforeEvent));
        Assert.Equal((DateTimeKind.Utc, DateTimeKind.Utc), (reminder.FireAtUtc.Kind, reminder.HandledAt!.Value.Kind));
        Assert.Equal((fire, handled), (reminder.FireAtUtc, reminder.HandledAt.Value));
    }

    /// <summary>Reconstitute defaults a null method to an e-mail.</summary>
    [Fact]
    public void Reconstitute_DefaultsNullMethod_ToEmail()
    {
        var reminder = CalendarEventReminder.Reconstitute(1, 1, null, 5, null, null, null, null, _now, null);

        Assert.Equal(ReminderMethods.Email, reminder.Method);
        Assert.Null(reminder.HandledAt);
    }
}
