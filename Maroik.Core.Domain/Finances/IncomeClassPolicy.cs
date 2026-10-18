using ErrorOr;
using Maroik.Core.Domain.Errors;

namespace Maroik.Core.Domain.Finances;

/// <summary>
/// Domain policy that validates income main/subclass combinations.
/// Returns <see cref="Result.Success"/> when the combination is recognized,
/// or a validation error when it is not.
/// <para>
/// <see cref="Taxonomy"/> (typed by the <see cref="IncomeMainClass"/> / <see cref="IncomeSubClass"/> enums)
/// is the single source of truth; the string views and <see cref="Validate"/> are derived from it, and the presentation
/// layer serializes them into the create/edit forms so the client does not hard-code the same lists.
/// </para>
/// </summary>
public static class IncomeClassPolicy
{
    /// <summary>
    /// The recognized sub-classes of each income main class, in display order — the single source of truth, typed so a
    /// misspelt class is a compile error. The string views below are generated from it.
    /// </summary>
    public static readonly IReadOnlyDictionary<IncomeMainClass, IReadOnlyList<IncomeSubClass>> Taxonomy =
        new Dictionary<IncomeMainClass, IReadOnlyList<IncomeSubClass>>
        {
            [IncomeMainClass.RegularIncome] =
            [
                IncomeSubClass.LaborIncome, IncomeSubClass.BusinessIncome, IncomeSubClass.PensionIncome,
                IncomeSubClass.FinancialIncome, IncomeSubClass.RentalIncome, IncomeSubClass.OtherIncome
            ],
            [IncomeMainClass.IrregularIncome] = [IncomeSubClass.LaborIncome, IncomeSubClass.OtherIncome]
        };

    /// <summary>
    /// The income main classes in display order (the enum's declaration order); the first is the create form's default. The
    /// forms render their main-class options from this list.
    /// </summary>
    public static readonly IReadOnlyList<string> MainClasses = [.. Enum.GetValues<IncomeMainClass>().Select(m => m.ToString())];

    /// <summary>
    /// <see cref="Taxonomy"/> as the strings stored in the database: what <see cref="Validate"/> checks and what the
    /// presentation layer serializes into the create/edit forms.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> SubClassesByMainClass =
        Taxonomy.ToDictionary(pair => pair.Key.ToString(), IReadOnlyList<string> (pair) => [.. pair.Value.Select(sub => sub.ToString())]);

    /// <summary>
    /// Validates <paramref name="mainClass"/> / <paramref name="subClass"/>.
    /// Returns a validation error if the combination is not recognized.
    /// </summary>
    public static ErrorOr<Success> Validate(string? mainClass, string? subClass)
    {
        if (mainClass is null || !SubClassesByMainClass.TryGetValue(mainClass, out var subClasses))
            return DomainError.Validation("IncomeClass.Invalid", "Unknown income main class.");

        return subClass is not null && subClasses.Contains(subClass)
            ? Result.Success
            : DomainError.Validation("IncomeClass.Invalid", "Invalid sub-class for {0}.", mainClass);
    }
}
