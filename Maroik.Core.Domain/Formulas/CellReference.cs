namespace Maroik.Core.Domain.Formulas;

/// <summary>
/// The parts of one cell reference as written: its column letters and its row — a row number in the A1 form the user types, or a
/// stable row key in the stored (canonical) form — each with its absolute ("$") marker.
/// </summary>
/// <param name="Column">The column letters, upper-case.</param>
/// <param name="ColumnAbsolute">True when the column is written with "$".</param>
/// <param name="RowNumber">The row number (A1 form), or null in the canonical form.</param>
/// <param name="RowKey">The row key (canonical form), or null in the A1 form.</param>
/// <param name="RowAbsolute">True when the row is written with "$".</param>
public sealed record CellReference(string Column, bool ColumnAbsolute, int? RowNumber, string? RowKey, bool RowAbsolute);
