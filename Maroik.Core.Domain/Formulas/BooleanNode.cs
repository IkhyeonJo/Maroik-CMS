namespace Maroik.Core.Domain.Formulas;

/// <summary>TRUE or FALSE.</summary>
/// <param name="Span">Where it is.</param>
/// <param name="Value">Its value.</param>
public sealed record BooleanNode(TextSpan Span, bool Value) : FormulaNode(Span);
