namespace Maroik.Core.Domain.Formulas;

/// <summary>
/// What the editor shows while a formula is typed (A1 form): each token's colour class, a colour index per distinct reference (the
/// same reference written twice keeps its colour, a range is one reference, as in Excel) with the home-sheet cells it outlines, the
/// partner of each parenthesis, and the problems with their spans. The client only paints this; the language lives here.
/// </summary>
public static class FormulaHighlighter
{
    /// <summary>The highlight of <paramref name="input"/> in <paramref name="scope"/>; input that is not a formula has none.</summary>
    public static FormulaHighlight Highlight(string input, FormulaScope scope)
    {
        if (!input.StartsWith('=')) return new FormulaHighlight([], [], []);

        FormulaSyntax syntax = FormulaParser.Parse(input, canonical: false);
        BoundFormula bound = FormulaBinder.Bind(syntax, scope);
        IReadOnlyList<FormulaToken> tokens = syntax.Tokens;

        int?[] referenceOf = new int?[tokens.Count];
        List<HighlightReference> references = [];
        Dictionary<string, int> seen = new(StringComparer.Ordinal);
        for (int i = 0; i < tokens.Count; i++)
        {
            int first = i;
            FormulaToken? sheet = tokens[i].Kind == FormulaTokenKind.SheetPrefix ? tokens[i] : null;
            int reference = sheet is null ? i : i + 1;
            if (reference >= tokens.Count || tokens[reference].Kind != FormulaTokenKind.Reference) continue;
            int last = reference + 2 < tokens.Count && tokens[reference + 1].Kind == FormulaTokenKind.Colon && tokens[reference + 2].Kind == FormulaTokenKind.Reference
                ? reference + 2
                : reference;

            string? sheetName = sheet is null ? scope.HomeSheet : scope.ResolveSheet((string)sheet.Value!);
            CellReference start = tokens[reference].Reference!, end = tokens[last].Reference!;
            string key = (sheetName ?? ((string)sheet!.Value!).ToUpperInvariant()) + "!" + start.Column + start.RowNumber + ":" + end.Column + end.RowNumber;
            if (!seen.TryGetValue(key, out int index))
            {
                index = references.Count;
                seen[key] = index;
                references.Add(Outline(index, sheetName == scope.HomeSheet, start, end, scope));
            }

            for (int t = first; t <= last; t++)
                if (tokens[t].Kind is FormulaTokenKind.SheetPrefix or FormulaTokenKind.Reference) referenceOf[t] = index;
            i = last;
        }

        int?[] partner = new int?[tokens.Count];
        Stack<int> open = new();
        for (int i = 0; i < tokens.Count; i++)
        {
            if (tokens[i].Kind == FormulaTokenKind.OpenParen) open.Push(i);
            else if (tokens[i].Kind == FormulaTokenKind.CloseParen && open.TryPop(out int match))
            {
                partner[i] = match;
                partner[match] = i;
            }
        }

        List<HighlightToken> highlighted = [.. tokens.Select((t, i) => new HighlightToken(t.Span, ClassOf(t), referenceOf[i], partner[i]))];
        List<FormulaProblem> problems = [.. bound.Errors.Select(d => new FormulaProblem(d, true)).Concat(bound.Warnings.Select(d => new FormulaProblem(d, false)))
            .OrderBy(p => p.Diagnostic.Span.Start)];
        return new FormulaHighlight(highlighted, references, problems);
    }

    /// <summary>A reference's outline: its A1 corners when it is on the home sheet and inside it; none otherwise.</summary>
    private static HighlightReference Outline(int index, bool homeSheet, CellReference start, CellReference end, FormulaScope scope)
    {
        int rows = scope.RowKeysOf(scope.HomeSheet).Count;
        int c1 = FormulaColumns.Number(start.Column), c2 = FormulaColumns.Number(end.Column);
        if (!homeSheet || start.RowNumber is not { } r1 || end.RowNumber is not { } r2 || Math.Max(r1, r2) > rows || Math.Max(c1, c2) > scope.ColumnCount)
            return new HighlightReference(index, null, null);

        string Cell(int column, int row) => FormulaColumns.Letters(column) + row.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return new HighlightReference(index, Cell(Math.Min(c1, c2), Math.Min(r1, r2)), Cell(Math.Max(c1, c2), Math.Max(r1, r2)));
    }

    /// <summary>A token's colour class.</summary>
    private static string ClassOf(FormulaToken token) => token.Kind switch
    {
        FormulaTokenKind.Number => "number",
        FormulaTokenKind.String => "string",
        FormulaTokenKind.Boolean => "boolean",
        FormulaTokenKind.ErrorLiteral => "error-literal",
        FormulaTokenKind.Reference => "reference",
        FormulaTokenKind.SheetPrefix => "sheet",
        FormulaTokenKind.Function => FormulaFunctions.Find((string)token.Value!) is null ? "unknown-function" : "function",
        FormulaTokenKind.Name => "name",
        FormulaTokenKind.OpenParen or FormulaTokenKind.CloseParen => "paren",
        FormulaTokenKind.Comma => "comma",
        FormulaTokenKind.Error => "error",
        _ => "operator",
    };
}
