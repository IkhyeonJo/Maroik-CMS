using ErrorOr;
using Maroik.Core.Domain.Errors;
using Maroik.Core.Domain.Finances;
using Maroik.Core.Domain.Formulas;

namespace Maroik.Core.Domain.Budgets;

/// <summary>
/// An account's budget sheets calculated together: every stored input and every computed cell is defined in one
/// <see cref="RecalculationEngine"/>, so a formula can read any loaded sheet and an edit reaches its dependents on every sheet.
/// It shows a sheet (<see cref="View"/>) with each budget cell against the actuals, and turns what the user does — typing
/// (<see cref="Edit"/>), paste / fill (<see cref="Copy"/>), carrying last year over (<see cref="CarryOver"/>) — into the inputs to
/// store and the cells whose value changed. <see cref="Preview"/> colours an input being typed and shows the value it would have.
/// <para>
/// A budget cell's value is its budget only as a number from 0 to <see cref="FinanceAmountPolicy.MaxAbsoluteAmount"/>, rounded to
/// <see cref="FinanceAmountPolicy.MaxDecimalPlaces"/> decimals; a negative or larger number is #NUM!, and what its readers see too.
/// </para>
/// </summary>
public sealed class BudgetWorkbook
{
    /// <summary>One loaded sheet.</summary>
    private sealed class Sheet(BudgetSheetData data, BudgetSheetLayout layout, string name)
    {
        /// <summary>The loaded data.</summary>
        public BudgetSheetData Data { get; } = data;

        /// <summary>The sheet's rows and cell roles.</summary>
        public BudgetSheetLayout Layout { get; } = layout;

        /// <summary>The sheet's formula name.</summary>
        public string Name { get; } = name;

        /// <summary>The row keys, top to bottom.</summary>
        public IReadOnlyList<string> RowKeys { get; } = [.. layout.Rows.Select(r => r.Key)];

        /// <summary>The current inputs of its editable cells.</summary>
        public Dictionary<BudgetCellKey, string> Inputs { get; } = [];

        /// <summary>The scope its formulas are bound in (set once every sheet is known).</summary>
        public FormulaScope Scope { get; set; } = null!;
    }

    /// <summary>The loaded sheets by name.</summary>
    private readonly Dictionary<string, Sheet> _sheets = new(StringComparer.Ordinal);

    /// <summary>The actuals.</summary>
    private readonly BudgetActuals _actuals;

    /// <summary>The calculation.</summary>
    private readonly RecalculationEngine _engine;

    /// <summary>The columns A..Z.</summary>
    private static readonly IReadOnlyList<string> Columns = [.. Enumerable.Range(1, BudgetSheetLayout.ColumnCount).Select(FormulaColumns.Letters)];

    /// <summary>Loads <paramref name="sheets"/> with <paramref name="actuals"/> and calculates them.</summary>
    public BudgetWorkbook(IEnumerable<BudgetSheetData> sheets, BudgetActuals actuals)
    {
        _actuals = actuals;
        _engine = new RecalculationEngine(ActualOf, Normalize);
        foreach (BudgetSheetData data in sheets)
        {
            string name = BudgetSheetName.Format(data.Kind, data.Year);
            _sheets[name] = new Sheet(data, BudgetSheetLayout.Create(data.Kind, data.Lines), name);
        }

        foreach (Sheet sheet in _sheets.Values)
        {
            sheet.Scope = new FormulaScope(sheet.Name, text => Resolve(text, sheet.Data.Year), name => _sheets[name].RowKeys, BudgetSheetLayout.ColumnCount);
            foreach (var (key, input) in sheet.Data.Inputs)
            {
                if (sheet.Layout.Role(key.RowKey, key.Column) is not (BudgetCellRole.Budget or BudgetCellRole.Free)) continue;
                sheet.Inputs[key] = input;
                _engine.Define(new CellAddress(sheet.Name, key.RowKey, key.Column), BudgetCellInput.Define(input, sheet.Scope));
            }

            foreach (BudgetRow row in sheet.Layout.Rows)
                foreach (string column in Columns)
                    if (sheet.Layout.ComputedFormula(row.Key, column) is { } formula)
                        _engine.Define(new CellAddress(sheet.Name, row.Key, column), BudgetCellInput.Define(formula, sheet.Scope));
        }

        _engine.RecalculateAll();
    }

