using System.ComponentModel.DataAnnotations;
using Maroik.Core.Domain.Finance;
using Maroik.Website.Models.ViewModels.AccountBook;
using Maroik.Website.Models.ViewModels.Notice;

namespace Maroik.Website.Tests.Models;

/// <summary>
/// The cross-field rules the finance input view models add to their attributes: a transfer subclass needs a deposit
/// asset, and the local "created" date-time must be well-formed — so a bad value is caught before the mapper parses it.
/// </summary>
public class InputViewModelValidationTests
{
    /// <summary>A sub-class that requires a deposit asset.</summary>
    private static readonly string _transferSubClass = ExpenditureClassPolicy.DepositAssetSubClasses[0];
    /// <summary>A well-formed <c>Created</c> value.</summary>
    private const string ValidCreated = "2024-05-01 09:30:00";

    /// <summary>Runs <paramref name="model"/>'s own validation and returns the results.</summary>
    private static List<ValidationResult> Validate(IValidatableObject model) =>
        [.. model.Validate(new ValidationContext(model))];

    // -- ExpenditureInputViewModel ------------------------------------------------------------------------

    /// <summary>A transfer expenditure without a deposit asset is refused, naming the field.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Expenditure_TransferWithoutADepositAsset_IsRefused(string? depositAsset)
    {
        var results = Validate(new ExpenditureInputViewModel { SubClass = _transferSubClass, MyDepositAsset = depositAsset, Created = ValidCreated });

        ValidationResult single = Assert.Single(results);
        Assert.Equal([nameof(ExpenditureInputViewModel.MyDepositAsset)], single.MemberNames);
    }

    /// <summary>A transfer with an asset, and a non-transfer without one, are both fine.</summary>
    [Fact]
    public void Expenditure_TransferWithAnAsset_AndNonTransferWithout_AreAccepted()
    {
        Assert.Empty(Validate(new ExpenditureInputViewModel { SubClass = _transferSubClass, MyDepositAsset = "Bank", Created = ValidCreated }));
        Assert.Empty(Validate(new ExpenditureInputViewModel { SubClass = "NotATransfer", MyDepositAsset = null, Created = ValidCreated }));
        Assert.Empty(Validate(new ExpenditureInputViewModel { SubClass = null, Created = ValidCreated }));
    }

    /// <summary>A missing or malformed created date-time is refused, naming the field.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("2024-05-01")]
    [InlineData("2024-05-01T09:30:00")]
    [InlineData("not a date")]
    [InlineData("2024-13-40 25:61:61")]
    public void Expenditure_AMalformedCreated_IsRefused(string? created)
    {
        var results = Validate(new ExpenditureInputViewModel { SubClass = "NotATransfer", Created = created });

        Assert.Equal([nameof(ExpenditureInputViewModel.Created)], Assert.Single(results).MemberNames);
    }

    // -- IncomeInputViewModel -----------------------------------------------------------------------------

    /// <summary>The income's created date-time must be a well-formed local date-time.</summary>
    [Theory]
    [InlineData(ValidCreated, true)]
    [InlineData("2024-05-01", false)]
    [InlineData(null, false)]
    [InlineData("31/12/2024 10:00:00", false)]
    public void Income_Created_MustBeAWellFormedLocalDateTime(string? created, bool valid)
    {
        var results = Validate(new IncomeInputViewModel { Created = created });

        Assert.Equal(valid, results.Count == 0);
        if (!valid) Assert.Equal([nameof(IncomeInputViewModel.Created)], Assert.Single(results).MemberNames);
    }

    // -- FixedExpenditureInputViewModel -------------------------------------------------------------------

    /// <summary>A fixed transfer expenditure needs a deposit asset, exactly as a one-off one does.</summary>
    [Theory]
    [InlineData(null, false)]
    [InlineData("  ", false)]
    [InlineData("Bank", true)]
    public void FixedExpenditure_TransferNeedsADepositAsset(string? depositAsset, bool valid)
    {
        var results = Validate(new FixedExpenditureInputViewModel { SubClass = _transferSubClass, MyDepositAsset = depositAsset });

        Assert.Equal(valid, results.Count == 0);
        if (!valid) Assert.Equal([nameof(FixedExpenditureInputViewModel.MyDepositAsset)], Assert.Single(results).MemberNames);
    }

    /// <summary>A fixed expenditure of another subclass needs no deposit asset.</summary>
    [Fact]
    public void FixedExpenditure_NonTransfer_NeedsNoDepositAsset()
    {
        Assert.Empty(Validate(new FixedExpenditureInputViewModel { SubClass = "NotATransfer" }));
        Assert.Empty(Validate(new FixedExpenditureInputViewModel { SubClass = null }));
    }
}
