namespace Maroik.Core.Domain.Formulas;

/// <summary>An error value (#DIV/0!, #VALUE!, ...).</summary>
/// <param name="Kind">Which error.</param>
public sealed record ErrorValue(FormulaErrorKind Kind) : FormulaValue;
