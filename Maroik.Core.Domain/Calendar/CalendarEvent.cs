using ErrorOr;
using Maroik.Core.Domain.Localization;
using Maroik.Core.Domain.Primitives;
using Maroik.Core.Domain.ValueObjects;

namespace Maroik.Core.Domain.Calendar;

/// <summary>
/// Aggregate root for a calendar event.
/// Reminders and attached files are persisted independently via their own repositories
/// (<see cref="CalendarEventReminder"/>, <see cref="CalendarEventAttachedFile"/>) rather than
/// hydrated as part of this aggregate, so this class does not mediate their persistence.
/// References the parent <see cref="Calendar"/> by <see cref="CalendarId"/> (no object reference).
/// </summary>
public sealed class CalendarEvent : AggregateRoot<long>
{
    /// <summary>ID of the calendar this event belongs to.</summary>
    public long CalendarId { get; private set; }

    /// <summary>Event title displayed on the calendar.</summary>
    public string Title { get; private set; }

    /// <summary>Optional rich-text description.</summary>
    public string? Description { get; private set; }

    /// <summary>When true, the event spans the entire day (no specific start/end times).</summary>
    public bool AllDay { get; private set; }

    /// <summary>Event start instant. Always UTC — normalized to <see cref="DateTimeKind.Utc"/> on every assignment.</summary>
    public DateTime StartDate { get; private set; }

    /// <summary>Event end instant. Always UTC — normalized to <see cref="DateTimeKind.Utc"/> on every assignment.</summary>
    public DateTime EndDate { get; private set; }

    /// <summary>IANA time-zone ID of the start date. Null means UTC.</summary>
    public string? StartDateTimeZoneIanaId { get; private set; }

    /// <summary>IANA time-zone ID of the end date. Null means UTC.</summary>
    public string? EndDateTimeZoneIanaId { get; private set; }

    /// <summary>Physical or virtual location of the event.</summary>
    public string? Location { get; private set; }

    /// <summary>
    /// Availability status shown to subscribers — see <see cref="CalendarEventStatuses"/> for the
    /// allowed non-null values. <see langword="null"/> means "use the default" (the repository
    /// persists it as <see cref="CalendarEventStatuses.Busy"/>).
    /// </summary>
    public string? Status { get; private set; }

    /// <summary>Optional reference to a recurrence rule row.</summary>
    public long? RecurrenceId { get; private set; }

    /// <summary>UTC timestamp when the event was created.</summary>
    public DateTime Created { get; private set; }

    /// <summary>UTC timestamp of the most recent update.</summary>
    public DateTime Updated { get; private set; }

    /// <summary>Sets the event's fields (both instants pinned to UTC via <see cref="AsUtc"/>) and its
    /// <see cref="Created"/>/<see cref="Updated"/> stamps (the creation time for <see cref="Create"/>,
    /// the stored values for <see cref="Reconstitute"/>).</summary>
    private CalendarEvent(
        long id,
        long calendarId,
        string title,
        string? description,
        bool allDay,
        DateTime startDate,
        DateTime endDate,
        string? startTz,
        string? endTz,
        string? location,
        string? status,
        long? recurrenceId,
        DateTime created,
        DateTime updated) : base(id)
    {
        CalendarId = calendarId;
        Title = title;
        Description = description;
        AllDay = allDay;
        StartDate = AsUtc(startDate);
        EndDate = AsUtc(endDate);
        StartDateTimeZoneIanaId = startTz;
        EndDateTimeZoneIanaId = endTz;
        Location = location;
        Status = status;
        RecurrenceId = recurrenceId;
        Created = created;
        Updated = updated;
    }

    /// <summary>
    /// Stamps <paramref name="value"/> as <see cref="DateTimeKind.Utc"/> without shifting it. Every
    /// caller — the create/update factories and <see cref="Reconstitute"/> (Npgsql hands back
    /// <see cref="DateTimeKind.Unspecified"/> for the <c>timestamp without time zone</c> column) —
    /// already deals in UTC instants; this makes that contract visible on the value instead of only
    /// in the doc comment, so nothing downstream has to guess the <see cref="DateTime.Kind"/>.
    /// </summary>
    private static DateTime AsUtc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    // ------------------------------------------------------------------------
    // Factory / Reconstitution
    // ------------------------------------------------------------------------

    /// <summary>
    /// Rebuilds a <see cref="CalendarEvent"/> from trusted raw values from the repository layer.
    /// Bypasses validation — use only with data from the trusted repository layer.
    /// Child reminders and files must be loaded separately.
    /// </summary>
    public static CalendarEvent Reconstitute(
        long id,
        long calendarId,
        string title,
        string? description,
        bool allDay,
        DateTime startDate,
        DateTime endDate,
        string? startTz,
        string? endTz,
        string? location,
        string? status,
        long? recurrenceId,
        DateTime created,
        DateTime updated)
    {
        var calendarEvent = new CalendarEvent(
            id, calendarId, title, description, allDay,
            startDate, endDate, startTz, endTz, location, status, recurrenceId, created, updated);

        return calendarEvent;
    }

