using Maroik.Core.Contract.Dtos;
using Maroik.Core.Domain.Calendar;

namespace Maroik.Core.Service.Mappers;

/// <summary>Maps <see cref="Calendar"/> domain objects to <see cref="CalendarResponse"/> DTOs.</summary>
internal static class CalendarMapper
{
    /// <summary>Converts a <see cref="Calendar"/> domain object to its corresponding <see cref="CalendarResponse"/> DTO.</summary>
    internal static CalendarResponse ToResponse(Calendar calendar) => new()
    {
        Id = calendar.Id,
        AccountEmail = calendar.AccountEmail.Value,
        Name = calendar.Name,
        Description = calendar.Description,
        TimeZoneIanaId = calendar.TimeZone.Value,
        HtmlColorCode = calendar.ColorCode.Value,
        Created = calendar.Created,
        Updated = calendar.Updated
    };
}
