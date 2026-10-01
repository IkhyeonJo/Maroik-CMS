using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Domain.Finance;
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

    /// <summary>Mock <c>IAssetBalanceDomainService</c> injected into the system under test.</summary>
    private readonly Mock<IAssetBalanceDomainService> _assetBalance = new();

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

    // -- ApplyAssetBalanceAdjustmentsAsync(string accountEmail, ...) — fetches then applies --------

    /// <summary>The accountEmail overload fetches the referenced assets itself, then applies and saves them.</summary>
    [Fact]
    public async Task ApplyAssetBalanceAdjustmentsAsync_WithAccountEmail_FetchesThenAppliesAndSaves()
    {
        Asset wallet = MakeAsset("Wallet", amount: 100m);
        _assetBalance.Setup(a => a.GetAssetsAsync(Email, It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, Asset>(StringComparer.Ordinal) { ["Wallet"] = wallet });
        Asset? saved = null;
        _assetBalance.Setup(a => a.SaveAsync(It.IsAny<Asset>(), It.IsAny<CancellationToken>()))
            .Callback<Asset, CancellationToken>((asset, _) => saved = asset)
            .Returns(Task.CompletedTask);

        var result = await _assetBalance.Object.ApplyAssetBalanceAdjustmentsAsync(
            Email, [new AssetBalanceAdjustment("Wallet", 30m, IsDeposit: true)],
            "NotFound", "not found", Now, TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        _assetBalance.Verify(a => a.GetAssetsAsync(Email, It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.NotNull(saved);
        Assert.Equal(130m, saved.Balance.Amount);
    }

    // -- ApplyAssetBalanceAdjustmentsAsync(Dictionary<string, Asset>, ...) — applies to given assets --

    /// <summary>
    /// The pre-fetched-dictionary overload never calls GetAssetsAsync — this is the fix for the
    /// double-fetch ExpenditureService/IncomeService's Update/Delete used to make (once for their
    /// own pre-write validation, once again inside this method).
    /// </summary>
    [Fact]
    public async Task ApplyAssetBalanceAdjustmentsAsync_WithDictionary_NeverFetches()
    {
        Dictionary<string, Asset> assets = new(StringComparer.Ordinal) { ["Wallet"] = MakeAsset("Wallet") };
        _assetBalance.Setup(a => a.SaveAsync(It.IsAny<Asset>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await _assetBalance.Object.ApplyAssetBalanceAdjustmentsAsync(
            assets, [new AssetBalanceAdjustment("Wallet", 30m, IsDeposit: true)],
            "NotFound", "not found", Now, TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        _assetBalance.Verify(a => a.GetAssetsAsync(It.IsAny<string>(), It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Deposits and withdrawals apply in order and only the touched assets are saved.</summary>
    [Fact]
    public async Task ApplyAssetBalanceAdjustmentsAsync_WithDictionary_DepositsAndWithdraws_SavesOnlyTouchedAssets()
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

        var result = await _assetBalance.Object.ApplyAssetBalanceAdjustmentsAsync(
            assets,
            [
                new AssetBalanceAdjustment("Payment", 40m, IsDeposit: false),
                new AssetBalanceAdjustment("Deposit", 40m, IsDeposit: true)
            ],
            "NotFound", "not found", Now, TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        Assert.Equal(60m, payment.Balance.Amount);
        Assert.Equal(90m, deposit.Balance.Amount);
        Assert.Equal(999m, untouched.Balance.Amount);
        Assert.Equal(2, saved.Count);
        Assert.DoesNotContain(untouched, saved);
    }

    /// <summary>Entries with an empty asset name are skipped entirely (not looked up, not saved).</summary>
    [Fact]
    public async Task ApplyAssetBalanceAdjustmentsAsync_WithDictionary_SkipsEmptyAssetNameEntries()
    {
        Dictionary<string, Asset> assets = new(StringComparer.Ordinal) { ["Wallet"] = MakeAsset("Wallet") };
        var saveCalls = 0;
        _assetBalance.Setup(a => a.SaveAsync(It.IsAny<Asset>(), It.IsAny<CancellationToken>()))
            .Callback(() => saveCalls++).Returns(Task.CompletedTask);

        var result = await _assetBalance.Object.ApplyAssetBalanceAdjustmentsAsync(
            assets, [new AssetBalanceAdjustment(null, 10m, IsDeposit: true), new AssetBalanceAdjustment("", 10m, IsDeposit: true)],
            "NotFound", "not found", Now, TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        Assert.Equal(0, saveCalls);
    }

    /// <summary>An adjustment referencing a name absent from the dictionary fails with the caller's not-found error, saving nothing.</summary>
    [Fact]
    public async Task ApplyAssetBalanceAdjustmentsAsync_WithDictionary_ReturnsNotFound_WhenNameMissingFromDictionary()
    {
        Dictionary<string, Asset> assets = new(StringComparer.Ordinal) { ["Wallet"] = MakeAsset("Wallet") };
        var saveCalls = 0;
        _assetBalance.Setup(a => a.SaveAsync(It.IsAny<Asset>(), It.IsAny<CancellationToken>()))
            .Callback(() => saveCalls++).Returns(Task.CompletedTask);

        var result = await _assetBalance.Object.ApplyAssetBalanceAdjustmentsAsync(
            assets,
            [
                new AssetBalanceAdjustment("Wallet", 10m, IsDeposit: true),
                new AssetBalanceAdjustment("Missing", 10m, IsDeposit: true)
            ],
            "Income.AssetNotFound", "The asset could not be found.", Now, TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
        Assert.Equal("Income.AssetNotFound", result.FirstError.Code);
        // Not even "Wallet" (processed before the missing one) is saved — the whole adjustment
        // batch fails atomically rather than partially persisting.
        Assert.Equal(0, saveCalls);
    }

    /// <summary>An adjustment recorded in a different currency than the asset's current one is rejected, not silently misapplied.</summary>
    [Fact]
    public async Task ApplyAssetBalanceAdjustmentsAsync_WithDictionary_ReturnsError_OnCurrencyMismatch()
    {
        Dictionary<string, Asset> assets = new(StringComparer.Ordinal) { ["Wallet"] = MakeAsset("Wallet", currency: "KRW") };
        _assetBalance.Setup(a => a.SaveAsync(It.IsAny<Asset>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await _assetBalance.Object.ApplyAssetBalanceAdjustmentsAsync(
            assets, [new AssetBalanceAdjustment("Wallet", 10m, IsDeposit: true, Currency: "USD")],
            "NotFound", "not found", Now, TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
        Assert.Equal("Asset.CurrencyMismatch", result.FirstError.Code);
    }
}
