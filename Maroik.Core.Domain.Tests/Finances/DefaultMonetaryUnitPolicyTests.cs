using Maroik.Core.Domain.Finances;
using Maroik.Core.Domain.ValueObjects;
namespace Maroik.Core.Domain.Tests.Finances;

/// <summary>
/// Unit tests for <see cref="DefaultMonetaryUnitPolicy"/> — resolving the effective default
/// unit from the stored value and the account's current assets.
/// </summary>
public class DefaultMonetaryUnitPolicyTests
{
    /// <summary>A persisted asset named <paramref name="productName"/> held in <paramref name="currency"/>.</summary>
    private static Asset MakeAsset(string productName, string currency, bool deleted = false) => Asset.Reconstitute(
        productName,
        "user@example.com",
        "Deposit",
        1000m,
        currency,
        note: null,
        deleted: deleted,
        created: DateTime.UtcNow,
        updated: DateTime.UtcNow);

    /// <summary>Resolve returns null when there are no assets to fall back to.</summary>
    [Fact]
    public void Resolve_ReturnsNull_WhenNoAssets()
    {
        var result = DefaultMonetaryUnitPolicy.Resolve(CurrencyCode.Create("KRW").Value, []);

        Assert.Null(result);
    }

    /// <summary>Resolve keeps the current unit when it still matches one of the assets.</summary>
    [Fact]
    public void Resolve_KeepsCurrentUnit_WhenStillBackedByAnAsset()
    {
        Asset[] assets = [MakeAsset("A", "KRW"), MakeAsset("B", "USD")];

        var result = DefaultMonetaryUnitPolicy.Resolve(CurrencyCode.Create("USD").Value, assets);

        Assert.Equal("USD", result?.Value);
    }

    /// <summary>Resolve falls back to the most common currency when the current unit is null.</summary>
    [Fact]
    public void Resolve_FallsBackToMostCommonCurrency_WhenCurrentUnitIsNull()
    {
        Asset[] assets = [MakeAsset("A", "KRW"), MakeAsset("B", "KRW"), MakeAsset("C", "USD")];

        var result = DefaultMonetaryUnitPolicy.Resolve(null, assets);

        Assert.Equal("KRW", result?.Value);
    }

    /// <summary>Resolve falls back to the most common currency when the current unit no longer matches any asset.</summary>
    [Fact]
    public void Resolve_FallsBackToMostCommonCurrency_WhenCurrentUnitNoLongerBackedByAnyAsset()
    {
        Asset[] assets = [MakeAsset("A", "KRW"), MakeAsset("B", "KRW"), MakeAsset("C", "USD")];

        var result = DefaultMonetaryUnitPolicy.Resolve(CurrencyCode.Create("JPY").Value, assets);

        Assert.Equal("KRW", result?.Value);
    }

    // -- Choose -----------------------------------------------------------------

    /// <summary>A requested unit carried by an active asset is chosen.</summary>
    [Fact]
    public void Choose_ReturnsTheRequestedUnit_WhenAnActiveAssetCarriesIt()
        => Assert.Equal("USD", DefaultMonetaryUnitPolicy.Choose("USD", [MakeAsset("Bank", "KRW"), MakeAsset("Broker", "USD")]));

    /// <summary>A requested unit no asset carries, or none at all, is not chosen.</summary>
    [Theory]
    [InlineData("JPY")]
    [InlineData(null)]
    public void Choose_ReturnsNull_WhenNoAssetCarriesTheRequestedUnit(string? requested)
        => Assert.Null(DefaultMonetaryUnitPolicy.Choose(requested, [MakeAsset("Bank", "KRW")]));

    /// <summary>A unit only a deleted asset carries is still chosen: the dashboard counts deleted assets too.</summary>
    [Fact]
    public void Choose_ReturnsTheRequestedUnit_WhenOnlyADeletedAssetCarriesIt()
        => Assert.Equal("USD", DefaultMonetaryUnitPolicy.Choose("USD", [MakeAsset("Bank", "KRW"), MakeAsset("Old", "USD", deleted: true)]));

    // -- Selectable -------------------------------------------------------------

    /// <summary>The selectable units are the distinct currencies of every asset, deleted ones included.</summary>
    [Fact]
    public void Selectable_ListsTheDistinctCurrenciesOfEveryAsset_IncludingDeletedOnes()
        => Assert.Equal(["KRW", "USD", "JPY"], DefaultMonetaryUnitPolicy.Selectable(
            [MakeAsset("Bank", "KRW"), MakeAsset("Broker", "USD"), MakeAsset("Card", "KRW"), MakeAsset("Old", "JPY", deleted: true)]));
}
