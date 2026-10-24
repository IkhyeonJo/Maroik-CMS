using System.Text;
using ErrorOr;
using Maroik.Core.Domain.Errors;
using Maroik.Core.Domain.Finances;
using Maroik.Core.Domain.ValueObjects;

namespace Maroik.Core.Domain.Budgets;

/// <summary>
/// A line of a budget sheet: one sub-class of one main class in one currency. Its <see cref="Key"/> is made of those three, so the
/// same line has the same key in every year's sheet and a stored formula that names it keeps working when rows move or a formula is
/// copied from last year.
/// </summary>
/// <param name="MainClass">The main class.</param>
/// <param name="SubClass">The sub-class.</param>
/// <param name="Currency">The currency label, normalized as <see cref="CurrencyCode"/> does (trimmed, upper-case).</param>
public sealed record BudgetLine(string MainClass, string SubClass, string Currency)
{
    /// <summary>The line's row key: "L.{main}.{sub}.{currency's UTF-8 bytes in hex}" (letters, digits and dots only).</summary>
    public string Key => $"L.{MainClass}.{SubClass}.{Hex(Currency)}";

    /// <summary>The hex of a currency label's UTF-8 bytes (keys stay within letters, digits and dots whatever the label).</summary>
    public static string Hex(string currency) => Convert.ToHexString(Encoding.UTF8.GetBytes(currency));

    /// <summary>A line of a <paramref name="kind"/> sheet, refused when the class is not in the kind's taxonomy or the currency is invalid.</summary>
    public static ErrorOr<BudgetLine> Create(BudgetKind kind, string? mainClass, string? subClass, string? currency)
    {
        List<Error>? classErrors = kind switch
        {
            BudgetKind.Income => IncomeClassPolicy.Validate(mainClass, subClass).ErrorsOrEmptyList,
            BudgetKind.Expenditure => ExpenditureClassPolicy.Validate(mainClass, subClass).ErrorsOrEmptyList,
            _ => [DomainError.Validation("Budget.KindInvalid", "Unknown budget kind.")],
        };
        if (classErrors.Count > 0) return classErrors;

        ErrorOr<CurrencyCode> code = CurrencyCode.Create(currency);
        if (code.IsError) return code.Errors;

        return new BudgetLine(mainClass!, subClass!, code.Value.Value);
    }

    /// <summary>The main classes of a kind with their sub-classes, in taxonomy order.</summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> Taxonomy(BudgetKind kind) =>
        kind == BudgetKind.Income ? IncomeClassPolicy.SubClassesByMainClass : ExpenditureClassPolicy.SubClassesByMainClass;

    /// <summary>The main classes of a kind in taxonomy order.</summary>
    public static IReadOnlyList<string> MainClasses(BudgetKind kind) =>
        kind == BudgetKind.Income ? IncomeClassPolicy.MainClasses : ExpenditureClassPolicy.MainClasses;

    /// <summary>A new sheet's lines: every class of the kind in <paramref name="currency"/> (none when there is no currency yet).</summary>
    public static IReadOnlyList<BudgetLine> Defaults(BudgetKind kind, string? currency) =>
        currency is null ? [] : [.. MainClasses(kind).SelectMany(main => Taxonomy(kind)[main].Select(sub => new BudgetLine(main, sub, currency)))];
}
