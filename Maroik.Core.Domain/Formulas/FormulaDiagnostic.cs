using ErrorOr;

namespace Maroik.Core.Domain.Formulas;

/// <summary>A problem in a formula, at the exact span the editor underlines; the error carries the message template (DomainError).</summary>
/// <param name="Span">Where the problem is.</param>
/// <param name="Error">What it is.</param>
public sealed record FormulaDiagnostic(TextSpan Span, Error Error);
