using System;
using System.Collections.Generic;

namespace Maroik.Core.PostgreSQL.Models;

/// <summary>
/// KanbanCard
/// </summary>
public partial class KanbanCard
{
    /// <summary>
    /// PK
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Account Email (ID)
    /// </summary>
    public string AccountEmail { get; set; } = null!;

    /// <summary>
    /// Lane (Backlog, ToDo, InProgress, Done)
    /// </summary>
    public string Lane { get; set; } = null!;

    /// <summary>
    /// Title
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// Description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// DueDate
    /// </summary>
    public DateOnly? DueDate { get; set; }

    /// <summary>
    /// Created
    /// </summary>
    public DateTime Created { get; set; }

    /// <summary>
    /// Updated
    /// </summary>
    public DateTime Updated { get; set; }

    /// <summary>The account that owns this card.</summary>
    public virtual Account AccountEmailNavigation { get; set; } = null!;
}