    /// <summary>
    /// Creates a new calendar event.
    /// </summary>
    public static ErrorOr<CalendarEvent> Create(
        long calendarId,
        string? title,
        string? description,
        bool allDay,
        DateTime startDate,
        DateTime endDate,
        string? startTz,
        string? endTz,
        string? location,
        string? status,
        DateTime utcNow,
        long? recurrenceId = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            return LocalizableError.Validation("CalendarEvent.TitleEmpty", "Event title cannot be empty.");

        if (title.Length > TitledContentPolicy.MaxTitleLength)
            return LocalizableError.Validation("CalendarEvent.TitleTooLong", "Event title must be {0} characters or fewer.", TitledContentPolicy.MaxTitleLength);

        if (description?.Length > TitledContentPolicy.MaxBodyLength)
            return LocalizableError.Validation("CalendarEvent.DescriptionTooLong", "Event description must be {0} characters or fewer.", TitledContentPolicy.MaxBodyLength);

        // Persisted in a character varying(255) column: reject an over-long value here (a clean
        // validation error) instead of a raw "value too long" (SQLSTATE 22001) from the write.
        if (location?.Length > ShortTextPolicy.MaxLength)
            return LocalizableError.Validation("CalendarEvent.LocationTooLong", "Event location must be {0} characters or fewer.", ShortTextPolicy.MaxLength);

        if (!string.IsNullOrWhiteSpace(status) && !CalendarEventStatuses.IsKnown(status))
            return LocalizableError.Validation("CalendarEvent.StatusInvalid", "Event status is not a recognised value.");

        var timesResult = ValidateDatesAndZones(allDay, startDate, endDate, startTz, endTz);
        if (timesResult.IsError) return timesResult.Errors;

        var calendarEvent = new CalendarEvent(0, calendarId, title, description, allDay,
            startDate, endDate, startTz, endTz, location, status, recurrenceId, utcNow, utcNow);

        return calendarEvent;
    }

    /// <summary>
    /// Shared validation for the date range and the optional per-endpoint time-zone IDs, run by
    /// both <see cref="Create"/> and <see cref="Update"/>. The range is checked for an all-day
    /// event too (on the date part) — a "no end before start" rule that only fired for timed
    /// events let an all-day event be saved end-before-start. A non-null start/end zone must
    /// resolve to a known zone on the host (null means UTC).
    /// </summary>
    private static ErrorOr<Success> ValidateDatesAndZones(
        bool allDay, DateTime startDate, DateTime endDate, string? startTz, string? endTz)
    {
        bool rangeInvalid = allDay ? endDate.Date < startDate.Date : endDate < startDate;
        if (rangeInvalid)
            return LocalizableError.Validation("CalendarEvent.InvalidDateRange", "End date must not be earlier than start date.");

        foreach (string? tz in new[] { startTz, endTz })
        {
            if (!string.IsNullOrWhiteSpace(tz) && TimeZoneId.Create(tz).IsError)
                return LocalizableError.Validation("CalendarEvent.InvalidTimeZone", "'{0}' is not a recognised time-zone ID.", tz);
        }

        return Result.Success;
    }

    // ------------------------------------------------------------------------
    // Domain behaviours
    // ------------------------------------------------------------------------

    /// <summary>Re-assigns the event to a different calendar or changes its recurrence rule.</summary>
    public void Reassign(long calendarId, long? recurrenceId, DateTime utcNow)
    {
        CalendarId = calendarId;
        RecurrenceId = recurrenceId;
        Updated = utcNow;
    }

    /// <summary>Updates the event's editable fields.</summary>
    public ErrorOr<Success> Update(
        string? title,
        string? description,
        bool allDay,
        DateTime startDate,
        DateTime endDate,
        string? startTz,
        string? endTz,
        string? location,
        string? status,
        DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(title))
            return LocalizableError.Validation("CalendarEvent.TitleEmpty", "Event title cannot be empty.");

        if (title.Length > TitledContentPolicy.MaxTitleLength)
            return LocalizableError.Validation("CalendarEvent.TitleTooLong", "Event title must be {0} characters or fewer.", TitledContentPolicy.MaxTitleLength);

        if (description?.Length > TitledContentPolicy.MaxBodyLength)
            return LocalizableError.Validation("CalendarEvent.DescriptionTooLong", "Event description must be {0} characters or fewer.", TitledContentPolicy.MaxBodyLength);

        // Persisted in a character varying(255) column: reject an over-long value here (a clean
        // validation error) instead of a raw "value too long" (SQLSTATE 22001) from the write.
        if (location?.Length > ShortTextPolicy.MaxLength)
            return LocalizableError.Validation("CalendarEvent.LocationTooLong", "Event location must be {0} characters or fewer.", ShortTextPolicy.MaxLength);

        if (!string.IsNullOrWhiteSpace(status) && !CalendarEventStatuses.IsKnown(status))
            return LocalizableError.Validation("CalendarEvent.StatusInvalid", "Event status is not a recognised value.");

        var timesResult = ValidateDatesAndZones(allDay, startDate, endDate, startTz, endTz);
        if (timesResult.IsError) return timesResult.Errors;

        Title = title;
        Description = description;
        AllDay = allDay;
        StartDate = AsUtc(startDate);
        EndDate = AsUtc(endDate);
        StartDateTimeZoneIanaId = startTz;
        EndDateTimeZoneIanaId = endTz;
        Location = location;
        Status = status;
        Updated = utcNow;
        return Result.Success;
    }

}
