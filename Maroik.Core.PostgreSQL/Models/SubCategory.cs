using System;
using System.Collections.Generic;

namespace Maroik.Core.PostgreSQL.Models;

/// <summary>
/// SubCategory
/// </summary>
public partial class SubCategory
{
    /// <summary>
    /// ID
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Parent Category ID
    /// </summary>
    public long CategoryId { get; set; }

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
    /// Action
    /// </summary>
    public string Action { get; set; } = null!;

    /// <summary>
    /// Role
    /// </summary>
    public string Role { get; set; } = null!;

    /// <summary>
    /// Order
    /// </summary>
    public long Order { get; set; }

    /// <summary>The category this item belongs to.</summary>
    public virtual Category Category { get; set; } = null!;
}
