using ErrorOr;
using Maroik.Core.Domain.Errors;
using Maroik.Core.Domain.Primitives;

namespace Maroik.Core.Domain.Calendar;

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

    /// <summary>Notification delivery method — see <see cref="ReminderMethods"/> for the allowed values.</summary>
    public string Method { get; private set; }

    /// <summary>Minutes before the event to send the reminder.</summary>
    public long? MinutesBeforeEvent { get; private set; }

    /// <summary>Hours before the event to send the reminder.</summary>
    public long? HoursBeforeEvent { get; private set; }

    /// <summary>Days before the event to send the reminder.</summary>
    public long? DaysBeforeEvent { get; private set; }

    /// <summary>Weeks before the event to send the reminder.</summary>
    public long? WeeksBeforeEvent { get; private set; }

    /// <summary>Time-of-day at which the reminder fires on the target day.</summary>
    public TimeOnly? TimesBeforeEvent { get; private set; }

    /// <summary>Sets every field; reached only through <see cref="Reconstitute"/> / <see cref="Create"/>.</summary>
    private CalendarEventReminder(
        long id,
        long calendarEventId,
        string method,
        long? minutesBefore,
        long? hoursBefore,
        long? daysBefore,
        long? weeksBefore,
        TimeOnly? timesBefore) : base(id)
    {
        CalendarEventId = calendarEventId;
        Method = method;
        MinutesBeforeEvent = minutesBefore;
        HoursBeforeEvent = hoursBefore;
        DaysBeforeEvent = daysBefore;
        WeeksBeforeEvent = weeksBefore;
        TimesBeforeEvent = timesBefore;
    }

    /// <summary>
    /// Rebuilds a <see cref="CalendarEventReminder"/> from trusted data from the repository layer.
    /// Bypasses validation — use only with data from the trusted repository layer.
    /// </summary>
    public static CalendarEventReminder Reconstitute(
        long id, long calendarEventId, string? method,
        long? minutesBefore, long? hoursBefore, long? daysBefore, long? weeksBefore, TimeOnly? timesBefore)
        => new(id, calendarEventId, method ?? "", minutesBefore, hoursBefore, daysBefore, weeksBefore, timesBefore);

    /// <summary>
    /// Creates a new reminder.
    /// Exactly one of the "before" parameters must be non-null, and it must sit within the
    /// lead-time range allowed by <see cref="CalendarReminderPolicy"/>.
    /// </summary>
    public static ErrorOr<CalendarEventReminder> Create(
        long calendarEventId,
        string? method,
        long? minutesBefore,
        long? hoursBefore,
        long? daysBefore,
        long? weeksBefore,
        TimeOnly? timesBefore)
    {
        if (string.IsNullOrWhiteSpace(method))
            return DomainError.Validation("Reminder.MethodEmpty", "Notification method cannot be empty.");

        // Constrain to the known taxonomy here rather than trusting the caller: otherwise the DB
        // CalendarEventReminder_Method_check constraint would be the only thing rejecting
        // anything else, which surfaces as a raw DB exception instead of this validation error.
        if (!ReminderMethods.IsKnown(method))
            return DomainError.Validation("Reminder.MethodInvalid", "Notification method is not a recognised value.");

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

        return new CalendarEventReminder(0, calendarEventId, method,
            minutesBefore, hoursBefore, daysBefore, weeksBefore, timesBefore);
    }
}
