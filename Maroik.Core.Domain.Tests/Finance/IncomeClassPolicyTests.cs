using Maroik.Core.Domain.Finance;
namespace Maroik.Core.Domain.Tests.Finance;

/// <summary>
/// Unit tests for <see cref="IncomeClassPolicy"/>.
/// Covers all valid main/subclass combinations, invalid subclasses per main class,
/// and unknown main classes including null and empty inputs.
/// </summary>
public class IncomeClassPolicyTests
{
    // -- RegularIncome ---------------------------------------------------------

    /// <summary>Validate returns success for all regular income sub classes.</summary>
    [Theory]
    [InlineData("LaborIncome")]
    [InlineData("BusinessIncome")]
    [InlineData("PensionIncome")]
    [InlineData("FinancialIncome")]
    [InlineData("RentalIncome")]
    [InlineData("OtherIncome")]
    public void Validate_ReturnsSuccess_ForAllRegularIncomeSubClasses(string subClass)
    {
        var result = IncomeClassPolicy.Validate("RegularIncome", subClass);

        Assert.False(result.IsError);
    }

    /// <summary>Validate returns error, when regular income subclass invalid.</summary>
    [Theory]
    [InlineData("UnknownSub")]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_ReturnsError_WhenRegularIncomeSubClassInvalid(string? subClass)
    {
        var result = IncomeClassPolicy.Validate("RegularIncome", subClass);

        Assert.True(result.IsError);
        Assert.Equal("IncomeClass.Invalid", result.FirstError.Code);
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
        var result = IncomeClassPolicy.Validate("RegularIncome", "UnknownSub");

        Assert.Equal("Invalid sub-class for RegularIncome.", result.FirstError.Description);
        Assert.Equal("Invalid sub-class for {0}.", result.FirstError.Metadata!["ResourceKey"]);
        Assert.Equal(["RegularIncome"], (object[])result.FirstError.Metadata["ResourceArgs"]);
    }

    // -- IrregularIncome -------------------------------------------------------

    /// <summary>Validate returns success for all irregular income sub classes.</summary>
    [Theory]
    [InlineData("LaborIncome")]
    [InlineData("OtherIncome")]
    public void Validate_ReturnsSuccess_ForAllIrregularIncomeSubClasses(string subClass)
    {
        var result = IncomeClassPolicy.Validate("IrregularIncome", subClass);

        Assert.False(result.IsError);
    }

    /// <summary>Validate returns error, when irregular income sub class invalid.</summary>
    [Theory]
    [InlineData("BusinessIncome")]
    [InlineData("PensionIncome")]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_ReturnsError_WhenIrregularIncomeSubClassInvalid(string? subClass)
    {
        var result = IncomeClassPolicy.Validate("IrregularIncome", subClass);

        Assert.True(result.IsError);
        Assert.Equal("IncomeClass.Invalid", result.FirstError.Code);
    }

    // -- Unknown main class ----------------------------------------------------

    /// <summary>Validate returns error, when main class unknown.</summary>
    [Theory]
    [InlineData("Unknown")]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_ReturnsError_WhenMainClassUnknown(string? mainClass)
    {
        var result = IncomeClassPolicy.Validate(mainClass, "LaborIncome");

        Assert.True(result.IsError);
        Assert.Equal("IncomeClass.Invalid", result.FirstError.Code);
    }

    // -- SubClassesByMainClass (serialized into the create/edit forms) --------

    /// <summary>The published taxonomy has exactly the two known main classes.</summary>
    [Fact]
    public void SubClassesByMainClass_HasTheKnownMainClasses()
    {
        Assert.Equal(
 #pragma warning disable CA1861
            new[] { "RegularIncome", "IrregularIncome" }.OrderBy(x => x),
 #pragma warning restore CA1861
            IncomeClassPolicy.SubClassesByMainClass.Keys.OrderBy(x => x));
    }

    /// <summary>Every published (main, sub) pair validates, and nothing outside it does.</summary>
    [Theory]
    [InlineData("RegularIncome")]
    [InlineData("IrregularIncome")]
    public void SubClassesByMainClass_AgreesWithValidate(string mainClass)
    {
        var published = IncomeClassPolicy.SubClassesByMainClass[mainClass];

        Assert.All(published, sub => Assert.False(IncomeClassPolicy.Validate(mainClass, sub).IsError));

        var everySubClass = IncomeClassPolicy.SubClassesByMainClass.Values.SelectMany(x => x).Distinct();
        foreach (var sub in everySubClass.Except(published))
            Assert.True(IncomeClassPolicy.Validate(mainClass, sub).IsError);
    }
}
