// ReSharper disable PropertyCanBeMadeInitOnly.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Maroik.Core.Contract.Dtos;

/// <summary>
/// Data transfer object returned when reading a navigation menu category.
/// </summary>
public class CategoryResponse
{
    /// <summary>Category ID (primary key).</summary>
    public long Id { get; set; }

    /// <summary>Internal code name (e.g. "Dashboard").</summary>
    public string? Name { get; set; }

    /// <summary>Label shown in the sidebar navigation.</summary>
    public string? DisplayName { get; set; }

    /// <summary>Icon path displayed beside the label.</summary>
    public string? IconPath { get; set; }

    /// <summary>Target MVC controller name.</summary>
    public string? Controller { get; set; }

    /// <summary>Target MVC action name.</summary>
    public string? Action { get; set; }

    /// <summary>Minimum role to display this menu item ("Admin" or "User").</summary>
    public string? Role { get; set; }

    /// <summary>Sidebar display order (ascending).</summary>
    public long Order { get; set; }
}
