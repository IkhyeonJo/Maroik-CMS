using ErrorOr;
using Maroik.Core.Domain.Errors;
using Maroik.Core.Domain.Finances;
using Maroik.Core.Domain.Formulas;

namespace Maroik.Core.Domain.Budgets;

/// <summary>
/// What a budget-sheet cell accepts, by its role: a budget cell a number or a formula (its value is that month's budget); a free cell a
/// number, TRUE / FALSE, text or a formula; label, computed and blank cells nothing. A formula Excel would refuse (a syntax error, a
/// wrong argument count) is refused with its first problem; one whose only problems are #NAME? / #REF! is stored, as in Excel.
/// Inputs are stored with row keys (<see cref="FormulaRewriter.ToCanonical"/>), numbers in their plain invariant form.
/// </summary>
public static class BudgetCellInput
{
    /// <summary>The longest input a cell holds.</summary>
    public const int MaxLength = 1000;

    /// <summary>The stored form of <paramref name="input"/> typed into a cell of <paramref name="role"/>; null clears the cell.</summary>
    public static ErrorOr<string?> Parse(BudgetCellRole role, string? input, FormulaScope scope)
    {
        if (role is not (BudgetCellRole.Budget or BudgetCellRole.Free))
            return DomainError.Validation("Budget.CellReadOnly", "This cell cannot be edited.");
        if (input is not null && input.Length > MaxLength)
            return DomainError.Validation("Budget.InputTooLong", "A cell can hold at most {0} characters.", MaxLength);

        string text = input?.Trim() ?? "";
        if (text.Length == 0) return (string?)null;

        if (text.StartsWith('='))
        {
            FormulaHighlight highlight = FormulaHighlighter.Highlight(text, scope);
            if (highlight.Problems.FirstOrDefault(p => p.Blocking) is { } problem) return problem.Diagnostic.Error;
            return FormulaRewriter.ToCanonical(text, scope);
        }

        if (FinanceAmountPolicy.TryParseInput(text, out decimal number))
            return FormulaEvaluator.FormatNumber(number);
        if (role == BudgetCellRole.Budget)
            return DomainError.Validation("Budget.CellNeedsNumber", "A budget cell takes a number or a formula.");
        if (text.Equals("TRUE", StringComparison.OrdinalIgnoreCase) || text.Equals("FALSE", StringComparison.OrdinalIgnoreCase))
            return text.ToUpperInvariant();
        return input;
    }

    /// <summary>The definition of a stored input: a bound formula, or a number, boolean or text constant.</summary>
    public static CellDefinition Define(string stored, FormulaScope scope)
    {
        if (stored.StartsWith('='))
            return CellDefinition.Of(FormulaBinder.Bind(FormulaParser.Parse(stored, canonical: true), scope));
        if (decimal.TryParse(stored, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out decimal number))
            return CellDefinition.Of(new NumberValue(number));
        if (stored is "TRUE" or "FALSE")
            return CellDefinition.Of(new BooleanValue(stored == "TRUE"));
        return CellDefinition.Of(new TextValue(stored));
    }
}
