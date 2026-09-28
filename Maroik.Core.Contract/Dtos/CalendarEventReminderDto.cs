// ReSharper disable PropertyCanBeMadeInitOnly.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Maroik.Core.Contract.Dtos;

/// <summary>
/// Data transfer object returned when reading a calendar event reminder.
/// </summary>
public class CalendarEventReminderDto
{
    /// <summary>Reminder ID (primary key).</summary>
    public long Id { get; set; }

    /// <summary>ID of the associated calendar event.</summary>
    public long CalendarEventId { get; set; }

    /// <summary>Notification delivery method — see <c>Maroik.Core.Domain.Calendar.ReminderMethods</c> for the allowed values.</summary>
    public string? Method { get; set; }

    /// <summary>Minutes before the event to trigger the reminder.</summary>
    public long? MinutesBeforeEvent { get; set; }

    /// <summary>Hours before the event to trigger the reminder.</summary>
    public long? HoursBeforeEvent { get; set; }

    /// <summary>Days before the event to trigger the reminder.</summary>
    public long? DaysBeforeEvent { get; set; }

    /// <summary>Weeks before the event to trigger the reminder.</summary>
    public long? WeeksBeforeEvent { get; set; }

    /// <summary>Specific time-of-day on the trigger day.</summary>
    public TimeOnly? TimesBeforeEvent { get; set; }
}
