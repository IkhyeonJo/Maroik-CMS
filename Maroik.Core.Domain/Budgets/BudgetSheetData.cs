namespace Maroik.Core.Domain.Budgets;

/// <summary>One sheet as loaded for a workbook: kind, year, whether it is stored yet, its lines and its stored cell inputs.</summary>
/// <param name="Kind">Income or expenditure.</param>
/// <param name="Year">The year.</param>
/// <param name="Exists">False for a sheet not stored yet (shown with its default lines).</param>
/// <param name="Lines">Its lines.</param>
/// <param name="Inputs">Its stored cell inputs.</param>
public sealed record BudgetSheetData(BudgetKind Kind, int Year, bool Exists, IReadOnlyList<BudgetLine> Lines, IReadOnlyDictionary<BudgetCellKey, string> Inputs);
