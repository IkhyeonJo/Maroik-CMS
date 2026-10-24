namespace Maroik.Core.Domain.Budgets;

/// <summary>Records of a line the sheet does not have, so nothing recorded is hidden.</summary>
/// <param name="Line">The line.</param>
/// <param name="Months">Its twelve monthly totals.</param>
public sealed record BudgetUnbudgetedLine(BudgetLine Line, IReadOnlyList<decimal> Months);
