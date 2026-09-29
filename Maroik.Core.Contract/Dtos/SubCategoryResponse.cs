// ReSharper disable PropertyCanBeMadeInitOnly.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Maroik.Core.Contract.Dtos;

/// <summary>
/// Data transfer object returned when reading a navigation sub-menu item.
/// </summary>
public class SubCategoryResponse
{
    /// <summary>Sub-category ID (primary key).</summary>
    public long Id { get; set; }

    /// <summary>ID of the parent Category.</summary>
    public long CategoryId { get; set; }

    /// <summary>Internal code name.</summary>
    public string? Name { get; set; }

    /// <summary>Label displayed in the sidebar dropdown.</summary>
    public string? DisplayName { get; set; }

    /// <summary>CSS icon classes rendered beside the label.</summary>
    public string? IconPath { get; set; }

    /// <summary>Target MVC action name.</summary>
    public string? Action { get; set; }

    /// <summary>The one role whose sidebar shows this sub-menu item ("Admin", "User" or "Anonymous"; exact match, not a hierarchy).</summary>
    public string? Role { get; set; }

    /// <summary>Display order within the parent category (ascending).</summary>
    public long Order { get; set; }
}
