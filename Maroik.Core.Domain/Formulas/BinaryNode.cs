namespace Maroik.Core.Domain.Formulas;

/// <summary>A binary operation.</summary>
/// <param name="Span">Where it is.</param>
/// <param name="Operator">The operator as written (+ - * / ^ &amp; = &lt;&gt; &lt; &gt; &lt;= &gt;=).</param>
/// <param name="Left">The left operand.</param>
/// <param name="Right">The right operand.</param>
public sealed record BinaryNode(TextSpan Span, string Operator, FormulaNode Left, FormulaNode Right) : FormulaNode(Span);
