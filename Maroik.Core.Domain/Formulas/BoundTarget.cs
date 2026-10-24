namespace Maroik.Core.Domain.Formulas;

/// <summary>What a reference or range node resolved to: its cells (row by row), or the error it is.</summary>
/// <param name="Cells">The cells, row by row; empty when <paramref name="Error"/> is set.</param>
/// <param name="IsRange">True for a range (several cells where a function expects them).</param>
/// <param name="Error">#REF! when the reference points outside the workbook; null otherwise.</param>
public sealed record BoundTarget(IReadOnlyList<CellAddress> Cells, bool IsRange, FormulaErrorKind? Error);