    /// <summary>The loaded sheet a formula's sheet name means, or null.</summary>
    private string? Resolve(string text, int homeYear) =>
        BudgetSheetName.TryParse(text, homeYear, out BudgetKind kind, out int year) && _sheets.ContainsKey(BudgetSheetName.Format(kind, year))
            ? BudgetSheetName.Format(kind, year)
            : null;

    /// <summary>The sheets an input names (a name without a year meaning <paramref name="homeYear"/>), in order of appearance.</summary>
    public static IReadOnlyList<(BudgetKind Kind, int Year)> SheetsReferencedBy(string input, int homeYear) =>
        [.. FormulaLexer.Tokenize(input, 0, canonical: true)
            .Where(t => t.Kind == FormulaTokenKind.SheetPrefix)
            .Select(t => BudgetSheetName.TryParse((string)t.Value!, homeYear, out BudgetKind kind, out int year) ? ((BudgetKind, int)?)(kind, year) : null)
            .OfType<(BudgetKind, int)>()
            .Distinct()];

    /// <summary>The actual total behind a budget cell, or null for any other cell.</summary>
    private decimal? ActualOf(CellAddress address)
    {
        Sheet sheet = _sheets[address.Sheet];
        if (sheet.Layout.Role(address.RowKey, address.Column) != BudgetCellRole.Budget) return null;
        return _actuals.Of(sheet.Data.Kind, sheet.Data.Year, BudgetSheetLayout.MonthOf(address.Column)!.Value, sheet.Layout.LineOf(address.RowKey)!);
    }

    /// <summary>A budget cell's number is its budget only within the amount range, rounded to the stored decimals; otherwise #NUM!.</summary>
    private FormulaValue Normalize(CellAddress address, FormulaValue value)
    {
        if (value is not NumberValue number || _sheets[address.Sheet].Layout.Role(address.RowKey, address.Column) != BudgetCellRole.Budget) return value;
        return number.Value is < 0 or > FinanceAmountPolicy.MaxAbsoluteAmount
            ? new ErrorValue(FormulaErrorKind.Num)
            : new NumberValue(Math.Round(number.Value, FinanceAmountPolicy.MaxDecimalPlaces, MidpointRounding.AwayFromZero));
    }

    /// <summary>The sheet with its rows, the cells that have something to show and the records of lines it does not have.</summary>
    public BudgetSheetView View(BudgetKind kind, int year)
    {
        Sheet sheet = _sheets[BudgetSheetName.Format(kind, year)];
        List<BudgetCellView> cells = [];
        foreach (BudgetRow row in sheet.Layout.Rows)
            foreach (string column in Columns)
            {
                var key = new BudgetCellKey(row.Key, column);
                if (sheet.Inputs.ContainsKey(key) || sheet.Layout.Role(row.Key, column) is BudgetCellRole.Budget or BudgetCellRole.Computed)
                    cells.Add(CellView(sheet, key));
            }

        List<BudgetUnbudgetedLine> unbudgeted = [.. _actuals.LinesOf(kind, year)
            .Where(l => sheet.Layout.LineOf(l.Line.Key) is null && l.Months.Any(m => m != 0))
            .OrderBy(l => l.Line.MainClass, StringComparer.Ordinal).ThenBy(l => l.Line.SubClass, StringComparer.Ordinal).ThenBy(l => l.Line.Currency, StringComparer.Ordinal)
            .Select(l => new BudgetUnbudgetedLine(l.Line, l.Months))];
        return new BudgetSheetView(kind, year, sheet.Data.Exists, sheet.Layout.Rows, cells, unbudgeted);
    }

    /// <summary>One cell as shown.</summary>
    private BudgetCellView CellView(Sheet sheet, BudgetCellKey key)
    {
        BudgetCellRole role = sheet.Layout.Role(key.RowKey, key.Column)!.Value;
        string? input = sheet.Inputs.GetValueOrDefault(key) ?? sheet.Layout.ComputedFormula(key.RowKey, key.Column);
        FormulaValue value = _engine.ValueOf(new CellAddress(sheet.Name, key.RowKey, key.Column));
        return new BudgetCellView(sheet.Layout.A1(key.RowKey, key.Column), role, input is null ? null : FormulaRewriter.ToA1(input, sheet.Scope), value,
            role == BudgetCellRole.Budget ? Status(sheet, key, value) : null);
    }

