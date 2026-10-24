namespace Maroik.Core.Domain.Formulas;

/// <summary>What an evaluation reads: cell values, and the actual totals behind budget cells.</summary>
/// <param name="ValueOf">The current value of a cell (empty when it has no input).</param>
/// <param name="ActualOf">The actual total for a budget cell, or null when the cell is not a budget cell.</param>
public sealed record FormulaContext(Func<CellAddress, FormulaValue> ValueOf, Func<CellAddress, decimal?> ActualOf);
