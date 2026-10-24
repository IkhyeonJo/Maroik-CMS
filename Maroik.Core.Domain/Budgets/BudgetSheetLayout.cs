using Maroik.Core.Domain.Formulas;

namespace Maroik.Core.Domain.Budgets;

/// <summary>
/// The shape of a budget sheet: its rows — per main class (taxonomy order) a header, its lines (sub-class order, then currency) and a
/// subtotal per currency; a total per currency; then <see cref="FreeRowCount"/> free rows — and its columns A..Z: A labels, B..M the
/// months (January..December), N the year total, O..Z free. It says what each cell is (<see cref="BudgetCellRole"/>) and writes
/// the formulas of the computed cells, in the stored form with row keys.
/// </summary>
public sealed class BudgetSheetLayout
{
    /// <summary>How many free rows follow the fixed ones.</summary>
    public const int FreeRowCount = 30;

    /// <summary>How many columns a sheet has (A..Z).</summary>
    public const int ColumnCount = 26;

    /// <summary>The year-total column.</summary>
    public const string YearColumn = "N";

    /// <summary>The rows, top to bottom.</summary>
    public IReadOnlyList<BudgetRow> Rows { get; }

    /// <summary>The rows by key.</summary>
    private readonly Dictionary<string, BudgetRow> _byKey;

    /// <summary>The lines by row key.</summary>
    private readonly Dictionary<string, BudgetLine> _lines;

    /// <summary>Builds the layout from its rows and lines.</summary>
    private BudgetSheetLayout(List<BudgetRow> rows, IEnumerable<BudgetLine> lines)
    {
        Rows = rows;
        _byKey = rows.ToDictionary(r => r.Key, StringComparer.Ordinal);
        _lines = lines.ToDictionary(l => l.Key, StringComparer.Ordinal);
    }

    /// <summary>The layout of a <paramref name="kind"/> sheet with <paramref name="lines"/>.</summary>
    public static BudgetSheetLayout Create(BudgetKind kind, IEnumerable<BudgetLine> lines)
    {
        List<BudgetLine> all = [.. lines.DistinctBy(l => l.Key)];
        List<BudgetRow> rows = [];
        IReadOnlyDictionary<string, IReadOnlyList<string>> taxonomy = BudgetLine.Taxonomy(kind);
        foreach (string main in BudgetLine.MainClasses(kind))
        {
            List<BudgetLine> group = [.. all.Where(l => l.MainClass == main)
                .OrderBy(l => IndexOf(taxonomy[main], l.SubClass)).ThenBy(l => l.Currency, StringComparer.Ordinal)];
            if (group.Count == 0) continue;
            rows.Add(Row(rows, "H." + main, BudgetRowKind.Header, main, null, null));
            foreach (BudgetLine line in group)
                rows.Add(Row(rows, line.Key, BudgetRowKind.Line, main, line.SubClass, line.Currency));
            foreach (string currency in Currencies(group))
                rows.Add(Row(rows, $"S.{main}.{BudgetLine.Hex(currency)}", BudgetRowKind.Subtotal, main, null, currency));
        }

        foreach (string currency in Currencies(all.Where(l => rows.Any(r => r.Key == l.Key))))
            rows.Add(Row(rows, "T." + BudgetLine.Hex(currency), BudgetRowKind.Total, null, null, currency));
        for (int i = 1; i <= FreeRowCount; i++)
            rows.Add(Row(rows, "F." + i.ToString(System.Globalization.CultureInfo.InvariantCulture), BudgetRowKind.Free, null, null, null));

        return new BudgetSheetLayout(rows, all.Where(l => rows.Any(r => r.Key == l.Key)));
    }

    /// <summary>The next row.</summary>
    private static BudgetRow Row(List<BudgetRow> rows, string key, BudgetRowKind kind, string? main, string? sub, string? currency) =>
        new(rows.Count + 1, key, kind, main, sub, currency);

    /// <summary>The distinct currencies of some lines, in ordinal order.</summary>
    private static IEnumerable<string> Currencies(IEnumerable<BudgetLine> lines) =>
        lines.Select(l => l.Currency).Distinct().Order(StringComparer.Ordinal);

    /// <summary>The position of <paramref name="value"/> in <paramref name="list"/> (unknown values last).</summary>
    private static int IndexOf(IReadOnlyList<string> list, string value)
    {
        for (int i = 0; i < list.Count; i++)
            if (list[i] == value) return i;
        return int.MaxValue;
    }

    /// <summary>The row with <paramref name="key"/>, or null.</summary>
    public BudgetRow? Row(string key) => _byKey.GetValueOrDefault(key);

    /// <summary>The row at <paramref name="number"/> (1-based), or null.</summary>
    public BudgetRow? RowAt(int number) => number >= 1 && number <= Rows.Count ? Rows[number - 1] : null;

    /// <summary>The line of a line row, or null.</summary>
    public BudgetLine? LineOf(string key) => _lines.GetValueOrDefault(key);

    /// <summary>The month of a column: B = 1 .. M = 12; null for any other column.</summary>
    public static int? MonthOf(string column) =>
        column.Length == 1 && column[0] is >= 'B' and <= 'M' ? column[0] - 'A' : null;

    /// <summary>What a cell is, or null when it is outside the sheet.</summary>
    public BudgetCellRole? Role(string key, string column)
    {
        if (Row(key) is not { } row || column.Length != 1 || column[0] is < 'A' or > 'Z') return null;
        if (row.EditableColumns.Contains(column[0], StringComparison.Ordinal))
            return row.Kind == BudgetRowKind.Line && MonthOf(column) is not null ? BudgetCellRole.Budget : BudgetCellRole.Free;
        if (column == "A") return BudgetCellRole.Label;
        return row.Kind == BudgetRowKind.Header ? BudgetCellRole.Blank : BudgetCellRole.Computed;
    }

    /// <summary>The formula of a computed cell (stored form), or null for any other cell.</summary>
    public string? ComputedFormula(string key, string column)
    {
        if (Role(key, column) != BudgetCellRole.Computed) return null;
        BudgetRow row = Row(key)!;
        IEnumerable<string> keys = row.Kind switch
        {
            BudgetRowKind.Line => [],
            BudgetRowKind.Subtotal => Rows.Where(r => r.Kind == BudgetRowKind.Line && r.MainClass == row.MainClass && r.Currency == row.Currency).Select(r => r.Key),
            _ => Rows.Where(r => r.Kind == BudgetRowKind.Subtotal && r.Currency == row.Currency).Select(r => r.Key),
        };
        return row.Kind == BudgetRowKind.Line
            ? $"=SUM(B{{{key}}}:M{{{key}}})"
            : "=SUM(" + string.Join(",", keys.Select(k => $"{column}{{{k}}}")) + ")";
    }

    /// <summary>The A1 address of a cell.</summary>
    public string A1(string key, string column) => column + Row(key)!.Number.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>The cell an A1 address names (any case), or null when it is not one of the sheet's cells.</summary>
    public (string Key, string Column)? Locate(string a1)
    {
        IReadOnlyList<FormulaToken> tokens = FormulaLexer.Tokenize(a1, 0, canonical: false);
        if (tokens is not [{ Kind: FormulaTokenKind.Reference, Reference: { RowNumber: { } number } cell }] || cell.Column.Length != 1) return null;
        return RowAt(number) is { } row ? (row.Key, cell.Column) : null;
    }
}
