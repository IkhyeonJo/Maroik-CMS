namespace Maroik.Core.Domain.Formulas;

/// <summary>What the editor shows for a formula being typed.</summary>
/// <param name="Tokens">Every token, coloured.</param>
/// <param name="References">The distinct references, by colour index.</param>
/// <param name="Problems">The problems, in text order.</param>
public sealed record FormulaHighlight(IReadOnlyList<HighlightToken> Tokens, IReadOnlyList<HighlightReference> References, IReadOnlyList<FormulaProblem> Problems)
{
    /// <summary>True when nothing blocks saving.</summary>
    public bool CanSave => Problems.All(p => !p.Blocking);
}
