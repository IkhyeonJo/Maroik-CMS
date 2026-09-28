using Maroik.Core.Contract.Dtos;
using Maroik.Core.Domain.Calendar;

namespace Maroik.Core.Service.Mappers;

/// <summary>Maps <see cref="CalendarShared"/> domain objects to <see cref="CalendarSharedResponse"/> DTOs.</summary>
internal static class CalendarSharedMapper
{
    /// <summary>Converts a <see cref="CalendarShared"/> domain object to its corresponding <see cref="CalendarSharedResponse"/> DTO.</summary>
    internal static CalendarSharedResponse ToResponse(CalendarShared shared) => new()
    {
        CalendarId = shared.Id,
        User = shared.User,
        Anonymous = shared.Anonymous
    };
}
