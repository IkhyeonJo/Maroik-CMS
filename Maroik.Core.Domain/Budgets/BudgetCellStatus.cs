namespace Maroik.Core.Domain.Budgets;

/// <summary>A budget cell against the actuals.</summary>
/// <param name="Budget">The budget (the cell's number, rounded to four decimals), or null when the cell holds no number.</param>
/// <param name="Actual">The actual total of the line in the month.</param>
/// <param name="Remaining">Budget minus actual, or null without a budget.</param>
/// <param name="State">Where the actual stands against the budget.</param>
public sealed record BudgetCellStatus(decimal? Budget, decimal Actual, decimal? Remaining, BudgetCellState State);
