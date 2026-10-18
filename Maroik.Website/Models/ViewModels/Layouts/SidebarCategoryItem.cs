using Maroik.Core.Contract.Dtos;

namespace Maroik.Website.Models.ViewModels.Layouts;

/// <summary>A top-level menu entry annotated with whether it (or one of its children) is the currently active route.</summary>
public sealed class SidebarCategoryItem
{
    /// <summary>The category DTO to render.</summary>
    public required CategoryResponse Category { get; init; }

    /// <summary>True when this category (or one of its sub-categories) matches the current controller/action.</summary>
    public bool IsActive { get; init; }

    /// <summary>The sub-menu entries nested under this category.</summary>
    public required IReadOnlyList<SidebarSubCategoryItem> SubCategories { get; init; }
}
