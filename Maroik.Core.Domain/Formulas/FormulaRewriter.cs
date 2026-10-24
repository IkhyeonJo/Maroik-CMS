using System.Text;

namespace Maroik.Core.Domain.Formulas;

/// <summary>
/// Rewrites the references of a formula, leaving the rest of its text as typed: between the A1 form the user types and the stored
/// form whose rows are stable keys (<c>B{key}</c>, so inserting or removing a row never breaks a reference), and the shift of relative
/// references when a formula is copied to another cell (Excel's paste and fill). A reference that cannot be resolved becomes
/// <c>#REF!</c> — the whole range, sheet prefix included — as Excel writes it. Function names are upper-cased. Input that is not a
/// formula (no leading "=") is returned unchanged.
/// </summary>
public static class FormulaRewriter
{
    /// <summary>What stands in for a reference that cannot be resolved.</summary>
    private const string RefError = "#REF!";

    /// <summary>The stored form of A1 input.</summary>
    public static string ToCanonical(string input, FormulaScope scope) =>
        Rewrite(input, scope, canonical: false, (rows, corner) => corner.Index is { } i ? Key(corner, rows[i]) : null);

    /// <summary>The A1 form of stored input.</summary>
    public static string ToA1(string input, FormulaScope scope) =>
        Rewrite(input, scope, canonical: true, (_, corner) => corner.Index is { } i
            ? Column(corner) + (corner.Cell.RowAbsolute ? "$" : "") + (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)
            : null);

    /// <summary>
    /// Stored input copied <paramref name="rows"/> rows down and <paramref name="columns"/> columns right: relative parts move,
    /// absolute ("$") parts stay; a reference moved off its sheet is #REF!.
    /// </summary>
    public static string Shift(string input, FormulaScope scope, int rows, int columns) =>
        Rewrite(input, scope, canonical: true, (sheetRows, corner) =>
        {
            if (corner.Index is not { } index) return null;
            int row = corner.Cell.RowAbsolute ? index : index + rows;
            int column = FormulaColumns.Number(corner.Cell.Column) + (corner.Cell.ColumnAbsolute ? 0 : columns);
            if (row < 0 || row >= sheetRows.Count || column < 1 || column > scope.ColumnCount) return null;
            return (corner.Cell.ColumnAbsolute ? "$" : "") + FormulaColumns.Letters(column) + (corner.Cell.RowAbsolute ? "$" : "") + "{" + sheetRows[row] + "}";
        });

    /// <summary>A corner of a reference unit: what was written and the row index it resolves to (null when it does not).</summary>
    private sealed record Corner(CellReference Cell, int? Index);

    /// <summary>The column part as written.</summary>
    private static string Column(Corner corner) => (corner.Cell.ColumnAbsolute ? "$" : "") + corner.Cell.Column;

    /// <summary>The stored text of a corner at a row key.</summary>
    private static string Key(Corner corner, string key) => Column(corner) + (corner.Cell.RowAbsolute ? "$" : "") + "{" + key + "}";

    /// <summary>
    /// Copies <paramref name="input"/>, replacing each reference unit ([sheet!]ref[:ref]) with what <paramref name="corner"/> makes of
    /// its corners (null for any corner = #REF! for the unit) and upper-casing function names.
    /// </summary>
    private static string Rewrite(string input, FormulaScope scope, bool canonical, Func<IReadOnlyList<string>, Corner, string?> corner)
    {
        if (!input.StartsWith('=')) return input;
        IReadOnlyList<FormulaToken> tokens = FormulaLexer.Tokenize(input, 1, canonical);
        var output = new StringBuilder();
        int copied = 0;
        for (int i = 0; i < tokens.Count; i++)
        {
            FormulaToken token = tokens[i];
            if (token.Kind == FormulaTokenKind.Function)
            {
                output.Append(input, copied, token.Span.Start - copied).Append((string)token.Value!);
                copied = token.Span.End;
                continue;
            }

            if (ReferenceUnit(tokens, i) is not { } unit) continue;
            output.Append(input, copied, unit.First.Span.Start - copied).Append(RewriteUnit(unit, scope, corner));
            copied = unit.Last.Span.End;
            i = unit.LastIndex;
        }

        return output.Append(input, copied, input.Length - copied).ToString();
    }

    /// <summary>A reference unit: its tokens and the parts written.</summary>
    private sealed record Unit(FormulaToken First, FormulaToken Last, int LastIndex, FormulaToken? Sheet, FormulaToken Start, FormulaToken? End);

    /// <summary>The reference unit starting at token <paramref name="i"/>, or null when none starts there.</summary>
    private static Unit? ReferenceUnit(IReadOnlyList<FormulaToken> tokens, int i)
    {
        FormulaToken? sheet = null;
        int p = i;
        if (tokens[p].Kind == FormulaTokenKind.SheetPrefix)
        {
            sheet = tokens[p];
            p++;
        }

        if (p >= tokens.Count || tokens[p].Kind != FormulaTokenKind.Reference) return null;
        FormulaToken start = tokens[p];
        if (p + 2 < tokens.Count && tokens[p + 1].Kind == FormulaTokenKind.Colon && tokens[p + 2].Kind == FormulaTokenKind.Reference)
            return new Unit(sheet ?? start, tokens[p + 2], p + 2, sheet, start, tokens[p + 2]);
        return new Unit(sheet ?? start, start, p, sheet, start, null);
    }

    /// <summary>The new text of a unit, or #REF! when it does not resolve.</summary>
    private static string RewriteUnit(Unit unit, FormulaScope scope, Func<IReadOnlyList<string>, Corner, string?> corner)
    {
        string? sheet = unit.Sheet is null ? scope.HomeSheet : scope.ResolveSheet((string)unit.Sheet.Value!);
        if (sheet is null) return RefError;
        IReadOnlyList<string> rows = scope.RowKeysOf(sheet);

        string? start = Resolve(unit.Start.Reference!, rows, scope, corner);
        string? end = unit.End is null ? "" : Resolve(unit.End.Reference!, rows, scope, corner);
        if (start is null || end is null) return RefError;
        return (unit.Sheet?.Text ?? "") + start + (unit.End is null ? "" : ":" + end);
    }

    /// <summary>One corner's new text, or null when its column or row is outside the sheet.</summary>
    private static string? Resolve(CellReference cell, IReadOnlyList<string> rows, FormulaScope scope, Func<IReadOnlyList<string>, Corner, string?> corner)
    {
        if (FormulaColumns.Number(cell.Column) > scope.ColumnCount) return null;
        int? index = cell.RowNumber is { } number
            ? (number <= rows.Count ? number - 1 : null)
            : (IndexOf(rows, cell.RowKey!) is var at && at >= 0 ? at : null);
        return corner(rows, new Corner(cell, index));
    }

    /// <summary>The index of <paramref name="value"/> in <paramref name="list"/>, or -1.</summary>
    private static int IndexOf(IReadOnlyList<string> list, string value)
    {
        for (int i = 0; i < list.Count; i++)
            if (list[i] == value) return i;
        return -1;
    }
}
