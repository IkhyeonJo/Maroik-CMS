using Maroik.Core.Contract.Dtos;
using Maroik.Core.Domain.Calendar;

namespace Maroik.Core.Service.Mappers;

/// <summary>Maps <see cref="OtherCalendar"/> domain objects to <see cref="OtherCalendarResponse"/> DTOs.</summary>
internal static class OtherCalendarMapper
{
    /// <summary>Converts an <see cref="OtherCalendar"/> domain object to its corresponding <see cref="OtherCalendarResponse"/> DTO.</summary>
    internal static OtherCalendarResponse ToResponse(OtherCalendar other) => new()
    {
        AccountEmail = other.AccountEmail.Value,
        CalendarId = other.CalendarId
    };
}
