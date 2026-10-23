using Maroik.Core.Contract.Dtos;
using Maroik.Core.Domain.Kanbans;

namespace Maroik.Core.Service.Mappers;

/// <summary>Maps <see cref="KanbanCard"/> domain objects to <see cref="KanbanCardResponse"/> DTOs.</summary>
internal static class KanbanCardMapper
{
    /// <summary>
    /// Converts a <see cref="KanbanCard"/> to its <see cref="KanbanCardResponse"/>, with <see cref="KanbanCardResponse.Overdue"/>
    /// taken from the card's due state at <paramref name="utcNow"/> in the owner's <paramref name="timeZoneIanaId"/>.
    /// </summary>
    internal static KanbanCardResponse ToResponse(KanbanCard card, DateTime utcNow, string timeZoneIanaId) => new()
    {
        Id = card.Id,
        Lane = card.Lane,
        Title = card.Title,
        Description = card.Description,
        DueDate = card.DueDate,
        Overdue = card.DueStateAt(utcNow, timeZoneIanaId) == KanbanDueState.Overdue,
        Created = card.Created,
        Updated = card.Updated
    };
}
