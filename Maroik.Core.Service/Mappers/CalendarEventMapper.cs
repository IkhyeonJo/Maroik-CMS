using Maroik.Core.Contract.Dtos;
using Maroik.Core.Domain.Calendar;

namespace Maroik.Core.Service.Mappers;

/// <summary>Maps <see cref="CalendarEvent"/> domain objects to <see cref="CalendarEventResponse"/> DTOs.</summary>
internal static class CalendarEventMapper
{
    /// <summary>Converts a <see cref="CalendarEvent"/> domain object to its corresponding <see cref="CalendarEventResponse"/> DTO.</summary>
    internal static CalendarEventResponse ToResponse(CalendarEvent evt) => new()
    {
        Id = evt.Id,
        CalendarId = evt.CalendarId,
        Title = evt.Title,
        Description = evt.Description,
        AllDay = evt.AllDay,
        StartDate = evt.StartDate,
        EndDate = evt.EndDate,
        StartDateTimeZoneIanaId = evt.StartDateTimeZoneIanaId,
        EndDateTimeZoneIanaId = evt.EndDateTimeZoneIanaId,
        Location = evt.Location,
        Status = evt.Status,
        RecurrenceId = evt.RecurrenceId,
        Created = evt.Created,
        Updated = evt.Updated
    };
}
