namespace Maroik.Core.Domain.Formulas;

/// <summary>A word that is not a reference, boolean or function; it evaluates to #NAME?.</summary>
/// <param name="Span">Where it is.</param>
/// <param name="Name">The word, upper-case.</param>
public sealed record NameNode(TextSpan Span, string Name) : FormulaNode(Span);
