namespace Maroik.Core.Domain.Formulas;

/// <summary>A text literal.</summary>
/// <param name="Span">Where it is.</param>
/// <param name="Value">The text, without its quotes.</param>
public sealed record TextNode(TextSpan Span, string Value) : FormulaNode(Span);
