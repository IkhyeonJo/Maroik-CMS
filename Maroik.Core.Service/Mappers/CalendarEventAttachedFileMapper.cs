using Maroik.Core.Contract.Dtos;
using Maroik.Core.Domain.Calendar;

namespace Maroik.Core.Service.Mappers;

/// <summary>Maps <see cref="CalendarEventAttachedFile"/> domain objects to <see cref="CalendarEventAttachedFileDto"/> DTOs.</summary>
internal static class CalendarEventAttachedFileMapper
{
    /// <summary>Converts a <see cref="CalendarEventAttachedFile"/> domain object to its corresponding <see cref="CalendarEventAttachedFileDto"/> DTO.</summary>
    internal static CalendarEventAttachedFileDto ToResponse(CalendarEventAttachedFile file) => new()
    {
        Id = file.Id,
        CalendarEventId = file.CalendarEventId,
        Size = file.Size,
        Name = file.Name,
        Extension = file.Extension,
        Path = file.Path
    };
}
