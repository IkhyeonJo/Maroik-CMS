// ReSharper disable PropertyCanBeMadeInitOnly.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Maroik.Core.Contract.Dtos;

/// <summary>
/// Data transfer object used to create or update a sub-menu item under a navigation category.
/// </summary>
public class SubCategoryRequest
{
    /// <summary>Sub-category ID (auto-incremented primary key; 0 for new items).</summary>
    public long Id { get; set; }

    /// <summary>ID of the parent Category (foreign key).</summary>
    public long CategoryId { get; set; }

    /// <summary>Internal code name (e.g. "FreeForum").</summary>
    public string? Name { get; set; }

    /// <summary>Label displayed in the sidebar dropdown.</summary>
    public string? DisplayName { get; set; }

    /// <summary>Icon path shown beside the sub-menu label.</summary>
    public string? IconPath { get; set; }

    /// <summary>MVC action name this sub-menu item links to (shares the parent category's controller).</summary>
    public string? Action { get; set; }

    /// <summary>Minimum role required to see this sub-menu item ("Admin" or "User").</summary>
    public string? Role { get; set; }

    /// <summary>Display order within the parent category (ascending).</summary>
    public long Order { get; set; }
}
