namespace Maroik.Core.Domain.Formulas;

/// <summary>
/// A parsed formula with its references resolved. <see cref="Errors"/> are problems Excel refuses a formula for (syntax, argument
/// counts): such a formula is not saved. <see cref="Warnings"/> are saved and show as #NAME? / #REF! (an unknown function or name,
/// a reference outside the workbook).
/// </summary>
/// <param name="Syntax">The parsed formula.</param>
/// <param name="Targets">What each reference / range node resolved to (keyed by node identity).</param>
/// <param name="Errors">Problems that block saving.</param>
/// <param name="Warnings">Problems that are saved and evaluate to an error value.</param>
/// <param name="Precedents">The distinct cells whose values the formula reads (ACTUAL's arguments are not read).</param>
public sealed record BoundFormula(FormulaSyntax Syntax, IReadOnlyDictionary<FormulaNode, BoundTarget> Targets, IReadOnlyList<FormulaDiagnostic> Errors,
    IReadOnlyList<FormulaDiagnostic> Warnings, IReadOnlyList<CellAddress> Precedents)
{
    /// <summary>True when the formula may be saved (no <see cref="Errors"/>).</summary>
    public bool CanSave => Errors.Count == 0;
}
