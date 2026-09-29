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

    /// <summary>The account that owns this asset.</summary>
    public virtual Account AccountEmailNavigation { get; set; } = null!;

    /// <summary>Expenditures paid from this asset (Expenditure.PaymentMethod).</summary>
    public virtual ICollection<Expenditure> ExpenditureAssetNavigations { get; set; } = new List<Expenditure>();

    /// <summary>Transfer-type expenditures credited to this asset (Expenditure.MyDepositAsset).</summary>
    public virtual ICollection<Expenditure> ExpenditureAssets { get; set; } = new List<Expenditure>();

    /// <summary>Fixed expenditures paid from this asset (FixedExpenditure.PaymentMethod).</summary>
    public virtual ICollection<FixedExpenditure> FixedExpenditureAssetNavigations { get; set; } = new List<FixedExpenditure>();

    /// <summary>Transfer-type fixed expenditures credited to this asset (FixedExpenditure.MyDepositAsset).</summary>
    public virtual ICollection<FixedExpenditure> FixedExpenditureAssets { get; set; } = new List<FixedExpenditure>();

    /// <summary>Fixed incomes deposited into this asset (FixedIncome.DepositMyAssetProductName).</summary>
    public virtual ICollection<FixedIncome> FixedIncomes { get; set; } = new List<FixedIncome>();

    /// <summary>Incomes deposited into this asset (Income.DepositMyAssetProductName).</summary>
    public virtual ICollection<Income> Incomes { get; set; } = new List<Income>();
}
