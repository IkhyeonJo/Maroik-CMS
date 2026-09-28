using Maroik.Core.Contract.Dtos;
using Maroik.Core.Domain.Menu;

namespace Maroik.Core.Service.Mappers;

/// <summary>Maps <see cref="SubCategory"/> domain objects to <see cref="SubCategoryResponse"/> DTOs.</summary>
internal static class SubCategoryMapper
{
    /// <summary>Converts a <see cref="SubCategory"/> domain object to its corresponding <see cref="SubCategoryResponse"/> DTO.</summary>
    internal static SubCategoryResponse ToResponse(SubCategory s) => new()
    {
        Id = s.Id,
        CategoryId = s.CategoryId,
        Name = s.Name,
        DisplayName = s.DisplayName,
        IconPath = s.IconPath,
        Action = s.Action,
        Role = s.Role,
        Order = s.Order
    };
}
