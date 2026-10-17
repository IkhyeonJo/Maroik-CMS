using Maroik.Core.Domain.Finance;
namespace Maroik.Core.Domain.Tests.Finance;

/// <summary>
/// Unit tests for <see cref="AssetUsagePolicy"/>: which assets an income / expenditure record may use, and which
/// refusal wins when several apply.
/// </summary>
public class AssetUsagePolicyTests
{
    /// <summary>The fixed "current time" the test assets are stamped with.</summary>
    private static readonly DateTime Now = new(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>An asset of the test account, active unless <paramref name="deleted"/>.</summary>
    private static Asset AssetOf(string name = "Wallet", string currency = "KRW", bool deleted = false)
        => Asset.Reconstitute(name, "user@example.com", "CashAsset", 1000m, currency, null, deleted, Now, Now);

    /// <summary>A check's outcome as one value: <see langword="null"/> when it passed, otherwise its refusal.</summary>
    private static AssetRefusal? Outcome(bool passed, AssetRefusal refusal) => passed ? null : refusal;

    // -- CanUse -----------------------------------------------------------------

    /// <summary>A missing required asset is refused as not found.</summary>
    [Fact]
    public void CanUse_RefusesAMissingAsset_AsNotFound()
        => Assert.Equal(AssetRefusal.NotFound, Outcome(AssetUsagePolicy.CanUse(null, out var refusal), refusal));

    /// <summary>A deleted required asset is refused as deleted.</summary>
    [Fact]
    public void CanUse_RefusesADeletedAsset()
        => Assert.Equal(AssetRefusal.Deleted, Outcome(AssetUsagePolicy.CanUse(AssetOf(deleted: true), out var refusal), refusal));

    /// <summary>An active required asset is accepted.</summary>
    [Fact]
    public void CanUse_AcceptsAnActiveAsset()
        => Assert.True(AssetUsagePolicy.CanUse(AssetOf(), out _));

    // -- CanTransferInto --------------------------------------------------------

    /// <summary>A transfer without a deposit asset is refused as not found.</summary>
    [Fact]
    public void CanTransferInto_RefusesAMissingDepositAsset_AsNotFound()
        => Assert.Equal(AssetRefusal.NotFound, Outcome(AssetUsagePolicy.CanTransferInto(AssetOf(), null, out var refusal), refusal));

    /// <summary>A deleted deposit asset is refused as deleted, before its currency is compared.</summary>
    [Fact]
    public void CanTransferInto_RefusesADeletedDepositAsset_BeforeComparingCurrencies()
        => Assert.Equal(AssetRefusal.Deleted,
            Outcome(AssetUsagePolicy.CanTransferInto(AssetOf(), AssetOf("Savings", "USD", deleted: true), out var refusal), refusal));

    /// <summary>A deposit asset in another currency than the payment asset is refused.</summary>
    [Fact]
    public void CanTransferInto_RefusesADepositAssetInAnotherCurrency()
        => Assert.Equal(AssetRefusal.CurrencyMismatch,
            Outcome(AssetUsagePolicy.CanTransferInto(AssetOf(), AssetOf("Savings", "USD"), out var refusal), refusal));

    /// <summary>An active deposit asset in the payment asset's currency is accepted.</summary>
    [Fact]
    public void CanTransferInto_AcceptsAnActiveDepositAssetInTheSameCurrency()
        => Assert.True(AssetUsagePolicy.CanTransferInto(AssetOf(), AssetOf("Savings"), out _));

    // -- CanMoveBack ------------------------------------------------------------

    /// <summary>Any deleted asset among those a record moved is refused as deleted.</summary>
    [Fact]
    public void CanMoveBack_RefusesWhenAnyAssetIsDeleted()
        => Assert.Equal(AssetRefusal.Deleted,
            Outcome(AssetUsagePolicy.CanMoveBack([AssetOf(), null, AssetOf("Savings", deleted: true)], out var refusal), refusal));

    /// <summary>Missing and active assets are not refused here.</summary>
    [Fact]
    public void CanMoveBack_AcceptsMissingAndActiveAssets()
        => Assert.True(AssetUsagePolicy.CanMoveBack([null, AssetOf()], out _));

    // -- CanRecordExpenditure ---------------------------------------------------

    /// <summary>The payment asset is checked first, missing before deleted, whatever the deposit asset is.</summary>
    [Fact]
    public void CanRecordExpenditure_ChecksThePaymentAssetFirst()
    {
        Assert.Equal(AssetRefusal.NotFound,
            Outcome(AssetUsagePolicy.CanRecordExpenditure(true, null, AssetOf("Savings", deleted: true), out var missing), missing));
        Assert.Equal(AssetRefusal.Deleted,
            Outcome(AssetUsagePolicy.CanRecordExpenditure(true, AssetOf(deleted: true), null, out var deleted), deleted));
    }

    /// <summary>A transfer's deposit asset is held to <see cref="AssetUsagePolicy.CanTransferInto"/>.</summary>
    [Fact]
    public void CanRecordExpenditure_ChecksATransfersDepositAsset()
    {
        Assert.Equal(AssetRefusal.NotFound,
            Outcome(AssetUsagePolicy.CanRecordExpenditure(true, AssetOf(), null, out var missing), missing));
        Assert.Equal(AssetRefusal.CurrencyMismatch,
            Outcome(AssetUsagePolicy.CanRecordExpenditure(true, AssetOf(), AssetOf("Savings", "USD"), out var mismatch), mismatch));
        Assert.True(AssetUsagePolicy.CanRecordExpenditure(true, AssetOf(), AssetOf("Savings"), out _));
    }

    /// <summary>A non-transfer expenditure ignores whatever deposit asset was resolved.</summary>
    [Fact]
    public void CanRecordExpenditure_IgnoresTheDepositAsset_ForANonTransfer()
        => Assert.True(AssetUsagePolicy.CanRecordExpenditure(false, AssetOf(), AssetOf("Savings", "USD", deleted: true), out _));

    // -- CanRetargetExpenditure -------------------------------------------------

    /// <summary>The new assets follow the same order as a new expenditure: a missing payment asset wins over a deleted deposit asset.</summary>
    [Fact]
    public void CanRetargetExpenditure_ChecksTheNewAssets_InTheSameOrderAsANewExpenditure()
    {
        Assert.Equal(AssetRefusal.NotFound,
            Outcome(AssetUsagePolicy.CanRetargetExpenditure(true, null, AssetOf("Savings", deleted: true), null, null, out var missing), missing));
        Assert.Equal(AssetRefusal.Deleted,
            Outcome(AssetUsagePolicy.CanRetargetExpenditure(true, AssetOf(deleted: true), null, null, null, out var deleted), deleted));
    }

    /// <summary>Every refusal of the new assets wins over a deleted previous asset.</summary>
    [Fact]
    public void CanRetargetExpenditure_ChecksTheNewAssets_BeforeThePreviousOnes()
    {
        Asset deletedPrevious = AssetOf("Old", deleted: true);
        Assert.Equal(AssetRefusal.NotFound,
            Outcome(AssetUsagePolicy.CanRetargetExpenditure(false, null, null, deletedPrevious, null, out var missing), missing));
        Assert.Equal(AssetRefusal.CurrencyMismatch,
            Outcome(AssetUsagePolicy.CanRetargetExpenditure(true, AssetOf(), AssetOf("Savings", "USD"), deletedPrevious, null, out var mismatch), mismatch));
    }

    /// <summary>A deleted previous payment or deposit asset is refused once the new assets pass.</summary>
    [Fact]
    public void CanRetargetExpenditure_RefusesADeletedPreviousAsset()
    {
        Assert.Equal(AssetRefusal.Deleted,
            Outcome(AssetUsagePolicy.CanRetargetExpenditure(false, AssetOf(), null, AssetOf("Old", deleted: true), null, out var payment), payment));
        Assert.Equal(AssetRefusal.Deleted,
            Outcome(AssetUsagePolicy.CanRetargetExpenditure(false, AssetOf(), null, AssetOf("Old"), AssetOf("OldSavings", deleted: true), out var deposit), deposit));
    }

    /// <summary>A non-transfer edit ignores the new deposit asset; active assets are accepted.</summary>
    [Fact]
    public void CanRetargetExpenditure_AcceptsActiveAssets_AndIgnoresANonTransfersDepositAsset()
    {
        Assert.True(AssetUsagePolicy.CanRetargetExpenditure(false, AssetOf(), AssetOf("Savings", "USD", deleted: true), AssetOf("Old"), null, out _));
        Assert.True(AssetUsagePolicy.CanRetargetExpenditure(true, AssetOf(), AssetOf("Savings"), AssetOf("Old"), AssetOf("OldSavings"), out _));
    }

    // -- CanRetargetIncome ------------------------------------------------------

    /// <summary>A deleted previous deposit asset wins over a missing new one.</summary>
    [Fact]
    public void CanRetargetIncome_RefusesADeletedPreviousAsset_First()
        => Assert.Equal(AssetRefusal.Deleted,
            Outcome(AssetUsagePolicy.CanRetargetIncome(AssetOf(deleted: true), "Savings", null, out var refusal), refusal));

    /// <summary>A named new deposit asset that does not exist is refused as not found.</summary>
    [Fact]
    public void CanRetargetIncome_RefusesANamedButMissingNewAsset()
        => Assert.Equal(AssetRefusal.NotFound,
            Outcome(AssetUsagePolicy.CanRetargetIncome(AssetOf(), "Savings", null, out var refusal), refusal));

    /// <summary>An unnamed new deposit asset is left to the income's own validation.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void CanRetargetIncome_LeavesAnUnnamedNewAsset_ToTheIncome(string? nextName)
        => Assert.True(AssetUsagePolicy.CanRetargetIncome(AssetOf(), nextName, null, out _));

    /// <summary>A deleted new deposit asset is refused; an active one is accepted.</summary>
    [Fact]
    public void CanRetargetIncome_RefusesADeletedNewAsset_AndAcceptsAnActiveOne()
    {
        Assert.Equal(AssetRefusal.Deleted,
            Outcome(AssetUsagePolicy.CanRetargetIncome(null, "Savings", AssetOf("Savings", deleted: true), out var refusal), refusal));
        Assert.True(AssetUsagePolicy.CanRetargetIncome(AssetOf(), "Savings", AssetOf("Savings"), out _));
    }
}
