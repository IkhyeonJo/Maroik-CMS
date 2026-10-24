namespace Maroik.Core.Domain.Formulas;

/// <summary>One token as the editor colours it.</summary>
/// <param name="Span">Where it is in the input.</param>
/// <param name="Class">
/// Its colour class: operator, function, unknown-function, paren, comma, reference, sheet, number, string, boolean, error-literal,
/// name or error.
/// </param>
/// <param name="Reference">For a reference or sheet token, the index of its distinct reference (its colour); null otherwise.</param>
/// <param name="Partner">For a parenthesis, the token index of the matching one; null otherwise or when unmatched.</param>
public sealed record HighlightToken(TextSpan Span, string Class, int? Reference, int? Partner);
