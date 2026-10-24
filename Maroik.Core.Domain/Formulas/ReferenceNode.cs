namespace Maroik.Core.Domain.Formulas;

/// <summary>A cell reference, with the sheet it names (null for the formula's own sheet).</summary>
/// <param name="Span">Where it is (the sheet prefix included).</param>
/// <param name="Sheet">The sheet name as written, or null.</param>
/// <param name="Cell">The reference's column and row.</param>
public sealed record ReferenceNode(TextSpan Span, string? Sheet, CellReference Cell) : FormulaNode(Span);
