// ReSharper disable PropertyCanBeMadeInitOnly.Global
using System.ComponentModel.DataAnnotations;

namespace Maroik.Website.Models.ViewModels.Management;

/// <summary>
/// Read-only view model for displaying a navigation sub-menu item record in the admin management area.
/// Uses <see langword="long"/> / nullable types to match the database column types returned by the ORM.
/// </summary>
public class MenuOutputViewModel
{
    /// <summary>Unique database ID of the sub-menu item.</summary>
    [Display(Name = "Id")]
    public long Id { get; set; }

    /// <summary>ID of the parent Category this sub-menu item belongs to (nullable for orphaned items).</summary>
    [Required(ErrorMessage = "Please enter CategoryId")]
    [Display(Name = "CategoryId")]
    public long? CategoryId { get; set; }

    /// <summary>Internal programmatic name of the menu item.</summary>
    [Required(ErrorMessage = "Please enter Name")]
    [Display(Name = "Name")]
    public string? Name { get; set; }

    /// <summary>Localised display label shown in the navigation sidebar.</summary>
    [Required(ErrorMessage = "Please enter DisplayName")]
    [Display(Name = "DisplayName")]
    public string? DisplayName { get; set; }

    /// <summary>Server-side path to the icon image displayed next to the menu label.</summary>
    [Required(ErrorMessage = "Please enter IconPath")]
    [Display(Name = "IconPath")]
    public string? IconPath { get; set; }

    /// <summary>MVC controller name this menu item links to (without the "Controller" suffix).</summary>
    [Required(ErrorMessage = "Please enter Controller")]
    [Display(Name = "Controller")]
    public string? Controller { get; set; }

    /// <summary>MVC action method name this menu item links to.</summary>
    [Required(ErrorMessage = "Please enter Action")]
    [Display(Name = "Action")]
    public string? Action { get; set; }

    /// <summary>Minimum role required to see this menu item (e.g. "Admin", "User", "Anonymous").</summary>
    [Required(ErrorMessage = "Please enter Role")]
    [Display(Name = "Role")]
    public string? Role { get; set; }

    /// <summary>Display order of this item within its parent category.</summary>
    [Display(Name = "Order")]
    public long Order { get; set; }
}
