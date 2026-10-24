using System;
using System.Collections.Generic;

namespace Maroik.Core.PostgreSQL.Models;

/// <summary>
/// BudgetSheetCell
/// </summary>
public partial class BudgetSheetCell
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
    /// Stable row key (L.&lt;main&gt;.&lt;sub&gt;.&lt;currency hex&gt;, F.&lt;n&gt;, ...)
    /// </summary>
    public string RowKey { get; set; } = null!;

    /// <summary>
    /// Column letter (A..Z)
    /// </summary>
    public string Column { get; set; } = null!;

    /// <summary>
    /// Stored input (a number, text, TRUE/FALSE, or a formula with row keys)
    /// </summary>
    public string Input { get; set; } = null!;

    /// <summary>
    /// Updated
    /// </summary>
    public DateTime Updated { get; set; }

    /// <summary>The sheet this cell belongs to.</summary>
    public virtual BudgetSheet Sheet { get; set; } = null!;
}
