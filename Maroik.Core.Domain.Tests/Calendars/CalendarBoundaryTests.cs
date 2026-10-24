using Maroik.Core.Domain.Calendars;
using Maroik.Core.Domain.Primitives;
using Maroik.Core.Domain.Tests.Fixtures;
using DomainCalendar = Maroik.Core.Domain.Calendars.Calendar;

namespace Maroik.Core.Domain.Tests.Calendars;

/// <summary>
/// Exact limits and round trips of the calendar rules: names, event texts and attachments at their limits, an event
/// that ends when it starts, reminder errors, and reconstituted calendars and events keeping every field.
/// </summary>
public class CalendarBoundaryTests
{
    /// <summary>The fixed "current time" every domain call in this class receives.</summary>
    private static readonly DateTime _now = new(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>Owner e-mail used by every aggregate built in these tests.</summary>
    private const string Email = "user@example.com";

    /// <summary>The fixed "current time" every domain call in this class receives.</summary>
    private static readonly DateTime Now = new(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>A string of exactly <paramref name="length"/> characters.</summary>
    private static string Text(int length) => new('x', length);

    /// <summary>Start time of the calendar events built by <see cref="Event"/>.</summary>
    private static readonly DateTime _start = new(2026, 1, 1, 9, 0, 0);

    /// <summary>Result of <c>CalendarEvent.Create</c> for a one-hour UTC event with the given text fields and status.</summary>
    private static ErrorOr.ErrorOr<CalendarEvent> Event(string title = "t", string? description = null, string? location = null, string? status = null) =>
        CalendarEvent.Create(1, title, description, false, _start, _start.AddHours(1), "UTC", "UTC", location, status, Now);

    /// <summary>Verifies that <c>Reassign</c> sets the new calendar and recurrence IDs and does not move <c>Updated</c> backwards.</summary>
    [Fact]
    public void CalendarEvent_Reassign_MovesTheEventAndTouchesUpdated()
    {
        DateTime start = new(2026, 1, 1, 9, 0, 0);
        CalendarEvent ev = CalendarEvent.Create(1, "t", null, false, start, start.AddHours(1), "UTC", "UTC", null, null, _now).Value;
        DateTime before = ev.Updated;

        ev.Reassign(5, 9, _now);

        Assert.Equal(5, ev.CalendarId);
        Assert.Equal(9, ev.RecurrenceId);
        Assert.True(ev.Updated >= before);
    }

    /// <summary>Verifies the lead-time errors of <c>CalendarEventReminder.Create</c>.</summary>
    [Fact]
    public void Reminder_Create_ErrorContract()
    {
        DateTime now = new(2026, 10, 24, 0, 0, 0, DateTimeKind.Utc);
        CalendarEvent ev = CalendarEvent.Reconstitute(1, 1, "T", null, false, now.AddDays(1), now.AddDays(1), "UTC", "UTC", null, "Busy", null, now, now);

        ErrorAssert.Validation(CalendarEventReminder.Create(1, ev, "UTC", null, null, null, null, null, now), "Reminder.InvalidLeadTime", "Exactly one lead-time field (minutes/hours/days/weeks) must be set.");
        ErrorAssert.Validation(CalendarEventReminder.Create(1, ev, "UTC", 5, 1, null, null, null, now), "Reminder.InvalidLeadTime", "Exactly one lead-time field (minutes/hours/days/weeks) must be set.");
        ErrorAssert.Validation(CalendarEventReminder.Create(1, ev, "UTC", null, null, null, 5, null, now), "Reminder.LeadTimeOutOfRange",
            "Reminder lead time is outside the allowed range (0 to 28 days before the event).");
    }

    /// <summary>Verifies that <c>Calendar.Reconstitute</c> restores every field.</summary>
    [Fact]
    public void Calendar_Reconstitute_RestoresEveryField()
    {
        DateTime created = new(2025, 1, 2, 3, 4, 5), updated = new(2025, 6, 7, 8, 9, 10);

        DomainCalendar calendar = DomainCalendar.Reconstitute(4, Email, "Work", "desc", "Asia/Seoul", "#112233", created, updated);

        Assert.Equal((4L, Email, "Work", "desc", "Asia/Seoul", "#112233", created, updated),
            (calendar.Id, calendar.AccountEmail.Value, calendar.Name, calendar.Description, calendar.TimeZone.Value, calendar.ColorCode.Value, calendar.Created, calendar.Updated));
    }

    /// <summary>Verifies that <c>CalendarEvent.Reconstitute</c> restores every field.</summary>
    [Fact]
    public void CalendarEvent_Reconstitute_RestoresEveryField()
    {
        DateTime start = new(2026, 1, 1, 9, 0, 0), end = new(2026, 1, 1, 10, 0, 0), created = new(2025, 1, 2), updated = new(2025, 3, 4);

        CalendarEvent ev = CalendarEvent.Reconstitute(6, 2, "Title", "desc", true, start, end, "UTC", "Asia/Seoul", "Room", "Confirmed", 9, created, updated);

        Assert.Equal((6L, 2L, "Title", "desc", true, start, end, "UTC", "Asia/Seoul", "Room", "Confirmed", (long?)9, created, updated),
            (ev.Id, ev.CalendarId, ev.Title, ev.Description, ev.AllDay, ev.StartDate, ev.EndDate, ev.StartDateTimeZoneIanaId, ev.EndDateTimeZoneIanaId, ev.Location, ev.Status, ev.RecurrenceId, ev.Created, ev.Updated));
    }

    /// <summary>Verifies that a calendar name at the limit is accepted, and that too-long, empty and angle-bracket names are refused.</summary>
    [Fact]
    public void Calendar_Create_AcceptsANameAtTheLimit_AndRefusesOneMore()
    {
        Assert.False(DomainCalendar.Create("a@b.com", Text(ShortTextPolicy.MaxLength), null, "UTC", "#112233", Now).IsError);

        ErrorAssert.Validation(DomainCalendar.Create("a@b.com", Text(ShortTextPolicy.MaxLength + 1), null, "UTC", "#112233", Now),
            "Calendar.NameTooLong", "Calendar name must be {0} characters or fewer.", ShortTextPolicy.MaxLength);
        ErrorAssert.Validation(DomainCalendar.Create("a@b.com", " ", null, "UTC", "#112233", Now),
            "Calendar.NameEmpty", "Calendar name cannot be empty.");
        ErrorAssert.Validation(DomainCalendar.Create("a@b.com", "a<b", null, "UTC", "#112233", Now),
            "Calendar.NameInvalid", "Calendar name cannot contain angle brackets or control characters.");
    }

    /// <summary>
    /// A calendar description is held to the same body limit as an event description, on create and on update: the forms cap it at
    /// that length, and without the rule a direct POST could store any length.
    /// </summary>
    [Fact]
    public void Calendar_Description_AcceptsTheBodyLimit_AndRefusesOneMore()
    {
        Assert.False(DomainCalendar.Create("a@b.com", "n", Text(TitledContentPolicy.MaxBodyLength), "UTC", "#112233", Now).IsError);

        ErrorAssert.Validation(DomainCalendar.Create("a@b.com", "n", Text(TitledContentPolicy.MaxBodyLength + 1), "UTC", "#112233", Now),
            "Calendar.DescriptionTooLong", "Calendar description must be {0} characters or fewer.", TitledContentPolicy.MaxBodyLength);

        DomainCalendar calendar = DomainCalendar.Create("a@b.com", "n", null, "UTC", "#112233", Now).Value;
        Assert.False(calendar.Update("n", Text(TitledContentPolicy.MaxBodyLength), "UTC", "#112233", Now).IsError);
        ErrorAssert.Validation(calendar.Update("n", Text(TitledContentPolicy.MaxBodyLength + 1), "UTC", "#112233", Now),
            "Calendar.DescriptionTooLong", "Calendar description must be {0} characters or fewer.", TitledContentPolicy.MaxBodyLength);
        Assert.Equal(TitledContentPolicy.MaxBodyLength, calendar.Description?.Length);
    }

    /// <summary>Verifies the title / description / location length limits, the empty title and the unknown status on <c>CalendarEvent.Create</c>.</summary>
    [Fact]
    public void CalendarEvent_Create_AcceptsTextAtTheLimits_AndRefusesOneMore()
    {
        Assert.False(Event(Text(TitledContentPolicy.MaxTitleLength), Text(TitledContentPolicy.MaxBodyLength), Text(ShortTextPolicy.MaxLength)).IsError);

        ErrorAssert.Validation(Event(Text(TitledContentPolicy.MaxTitleLength + 1)), "CalendarEvent.TitleTooLong",
            "Event title must be {0} characters or fewer.", TitledContentPolicy.MaxTitleLength);
        ErrorAssert.Validation(Event(description: Text(TitledContentPolicy.MaxBodyLength + 1)), "CalendarEvent.DescriptionTooLong",
            "Event description must be {0} characters or fewer.", TitledContentPolicy.MaxBodyLength);
        ErrorAssert.Validation(Event(location: Text(ShortTextPolicy.MaxLength + 1)), "CalendarEvent.LocationTooLong",
            "Event location must be {0} characters or fewer.", ShortTextPolicy.MaxLength);
        ErrorAssert.Validation(Event(title: " "), "CalendarEvent.TitleEmpty", "Event title cannot be empty.");
        ErrorAssert.Validation(Event(status: "nonsense"), "CalendarEvent.StatusInvalid", "Event status is not a recognised value.");
    }

    /// <summary>Verifies the title / description / location length limits, the empty title and the unknown status on <c>CalendarEvent.Update</c>.</summary>
    [Fact]
    public void CalendarEvent_Update_AcceptsTextAtTheLimits_AndRefusesOneMore()
    {
        CalendarEvent ev = Event().Value;

        Assert.False(ev.Update(Text(TitledContentPolicy.MaxTitleLength), Text(TitledContentPolicy.MaxBodyLength), false,
            _start, _start.AddHours(1), "UTC", "UTC", Text(ShortTextPolicy.MaxLength), null, Now).IsError);

        ErrorAssert.Validation(ev.Update(Text(TitledContentPolicy.MaxTitleLength + 1), null, false, _start, _start.AddHours(1), "UTC", "UTC", null, null, Now),
            "CalendarEvent.TitleTooLong", "Event title must be {0} characters or fewer.", TitledContentPolicy.MaxTitleLength);
        ErrorAssert.Validation(ev.Update("t", Text(TitledContentPolicy.MaxBodyLength + 1), false, _start, _start.AddHours(1), "UTC", "UTC", null, null, Now),
            "CalendarEvent.DescriptionTooLong", "Event description must be {0} characters or fewer.", TitledContentPolicy.MaxBodyLength);
        ErrorAssert.Validation(ev.Update("t", null, false, _start, _start.AddHours(1), "UTC", "UTC", Text(ShortTextPolicy.MaxLength + 1), null, Now),
            "CalendarEvent.LocationTooLong", "Event location must be {0} characters or fewer.", ShortTextPolicy.MaxLength);
        ErrorAssert.Validation(ev.Update(" ", null, false, _start, _start.AddHours(1), "UTC", "UTC", null, null, Now),
            "CalendarEvent.TitleEmpty", "Event title cannot be empty.");
        ErrorAssert.Validation(ev.Update("t", null, false, _start, _start.AddHours(1), "UTC", "UTC", null, "nonsense", Now),
            "CalendarEvent.StatusInvalid", "Event status is not a recognised value.");
    }

    /// <summary>Verifies that a zero-length event is valid and an end one minute before the start is refused.</summary>
    [Fact]
    public void CalendarEvent_EndEqualToStart_IsAccepted_ButEndBeforeStart_IsRefused()
    {
        Assert.False(CalendarEvent.Create(1, "t", null, false, _start, _start, "UTC", "UTC", null, null, Now).IsError);

        ErrorAssert.Validation(CalendarEvent.Create(1, "t", null, false, _start, _start.AddMinutes(-1), "UTC", "UTC", null, null, Now),
            "CalendarEvent.InvalidDateRange", "End date must not be earlier than start date.");
    }

    /// <summary>Verifies the size, empty and length limits of a calendar-event attachment (a size of 0 is legal).</summary>
    [Fact]
    public void CalendarEventAttachedFile_Create_AcceptsTheLimits_AndRefusesOneMore()
    {
        Assert.False(CalendarEventAttachedFile.Create(1, 0, Text(ShortTextPolicy.MaxLength), Text(ShortTextPolicy.MaxLength), Text(ShortTextPolicy.MaxLength)).IsError);

        ErrorAssert.Validation(CalendarEventAttachedFile.Create(1, -1, "n", ".e", "p"), "CalendarEventAttachedFile.InvalidSize", "File size cannot be negative.");
        ErrorAssert.Validation(CalendarEventAttachedFile.Create(1, 1, " ", ".e", "p"), "CalendarEventAttachedFile.NameEmpty", "File name cannot be empty.");
        ErrorAssert.Validation(CalendarEventAttachedFile.Create(1, 1, "n", ".e", " "), "CalendarEventAttachedFile.PathEmpty", "File storage path cannot be empty.");
        ErrorAssert.Validation(CalendarEventAttachedFile.Create(1, 1, "n", Text(ShortTextPolicy.MaxLength + 1), "p"), "CalendarEventAttachedFile.ExtensionTooLong", "File extension must be {0} characters or fewer.", ShortTextPolicy.MaxLength);
        ErrorAssert.Validation(CalendarEventAttachedFile.Create(1, 1, "n", ".e", Text(ShortTextPolicy.MaxLength + 1)), "CalendarEventAttachedFile.PathTooLong", "File storage path must be {0} characters or fewer.", ShortTextPolicy.MaxLength);
    }
}
