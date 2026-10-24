namespace Maroik.Core.Domain.Formulas;

/// <summary>Stands in for what could not be parsed; a formula holding one has a diagnostic and is not saved.</summary>
/// <param name="Span">Where it is.</param>
public sealed record InvalidNode(TextSpan Span) : FormulaNode(Span);
