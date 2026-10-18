using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Domain.Finances;
using Maroik.Core.Domain.ValueObjects;
using Maroik.Core.Service.Extensions;
using Moq;

namespace Maroik.Core.Service.Tests.Extensions;

/// <summary>
/// Unit tests for <see cref="AssetBalanceOrdinalLockExtensions.GetAssetsOrdinalAsync"/>.
/// Verifies the deadlock-avoidance contract: assets are always requested from the batched
/// row-locking query in <see cref="StringComparer.Ordinal"/> order regardless of caller-supplied
/// order, duplicate names are only requested once, and blank/null names are filtered out before
/// the single batched call — all-in-one round trip rather than one query per name.
/// </summary>
public class AssetBalanceOrdinalLockExtensionsTests
{
    /// <summary>The fixed "current time" passed to the adjustments.</summary>
    private static readonly DateTime Now = new(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>Owner e-mail of every asset in these tests.</summary>
    private const string Email = "user@example.com";

    /// <summary>Mock <c>IAssetBalanceStore</c> injected into the system under test.</summary>
    private readonly Mock<IAssetBalanceStore> _assetBalance = new();

    /// <summary>A persisted, active deposit asset named <paramref name="productName"/>.</summary>
    private static Asset MakeAsset(string productName, decimal amount = 100m, string currency = "KRW") =>
        Asset.Reconstitute(productName, Email, "Deposit", amount, currency, null, false, DateTime.UtcNow, DateTime.UtcNow);

    /// <summary>Get assets ordinal async requests names in ordinal order, in a single batched call, regardless of input order.</summary>
    [Fact]
    public async Task GetAssetsOrdinalAsync_RequestsNames_InOrdinalOrder_InASingleBatchedCall()
    {
        List<string>? requestedNames = null;
        _assetBalance.Setup(a => a.GetAssetsAsync(Email, It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .Callback<string, IReadOnlyCollection<string>, CancellationToken>((_, names, _) => requestedNames = [.. names])
            .ReturnsAsync((string _, IReadOnlyCollection<string> names, CancellationToken _) =>
                names.ToDictionary(n => n, n => MakeAsset(n), StringComparer.Ordinal));

        // Deliberately scrambled/reverse-ordinal input order.
        string[] names = ["Zebra", "apple", "Mango"];

        Dictionary<string, Asset> result = await _assetBalance.Object.GetAssetsOrdinalAsync(
            Email, names, TestContext.Current.CancellationToken);

        // Ordinal comparison: uppercase letters sort before lowercase, so "Mango" < "Zebra" < "apple".
        Assert.Equal(["Mango", "Zebra", "apple"], requestedNames);
        _assetBalance.Verify(a => a.GetAssetsAsync(Email, It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(3, result.Count);
        Assert.Equal("Mango", result["Mango"].ProductName);
        Assert.Equal("Zebra", result["Zebra"].ProductName);
        Assert.Equal("apple", result["apple"].ProductName);
    }

    /// <summary>Get assets ordinal async requests duplicate names only once, in the batched call.</summary>
    [Fact]
    public async Task GetAssetsOrdinalAsync_RequestsDuplicateNames_OnlyOnce()
    {
        List<string>? requestedNames = null;
        _assetBalance.Setup(a => a.GetAssetsAsync(Email, It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .Callback<string, IReadOnlyCollection<string>, CancellationToken>((_, names, _) => requestedNames = [.. names])
            .ReturnsAsync((string _, IReadOnlyCollection<string> names, CancellationToken _) =>
                names.ToDictionary(n => n, n => MakeAsset(n), StringComparer.Ordinal));

        string[] names = ["Apple", "Apple", "Banana", "Apple"];

        Dictionary<string, Asset> result = await _assetBalance.Object.GetAssetsOrdinalAsync(
            Email, names, TestContext.Current.CancellationToken);

        Assert.Equal(["Apple", "Banana"], requestedNames);
        Assert.Equal(2, result.Count);
    }

    /// <summary>Get assets ordinal async filters out null or empty names before the batched call.</summary>
    [Fact]
    public async Task GetAssetsOrdinalAsync_FiltersOutNullOrEmptyNames_BeforeTheBatchedCall()
    {
        List<string>? requestedNames = null;
        _assetBalance.Setup(a => a.GetAssetsAsync(Email, It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .Callback<string, IReadOnlyCollection<string>, CancellationToken>((_, names, _) => requestedNames = [.. names])
            .ReturnsAsync((string _, IReadOnlyCollection<string> names, CancellationToken _) =>
                names.ToDictionary(n => n, n => MakeAsset(n), StringComparer.Ordinal));

        List<string?> names = ["Apple", null, "", "Banana"];

        Dictionary<string, Asset> result = await _assetBalance.Object.GetAssetsOrdinalAsync(
            Email, names, TestContext.Current.CancellationToken);

        Assert.Equal(["Apple", "Banana"], requestedNames);
        Assert.Equal(2, result.Count);
        Assert.True(result.ContainsKey("Apple"));
        Assert.True(result.ContainsKey("Banana"));
    }

    /// <summary>Get assets ordinal async passes through whatever the batched call returns, including a name with no match.</summary>
    [Fact]
    public async Task GetAssetsOrdinalAsync_ExcludesName_WhenBatchedCallOmitsIt()
    {
        _assetBalance.Setup(a => a.GetAssetsAsync(Email, It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, Asset>(StringComparer.Ordinal) { ["Apple"] = MakeAsset("Apple") });

        string[] names = ["Apple", "Missing"];

        Dictionary<string, Asset> result = await _assetBalance.Object.GetAssetsOrdinalAsync(
            Email, names, TestContext.Current.CancellationToken);

        Assert.Single(result);
        Assert.True(result.ContainsKey("Apple"));
        Assert.False(result.ContainsKey("Missing"));
    }

    // -- FindAsset ----------------------------------------------------------------------------------

    /// <summary>A name among the assets finds that asset.</summary>
    [Fact]
    public void FindAsset_ReturnsTheNamedAsset()
    {
        Asset wallet = MakeAsset("Wallet");
        Dictionary<string, Asset> assets = new(StringComparer.Ordinal) { ["Wallet"] = wallet };

        Assert.Same(wallet, assets.FindAsset("Wallet"));
    }

    /// <summary>An empty or absent name finds nothing.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Missing")]
    public void FindAsset_ReturnsNull_ForAnEmptyOrAbsentName(string? name)
    {
        Dictionary<string, Asset> assets = new(StringComparer.Ordinal) { ["Wallet"] = MakeAsset("Wallet") };

        Assert.Null(assets.FindAsset(name));
    }

    // -- ApplyBalanceMovementsAsync — applies to the given assets ---------------------------------

    /// <summary>A movement of <paramref name="amount"/> in <paramref name="currency"/> on <paramref name="assetName"/>.</summary>
    private static BalanceMovement Move(string assetName, decimal amount, bool isDeposit, string currency = "KRW") =>
        new(assetName, Money.Create(amount, currency).Value, isDeposit);

    /// <summary>
    /// The movements are applied to the dictionary the caller passes, never fetched again, so
    /// ExpenditureService/IncomeService's Update/Delete, which already fetched the assets for their
    /// own pre-write validation, do not fetch them a second time.
    /// </summary>
    [Fact]
    public async Task ApplyBalanceMovementsAsync_NeverFetches()
    {
        Dictionary<string, Asset> assets = new(StringComparer.Ordinal) { ["Wallet"] = MakeAsset("Wallet") };
        _assetBalance.Setup(a => a.SaveAsync(It.IsAny<Asset>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await _assetBalance.Object.ApplyBalanceMovementsAsync(
            assets, [Move("Wallet", 30m, isDeposit: true)],
            Now, TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        _assetBalance.Verify(a => a.GetAssetsAsync(It.IsAny<string>(), It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Deposits and withdrawals apply in order and only the touched assets are saved.</summary>
    [Fact]
    public async Task ApplyBalanceMovementsAsync_DepositsAndWithdraws_SavesOnlyTouchedAssets()
    {
        Asset payment = MakeAsset("Payment", amount: 100m);
        Asset deposit = MakeAsset("Deposit", amount: 50m);
        Asset untouched = MakeAsset("Untouched", amount: 999m);
        Dictionary<string, Asset> assets = new(StringComparer.Ordinal)
        {
            ["Payment"] = payment, ["Deposit"] = deposit, ["Untouched"] = untouched
        };
        List<Asset> saved = [];
        _assetBalance.Setup(a => a.SaveAsync(It.IsAny<Asset>(), It.IsAny<CancellationToken>()))
            .Callback<Asset, CancellationToken>((asset, _) => saved.Add(asset))
            .Returns(Task.CompletedTask);

        var result = await _assetBalance.Object.ApplyBalanceMovementsAsync(
            assets,
            [Move("Payment", 40m, isDeposit: false), Move("Deposit", 40m, isDeposit: true)],
            Now, TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        Assert.Equal(60m, payment.Balance.Amount);
        Assert.Equal(90m, deposit.Balance.Amount);
        Assert.Equal(999m, untouched.Balance.Amount);
        Assert.Equal(2, saved.Count);
        Assert.DoesNotContain(untouched, saved);
    }

    /// <summary>A movement with an empty asset name is skipped entirely (not looked up, not saved).</summary>
    [Fact]
    public async Task ApplyBalanceMovementsAsync_SkipsAMovementWithAnEmptyAssetName()
    {
        Dictionary<string, Asset> assets = new(StringComparer.Ordinal) { ["Wallet"] = MakeAsset("Wallet") };
        var saveCalls = 0;
        _assetBalance.Setup(a => a.SaveAsync(It.IsAny<Asset>(), It.IsAny<CancellationToken>()))
            .Callback(() => saveCalls++).Returns(Task.CompletedTask);

        var result = await _assetBalance.Object.ApplyBalanceMovementsAsync(
            assets, [Move("", 10m, isDeposit: true)],
            Now, TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        Assert.Equal(0, saveCalls);
    }

    /// <summary>A movement recorded in a different currency than the asset's current one is rejected, not silently misapplied.</summary>
    [Fact]
    public async Task ApplyBalanceMovementsAsync_ReturnsError_OnCurrencyMismatch()
    {
        Dictionary<string, Asset> assets = new(StringComparer.Ordinal) { ["Wallet"] = MakeAsset("Wallet", currency: "KRW") };
        _assetBalance.Setup(a => a.SaveAsync(It.IsAny<Asset>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await _assetBalance.Object.ApplyBalanceMovementsAsync(
            assets, [Move("Wallet", 10m, isDeposit: true, currency: "USD")],
            Now, TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
        Assert.Equal("Asset.CurrencyMismatch", result.FirstError.Code);
    }
}
