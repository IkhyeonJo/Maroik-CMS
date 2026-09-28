using Maroik.Core.Domain.Finance;
namespace Maroik.Core.Domain.Tests.Finance;

/// <summary>
/// Unit tests for <see cref="ExpenditureClassPolicy"/>.
/// Covers all valid main/subclass combinations (with and without deposit-asset requirement),
/// invalid subclasses per main class, and unknown main classes including null and empty inputs.
/// </summary>
public class ExpenditureClassPolicyTests
{
    // -- RegularSavings --------------------------------------------------------

    /// <summary>Validate returns true for regular savings sub classes.</summary>
    [Theory]
    [InlineData("Deposit")]
    [InlineData("Investment")]
    public void Validate_ReturnsTrue_ForRegularSavingsSubClasses(string subClass)
    {
        var result = ExpenditureClassPolicy.Validate("RegularSavings", subClass);

        Assert.False(result.IsError);
        Assert.True(result.Value);
    }

    /// <summary>Validate returns error, when regular savings subclass invalid.</summary>
    [Theory]
    [InlineData("Unknown")]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_ReturnsError_WhenRegularSavingsSubClassInvalid(string? subClass)
    {
        var result = ExpenditureClassPolicy.Validate("RegularSavings", subClass);

        Assert.True(result.IsError);
        Assert.Equal("ExpenditureClass.Invalid", result.FirstError.Code);
    }

    /// <summary>
    /// A recognized main class with an unrecognized subclass is built via <c>LocalizableError</c>:
    /// its Metadata carries the composite-format resource template and the raw main-class name
    /// separately, so <c>ServiceResult.FromError</c> can hand the UI something resx-localizable
    /// instead of the already-baked, value-embedding sentence in <see cref="ErrorOr.Error.Description"/>.
    /// </summary>
    [Fact]
    public void Validate_ReturnsError_WithLocalizableMetadata_WhenSubClassInvalid()
    {
        var result = ExpenditureClassPolicy.Validate("RegularSavings", "Unknown");

        Assert.Equal("Invalid sub-class for RegularSavings.", result.FirstError.Description);
        Assert.Equal("Invalid sub-class for {0}.", result.FirstError.Metadata!["ResourceKey"]);
        Assert.Equal(["RegularSavings"], (object[])result.FirstError.Metadata["ResourceArgs"]);
    }

    // -- NonConsumerSpending ---------------------------------------------------

    /// <summary>Validate returns true for non consumer spending with deposit asset.</summary>
    [Theory]
    [InlineData("PublicPension")]
    [InlineData("DebtRepayment")]
    public void Validate_ReturnsTrue_ForNonConsumerSpendingWithDepositAsset(string subClass)
    {
        var result = ExpenditureClassPolicy.Validate("NonConsumerSpending", subClass);

        Assert.False(result.IsError);
        Assert.True(result.Value);
    }

    /// <summary>Validate returns false for non consumer spending without deposit asset.</summary>
    [Theory]
    [InlineData("Tax")]
    [InlineData("SocialInsurance")]
    [InlineData("InterHouseholdTransferExpenses")]
    [InlineData("NonProfitOrganizationTransfer")]
    public void Validate_ReturnsFalse_ForNonConsumerSpendingWithoutDepositAsset(string subClass)
    {
        var result = ExpenditureClassPolicy.Validate("NonConsumerSpending", subClass);

        Assert.False(result.IsError);
        Assert.False(result.Value);
    }

    /// <summary>Validate returns error, when non consumer spending subclass invalid.</summary>
    [Theory]
    [InlineData("Unknown")]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_ReturnsError_WhenNonConsumerSpendingSubClassInvalid(string? subClass)
    {
        var result = ExpenditureClassPolicy.Validate("NonConsumerSpending", subClass);

        Assert.True(result.IsError);
        Assert.Equal("ExpenditureClass.Invalid", result.FirstError.Code);
    }

    // -- ConsumerSpending ------------------------------------------------------

    /// <summary>Validate returns false for all consumer spending sub classes.</summary>
    [Theory]
    [InlineData("MealOrEatOutExpenses")]
    [InlineData("HousingOrSuppliesCost")]
    [InlineData("EducationExpenses")]
    [InlineData("MedicalExpenses")]
    [InlineData("TransportationCost")]
    [InlineData("CommunicationCost")]
    [InlineData("LeisureOrCulture")]
    [InlineData("ClothingOrShoes")]
    [InlineData("PinMoney")]
    [InlineData("ProtectionTypeInsurance")]
    [InlineData("OtherExpenses")]
    [InlineData("UnknownExpenditure")]
    public void Validate_ReturnsFalse_ForAllConsumerSpendingSubClasses(string subClass)
    {
        var result = ExpenditureClassPolicy.Validate("ConsumerSpending", subClass);

        Assert.False(result.IsError);
        Assert.False(result.Value);
    }

    /// <summary>Validate returns error, when consumer spending subclass invalid.</summary>
    [Theory]
    [InlineData("Unknown")]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_ReturnsError_WhenConsumerSpendingSubClassInvalid(string? subClass)
    {
        var result = ExpenditureClassPolicy.Validate("ConsumerSpending", subClass);

        Assert.True(result.IsError);
        Assert.Equal("ExpenditureClass.Invalid", result.FirstError.Code);
    }

    // -- Unknown main class ----------------------------------------------------

    /// <summary>Validate returns error, when main class unknown.</summary>
    [Theory]
    [InlineData("Unknown")]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_ReturnsError_WhenMainClassUnknown(string? mainClass)
    {
        var result = ExpenditureClassPolicy.Validate(mainClass, "Tax");

        Assert.True(result.IsError);
        Assert.Equal("ExpenditureClass.Invalid", result.FirstError.Code);
    }

    // -- SubClassesByMainClass / DepositAssetSubClasses (serialized into forms) ---

    /// <summary>The published taxonomy has exactly the three known main classes.</summary>
    [Fact]
    public void SubClassesByMainClass_HasTheKnownMainClasses()
    {
        Assert.Equal(
            new[] { "RegularSavings", "NonConsumerSpending", "ConsumerSpending" }.OrderBy(x => x),
            ExpenditureClassPolicy.SubClassesByMainClass.Keys.OrderBy(x => x));
    }

    /// <summary>
    /// Every published (main, sub) pair validates, its deposit-asset result matches membership
    /// in <see cref="ExpenditureClassPolicy.DepositAssetSubClasses"/>, and every deposit-asset
    /// subclass is itself part of the published taxonomy.
    /// </summary>
    [Theory]
    [InlineData("RegularSavings")]
    [InlineData("NonConsumerSpending")]
    [InlineData("ConsumerSpending")]
    public void SubClassesByMainClass_AgreesWithValidate_AndDepositAssetSet(string mainClass)
    {
        foreach (var sub in ExpenditureClassPolicy.SubClassesByMainClass[mainClass])
        {
            var result = ExpenditureClassPolicy.Validate(mainClass, sub);
            Assert.False(result.IsError);
            Assert.Equal(ExpenditureClassPolicy.DepositAssetSubClasses.Contains(sub), result.Value);
        }

        var allPublished = ExpenditureClassPolicy.SubClassesByMainClass.Values.SelectMany(x => x).ToHashSet();
        Assert.All(ExpenditureClassPolicy.DepositAssetSubClasses, sub => Assert.Contains(sub, allPublished));
    }
}
