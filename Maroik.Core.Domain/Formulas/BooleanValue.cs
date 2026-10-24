namespace Maroik.Core.Domain.Formulas;

/// <summary>TRUE or FALSE.</summary>
/// <param name="Value">The boolean.</param>
public sealed record BooleanValue(bool Value) : FormulaValue;
