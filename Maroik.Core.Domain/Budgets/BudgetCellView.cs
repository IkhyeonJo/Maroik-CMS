using Maroik.Core.Domain.Formulas;

namespace Maroik.Core.Domain.Budgets;

/// <summary>A cell as the sheet shows it.</summary>
/// <param name="Address">Its A1 address.</param>
/// <param name="Role">What it is.</param>
/// <param name="Input">Its input in A1 form (a computed cell's formula included), or null when it has none.</param>
/// <param name="Value">Its value.</param>
/// <param name="Status">For a budget cell, its budget against the actuals; null otherwise.</param>
public sealed record BudgetCellView(string Address, BudgetCellRole Role, string? Input, FormulaValue Value, BudgetCellStatus? Status);
