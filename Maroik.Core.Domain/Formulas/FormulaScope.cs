namespace Maroik.Core.Domain.Formulas;

/// <summary>What a formula is bound against: the sheet it lives in and the shape of the workbook.</summary>
/// <param name="HomeSheet">The canonical name of the formula's own sheet.</param>
/// <param name="ResolveSheet">The canonical name of a sheet as written in a formula (case-insensitive), or null when there is none.</param>
/// <param name="RowKeysOf">The row keys of a sheet, top to bottom (row number n is index n - 1).</param>
/// <param name="ColumnCount">How many columns every sheet has (A is column 1).</param>
public sealed record FormulaScope(string HomeSheet, Func<string, string?> ResolveSheet, Func<string, IReadOnlyList<string>> RowKeysOf, int ColumnCount);
