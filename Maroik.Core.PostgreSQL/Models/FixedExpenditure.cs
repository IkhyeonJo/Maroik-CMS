using System;
using System.Collections.Generic;

namespace Maroik.Core.PostgreSQL.Models;

/// <summary>
/// FixedExpenditure
/// </summary>
public partial class FixedExpenditure
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
    /// PaymentMethod
    /// </summary>
    public string PaymentMethod { get; set; } = null!;

    /// <summary>
    /// MyDepositAsset
    /// </summary>
    public string? MyDepositAsset { get; set; }

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

    public virtual Asset? Asset { get; set; }

    public virtual Asset AssetNavigation { get; set; } = null!;
}
