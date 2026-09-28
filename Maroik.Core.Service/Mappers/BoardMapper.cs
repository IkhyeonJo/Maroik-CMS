using Maroik.Core.Contract.Dtos;
using Maroik.Core.Domain.Board;

namespace Maroik.Core.Service.Mappers;

/// <summary>Maps <see cref="Board"/> domain objects to <see cref="BoardResponse"/> DTOs.</summary>
internal static class BoardMapper
{
    /// <summary>Converts a <see cref="Board"/> domain object to its corresponding <see cref="BoardResponse"/> DTO.</summary>
    internal static BoardResponse ToResponse(Board board) => new()
    {
        Id = board.Id,
        Type = board.Type,
        Title = board.Title,
        Content = board.Content,
        Writer = board.Writer,
        Created = board.Created,
        Updated = board.Updated,
        View = board.View,
        Deleted = board.Deleted,
        Locked = board.Locked,
        Noticed = board.Noticed
    };
}
