using Maroik.Core.Contract.Dtos;
using Maroik.Core.Domain.Board;

namespace Maroik.Core.Service.Mappers;

/// <summary>Maps <see cref="BoardComment"/> domain objects to <see cref="BoardCommentResponse"/> DTOs.</summary>
internal static class BoardCommentMapper
{
    /// <summary>Converts a <see cref="BoardComment"/> domain object to its corresponding <see cref="BoardCommentResponse"/> DTO.</summary>
    internal static BoardCommentResponse ToResponse(BoardComment comment) => new()
    {
        Id = comment.Id,
        BoardId = comment.BoardId,
        Order = comment.Order,
        AvatarImagePath = comment.AvatarImagePath,
        Writer = comment.Writer,
        Content = comment.Content,
        Created = comment.Created,
        Deleted = comment.Deleted
    };
}
