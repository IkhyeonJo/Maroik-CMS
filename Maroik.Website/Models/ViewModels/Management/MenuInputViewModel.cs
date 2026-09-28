// ReSharper disable UnusedAutoPropertyAccessor.Global
using System.ComponentModel.DataAnnotations;

namespace Maroik.Website.Models.ViewModels.Management;

/// <summary>
/// Form model for admin create / edit operations on navigation sub-menu items (SubCategory).
/// Each item belongs to a top-level Category and is routed to a specific controller action.
/// </summary>
public class MenuInputViewModel
{
    /// <summary>Database row ID of the sub-menu item; 0 for a new item, positive for an edit.</summary>
    [Display(Name = "Id")]
    public int Id { get; set; }

    /// <summary>ID of the parent Category this sub-menu item belongs to.</summary>
    [Display(Name = "CategoryId")]
    public int CategoryId { get; set; }

    /// <summary>Internal programmatic name of the menu item (used in code references).</summary>
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

    /// <summary>Display order of this item within its parent category (lower numbers appear first).</summary>
    [Display(Name = "Order")]
    public int Order { get; set; }
}