    /// <summary>A budget cell against the actuals.</summary>
    private BudgetCellStatus Status(Sheet sheet, BudgetCellKey key, FormulaValue value)
    {
        decimal actual = _actuals.Of(sheet.Data.Kind, sheet.Data.Year, BudgetSheetLayout.MonthOf(key.Column)!.Value, sheet.Layout.LineOf(key.RowKey)!);
        if (value is not NumberValue budget)
            return new BudgetCellStatus(null, actual, null, actual != 0 ? BudgetCellState.Unbudgeted : BudgetCellState.None);

        BudgetCellState state = actual.CompareTo(budget.Value) switch
        {
            < 0 => BudgetCellState.Under,
            0 => BudgetCellState.Reached,
            _ => BudgetCellState.Over,
        };
        return new BudgetCellStatus(budget.Value, actual, budget.Value - actual, state);
    }

    /// <summary>The sheet of an edit, or the error that it is not loaded.</summary>
    private ErrorOr<Sheet> SheetOf(BudgetKind kind, int year) =>
        _sheets.TryGetValue(BudgetSheetName.Format(kind, year), out Sheet? sheet)
            ? sheet
            : DomainError.Validation("Budget.SheetUnknown", "There is no sheet {0}.", BudgetSheetName.Format(kind, year));

    /// <summary>The error for an address the sheet does not have.</summary>
    private static Error UnknownCell(string address) =>
        DomainError.Validation("Budget.CellUnknown", "There is no cell {0} in this sheet.", address);

    /// <summary>
    /// Applies typed edits to a sheet: each is checked against its cell's role (all of them before any is applied), stored canonically,
    /// and what the edits reach is recalculated. Returns the inputs to store and the sheet's changed cells (the edited ones always).
    /// </summary>
    public ErrorOr<BudgetEdit> Edit(BudgetKind kind, int year, IReadOnlyList<BudgetCellEdit> edits)
    {
        ErrorOr<Sheet> found = SheetOf(kind, year);
        if (found.IsError) return found.Errors;
        Sheet sheet = found.Value;

        List<BudgetCellWrite> writes = [];
        foreach (BudgetCellEdit edit in edits)
        {
            if (sheet.Layout.Locate(edit.Address) is not var (rowKey, column)) return UnknownCell(edit.Address);
            ErrorOr<string?> stored = BudgetCellInput.Parse(sheet.Layout.Role(rowKey, column)!.Value, edit.Input, sheet.Scope);
            if (stored.IsError) return stored.Errors;
            writes.Add(new BudgetCellWrite(rowKey, column, stored.Value));
        }

        List<CellAddress> addresses = [];
        foreach (BudgetCellWrite write in writes)
        {
            var key = new BudgetCellKey(write.RowKey, write.Column);
            var address = new CellAddress(sheet.Name, write.RowKey, write.Column);
            if (write.Input is null) sheet.Inputs.Remove(key);
            else sheet.Inputs[key] = write.Input;
            _engine.Define(address, write.Input is null ? null : BudgetCellInput.Define(write.Input, sheet.Scope));
            addresses.Add(address);
        }

        IReadOnlyList<CellAddress> changed = _engine.Recalculate(addresses);
        List<BudgetCellView> views = [.. addresses.Concat(changed).Distinct()
            .Where(a => a.Sheet == sheet.Name)
            .Select(a => CellView(sheet, new BudgetCellKey(a.RowKey, a.Column)))];
        return new BudgetEdit(writes, views);
    }

