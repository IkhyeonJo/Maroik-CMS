using System;
using System.Collections.Generic;

namespace Maroik.Core.PostgreSQL.Models;

/// <summary>
/// Asset
/// </summary>
public partial class Asset
{
    /// <summary>
    /// ProductName
    /// </summary>
    public string ProductName { get; set; } = null!;

    /// <summary>
    /// AccountEmail (ID)
    /// </summary>
    public string AccountEmail { get; set; } = null!;

    /// <summary>
    /// Item
    /// </summary>
    public string Item { get; set; } = null!;

    /// <summary>
    /// Amount
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// MonetaryUnit (KRW, USD, ETC)
    /// </summary>
    public string MonetaryUnit { get; set; } = null!;

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
    /// Deleted
    /// </summary>
    public bool Deleted { get; set; }

    public virtual Account AccountEmailNavigation { get; set; } = null!;

    public virtual ICollection<Expenditure> ExpenditureAssetNavigations { get; set; } = new List<Expenditure>();

    public virtual ICollection<Expenditure> ExpenditureAssets { get; set; } = new List<Expenditure>();

    public virtual ICollection<FixedExpenditure> FixedExpenditureAssetNavigations { get; set; } = new List<FixedExpenditure>();

    public virtual ICollection<FixedExpenditure> FixedExpenditureAssets { get; set; } = new List<FixedExpenditure>();

    public virtual ICollection<FixedIncome> FixedIncomes { get; set; } = new List<FixedIncome>();

    public virtual ICollection<Income> Incomes { get; set; } = new List<Income>();
}
