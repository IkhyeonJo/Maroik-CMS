namespace Maroik.Core.Domain.Budgets;

/// <summary>A cell input to store (or, with a null input, to delete).</summary>
/// <param name="RowKey">The row's key.</param>
/// <param name="Column">The column.</param>
/// <param name="Input">The stored input, or null to clear the cell.</param>
public sealed record BudgetCellWrite(string RowKey, string Column, string? Input);
