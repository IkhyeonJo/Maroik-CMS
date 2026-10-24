using System;
using System.Collections.Generic;

namespace Maroik.Core.PostgreSQL.Models;

/// <summary>
/// BudgetSheetLine
/// </summary>
public partial class BudgetSheetLine
{
    /// <summary>
    /// PK
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// BudgetSheet Id
    /// </summary>
    public long SheetId { get; set; }

    /// <summary>
    /// Main class
    /// </summary>
    public string MainClass { get; set; } = null!;

    /// <summary>
    /// Sub class
    /// </summary>
    public string SubClass { get; set; } = null!;

    /// <summary>
    /// Currency label
    /// </summary>
    public string MonetaryUnit { get; set; } = null!;

    /// <summary>
    /// Created
    /// </summary>
    public DateTime Created { get; set; }

    /// <summary>The sheet this line belongs to.</summary>
    public virtual BudgetSheet Sheet { get; set; } = null!;
}
