using Maroik.Core.Contract.Dtos;
using Maroik.Core.Domain.Board;

namespace Maroik.Core.Service.Mappers;

/// <summary>Maps <see cref="BoardAttachedFile"/> domain objects to <see cref="BoardAttachedFileDto"/> DTOs.</summary>
internal static class BoardAttachedFileMapper
{
    /// <summary>Converts a <see cref="BoardAttachedFile"/> domain object to its corresponding <see cref="BoardAttachedFileDto"/> DTO.</summary>
    internal static BoardAttachedFileDto ToResponse(BoardAttachedFile file) => new()
    {
        Id = file.Id,
        BoardId = file.BoardId,
        Size = file.Size,
        Name = file.Name,
        Extension = file.Extension,
        Path = file.Path
    };
}
