using ErrorOr;
using Maroik.Core.Domain.Errors;

namespace Maroik.Core.Domain.Finances;

/// <summary>
/// Domain policy that validates and classifies expenditure main/subclass combinations.
/// Returns <c>true</c> when the combination requires a deposit asset (transfer-type spending),
/// <c>false</c> when no deposit asset is involved, or an error when the combination is invalid.
/// <para>
/// <see cref="Taxonomy"/> and <see cref="DepositAssetSubClasses"/> (typed by the <see cref="ExpenditureMainClass"/> / <see cref="ExpenditureSubClass"/> enums)
/// is the single source of truth; the string views and <see cref="Validate"/> are derived from it, and the presentation
/// layer serializes them into the create/edit forms so the client does not hard-code the same lists.
/// </para>
/// </summary>
public static class ExpenditureClassPolicy
{
    /// <summary>
    /// The recognized sub-classes of each expenditure main class, in display order — the single source of truth, typed so a
    /// misspelt class is a compile error. The string views below are generated from it.
    /// </summary>
    public static readonly IReadOnlyDictionary<ExpenditureMainClass, IReadOnlyList<ExpenditureSubClass>> Taxonomy =
        new Dictionary<ExpenditureMainClass, IReadOnlyList<ExpenditureSubClass>>
        {
            [ExpenditureMainClass.RegularSavings] = [ExpenditureSubClass.Deposit, ExpenditureSubClass.Investment],
            [ExpenditureMainClass.NonConsumerSpending] =
            [
                ExpenditureSubClass.PublicPension, ExpenditureSubClass.DebtRepayment, ExpenditureSubClass.Tax, ExpenditureSubClass.SocialInsurance,
                ExpenditureSubClass.InterHouseholdTransferExpenses, ExpenditureSubClass.NonProfitOrganizationTransfer
            ],
            [ExpenditureMainClass.ConsumerSpending] =
            [
                ExpenditureSubClass.MealOrEatOutExpenses, ExpenditureSubClass.HousingOrSuppliesCost, ExpenditureSubClass.EducationExpenses,
                ExpenditureSubClass.MedicalExpenses, ExpenditureSubClass.TransportationCost, ExpenditureSubClass.CommunicationCost,
                ExpenditureSubClass.LeisureOrCulture, ExpenditureSubClass.ClothingOrShoes, ExpenditureSubClass.PinMoney,
                ExpenditureSubClass.ProtectionTypeInsurance, ExpenditureSubClass.OtherExpenses, ExpenditureSubClass.UnknownExpenditure
            ]
        };

    /// <summary>
    /// The expenditure main classes in display order (the enum's declaration order); the first is the create form's default.
    /// The forms render their main-class options from this list.
    /// </summary>
    public static readonly IReadOnlyList<string> MainClasses = [.. Enum.GetValues<ExpenditureMainClass>().Select(m => m.ToString())];

    /// <summary>
    /// <see cref="Taxonomy"/> as the strings stored in the database: what <see cref="Validate"/> checks and what the
    /// presentation layer serializes into the create/edit forms.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> SubClassesByMainClass =
        Taxonomy.ToDictionary(pair => pair.Key.ToString(), IReadOnlyList<string> (pair) => [.. pair.Value.Select(sub => sub.ToString())]);

    /// <summary>
    /// SubClasses that represent a transfer into an asset and therefore require the user to
    /// pick a deposit asset. Every other recognized subClass does not.
    /// </summary>
    public static readonly IReadOnlyList<string> DepositAssetSubClasses =
        [.. new[] { ExpenditureSubClass.Deposit, ExpenditureSubClass.Investment, ExpenditureSubClass.PublicPension, ExpenditureSubClass.DebtRepayment }
            .Select(sub => sub.ToString())];

    /// <summary>
    /// Validates <paramref name="mainClass"/> / <paramref name="subClass"/> and returns whether
    /// the expenditure requires a deposit asset (<c>true</c>) or not (<c>false</c>).
    /// Returns a validation error if the combination is not recognized.
    /// </summary>
    public static ErrorOr<bool> Validate(string? mainClass, string? subClass)
    {
        if (mainClass is null || !SubClassesByMainClass.TryGetValue(mainClass, out var subClasses))
            return DomainError.Validation("ExpenditureClass.Invalid", "Unknown expenditure main class.");

        if (subClass is null || !subClasses.Contains(subClass))
            return DomainError.Validation("ExpenditureClass.Invalid", "Invalid sub-class for {0}.", mainClass);

        return DepositAssetSubClasses.Contains(subClass);
    }

    /// <summary>
    /// The deposit asset an expenditure (one-off or fixed) keeps, given whether its class is a transfer
    /// (<paramref name="requiresDepositAsset"/>, from <see cref="Validate"/>): a transfer moves money from
    /// <paramref name="paymentMethod"/> into <paramref name="myDepositAsset"/>, so it needs one, and not the same asset;
    /// any other expenditure has none, whatever was submitted. <paramref name="codePrefix"/> names the aggregate in the
    /// error code (<c>Expenditure</c> / <c>FixedExpenditure</c>).
    /// </summary>
    public static ErrorOr<string?> ResolveDepositAsset(string codePrefix, bool requiresDepositAsset, string paymentMethod, string? myDepositAsset)
    {
        if (!requiresDepositAsset)
            return (string?)null;

        if (string.IsNullOrWhiteSpace(myDepositAsset))
            return DomainError.Validation($"{codePrefix}.DepositAssetEmpty", "Deposit asset product name cannot be empty.");

        if (string.Equals(paymentMethod, myDepositAsset, StringComparison.Ordinal))
            return DomainError.Validation($"{codePrefix}.SameAsset", "The PaymentMethod and MyDepositAsset value cannot be the same.");

        return myDepositAsset;
    }
}
