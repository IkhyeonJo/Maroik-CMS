// ReSharper disable PropertyCanBeMadeInitOnly.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Maroik.Core.Contract.Dtos;

/// <summary>
/// Data transfer object used to create or update a top-level navigation menu category.
/// </summary>
public class CategoryRequest
{
    /// <summary>Category ID (auto-incremented primary key; 0 for new categories).</summary>
    public long Id { get; set; }

    /// <summary>Internal name used in code (e.g. "Dashboard").</summary>
    public string? Name { get; set; }

    /// <summary>Label displayed in the left sidebar navigation menu.</summary>
    public string? DisplayName { get; set; }

    /// <summary>CSS icon classes rendered next to the menu label (e.g. "nav-icon fas fa-bell").</summary>
    public string? IconPath { get; set; }

    /// <summary>MVC controller name this category links to; its sub-categories link to actions of the same controller.</summary>
    public string? Controller { get; set; }

    /// <summary>MVC action name this category links to (empty when the category only opens a sub-category dropdown).</summary>
    public string? Action { get; set; }

    /// <summary>The one role whose sidebar shows this menu item ("Admin", "User" or "Anonymous"; exact match, not a hierarchy).</summary>
    public string? Role { get; set; }

    /// <summary>Display order in the sidebar (lower number = shown higher).</summary>
    public long Order { get; set; }
}
