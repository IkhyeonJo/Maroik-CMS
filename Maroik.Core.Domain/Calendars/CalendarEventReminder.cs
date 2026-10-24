using ErrorOr;
using Maroik.Core.Domain.Errors;
using Maroik.Core.Domain.Primitives;

namespace Maroik.Core.Domain.Calendars;

/// <summary>
/// Aggregate root representing a reminder attached to a <see cref="CalendarEvent"/>.
/// Exactly one "BeforeEvent" field must carry a non-null value to define the lead time.
/// Persisted independently via its own <c>ICalendarEventReminderRepository</c> (not hydrated or
/// cascaded through the <see cref="CalendarEvent"/> aggregate), which is why it is an aggregate
/// root rather than a child entity; it references its event by <see cref="CalendarEventId"/> only.
/// </summary>
public sealed class CalendarEventReminder : AggregateRoot<long>
{
    /// <summary>ID of the parent calendar event.</summary>
    public long CalendarEventId { get; private set; }

    /// <summary>Delivery method: always <see cref="ReminderMethods.Email"/>.</summary>
    public string Method { get; private set; }

    /// <summary>Minutes before the event to send the reminder.</summary>
    public long? MinutesBeforeEvent { get; private set; }

    /// <summary>Hours before the event to send the reminder.</summary>
    public long? HoursBeforeEvent { get; private set; }

    /// <summary>Days before the event to send the reminder.</summary>
    public long? DaysBeforeEvent { get; private set; }

    /// <summary>Weeks before the event to send the reminder.</summary>
    public long? WeeksBeforeEvent { get; private set; }

    /// <summary>Time-of-day at which the reminder fires on the target day (all-day events).</summary>
    public TimeOnly? TimesBeforeEvent { get; private set; }

    /// <summary>When the reminder is due (UTC), worked out by <see cref="ReminderSchedule"/> when it is saved.</summary>
    public DateTime FireAtUtc { get; private set; }

    /// <summary>
    /// When the reminder was handled (UTC): sent, or skipped because it could not or should not be sent. Null while it waits;
    /// the scheduler only ever looks at unhandled reminders.
    /// </summary>
    public DateTime? HandledAt { get; private set; }

    /// <summary>Sets every field; reached only through <see cref="Reconstitute"/> / <see cref="Create"/>.</summary>
    private CalendarEventReminder(
        long id,
        long calendarEventId,
        string method,
        long? minutesBefore,
        long? hoursBefore,
        long? daysBefore,
        long? weeksBefore,
        TimeOnly? timesBefore,
        DateTime fireAtUtc,
        DateTime? handledAt) : base(id)
    {
        CalendarEventId = calendarEventId;
        Method = method;
        MinutesBeforeEvent = minutesBefore;
        HoursBeforeEvent = hoursBefore;
        DaysBeforeEvent = daysBefore;
        WeeksBeforeEvent = weeksBefore;
        TimesBeforeEvent = timesBefore;
        FireAtUtc = DateTime.SpecifyKind(fireAtUtc, DateTimeKind.Utc);
        HandledAt = handledAt is { } handled ? DateTime.SpecifyKind(handled, DateTimeKind.Utc) : null;
    }

    /// <summary>
    /// Rebuilds a <see cref="CalendarEventReminder"/> from trusted data from the repository layer (Npgsql hands the
    /// <c>timestamp without time zone</c> columns back as <see cref="DateTimeKind.Unspecified"/>; they are UTC).
    /// Bypasses validation — use only with data from the trusted repository layer.
    /// </summary>
    public static CalendarEventReminder Reconstitute(
        long id, long calendarEventId, string? method,
        long? minutesBefore, long? hoursBefore, long? daysBefore, long? weeksBefore, TimeOnly? timesBefore,
        DateTime fireAtUtc, DateTime? handledAt)
        => new(id, calendarEventId, method ?? ReminderMethods.Email, minutesBefore, hoursBefore, daysBefore, weeksBefore, timesBefore,
            fireAtUtc, handledAt);

    /// <summary>
    /// Creates an e-mail reminder for event <paramref name="calendarEventId"/>, scheduled from <paramref name="calendarEvent"/>'s
    /// start (a new event has no id of its own until it is saved, hence the separate id). Exactly one of the "before" parameters must be non-null, and
    /// it must sit within the lead-time range allowed by <see cref="CalendarReminderPolicy"/>. Its fire time comes from
    /// <see cref="ReminderSchedule"/> (an all-day event in <paramref name="calendarTimeZoneIanaId"/>); a fire time already before
    /// <paramref name="utcNow"/> is handled at once, as sending it now would arrive late.
    /// </summary>
    public static ErrorOr<CalendarEventReminder> Create(
        long calendarEventId,
        CalendarEvent calendarEvent,
        string calendarTimeZoneIanaId,
        long? minutesBefore,
        long? hoursBefore,
        long? daysBefore,
        long? weeksBefore,
        TimeOnly? timesBefore,
        DateTime utcNow)
    {
        // The client models each reminder as a single unit+value pair (a dropdown picks minutes/
        // hours/days/weeks, then one value field) — multiple simultaneous lead-time fields have no
        // defined meaning, so exactly one must be set.
        int leadTimeFieldCount = (minutesBefore.HasValue ? 1 : 0) + (hoursBefore.HasValue ? 1 : 0)
                                  + (daysBefore.HasValue ? 1 : 0) + (weeksBefore.HasValue ? 1 : 0);

        if (leadTimeFieldCount != 1)
            return DomainError.Validation("Reminder.InvalidLeadTime", "Exactly one lead-time field (minutes/hours/days/weeks) must be set.");

        if (!CalendarReminderPolicy.IsLeadTimeWithinRange(minutesBefore, hoursBefore, daysBefore, weeksBefore))
            return DomainError.Validation("Reminder.LeadTimeOutOfRange",
                "Reminder lead time is outside the allowed range (0 to 28 days before the event).");

        DateTime fireAtUtc = ReminderSchedule.FireAtUtc(calendarEvent.AllDay, calendarEvent.StartDate, calendarTimeZoneIanaId,
            minutesBefore, hoursBefore, daysBefore, weeksBefore, timesBefore);

        return new CalendarEventReminder(0, calendarEventId, ReminderMethods.Email,
            minutesBefore, hoursBefore, daysBefore, weeksBefore, timesBefore, fireAtUtc, fireAtUtc < utcNow ? utcNow : null);
    }

    /// <summary>Records that the reminder was handled (sent, or skipped) at <paramref name="utcNow"/>.</summary>
    public void MarkHandled(DateTime utcNow) => HandledAt = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
}