    /// <summary>
    /// Copies the cells of <paramref name="source"/> ("B2" or "B2:C3") to <paramref name="target"/> as Excel pastes: tiled when the
    /// target is a multiple of the source block, otherwise once at the target's top-left; formulas move their relative references by
    /// the offset (a computed cell copies its formula, a label or blank cell copies as empty). Each target cell must accept what it
    /// receives, and the paste must fit in the sheet.
    /// </summary>
    public ErrorOr<BudgetEdit> Copy(BudgetKind kind, int year, string source, string target)
    {
        ErrorOr<Sheet> found = SheetOf(kind, year);
        if (found.IsError) return found.Errors;
        Sheet sheet = found.Value;

        if (Block(sheet, source) is not var (r1, c1, h, w)) return UnknownCell(source);
        if (Block(sheet, target) is not var (t1, tc1, th, tw)) return UnknownCell(target);
        if (th % h != 0 || tw % w != 0) (th, tw) = (h, w);
        if (t1 + th - 1 > sheet.RowKeys.Count || tc1 + tw - 1 > BudgetSheetLayout.ColumnCount)
            return DomainError.Validation("Budget.PasteOutside", "The paste does not fit in the sheet.");

        List<BudgetCellEdit> edits = [];
        for (int dr = 0; dr < th; dr++)
            for (int dc = 0; dc < tw; dc++)
            {
                int sr = r1 + (dr % h), sc = c1 + (dc % w), tr = t1 + dr, tc = tc1 + dc;
                string sourceKey = sheet.RowKeys[sr - 1], sourceColumn = FormulaColumns.Letters(sc);
                string? input = sheet.Inputs.GetValueOrDefault(new BudgetCellKey(sourceKey, sourceColumn)) ?? sheet.Layout.ComputedFormula(sourceKey, sourceColumn);
                string? moved = input is null ? null : FormulaRewriter.ToA1(FormulaRewriter.Shift(input, sheet.Scope, tr - sr, tc - sc), sheet.Scope);
                edits.Add(new BudgetCellEdit(FormulaColumns.Letters(tc) + tr.ToString(System.Globalization.CultureInfo.InvariantCulture), moved));
            }

        return Edit(kind, year, edits);
    }

    /// <summary>A block "B2" or "B2:C3" as its top row, left column, height and width; null when a corner is not a cell of the sheet.</summary>
    private static (int Row, int Column, int Height, int Width)? Block(Sheet sheet, string text)
    {
        string[] corners = text.Split(':');
        if (corners.Length > 2) return null;
        List<(int Row, int Column)> cells = [];
        foreach (string corner in corners)
        {
            if (sheet.Layout.Locate(corner) is not var (key, column)) return null;
            cells.Add((sheet.Layout.Row(key)!.Number, FormulaColumns.Number(column)));
        }

        int top = cells.Min(c => c.Row), left = cells.Min(c => c.Column);
        return (top, left, cells.Max(c => c.Row) - top + 1, cells.Max(c => c.Column) - left + 1);
    }

    /// <summary>
    /// The colouring of <paramref name="input"/> being typed into <paramref name="address"/>, and — for a formula that can be saved —
    /// the value it would have there (#CYCLE! when it would read its own cell), without changing anything.
    /// </summary>
    public BudgetFormulaPreview Preview(BudgetKind kind, int year, string address, string input)
    {
        Sheet sheet = _sheets[BudgetSheetName.Format(kind, year)];
        FormulaHighlight highlight = FormulaHighlighter.Highlight(input, sheet.Scope);
        if (!input.StartsWith('=') || !highlight.CanSave || sheet.Layout.Locate(address) is not var (key, column))
            return new BudgetFormulaPreview(highlight, null);

        var cell = new CellAddress(sheet.Name, key, column);
        FormulaValue value = _engine.Preview(cell, FormulaBinder.Bind(FormulaParser.Parse(input, canonical: false), sheet.Scope));
        return new BudgetFormulaPreview(highlight, Normalize(cell, value));
    }

    /// <summary>
    /// Fills this year's empty budget cells with last year's inputs, for the lines both sheets have (an edit, recalculated like any
    /// other). Nothing is copied when last year's sheet is not loaded.
    /// </summary>
    public ErrorOr<BudgetEdit> CarryOver(BudgetKind kind, int year)
    {
        ErrorOr<Sheet> found = SheetOf(kind, year);
        if (found.IsError) return found.Errors;
        Sheet sheet = found.Value;
        if (!_sheets.TryGetValue(BudgetSheetName.Format(kind, year - 1), out Sheet? last)) return new BudgetEdit([], []);

        List<BudgetCellEdit> edits = [];
        foreach (BudgetRow row in sheet.Layout.Rows.Where(r => r.Kind == BudgetRowKind.Line && last.Layout.LineOf(r.Key) is not null))
            foreach (string column in Columns.Where(c => BudgetSheetLayout.MonthOf(c) is not null))
            {
                var key = new BudgetCellKey(row.Key, column);
                if (!sheet.Inputs.ContainsKey(key) && last.Inputs.TryGetValue(key, out string? input))
                    edits.Add(new BudgetCellEdit(sheet.Layout.A1(row.Key, column), FormulaRewriter.ToA1(input, sheet.Scope)));
            }

        return Edit(kind, year, edits);
    }
}
