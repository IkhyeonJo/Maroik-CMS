namespace Maroik.Core.Domain.Calendars;

/// <summary>
/// When a calendar event's reminder fires. A timed event's reminder fires its lead time before the event's start instant. An all-day
/// event is a bare date (stored as midnight UTC, no zone), so its reminder fires on the day its lead time (days / weeks) before the
/// event's first day, at the reminder's time of day — <see cref="CalendarReminderPolicy.DefaultReminderTimeOfDay"/> when none is
/// given — in the calendar's time zone; a lead time in minutes / hours counts back from the first day's midnight there.
/// A local time inside a daylight-saving gap moves forward to the first instant after the gap; one that happens twice takes the
/// earlier instant.
/// </summary>
public static class ReminderSchedule
{
    /// <summary>The UTC instant the reminder fires.</summary>
    /// <param name="allDay">Whether the event is all-day.</param>
    /// <param name="startUtc">The event's start: an instant (timed) or a bare date as midnight UTC (all-day).</param>
    /// <param name="calendarTimeZoneIanaId">The calendar's IANA time zone, used for an all-day event.</param>
    /// <param name="minutesBefore">Lead time in minutes, or null.</param>
    /// <param name="hoursBefore">Lead time in hours, or null.</param>
    /// <param name="daysBefore">Lead time in days, or null.</param>
    /// <param name="weeksBefore">Lead time in weeks, or null.</param>
    /// <param name="timeOfDay">An all-day event's time of day for a lead time in days / weeks.</param>
    public static DateTime FireAtUtc(bool allDay, DateTime startUtc, string calendarTimeZoneIanaId,
        long? minutesBefore, long? hoursBefore, long? daysBefore, long? weeksBefore, TimeOnly? timeOfDay)
    {
        TimeSpan lead = TimeSpan.FromMinutes(minutesBefore ?? 0) + TimeSpan.FromHours(hoursBefore ?? 0)
                        + TimeSpan.FromDays((daysBefore ?? 0) + (7 * (weeksBefore ?? 0)));

        if (!allDay)
            return DateTime.SpecifyKind(startUtc, DateTimeKind.Utc) - lead;

        DateTime firstDay = DateTime.SpecifyKind(startUtc.Date, DateTimeKind.Unspecified);
        DateTime local = minutesBefore.HasValue || hoursBefore.HasValue
            ? firstDay - lead
            : (firstDay - lead).Add((timeOfDay ?? TimeOnly.Parse(CalendarReminderPolicy.DefaultReminderTimeOfDay,
                System.Globalization.CultureInfo.InvariantCulture)).ToTimeSpan());

        return ToUtc(local, TimeZoneInfo.FindSystemTimeZoneById(calendarTimeZoneIanaId));
    }

    /// <summary>
    /// <paramref name="local"/> (a wall-clock time in <paramref name="zone"/>) as UTC: a time inside a daylight-saving gap moves to
    /// the end of the gap (the first minute that exists), an ambiguous time takes its earlier instant (the larger offset).
    /// </summary>
    private static DateTime ToUtc(DateTime local, TimeZoneInfo zone)
    {
        while (zone.IsInvalidTime(local))
            local = local.AddMinutes(1);

        TimeSpan offset = zone.IsAmbiguousTime(local) ? zone.GetAmbiguousTimeOffsets(local).Max() : zone.GetUtcOffset(local);
        return DateTime.SpecifyKind(local - offset, DateTimeKind.Utc);
    }
}
