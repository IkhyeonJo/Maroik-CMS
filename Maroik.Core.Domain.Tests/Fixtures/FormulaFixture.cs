using Maroik.Core.Domain.Formulas;

namespace Maroik.Core.Domain.Tests.Fixtures;

/// <summary>
/// A small workbook for the formula tests: sheets "S" (the home sheet) and "T", each with rows keyed "R1" .. "R10" at row numbers
/// 1 .. 10 and columns A .. Z; cell values come from a dictionary keyed by A1 text ("B2", "T!C3"), actuals from another.
/// </summary>
internal static class FormulaFixture
{
    /// <summary>The row keys of every sheet.</summary>
    private static readonly IReadOnlyList<string> Rows = [.. Enumerable.Range(1, 10).Select(n => $"R{n}")];

    /// <summary>The scope a formula of sheet S is bound in.</summary>
    public static FormulaScope Scope { get; } = new("S",
        name => name.Equals("S", StringComparison.OrdinalIgnoreCase) ? "S" : name.Equals("T", StringComparison.OrdinalIgnoreCase) ? "T" : null,
        _ => Rows, 26);

    /// <summary>The address of A1 text such as "B2" or "T!C3".</summary>
    public static CellAddress At(string a1)
    {
        string sheet = a1.Contains('!', StringComparison.Ordinal) ? a1[..a1.IndexOf('!', StringComparison.Ordinal)] : "S";
        string cell = a1[(a1.IndexOf('!', StringComparison.Ordinal) + 1)..];
        return new CellAddress(sheet, "R" + cell[1..], cell[..1]);
    }

    /// <summary>Parses and binds a formula of sheet S (A1 form).</summary>
    public static BoundFormula Bind(string input) => FormulaBinder.Bind(FormulaParser.Parse(input, canonical: false), Scope);

    /// <summary>Evaluates a formula of sheet S against the given cell values and actuals.</summary>
    public static FormulaValue Evaluate(string input, IReadOnlyDictionary<string, FormulaValue>? cells = null, IReadOnlyDictionary<string, decimal>? actuals = null)
    {
        Dictionary<CellAddress, FormulaValue> values = (cells ?? new Dictionary<string, FormulaValue>()).ToDictionary(p => At(p.Key), p => p.Value);
        Dictionary<CellAddress, decimal> actualValues = (actuals ?? new Dictionary<string, decimal>()).ToDictionary(p => At(p.Key), p => p.Value);
        var context = new FormulaContext(
            address => values.GetValueOrDefault(address, EmptyValue.Instance),
            address => actualValues.TryGetValue(address, out decimal actual) ? actual : null);
        return FormulaEvaluator.Evaluate(Bind(input), context);
    }
}
