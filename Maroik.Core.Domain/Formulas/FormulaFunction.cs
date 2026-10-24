namespace Maroik.Core.Domain.Formulas;

/// <summary>A function of the catalog.</summary>
/// <param name="Name">Its name, upper-case.</param>
/// <param name="MinArguments">Fewest arguments.</param>
/// <param name="MaxArguments">Most arguments.</param>
/// <param name="Signature">The argument hint the editor shows, e.g. "ROUND(number, num_digits)".</param>
public sealed record FormulaFunction(string Name, int MinArguments, int MaxArguments, string Signature);
