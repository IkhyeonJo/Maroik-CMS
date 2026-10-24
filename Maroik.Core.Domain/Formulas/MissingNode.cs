namespace Maroik.Core.Domain.Formulas;

/// <summary>An empty function argument, as in ROUND(1,); it evaluates to an empty value.</summary>
/// <param name="Span">Where it is (zero-length).</param>
public sealed record MissingNode(TextSpan Span) : FormulaNode(Span);
