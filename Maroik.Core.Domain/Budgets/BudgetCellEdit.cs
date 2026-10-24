namespace Maroik.Core.Domain.Budgets;

/// <summary>An edit as typed: a cell's A1 address and its new input.</summary>
/// <param name="Address">The A1 address.</param>
/// <param name="Input">What was typed (empty or null clears the cell).</param>
public sealed record BudgetCellEdit(string Address, string? Input);
