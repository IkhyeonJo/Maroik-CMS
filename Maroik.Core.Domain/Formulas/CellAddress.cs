namespace Maroik.Core.Domain.Formulas;

/// <summary>A cell of the workbook: its sheet, its row's stable key and its column letter.</summary>
/// <param name="Sheet">The sheet's canonical name (e.g. "Income2026").</param>
/// <param name="RowKey">The row's stable key.</param>
/// <param name="Column">The column letter.</param>
public sealed record CellAddress(string Sheet, string RowKey, string Column);
