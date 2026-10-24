using System;
using System.Collections.Generic;

namespace Maroik.Core.PostgreSQL.Models;

/// <summary>
/// BudgetSheet
/// </summary>
public partial class BudgetSheet
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
    /// Income or Expenditure
    /// </summary>
    public string Kind { get; set; } = null!;

    /// <summary>
    /// Year of the sheet
    /// </summary>
    public int Year { get; set; }

    /// <summary>
    /// Created
    /// </summary>
    public DateTime Created { get; set; }

    /// <summary>
    /// Updated (last edit of its lines or cells)
    /// </summary>
    public DateTime Updated { get; set; }

    /// <summary>The account that owns this sheet.</summary>
    public virtual Account AccountEmailNavigation { get; set; } = null!;

    /// <summary>The sheet's cell inputs (BudgetSheetCell.SheetId).</summary>
    public virtual ICollection<BudgetSheetCell> BudgetSheetCells { get; set; } = new List<BudgetSheetCell>();

    /// <summary>The sheet's lines (BudgetSheetLine.SheetId).</summary>
    public virtual ICollection<BudgetSheetLine> BudgetSheetLines { get; set; } = new List<BudgetSheetLine>();
}
