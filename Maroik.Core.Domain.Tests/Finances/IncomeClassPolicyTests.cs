using Maroik.Core.Domain.Finances;
namespace Maroik.Core.Domain.Tests.Finances;

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
    /// A recognized main class with an unrecognized subclass is built via <c>DomainError</c>:
    /// its Metadata carries the composite-format resource template and the raw main-class name
    /// separately, so <c>ServiceResult.FromError</c> can hand the UI something resx-localizable
    /// instead of the already-baked, value-embedding sentence in <see cref="ErrorOr.Error.Description"/>.
    /// </summary>
    [Fact]
    public void Validate_ReturnsError_WithLocalizableMetadata_WhenSubClassInvalid()
    {
        var result = IncomeClassPolicy.Validate("RegularIncome", "UnknownSub");

        Assert.Equal("Invalid sub-class for RegularIncome.", result.FirstError.Description);
        Assert.Equal("Invalid sub-class for {0}.", result.FirstError.Metadata!["MessageTemplate"]);
        Assert.Equal(["RegularIncome"], (object[])result.FirstError.Metadata["MessageArgs"]);
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

    /// <summary>The two income main classes the taxonomy must publish.</summary>
    private static readonly string[] _knownMainClasses = ["RegularIncome", "IrregularIncome"];

    /// <summary>The published taxonomy has exactly the two known main classes.</summary>
    [Fact]
    public void SubClassesByMainClass_HasTheKnownMainClasses()
    {
        Assert.Equal(
            _knownMainClasses.OrderBy(x => x),
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

    // -- MainClasses -----------------------------------------------------------

    /// <summary>MainClasses lists the main classes in the order the forms offer them, the first being the create form's default.</summary>
    [Fact]
    public void MainClasses_ListsTheMainClasses_InDisplayOrder()
    {
        Assert.Equal(["RegularIncome", "IrregularIncome"], IncomeClassPolicy.MainClasses);
    }

    /// <summary>MainClasses names exactly the main classes the taxonomy recognizes, so a form never offers one Validate rejects.</summary>
    [Fact]
    public void MainClasses_MatchesTheTaxonomysKeys()
    {
        Assert.Equal(IncomeClassPolicy.SubClassesByMainClass.Keys.Order(), IncomeClassPolicy.MainClasses.Order());
    }

    // -- The taxonomy's persisted values -----------------------------------------

    /// <summary>
    /// The exact strings stored in Income.MainClass / SubClass and used as resx keys: generated from the
    /// <see cref="IncomeMainClass"/> / <see cref="IncomeSubClass"/> enums, so renaming a member changes stored data and must fail here.
    /// </summary>
    [Fact]
    public void SubClassesByMainClass_IsExactlyThePersistedTaxonomy()
    {
        Assert.Equal(["RegularIncome", "IrregularIncome"], IncomeClassPolicy.SubClassesByMainClass.Keys.Order().Reverse());
        Assert.Equal(["LaborIncome", "BusinessIncome", "PensionIncome", "FinancialIncome", "RentalIncome", "OtherIncome"], IncomeClassPolicy.SubClassesByMainClass["RegularIncome"]);
        Assert.Equal(["LaborIncome", "OtherIncome"], IncomeClassPolicy.SubClassesByMainClass["IrregularIncome"]);
    }

    /// <summary>The typed taxonomy lists each main class's sub-classes in display order.</summary>
    [Fact]
    public void Taxonomy_ListsTheSubClassesOfEachMainClass()
    {
        Assert.Equal([IncomeMainClass.RegularIncome, IncomeMainClass.IrregularIncome], IncomeClassPolicy.Taxonomy.Keys.Order());
        Assert.Equal([IncomeSubClass.LaborIncome, IncomeSubClass.OtherIncome], IncomeClassPolicy.Taxonomy[IncomeMainClass.IrregularIncome]);
        Assert.Equal(6, IncomeClassPolicy.Taxonomy[IncomeMainClass.RegularIncome].Count);
    }
}
