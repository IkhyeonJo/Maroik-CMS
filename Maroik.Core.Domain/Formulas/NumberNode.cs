namespace Maroik.Core.Domain.Formulas;

/// <summary>A number literal.</summary>
/// <param name="Span">Where it is.</param>
/// <param name="Value">Its value.</param>
public sealed record NumberNode(TextSpan Span, decimal Value) : FormulaNode(Span);
