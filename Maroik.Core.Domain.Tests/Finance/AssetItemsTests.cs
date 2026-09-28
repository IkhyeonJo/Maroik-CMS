using Maroik.Core.Domain.Finance;
namespace Maroik.Core.Domain.Tests.Finance;

/// <summary>
/// Unit tests for <see cref="AssetItems"/>, pinning its <see cref="Maroik.Core.Domain.Primitives.StringTaxonomy"/>-backed
/// <c>All</c>/<c>IsKnown</c> surface (built from <see cref="AssetItemType"/>'s member names) after
/// that refactor.
/// </summary>
public class AssetItemsTests
{
    /// <summary>All contains exactly every <see cref="AssetItemType"/> member name.</summary>
    [Fact]
    public void All_ContainsExactlyEveryEnumMemberName()
    {
        Assert.Equal(new HashSet<string>(Enum.GetNames<AssetItemType>()), AssetItems.All);
    }

    /// <summary>IsKnown returns true for each defined <see cref="AssetItemType"/> member name.</summary>
    [Theory]
    [InlineData(nameof(AssetItemType.FreeDepositAndWithdrawal))]
    [InlineData(nameof(AssetItemType.TrustAsset))]
    [InlineData(nameof(AssetItemType.InsuranceAsset))]
    public void IsKnown_KnownItem_ReturnsTrue(string item) => Assert.True(AssetItems.IsKnown(item));

    /// <summary>IsKnown returns false for an unrecognized item and for null.</summary>
    [Theory]
    [InlineData("NotAnAssetItem")]
    [InlineData(null)]
    public void IsKnown_UnknownOrNull_ReturnsFalse(string? item) => Assert.False(AssetItems.IsKnown(item));
}
