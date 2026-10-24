using Maroik.Core.Domain.Errors;

namespace Maroik.Core.Domain.Formulas;

/// <summary>
/// Resolves a parsed formula against the workbook: each reference to a cell address (sheet, row key, column), each range to its
/// cells row by row, each call to a catalog function with its argument count checked. Syntax problems and wrong argument counts are
/// errors (the formula is not saved, as Excel refuses it); an unknown function, name or sheet and a reference outside a sheet are
/// warnings (the formula is saved and shows #NAME? / #REF!, as in Excel).
/// </summary>
public static class FormulaBinder
{
    /// <summary>Binds <paramref name="syntax"/> in <paramref name="scope"/>.</summary>
    public static BoundFormula Bind(FormulaSyntax syntax, FormulaScope scope)
    {
        var walker = new Walker(scope);
        walker.Visit(syntax.Root, readsValues: true);
        List<FormulaDiagnostic> errors = [.. syntax.Diagnostics, .. walker.Errors];
        return new BoundFormula(syntax, walker.Targets, [.. errors.OrderBy(d => d.Span.Start)], walker.Warnings,
            [.. walker.Precedents.Distinct()]);
    }

    /// <summary>Walks the tree, collecting targets, problems and precedents.</summary>
    private sealed class Walker(FormulaScope scope)
    {
        /// <summary>What each reference / range resolved to.</summary>
        public Dictionary<FormulaNode, BoundTarget> Targets { get; } = new(ReferenceEqualityComparer.Instance);

        /// <summary>Problems that block saving.</summary>
        public List<FormulaDiagnostic> Errors { get; } = [];

        /// <summary>Problems that are saved as error values.</summary>
        public List<FormulaDiagnostic> Warnings { get; } = [];

        /// <summary>Cells whose values the formula reads.</summary>
        public List<CellAddress> Precedents { get; } = [];

        /// <summary>Visits a node; <paramref name="readsValues"/> is false inside ACTUAL (it reads identities, not values).</summary>
        public void Visit(FormulaNode node, bool readsValues)
        {
            switch (node)
            {
                case ReferenceNode reference:
                    Resolve(reference, reference, reference, readsValues);
                    break;
                case RangeNode range:
                    Resolve(range, range.Start, range.End, readsValues);
                    break;
                case UnaryNode unary:
                    Visit(unary.Operand, readsValues);
                    break;
                case PercentNode percent:
                    Visit(percent.Operand, readsValues);
                    break;
                case BinaryNode binary:
                    Visit(binary.Left, readsValues);
                    Visit(binary.Right, readsValues);
                    break;
                case CallNode call:
                    VisitCall(call, readsValues);
                    break;
                case NameNode name:
                    Warn(Warnings, name.Span, "Formula.UnknownName", "'{0}' is not a known name.", name.Name);
                    break;
            }
        }

        /// <summary>Checks a call against the catalog and visits its arguments.</summary>
        private void VisitCall(CallNode call, bool readsValues)
        {
            FormulaFunction? function = FormulaFunctions.Find(call.Name);
            if (function is null)
                Warn(Warnings, call.NameSpan, "Formula.UnknownFunction", "There is no function named {0}.", call.Name);
            else if (call.Arguments.Count < function.MinArguments || call.Arguments.Count > function.MaxArguments)
                Warn(Errors, call.NameSpan, "Formula.ArgumentCount", "{0} takes {1} to {2} arguments.", function.Name, function.MinArguments, function.MaxArguments);

            bool argumentsRead = readsValues && function?.Name != "ACTUAL";
            foreach (FormulaNode argument in call.Arguments)
                Visit(argument, argumentsRead);
        }

        /// <summary>Resolves a reference (start = end) or a range to its cells.</summary>
        private void Resolve(FormulaNode node, ReferenceNode start, ReferenceNode end, bool readsValues)
        {
            bool isRange = !ReferenceEquals(start, end);
            string? sheet = start.Sheet is null ? scope.HomeSheet : scope.ResolveSheet(start.Sheet);
            if (sheet is null)
            {
                Fail(node, isRange, "Formula.UnknownSheet", "There is no sheet named '{0}'.", start.Sheet!);
                return;
            }

            int firstColumn = FormulaColumns.Number(start.Cell.Column), lastColumn = FormulaColumns.Number(end.Cell.Column);
            if (Math.Max(firstColumn, lastColumn) > scope.ColumnCount)
            {
                Fail(node, isRange, "Formula.ColumnOutOfRange", "The column {0} is outside the sheet.",
                    firstColumn > scope.ColumnCount ? start.Cell.Column : end.Cell.Column);
                return;
            }

            IReadOnlyList<string> rows = scope.RowKeysOf(sheet);
            int firstRow = RowIndex(rows, start.Cell), lastRow = RowIndex(rows, end.Cell);
            if (firstRow < 0 || lastRow < 0)
            {
                Fail(node, isRange, "Formula.RowOutOfRange", "The reference {0} is outside the sheet.", (firstRow < 0 ? start : end).Cell.Column + Row(firstRow < 0 ? start : end));
                return;
            }

            List<CellAddress> cells = [];
            for (int r = Math.Min(firstRow, lastRow); r <= Math.Max(firstRow, lastRow); r++)
                for (int c = Math.Min(firstColumn, lastColumn); c <= Math.Max(firstColumn, lastColumn); c++)
                    cells.Add(new CellAddress(sheet, rows[r], FormulaColumns.Letters(c)));

            Targets[node] = new BoundTarget(cells, isRange, null);
            if (readsValues) Precedents.AddRange(cells);
        }

        /// <summary>The row part of a reference as written.</summary>
        private static string Row(ReferenceNode reference) =>
            reference.Cell.RowNumber?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "{" + reference.Cell.RowKey + "}";

        /// <summary>The 0-based row index a reference names, or -1 when the sheet has no such row.</summary>
        private static int RowIndex(IReadOnlyList<string> rows, CellReference cell)
        {
            if (cell.RowNumber is { } number)
                return number <= rows.Count ? number - 1 : -1;
            for (int i = 0; i < rows.Count; i++)
                if (rows[i] == cell.RowKey) return i;
            return -1;
        }

        /// <summary>Marks a reference as #REF! with a warning at its span.</summary>
        private void Fail(FormulaNode node, bool isRange, string code, string template, string argument)
        {
            Targets[node] = new BoundTarget([], isRange, FormulaErrorKind.Ref);
            Warn(Warnings, node.Span, code, template, argument);
        }

        /// <summary>Adds a diagnostic to <paramref name="list"/>.</summary>
        private static void Warn(List<FormulaDiagnostic> list, TextSpan span, string code, string template, params object[] args) =>
            list.Add(new FormulaDiagnostic(span, DomainError.Validation(code, template, args)));
    }
}
