// ReSharper disable PropertyCanBeMadeInitOnly.Global
using System.ComponentModel.DataAnnotations;

namespace Maroik.Website.Models.ViewModels.Management;

/// <summary>
/// One row of the admin menu grid — either a top-level category (<see cref="CategoryId"/> null) or a
/// sub-menu item (<see cref="Controller"/> null). Uses <see langword="long"/> / nullable types to match
/// the database column types.
/// </summary>
public class MenuOutputViewModel
{
    /// <summary>Database ID of the category or sub-menu item.</summary>
    [Display(Name = "Id")]
    public long Id { get; set; }

    /// <summary>ID of the parent Category for a sub-menu item; null for a top-level category row.</summary>
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

    /// <summary>CSS icon classes rendered next to the menu label (e.g. "nav-icon fas fa-bell").</summary>
    [Required(ErrorMessage = "Please enter IconPath")]
    [Display(Name = "IconPath")]
    public string? IconPath { get; set; }

    /// <summary>MVC controller name a category links to (without the "Controller" suffix); null for a sub-menu row.</summary>
    [Required(ErrorMessage = "Please enter Controller")]
    [Display(Name = "Controller")]
    public string? Controller { get; set; }

    /// <summary>MVC action method name this menu item links to.</summary>
    [Required(ErrorMessage = "Please enter Action")]
    [Display(Name = "Action")]
    public string? Action { get; set; }

    /// <summary>The one role whose sidebar shows this menu item ("Admin", "User" or "Anonymous"; exact match, not a hierarchy).</summary>
    [Required(ErrorMessage = "Please enter Role")]
    [Display(Name = "Role")]
    public string? Role { get; set; }

    /// <summary>Display order of this item within its parent category.</summary>
    [Display(Name = "Order")]
    public long Order { get; set; }
}
