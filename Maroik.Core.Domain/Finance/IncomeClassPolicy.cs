using ErrorOr;
using Maroik.Core.Domain.Errors;

namespace Maroik.Core.Domain.Finance;

/// <summary>
/// Domain policy that validates income main/subclass combinations.
/// Returns <see cref="Result.Success"/> when the combination is recognized,
/// or a validation error when it is not.
/// <para>
/// <see cref="SubClassesByMainClass"/> is the single source of truth for the taxonomy;
/// <see cref="Validate"/> is derived from it, and the presentation layer serializes it
/// into the create/edit forms so the client no longer hard-codes the same lists.
/// </para>
/// </summary>
public static class IncomeClassPolicy
{
    /// <summary>
    /// The recognized subClasses for each income main class, in display order.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> SubClassesByMainClass =
        new Dictionary<string, IReadOnlyList<string>>
        {
            ["RegularIncome"] =
            [
                "LaborIncome", "BusinessIncome", "PensionIncome",
                "FinancialIncome", "RentalIncome", "OtherIncome"
            ],
            ["IrregularIncome"] = ["LaborIncome", "OtherIncome"]
        };

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
