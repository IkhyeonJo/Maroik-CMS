using Maroik.Core.Domain.Finance;
namespace Maroik.Core.Domain.Tests.Finance;

/// <summary>
/// Unit tests for <see cref="DefaultMonetaryUnitPolicy"/> — resolving the effective default
/// unit from the stored value and the account's current assets.
/// </summary>
public class DefaultMonetaryUnitPolicyTests
{
    /// <summary>A persisted asset named <paramref name="productName"/> held in <paramref name="currency"/>.</summary>
    private static Asset MakeAsset(string productName, string currency) => Asset.Reconstitute(
        productName,
        "user@example.com",
        "Deposit",
        1000m,
        currency,
        note: null,
        deleted: false,
        created: DateTime.UtcNow,
        updated: DateTime.UtcNow);

    /// <summary>Resolve returns null when there are no assets to fall back to.</summary>
    [Fact]
    public void Resolve_ReturnsNull_WhenNoAssets()
    {
        var result = DefaultMonetaryUnitPolicy.Resolve("KRW", []);

        Assert.Null(result);
    }

    /// <summary>Resolve keeps the current unit when it still matches one of the assets.</summary>
    [Fact]
    public void Resolve_KeepsCurrentUnit_WhenStillBackedByAnAsset()
    {
        Asset[] assets = [MakeAsset("A", "KRW"), MakeAsset("B", "USD")];

        var result = DefaultMonetaryUnitPolicy.Resolve("USD", assets);

        Assert.Equal("USD", result);
    }

    /// <summary>Resolve falls back to the most common currency when the current unit is null.</summary>
    [Fact]
    public void Resolve_FallsBackToMostCommonCurrency_WhenCurrentUnitIsNull()
    {
        Asset[] assets = [MakeAsset("A", "KRW"), MakeAsset("B", "KRW"), MakeAsset("C", "USD")];

        var result = DefaultMonetaryUnitPolicy.Resolve(null, assets);

        Assert.Equal("KRW", result);
    }

    /// <summary>Resolve falls back to the most common currency when the current unit no longer matches any asset.</summary>
    [Fact]
    public void Resolve_FallsBackToMostCommonCurrency_WhenCurrentUnitNoLongerBackedByAnyAsset()
    {
        Asset[] assets = [MakeAsset("A", "KRW"), MakeAsset("B", "KRW"), MakeAsset("C", "USD")];

        var result = DefaultMonetaryUnitPolicy.Resolve("JPY", assets);

        Assert.Equal("KRW", result);
    }
}
