using System;
using System.Collections.Generic;

namespace Maroik.Core.PostgreSQL.Models;

/// <summary>
/// FixedIncome
/// </summary>
public partial class FixedIncome
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
    /// DepositMonth
    /// </summary>
    public short DepositMonth { get; set; }

    /// <summary>
    /// DepositDay
    /// </summary>
    public short DepositDay { get; set; }

    /// <summary>
    /// MaturityDate
    /// </summary>
    public DateTime MaturityDate { get; set; }

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

    /// <summary>
    /// Unpunctuality
    /// </summary>
    public bool Unpunctuality { get; set; }

    /// <summary>The asset the income is deposited into; carries the amount's currency.</summary>
    public virtual Asset Asset { get; set; } = null!;
}
