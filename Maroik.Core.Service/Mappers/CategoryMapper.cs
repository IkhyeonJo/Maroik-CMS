using Maroik.Core.Contract.Dtos;
using Maroik.Core.Domain.Menu;

namespace Maroik.Core.Service.Mappers;

/// <summary>Maps <see cref="Category"/> domain objects to <see cref="CategoryResponse"/> DTOs.</summary>
internal static class CategoryMapper
{
    /// <summary>Converts a <see cref="Category"/> domain object to its corresponding <see cref="CategoryResponse"/> DTO.</summary>
    internal static CategoryResponse ToResponse(Category c) => new()
    {
        Id = c.Id,
        Name = c.Name,
        DisplayName = c.DisplayName,
        IconPath = c.IconPath,
        Controller = c.Controller,
        Action = c.Action,
        Role = c.Role,
        Order = c.Order
    };
}
