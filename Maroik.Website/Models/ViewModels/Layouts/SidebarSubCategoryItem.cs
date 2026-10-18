using Maroik.Core.Contract.Dtos;

namespace Maroik.Website.Models.ViewModels.Layouts;

/// <summary>A sub-menu entry annotated with whether it is the currently active route.</summary>
public sealed class SidebarSubCategoryItem
{
    /// <summary>The sub-category DTO to render.</summary>
    public required SubCategoryResponse SubCategory { get; init; }

    /// <summary>True when this sub-category matches the current controller/action.</summary>
    public bool IsActive { get; init; }
}
