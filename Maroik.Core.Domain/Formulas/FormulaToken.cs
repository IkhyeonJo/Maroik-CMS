using ErrorOr;

namespace Maroik.Core.Domain.Formulas;

/// <summary>One token of a formula.</summary>
/// <param name="Kind">What the token is.</param>
/// <param name="Span">Where it is in the cell input.</param>
/// <param name="Text">The text as typed.</param>
/// <param name="Value">
/// The token's meaning: the text of a string literal, the sheet name of a sheet prefix, the upper-case name of a function or name,
/// a <see cref="FormulaErrorKind"/> for an error literal, a bool for a boolean; null otherwise.
/// </param>
/// <param name="Reference">The reference's parts, for a reference token.</param>
/// <param name="Problem">Why the text could not be read, for an error token.</param>
public sealed record FormulaToken(FormulaTokenKind Kind, TextSpan Span, string Text, object? Value = null, CellReference? Reference = null, Error? Problem = null);
