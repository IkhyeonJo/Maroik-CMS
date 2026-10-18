
namespace Maroik.Website.Models.ViewModels.Layouts;

/// <summary>The fully resolved, render-ready sidebar menu tree for one role.</summary>
public sealed class SidebarMenuViewModel
{
    /// <summary>The top-level menu entries, in display order.</summary>
    public required IReadOnlyList<SidebarCategoryItem> Categories { get; init; }
}
