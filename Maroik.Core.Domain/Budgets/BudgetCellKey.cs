namespace Maroik.Core.Domain.Budgets;

/// <summary>A cell of one sheet: its row key and column.</summary>
/// <param name="RowKey">The row's stable key.</param>
/// <param name="Column">The column letter.</param>
public sealed record BudgetCellKey(string RowKey, string Column);
