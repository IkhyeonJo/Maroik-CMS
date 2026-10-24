namespace Maroik.Core.Domain.Formulas;

/// <summary>A parsed formula: its tree, its tokens (the "=" included) and its syntax diagnostics.</summary>
/// <param name="Root">The tree (never null; problems become <see cref="InvalidNode"/>s).</param>
/// <param name="Tokens">Every token of the cell input.</param>
/// <param name="Diagnostics">The problems found, in the order of the text.</param>
public sealed record FormulaSyntax(FormulaNode Root, IReadOnlyList<FormulaToken> Tokens, IReadOnlyList<FormulaDiagnostic> Diagnostics);
