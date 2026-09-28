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

    /// <summary>Path to the Font Awesome or custom icon shown next to the menu label.</summary>
    public string? IconPath { get; set; }

    /// <summary>MVC controller name this category links to (used when no sub-categories exist).</summary>
    public string? Controller { get; set; }

    /// <summary>MVC action name this category links to.</summary>
    public string? Action { get; set; }

    /// <summary>Minimum role required to see this menu item ("Admin" or "User").</summary>
    public string? Role { get; set; }

    /// <summary>Display order in the sidebar (lower number = shown higher).</summary>
    public long Order { get; set; }
}
