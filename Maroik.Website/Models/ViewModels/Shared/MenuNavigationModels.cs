using Maroik.Core.Contract.Dtos;

namespace Maroik.Website.Models.ViewModels.Shared;

/// <summary>Result of matching the current controller/action against a role's Category/SubCategory lists.</summary>
public sealed record ActiveMenuMatch(bool IsMatch, CategoryResponse? Category, SubCategoryResponse? SubCategory);

/// <summary>A sub-menu entry annotated with whether it is the currently active route.</summary>
public sealed class SidebarSubCategoryItem
{
    /// <summary>The sub-category DTO to render.</summary>
    public required SubCategoryResponse SubCategory { get; init; }

    /// <summary>True when this sub-category matches the current controller/action.</summary>
    public bool IsActive { get; init; }
}

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

/// <summary>The fully resolved, render-ready sidebar menu tree for one role.</summary>
public sealed class SidebarMenuViewModel
{
    /// <summary>The top-level menu entries, in display order.</summary>
    public required IReadOnlyList<SidebarCategoryItem> Categories { get; init; }
}
