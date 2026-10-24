namespace Maroik.Core.Domain.Formulas;

/// <summary>A node of a formula's syntax tree; its span covers the node's text in the cell input.</summary>
/// <param name="Span">Where the node is in the cell input.</param>
public abstract record FormulaNode(TextSpan Span);
