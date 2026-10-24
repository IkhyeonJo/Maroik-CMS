namespace Maroik.Core.Domain.Budgets;

/// <summary>Whether a budget sheet plans income or expenditure; it decides which class taxonomy the sheet's lines come from.</summary>
public enum BudgetKind
{
    /// <summary>An income sheet (classes of IncomeClassPolicy).</summary>
    Income,

    /// <summary>An expenditure sheet (classes of ExpenditureClassPolicy).</summary>
    Expenditure,
}
