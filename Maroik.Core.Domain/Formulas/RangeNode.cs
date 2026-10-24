namespace Maroik.Core.Domain.Formulas;

/// <summary>A range: two references around ":"; the sheet is the start's.</summary>
/// <param name="Span">Where it is.</param>
/// <param name="Start">The first corner.</param>
/// <param name="End">The opposite corner.</param>
public sealed record RangeNode(TextSpan Span, ReferenceNode Start, ReferenceNode End) : FormulaNode(Span);
