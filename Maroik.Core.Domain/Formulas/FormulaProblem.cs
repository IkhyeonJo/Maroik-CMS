namespace Maroik.Core.Domain.Formulas;

/// <summary>A problem of a formula being typed, and whether it blocks saving.</summary>
/// <param name="Diagnostic">The problem and its span.</param>
/// <param name="Blocking">True for an error (not saved), false for a warning (#NAME? / #REF!).</param>
public sealed record FormulaProblem(FormulaDiagnostic Diagnostic, bool Blocking);
