// ReSharper disable UnusedAutoPropertyAccessor.Global
using System.ComponentModel.DataAnnotations;

namespace Maroik.Website.Models.ViewModels.Management;

/// <summary>
/// JSON request body of the admin menu endpoints, shared by top-level categories (Category) and their
/// sub-menu items (SubCategory): a category uses <see cref="Controller"/>, a sub-category uses
/// <see cref="CategoryId"/> and links to an action of its parent's controller.
/// </summary>
public class MenuInputViewModel
{
    /// <summary>Database row ID of the menu item; 0 for a new item, positive for an edit or delete.</summary>
    [Display(Name = "Id")]
    public int Id { get; set; }

    /// <summary>ID of the parent Category (sub-category requests only).</summary>
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

    /// <summary>CSS icon classes rendered next to the menu label (e.g. "nav-icon fas fa-bell").</summary>
    [Required(ErrorMessage = "Please enter IconPath")]
    [Display(Name = "IconPath")]
    public string? IconPath { get; set; }

    /// <summary>MVC controller name this category links to (without the "Controller" suffix; category requests only).</summary>
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

    /// <summary>Display order of this item within its parent category (lower numbers appear first).</summary>
    [Display(Name = "Order")]
    public int Order { get; set; }
}
