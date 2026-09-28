namespace Maroik.Core.Domain.Calendar;

/// <summary>
/// Domain rules constraining the lead time of a <see cref="CalendarEventReminder"/>.
/// A reminder fires a fixed amount of time before its event, expressed in exactly one unit
/// (minutes/hours/days/weeks). The maximum lead time is 28 days — the four constants below
/// are that same ceiling restated per unit, so whichever unit the client picked is bounded
/// consistently.
/// Authoritative here; the Calendar page's client-side check is a UX mirror of these values.
/// </summary>
public static class CalendarReminderPolicy
{
    /// <summary>Maximum minutes before the event (28 days).</summary>
    public const long MaxMinutesBeforeEvent = 40_320;

    /// <summary>Maximum hours before the event (28 days).</summary>
    public const long MaxHoursBeforeEvent = 672;

    /// <summary>Maximum days before the event (4 weeks).</summary>
    public const long MaxDaysBeforeEvent = 28;

    /// <summary>Maximum weeks before the event.</summary>
    public const long MaxWeeksBeforeEvent = 4;

    /// <summary>
    /// Granularity, in minutes, of the "notify at HH:MM" picker for all-day reminders
    /// (the client builds a 00:00..23:45 list from this).
    /// </summary>
    public const int ReminderTimeOfDayStepMinutes = 15;

    /// <summary>Default "notify at" time-of-day pre-selected for a new all-day reminder.</summary>
    public const string DefaultReminderTimeOfDay = "09:00";

    /// <summary>
    /// Returns <see langword="true"/> when the single non-null lead-time value falls within
    /// <c>0</c>..its per-unit maximum (inclusive). Returns <see langword="true"/> when no
    /// lead-time field is set — the "exactly one must be set" rule is enforced separately by
    /// <see cref="CalendarEventReminder.Create"/>.
    /// </summary>
    public static bool IsLeadTimeWithinRange(
        long? minutesBefore, long? hoursBefore, long? daysBefore, long? weeksBefore)
    {
        if (minutesBefore is { } minutes) return minutes is >= 0 and <= MaxMinutesBeforeEvent;
        if (hoursBefore is { } hours) return hours is >= 0 and <= MaxHoursBeforeEvent;
        if (daysBefore is { } days) return days is >= 0 and <= MaxDaysBeforeEvent;
        if (weeksBefore is { } weeks) return weeks is >= 0 and <= MaxWeeksBeforeEvent;
        return true;
    }
}
