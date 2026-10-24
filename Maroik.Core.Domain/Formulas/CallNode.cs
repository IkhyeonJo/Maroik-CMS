namespace Maroik.Core.Domain.Formulas;

/// <summary>A function call.</summary>
/// <param name="Span">Where it is, name to closing parenthesis.</param>
/// <param name="Name">The function name, upper-case.</param>
/// <param name="NameSpan">Where the name is.</param>
/// <param name="Arguments">The arguments in order (an empty one is a <see cref="MissingNode"/>).</param>
public sealed record CallNode(TextSpan Span, string Name, TextSpan NameSpan, IReadOnlyList<FormulaNode> Arguments) : FormulaNode(Span);
