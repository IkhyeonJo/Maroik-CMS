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

    /// <summary>Icon path displayed beside the label.</summary>
    public string? IconPath { get; set; }

    /// <summary>Target MVC action name.</summary>
    public string? Action { get; set; }

    /// <summary>Minimum role to display this sub-menu item.</summary>
    public string? Role { get; set; }

    /// <summary>Display order within the parent category (ascending).</summary>
    public long Order { get; set; }
}
