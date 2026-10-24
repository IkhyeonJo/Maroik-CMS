namespace Maroik.Core.Domain.Formulas;

/// <summary>A postfix "%" (divides by 100).</summary>
/// <param name="Span">Where it is.</param>
/// <param name="Operand">What it applies to.</param>
public sealed record PercentNode(TextSpan Span, FormulaNode Operand) : FormulaNode(Span);
