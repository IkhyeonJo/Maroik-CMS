// ReSharper disable PropertyCanBeMadeInitOnly.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Maroik.Core.Contract.Dtos;

/// <summary>
/// Lightweight reminder descriptor passed from the view/controller to the service layer
/// when creating or updating calendar events.
/// </summary>
public class CalendarReminderDto
{
    /// <summary>Notification delivery method — see <c>Maroik.Core.Domain.Calendar.ReminderMethods</c> for the allowed values.</summary>
    public string Method { get; init; } = "";

    /// <summary>Minutes before the event to fire the reminder. Null if another unit is used.</summary>
    public long? MinutesBeforeEvent { get; init; }

    /// <summary>Hours before the event to fire the reminder. Null if another unit is used.</summary>
    public long? HoursBeforeEvent { get; init; }

    /// <summary>Days before the event to fire the reminder. Null if another unit is used.</summary>
    public long? DaysBeforeEvent { get; init; }

    /// <summary>Weeks before the event to fire the reminder. Null if another unit is used.</summary>
    public long? WeeksBeforeEvent { get; init; }

    /// <summary>
    /// Specific time-of-day at which the reminder fires on the target day. <see cref="TimeOnly"/>,
    /// matching <c>CalendarEventReminder</c>/<see cref="CalendarEventReminderDto"/> — not
    /// <see cref="TimeSpan"/>, which can represent a value outside one day's range and would
    /// otherwise reach <c>CalendarEventReminder.Create</c>'s validation only via an unchecked
    /// <c>TimeOnly.FromTimeSpan</c> conversion that throws instead of failing gracefully.
    /// </summary>
    public TimeOnly? TimesBeforeEvent { get; init; }
}
