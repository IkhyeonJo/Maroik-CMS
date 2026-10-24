namespace Maroik.Core.Domain.Formulas;

/// <summary>An error literal such as #REF!.</summary>
/// <param name="Span">Where it is.</param>
/// <param name="Error">The error it stands for.</param>
public sealed record ErrorLiteralNode(TextSpan Span, FormulaErrorKind Error) : FormulaNode(Span);
