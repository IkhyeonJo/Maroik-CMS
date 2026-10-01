using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Domain.Finance;
using Maroik.Core.Service.Services;
using Moq;

namespace Maroik.Core.Service.Tests.Services;

/// <summary>
/// Unit tests for <see cref="AssetBalanceStore"/>.
/// Pins the double-submit guard: balance reads must go through the row-locking
/// repository query (SELECT ... FOR UPDATE) so that concurrent balance mutations
/// of the same asset serialize inside the ambient unit-of-work transaction.
/// </summary>
public class AssetBalanceStoreTests
{
    /// <summary>Mock <c>IAssetRepository</c> injected into the system under test.</summary>
    private readonly Mock<IAssetRepository> _assetRepo = new();

    /// <summary>The service under test over the mocked repository.</summary>
    private AssetBalanceStore CreateSut() => new(_assetRepo.Object);

    /// <summary>A persisted, active 1000 KRW asset named <paramref name="name"/>.</summary>
    private static Asset MakeAsset(string name = "Wallet") =>
        Asset.Reconstitute(name, "user@example.com", "FreeDepositAndWithdrawal", 1000m, "KRW", null, false, DateTime.UtcNow, DateTime.UtcNow);

    /// <summary>Verifies that <c>GetAssetAsync</c> reads through the row-locking query, not the plain lookup.</summary>
    [Fact]
    public async Task GetAssetAsync_UsesRowLockingQuery()
    {
        var asset = MakeAsset();
        _assetRepo.Setup(r => r.FindByEmailAndProductNameForUpdateAsync("user@example.com", "Wallet", It.IsAny<CancellationToken>()))
            .ReturnsAsync(asset);
        var sut = CreateSut();

        Asset? result = await sut.GetAssetAsync("user@example.com", "Wallet", TestContext.Current.CancellationToken);

        Assert.Same(asset, result);
        _assetRepo.Verify(r => r.FindByEmailAndProductNameForUpdateAsync("user@example.com", "Wallet", It.IsAny<CancellationToken>()), Times.Once);
        _assetRepo.Verify(r => r.FindByEmailAndProductNameAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Verifies that <c>GetAssetAsync</c> returns null when the asset does not exist.</summary>
    [Fact]
    public async Task GetAssetAsync_ReturnsNull_WhenAssetNotFound()
    {
        _assetRepo.Setup(r => r.FindByEmailAndProductNameForUpdateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Asset?)null);
        var sut = CreateSut();

        Asset? result = await sut.GetAssetAsync("user@example.com", "Missing", TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    /// <summary>
    /// Verifies that <c>GetAssetsAsync</c> delegates to the batched row-locking repository query
    /// (a single round trip for every requested name, instead of one query per name) and keys the
    /// result by product name.
    /// </summary>
    [Fact]
    public async Task GetAssetsAsync_DelegatesToBatchedRowLockingQuery_AndKeysResultByProductName()
    {
        var wallet = MakeAsset();
        var savings = MakeAsset("Savings");
        _assetRepo.Setup(r => r.FindByEmailAndProductNamesForUpdateAsync(
                "user@example.com", It.Is<IReadOnlyCollection<string>>(n => n.Count == 2 && n.Contains("Wallet") && n.Contains("Savings")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([wallet, savings]);
        var sut = CreateSut();

        Dictionary<string, Asset> result = await sut.GetAssetsAsync(
            "user@example.com", ["Wallet", "Savings"], TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Count);
        Assert.Same(wallet, result["Wallet"]);
        Assert.Same(savings, result["Savings"]);
        _assetRepo.Verify(r => r.FindByEmailAndProductNamesForUpdateAsync(
            It.IsAny<string>(), It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Verifies that <c>SaveAsync</c> delegates to the repository update.</summary>
    [Fact]
    public async Task SaveAsync_DelegatesToRepositoryUpdate()
    {
        var asset = MakeAsset();
        var sut = CreateSut();

        await sut.SaveAsync(asset, TestContext.Current.CancellationToken);

        _assetRepo.Verify(r => r.UpdateEntityAsync(asset, It.IsAny<CancellationToken>()), Times.Once);
    }
}
