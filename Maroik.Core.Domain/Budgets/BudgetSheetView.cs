namespace Maroik.Core.Domain.Budgets;

/// <summary>A sheet as shown: its rows, its cells (every cell with an input, every computed and every budget cell) and the unbudgeted records.</summary>
/// <param name="Kind">Income or expenditure.</param>
/// <param name="Year">The year.</param>
/// <param name="Exists">False until the sheet is first edited.</param>
/// <param name="Rows">The rows.</param>
/// <param name="Cells">The cells that have something to show.</param>
/// <param name="Unbudgeted">Records of lines the sheet does not have.</param>
public sealed record BudgetSheetView(BudgetKind Kind, int Year, bool Exists, IReadOnlyList<BudgetRow> Rows, IReadOnlyList<BudgetCellView> Cells,
    IReadOnlyList<BudgetUnbudgetedLine> Unbudgeted);
