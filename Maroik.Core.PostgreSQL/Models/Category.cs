using System;
using System.Collections.Generic;

namespace Maroik.Core.PostgreSQL.Models;

/// <summary>
/// Category
/// </summary>
public partial class Category
{
    /// <summary>
    /// ID
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Name
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// DisplayName
    /// </summary>
    public string DisplayName { get; set; } = null!;

    /// <summary>
    /// IconPath
    /// </summary>
    public string IconPath { get; set; } = null!;

    /// <summary>
    /// Controller
    /// </summary>
    public string Controller { get; set; } = null!;

    /// <summary>
    /// Action
    /// </summary>
    public string? Action { get; set; }

    /// <summary>
    /// Role
    /// </summary>
    public string Role { get; set; } = null!;

    /// <summary>
    /// Order
    /// </summary>
    public long Order { get; set; }

    /// <summary>Dropdown items under this category.</summary>
    public virtual ICollection<SubCategory> SubCategories { get; set; } = new List<SubCategory>();
}
