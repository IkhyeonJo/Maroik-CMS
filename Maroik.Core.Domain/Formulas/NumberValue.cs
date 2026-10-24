namespace Maroik.Core.Domain.Formulas;

/// <summary>A number (decimal, not floating point).</summary>
/// <param name="Value">The number.</param>
public sealed record NumberValue(decimal Value) : FormulaValue;
