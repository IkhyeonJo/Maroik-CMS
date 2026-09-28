using Maroik.Core.Contract.Dtos;
using Maroik.Core.Domain.Calendar;

namespace Maroik.Core.Service.Mappers;

/// <summary>Maps <see cref="CalendarEventReminder"/> domain objects to <see cref="CalendarEventReminderDto"/> DTOs.</summary>
internal static class CalendarEventReminderMapper
{
    /// <summary>Converts a <see cref="CalendarEventReminder"/> domain object to its corresponding <see cref="CalendarEventReminderDto"/> DTO.</summary>
    internal static CalendarEventReminderDto ToResponse(CalendarEventReminder reminder) => new()
    {
        Id = reminder.Id,
        CalendarEventId = reminder.CalendarEventId,
        Method = reminder.Method,
        MinutesBeforeEvent = reminder.MinutesBeforeEvent,
        HoursBeforeEvent = reminder.HoursBeforeEvent,
        DaysBeforeEvent = reminder.DaysBeforeEvent,
        WeeksBeforeEvent = reminder.WeeksBeforeEvent,
        TimesBeforeEvent = reminder.TimesBeforeEvent
    };
}
