namespace Maroik.Core.Domain.Formulas;

/// <summary>A prefix "-" or "+".</summary>
/// <param name="Span">Where it is.</param>
/// <param name="Operator">"-" or "+".</param>
/// <param name="Operand">What it applies to.</param>
public sealed record UnaryNode(TextSpan Span, string Operator, FormulaNode Operand) : FormulaNode(Span);
