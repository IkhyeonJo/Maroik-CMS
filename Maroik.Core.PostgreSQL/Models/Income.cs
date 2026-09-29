using System;
using System.Collections.Generic;

namespace Maroik.Core.PostgreSQL.Models;

/// <summary>
/// Income
/// </summary>
public partial class Income
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
    /// MainClass
    /// </summary>
    public string MainClass { get; set; } = null!;

    /// <summary>
    /// SubClass
    /// </summary>
    public string SubClass { get; set; } = null!;

    /// <summary>
    /// Content
    /// </summary>
    public string Content { get; set; } = null!;

    /// <summary>
    /// Amount
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// DepositMyAssetProductName
    /// </summary>
    public string DepositMyAssetProductName { get; set; } = null!;

    /// <summary>
    /// Created
    /// </summary>
    public DateTime Created { get; set; }

    /// <summary>
    /// Updated
    /// </summary>
    public DateTime Updated { get; set; }

    /// <summary>
    /// Note
    /// </summary>
    public string Note { get; set; } = null!;

    /// <summary>The asset the income is deposited into; carries the amount's currency.</summary>
    public virtual Asset Asset { get; set; } = null!;
}
