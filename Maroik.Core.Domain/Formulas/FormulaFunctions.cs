namespace Maroik.Core.Domain.Formulas;

/// <summary>
/// The closed set of functions a formula can call (Excel names and semantics), with their argument counts and the hint the
/// editor's autocomplete shows. Nothing outside this list is ever invoked: an unknown name evaluates to #NAME?.
/// </summary>
public static class FormulaFunctions
{
    /// <summary>Every function, in the order the editor lists them.</summary>
    public static IReadOnlyList<FormulaFunction> All { get; } =
    [
        new("SUM", 1, FormulaParser.MaxArguments, "SUM(number1, [number2], ...)"),
        new("AVERAGE", 1, FormulaParser.MaxArguments, "AVERAGE(number1, [number2], ...)"),
        new("MIN", 1, FormulaParser.MaxArguments, "MIN(number1, [number2], ...)"),
        new("MAX", 1, FormulaParser.MaxArguments, "MAX(number1, [number2], ...)"),
        new("COUNT", 1, FormulaParser.MaxArguments, "COUNT(value1, [value2], ...)"),
        new("ROUND", 2, 2, "ROUND(number, num_digits)"),
        new("ROUNDUP", 2, 2, "ROUNDUP(number, num_digits)"),
        new("ROUNDDOWN", 2, 2, "ROUNDDOWN(number, num_digits)"),
        new("ABS", 1, 1, "ABS(number)"),
        new("IF", 2, 3, "IF(logical_test, value_if_true, [value_if_false])"),
        new("IFERROR", 2, 2, "IFERROR(value, value_if_error)"),
        new("AND", 1, FormulaParser.MaxArguments, "AND(logical1, [logical2], ...)"),
        new("OR", 1, FormulaParser.MaxArguments, "OR(logical1, [logical2], ...)"),
        new("NOT", 1, 1, "NOT(logical)"),
        new("ACTUAL", 1, 1, "ACTUAL(budget_cells)"),
    ];

    /// <summary>The function named <paramref name="name"/> (any case), or null.</summary>
    public static FormulaFunction? Find(string name) =>
        All.FirstOrDefault(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
}
