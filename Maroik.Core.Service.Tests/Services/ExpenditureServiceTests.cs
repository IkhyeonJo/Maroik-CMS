using Maroik.Core.Contract.Dtos;
using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Domain.Finance;
using Maroik.Core.Service.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Maroik.Core.Service.Tests.Services;

/// <summary>
/// Unit tests for <see cref="ExpenditureService"/>.
/// All repository dependencies are replaced with Moq mocks.
/// Covers expenditure CRUD, asset-balance adjustments on create/update/delete,
/// and category-class validation.
/// </summary>
public class ExpenditureServiceTests
{
    /// <summary>Mock <c>IExpenditureRepository</c> injected into the system under test.</summary>
    private readonly Mock<IExpenditureRepository> _expenditureRepo = new();
    /// <summary>Mock <c>IAssetBalanceDomainService</c> injected into the system under test.</summary>
    private readonly Mock<IAssetBalanceDomainService> _assetBalance = new();
    /// <summary>Mock <c>IUnitOfWork</c> injected into the system under test.</summary>
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    /// <summary>The service under test over the mocked dependencies.</summary>
    private ExpenditureService CreateSut() => new(
        _expenditureRepo.Object,
        _assetBalance.Object,
        _unitOfWork.Object,
        NullLogger<ExpenditureService>.Instance);

    /// <summary>Initializes the test fixture, setting up all required test doubles and the system under test.</summary>
    public ExpenditureServiceTests()
    {
        _unitOfWork.Setup(u => u.BeginAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _unitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _unitOfWork.Setup(u => u.RollbackAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _unitOfWork.Setup(u => u.DisposeAsync()).Returns(ValueTask.CompletedTask);

        // UpdateAsync/DeleteAsync now fetch a single record by id; bridge it to the list-based
        // setups the tests already declare via GetByAccountEmailAsync. UpdateAsync/DeleteAsync use
        // the FOR UPDATE variant — the row lock itself is covered by the repository tests, so here
        // it resolves identically.
        Func<string, long, CancellationToken, Task<Expenditure?>> findById = async (e, id, c) =>
            (await _expenditureRepo.Object.GetByAccountEmailAsync(e, c)).FirstOrDefault(x => x.Id == id);
        _expenditureRepo.Setup(r => r.FindByEmailAndIdAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .Returns(findById);
        _expenditureRepo.Setup(r => r.FindByEmailAndIdForUpdateAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .Returns(findById);

        // GetAssetsOrdinalAsync now fetches every asset in one batched call instead of looping
        // GetAssetAsync per name; bridge it to the per-name setups the tests below already declare.
        _assetBalance.Setup(r => r.GetAssetsAsync(It.IsAny<string>(), It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .Returns(async (string email, IReadOnlyCollection<string> names, CancellationToken ct) =>
            {
                var result = new Dictionary<string, Asset>(StringComparer.Ordinal);
                foreach (string name in names)
                {
                    Asset? asset = await _assetBalance.Object.GetAssetAsync(email, name, ct);
                    if (asset != null) result[name] = asset;
                }
                return result;
            });
    }

    // -- Helpers --------------------------------------------------------------

    /// <summary>A persisted asset named <paramref name="name"/>.</summary>
    private static Asset MakeAsset(string name, decimal amount = 1000m, string currency = "KRW", bool deleted = false) =>
        Asset.Reconstitute(name, "user@example.com", "FreeDepositAndWithdrawal", amount, currency, null, deleted, DateTime.UtcNow, DateTime.UtcNow);

    /// <summary>A consumer-spending request paid from <paramref name="paymentMethod"/> (no deposit target).</summary>
    private static ExpenditureRequest ConsumerRequest(string paymentMethod = "Wallet", decimal amount = 100m) => new()
    {
        MainClass = "ConsumerSpending",
        SubClass = "MealOrEatOutExpenses",
        PaymentMethod = paymentMethod,
        Amount = amount,
        Content = "Lunch"
    };

    /// <summary>A regular-savings transfer from <paramref name="paymentMethod"/> into <paramref name="depositAsset"/>.</summary>
    private static ExpenditureRequest SavingsTransferRequest(
        string paymentMethod = "Checking",
        string depositAsset = "Savings",
        decimal amount = 200m) => new()
        {
            MainClass = "RegularSavings",
            SubClass = "Deposit",
            PaymentMethod = paymentMethod,
            MyDepositAsset = depositAsset,
            Amount = amount,
            Content = "Monthly savings"
        };

    // -- CreateAsync ----------------------------------------------------------

    /// <summary>Verifies that <c>CreateAsync</c> returns fail when main class is invalid.</summary>
    [Fact]
    public async Task CreateAsync_ReturnsFail_WhenMainClassIsInvalid()
    {
        var sut = CreateSut();
        var request = new ExpenditureRequest { MainClass = "InvalidClass", SubClass = "Anything" };

        ServiceResult result = await sut.CreateAsync("user@example.com", request, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Verifies that <c>CreateAsync</c> returns fail when subclass is invalid for main class.</summary>
    [Fact]
    public async Task CreateAsync_ReturnsFail_WhenSubClassIsInvalidForMainClass()
    {
        var sut = CreateSut();
        var request = new ExpenditureRequest { MainClass = "ConsumerSpending", SubClass = "Deposit" };

        ServiceResult result = await sut.CreateAsync("user@example.com", request, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Verifies that <c>CreateAsync</c> returns fail when payment asset not found.</summary>
    [Fact]
    public async Task CreateAsync_ReturnsFail_WhenPaymentAssetNotFound()
    {
        _assetBalance.Setup(r => r.GetAssetAsync("user@example.com", "Wallet", It.IsAny<CancellationToken>())).ReturnsAsync((Asset?)null);
        var sut = CreateSut();

        ServiceResult result = await sut.CreateAsync("user@example.com", ConsumerRequest(), TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Verifies that <c>CreateAsync</c> returns fail when payment asset is deleted.</summary>
    [Fact]
    public async Task CreateAsync_ReturnsFail_WhenPaymentAssetIsDeleted()
    {
        _assetBalance.Setup(r => r.GetAssetAsync("user@example.com", "Wallet", It.IsAny<CancellationToken>())).ReturnsAsync(MakeAsset("Wallet", deleted: true));
        var sut = CreateSut();

        ServiceResult result = await sut.CreateAsync("user@example.com", ConsumerRequest(), TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains("deleted", result.ErrorKey, StringComparison.OrdinalIgnoreCase);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Verifies that <c>CreateAsync</c> returns fail when payment and deposit asset are same for transfer type.</summary>
    [Fact]
    public async Task CreateAsync_ReturnsFail_WhenPaymentAndDepositAssetAreSame_ForTransferType()
    {
        _assetBalance.Setup(r => r.GetAssetAsync(It.IsAny<string>(), "Wallet", It.IsAny<CancellationToken>())).ReturnsAsync(MakeAsset("Wallet"));
        var sut = CreateSut();
        var request = SavingsTransferRequest(paymentMethod: "Wallet", depositAsset: "Wallet");

        ServiceResult result = await sut.CreateAsync("user@example.com", request, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains("same", result.ErrorKey, StringComparison.OrdinalIgnoreCase);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Verifies that <c>CreateAsync</c> returns fail when deposit asset not found.</summary>
    [Fact]
    public async Task CreateAsync_ReturnsFail_WhenDepositAssetNotFound()
    {
        _assetBalance.Setup(r => r.GetAssetAsync("user@example.com", "Checking", It.IsAny<CancellationToken>())).ReturnsAsync(MakeAsset("Checking"));
        _assetBalance.Setup(r => r.GetAssetAsync("user@example.com", "Savings", It.IsAny<CancellationToken>())).ReturnsAsync((Asset?)null);
        var sut = CreateSut();

        ServiceResult result = await sut.CreateAsync("user@example.com", SavingsTransferRequest(), TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Verifies that <c>CreateAsync</c> returns fail when deposit asset is deleted.</summary>
    [Fact]
    public async Task CreateAsync_ReturnsFail_WhenDepositAssetIsDeleted()
    {
        _assetBalance.Setup(r => r.GetAssetAsync("user@example.com", "Checking", It.IsAny<CancellationToken>())).ReturnsAsync(MakeAsset("Checking"));
        _assetBalance.Setup(r => r.GetAssetAsync("user@example.com", "Savings", It.IsAny<CancellationToken>())).ReturnsAsync(MakeAsset("Savings", deleted: true));
        var sut = CreateSut();

        ServiceResult result = await sut.CreateAsync("user@example.com", SavingsTransferRequest(), TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains("deleted", result.ErrorKey, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Verifies that <c>CreateAsync</c> returns fail when currencies mismatch.</summary>
    [Fact]
    public async Task CreateAsync_ReturnsFail_WhenCurrenciesMismatch()
    {
        _assetBalance.Setup(r => r.GetAssetAsync("user@example.com", "Checking", It.IsAny<CancellationToken>())).ReturnsAsync(MakeAsset("Checking", currency: "KRW"));
        _assetBalance.Setup(r => r.GetAssetAsync("user@example.com", "Savings", It.IsAny<CancellationToken>())).ReturnsAsync(MakeAsset("Savings", currency: "USD"));
        var sut = CreateSut();

        ServiceResult result = await sut.CreateAsync("user@example.com", SavingsTransferRequest(), TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains("MonetaryUnit", result.ErrorKey);
    }

    /// <summary>Verifies that <c>CreateAsync</c> deducts payment asset for consumer spending.</summary>
    [Fact]
    public async Task CreateAsync_DeductsPaymentAsset_ForConsumerSpending()
    {
        const string email = "user@example.com";
        var wallet = MakeAsset("Wallet", amount: 1000m);
        _assetBalance.Setup(r => r.GetAssetAsync(email, "Wallet", It.IsAny<CancellationToken>())).ReturnsAsync(wallet);
        _expenditureRepo.Setup(r => r.CreateAsync(It.IsAny<Expenditure>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        Asset? updatedAsset = null;
        _assetBalance.Setup(r => r.SaveAsync(It.IsAny<Asset>(), It.IsAny<CancellationToken>()))
            .Callback<Asset, CancellationToken>((a, _) => updatedAsset = a)
            .Returns(Task.CompletedTask);
        var sut = CreateSut();

        ServiceResult result = await sut.CreateAsync(email, ConsumerRequest("Wallet", 150m), TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.NotNull(updatedAsset);
        Assert.Equal(850m, updatedAsset.Balance.Amount); // 1000 - 150
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Verifies that <c>CreateAsync</c> deducts payment and credits savings for transfer type.</summary>
    [Fact]
    public async Task CreateAsync_DeductsPaymentAndCreditsSavings_ForTransferType()
    {
        const string email = "user@example.com";
        var checking = MakeAsset("Checking", amount: 2000m);
        var savings = MakeAsset("Savings", amount: 500m);
        _assetBalance.Setup(r => r.GetAssetAsync(email, "Checking", It.IsAny<CancellationToken>())).ReturnsAsync(checking);
        _assetBalance.Setup(r => r.GetAssetAsync(email, "Savings", It.IsAny<CancellationToken>())).ReturnsAsync(savings);
        _expenditureRepo.Setup(r => r.CreateAsync(It.IsAny<Expenditure>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var updatedAssets = new List<Asset>();
        _assetBalance.Setup(r => r.SaveAsync(It.IsAny<Asset>(), It.IsAny<CancellationToken>()))
            .Callback<Asset, CancellationToken>((a, _) => updatedAssets.Add(a))
            .Returns(Task.CompletedTask);
        var sut = CreateSut();

        ServiceResult result = await sut.CreateAsync(email, SavingsTransferRequest("Checking", "Savings", 300m), TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(2, updatedAssets.Count);
        Assert.Equal(1700m, updatedAssets.First(a => a.ProductName == "Checking").Balance.Amount);
        Assert.Equal(800m, updatedAssets.First(a => a.ProductName == "Savings").Balance.Amount);
    }

    // -- UpdateAsync ----------------------------------------------------------

    /// <summary>Verifies that <c>UpdateAsync</c> returns fail when class validation fails.</summary>
    [Fact]
    public async Task UpdateAsync_ReturnsFail_WhenClassValidationFails()
    {
        var sut = CreateSut();
        var request = new ExpenditureRequest { Id = 1, MainClass = "BadClass", SubClass = "Anything" };

        ServiceResult result = await sut.UpdateAsync("user@example.com", request, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Verifies that <c>UpdateAsync</c> returns fail when expenditure not found.</summary>
    [Fact]
    public async Task UpdateAsync_ReturnsFail_WhenExpenditureNotFound()
    {
        _expenditureRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var sut = CreateSut();
        var request = ConsumerRequest();
        request.Id = 99;

        ServiceResult result = await sut.UpdateAsync("user@example.com", request, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
    }

    /// <summary>Verifies that <c>UpdateAsync</c> returns fail when previous payment asset is deleted.</summary>
    [Fact]
    public async Task UpdateAsync_ReturnsFail_WhenPreviousPaymentAssetIsDeleted()
    {
        const string email = "user@example.com";
        var previous = Expenditure.Reconstitute(1, email, "ConsumerSpending", "MealOrEatOutExpenses", null, 50m, "KRW", "Wallet", "", null, DateTime.UtcNow, DateTime.UtcNow);
        _expenditureRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([
            previous
        ]);
        _assetBalance.Setup(r => r.GetAssetAsync(email, "Wallet", It.IsAny<CancellationToken>())).ReturnsAsync(MakeAsset("Wallet", deleted: true));
        var sut = CreateSut();

        var request = ConsumerRequest();
        request.Id = 1;
        ServiceResult result = await sut.UpdateAsync(email, request, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains("deleted", result.ErrorKey, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies that <c>UpdateAsync</c> fails fast with AssetNotFound when the request retargets a
    /// non-transfer expenditure's payment method to an asset name that doesn't exist, instead of
    /// silently persisting the update and only failing later inside AdjustAssetBalancesAsync.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_ReturnsFail_WhenNewPaymentAssetNotFound()
    {
        const string email = "user@example.com";
        var previous = Expenditure.Reconstitute(1, email, "ConsumerSpending", "MealOrEatOutExpenses", null, 50m, "KRW", "Wallet", "", null, DateTime.UtcNow, DateTime.UtcNow);
        _expenditureRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([
            previous
        ]);
        _assetBalance.Setup(r => r.GetAssetAsync(email, "Wallet", It.IsAny<CancellationToken>())).ReturnsAsync(MakeAsset("Wallet"));
        _assetBalance.Setup(r => r.GetAssetAsync(email, "GhostWallet", It.IsAny<CancellationToken>())).ReturnsAsync((Asset?)null);
        var sut = CreateSut();

        var request = ConsumerRequest("GhostWallet");
        request.Id = 1;
        ServiceResult result = await sut.UpdateAsync(email, request, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        _expenditureRepo.Verify(r => r.UpdateEntityAsync(It.IsAny<Expenditure>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Verifies that <c>UpdateAsync</c> adjusts asset balance for consumer spending amount change.</summary>
    [Fact]
    public async Task UpdateAsync_AdjustsAssetBalance_ForConsumerSpendingAmountChange()
    {
        const string email = "user@example.com";
        // Previous expenditure: spent 50 from Wallet
        var previous = Expenditure.Reconstitute(1, email, "ConsumerSpending", "MealOrEatOutExpenses", null, 50m, "KRW", "Wallet", "", null, DateTime.UtcNow, DateTime.UtcNow);
        var wallet = MakeAsset("Wallet", amount: 1000m);
        _expenditureRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([
            previous
        ]);
        _assetBalance.Setup(r => r.GetAssetAsync(email, "Wallet", It.IsAny<CancellationToken>())).ReturnsAsync(wallet);
        _expenditureRepo.Setup(r => r.UpdateEntityAsync(It.IsAny<Expenditure>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        Asset? captured = null;
        _assetBalance.Setup(r => r.SaveAsync(It.IsAny<Asset>(), It.IsAny<CancellationToken>()))
            .Callback<Asset, CancellationToken>((a, _) => captured = a)
            .Returns(Task.CompletedTask);
        var sut = CreateSut();

        // New request: spend 100 from Wallet (revert 50, apply 100 → net -50)
        var request = ConsumerRequest();
        request.Id = 1;
        ServiceResult result = await sut.UpdateAsync(email, request, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.NotNull(captured);
        Assert.Equal(950m, captured.Balance.Amount); // 1000 + 50 - 100
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Verifies that <c>UpdateAsync</c> locks touched assets in ordinal name order (not
    /// payment/deposit role order) so concurrent transfer transactions on the same asset pair
    /// cannot deadlock against each other or against <c>CreateAsync</c>.</summary>
    [Fact]
    public async Task UpdateAsync_LocksAssetsInOrdinalOrder_ForTransferType()
    {
        const string email = "user@example.com";
        var previous = Expenditure.Reconstitute(1, email, "RegularSavings", "Deposit", null, 100m, "KRW", "Zebra", "Apple", null, DateTime.UtcNow, DateTime.UtcNow);
        _expenditureRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([
            previous
        ]);
        _expenditureRepo.Setup(r => r.UpdateEntityAsync(It.IsAny<Expenditure>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var callOrder = new List<string>();
        _assetBalance.Setup(r => r.GetAssetAsync(email, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, name, _) => callOrder.Add(name))
            .Returns<string, string, CancellationToken>((_, name, _) => Task.FromResult<Asset?>(MakeAsset(name)));
        _assetBalance.Setup(r => r.SaveAsync(It.IsAny<Asset>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var sut = CreateSut();

        // Payment method "Zebra" sorts after deposit asset "Apple"; the old code locked them in
        // payment-then-deposit role order (Zebra, Apple) instead of ordinal order.
        var request = SavingsTransferRequest("Zebra", "Apple", 100m);
        request.Id = 1;
        ServiceResult result = await sut.UpdateAsync(email, request, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(["Apple", "Zebra"], callOrder.Distinct());
    }

    // -- DeleteAsync ----------------------------------------------------------

    /// <summary>Verifies that <c>DeleteAsync</c> returns fail when expenditure not found.</summary>
    [Fact]
    public async Task DeleteAsync_ReturnsFail_WhenExpenditureNotFound()
    {
        _expenditureRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var sut = CreateSut();

        ServiceResult result = await sut.DeleteAsync("user@example.com", 99, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Verifies that <c>DeleteAsync</c> reverts payment asset balance for consumer spending.</summary>
    [Fact]
    public async Task DeleteAsync_RevertsPaymentAssetBalance_ForConsumerSpending()
    {
        const string email = "user@example.com";
        var expenditure = Expenditure.Reconstitute(1, email, "ConsumerSpending", "MealOrEatOutExpenses", null, 200m, "KRW", "Wallet", "", null, DateTime.UtcNow, DateTime.UtcNow);
        var wallet = MakeAsset("Wallet", amount: 800m);
        _expenditureRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([
            expenditure
        ]);
        _assetBalance.Setup(r => r.GetAssetAsync(email, "Wallet", It.IsAny<CancellationToken>())).ReturnsAsync(wallet);
        _expenditureRepo.Setup(r => r.DeleteByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        Asset? captured = null;
        _assetBalance.Setup(r => r.SaveAsync(It.IsAny<Asset>(), It.IsAny<CancellationToken>()))
            .Callback<Asset, CancellationToken>((a, _) => captured = a)
            .Returns(Task.CompletedTask);
        var sut = CreateSut();

        ServiceResult result = await sut.DeleteAsync(email, 1, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.NotNull(captured);
        Assert.Equal(1000m, captured.Balance.Amount); // 800 + 200 (reverted)
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Verifies that <c>DeleteAsync</c> reverts payment and deposit assets for transfer type.</summary>
    [Fact]
    public async Task DeleteAsync_RevertsPaymentAndDepositAssets_ForTransferType()
    {
        const string email = "user@example.com";
        var expenditure = Expenditure.Reconstitute(1, email, "RegularSavings", "Deposit", null, 300m, "KRW", "Checking", "Savings", null, DateTime.UtcNow, DateTime.UtcNow);
        var checking = MakeAsset("Checking", amount: 700m);
        var savings = MakeAsset("Savings", amount: 800m);
        _expenditureRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([
            expenditure
        ]);
        _assetBalance.Setup(r => r.GetAssetAsync(email, "Checking", It.IsAny<CancellationToken>())).ReturnsAsync(checking);
        _assetBalance.Setup(r => r.GetAssetAsync(email, "Savings", It.IsAny<CancellationToken>())).ReturnsAsync(savings);
        _expenditureRepo.Setup(r => r.DeleteByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var updatedAssets = new List<Asset>();
        _assetBalance.Setup(r => r.SaveAsync(It.IsAny<Asset>(), It.IsAny<CancellationToken>()))
            .Callback<Asset, CancellationToken>((a, _) => updatedAssets.Add(a))
            .Returns(Task.CompletedTask);
        var sut = CreateSut();

        ServiceResult result = await sut.DeleteAsync(email, 1, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(2, updatedAssets.Count);
        Assert.Equal(1000m, updatedAssets.First(a => a.ProductName == "Checking").Balance.Amount); // 700 + 300
        Assert.Equal(500m, updatedAssets.First(a => a.ProductName == "Savings").Balance.Amount);   // 800 - 300
    }

    // -- GetExpendituresAsync -------------------------------------------------

    /// <summary>Verifies that <c>GetExpendituresAsync</c> delegates to repository.</summary>
    [Fact]
    public async Task GetExpendituresAsync_DelegatesToRepository()
    {
        const string email = "user@example.com";
        var list = new List<Expenditure>
        {
            Expenditure.Reconstitute(1, email, "ConsumerSpending", "MealOrEatOutExpenses", null, 100m, "KRW", "Wallet", "", null, DateTime.UtcNow, DateTime.UtcNow)
        };
        _expenditureRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(list);
        var sut = CreateSut();

        List<ExpenditureResponse> result = await sut.GetExpendituresAsync(email, TestContext.Current.CancellationToken);

        var item = Assert.Single(result);
        Assert.Equal(1, item.Id);
    }

    // -- SearchExpendituresAsync ------------------------------------------------

    /// <summary>The search is delegated to the repository for the caller's account and its rows are mapped to responses.</summary>
    [Fact]
    public async Task SearchExpendituresAsync_DelegatesToRepository_AndMapsTheRows()
    {
        const string email = "user@example.com";
        _expenditureRepo.Setup(r => r.SearchByAccountEmailAsync(email, "lunch", It.IsAny<CancellationToken>()))
            .ReturnsAsync([Expenditure.Reconstitute(7, email, "ConsumerSpending", "MealOrEatOutExpenses", "Lunch", 100m, "KRW", "Wallet", "", null, DateTime.UtcNow, DateTime.UtcNow)]);

        List<ExpenditureResponse> result = await CreateSut().SearchExpendituresAsync(email, "lunch", TestContext.Current.CancellationToken);

        Assert.Equal(7, Assert.Single(result).Id);
    }

    // -- CreateAsync: money-affecting failure branches -------------------------------

    /// <summary>Owner e-mail the asset lookups are keyed on.</summary>
    private const string Email = "user@example.com";

    /// <summary>Makes the asset lookup return each of <paramref name="assets"/> by product name.</summary>
    private void GivenAssets(params Asset[] assets)
    {
        foreach (Asset a in assets)
            _assetBalance.Setup(r => r.GetAssetAsync(Email, a.ProductName, It.IsAny<CancellationToken>())).ReturnsAsync(a);
    }

    /// <summary>Makes the owner's expenditure list contain only <paramref name="previous"/>.</summary>
    private void GivenExistingExpenditure(Expenditure previous) =>
        _expenditureRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([previous]);

    /// <summary>A persisted consumer-spending expenditure (id 1) paid from <paramref name="payment"/>.</summary>
    private static Expenditure ExistingConsumer(decimal amount = 50m, string payment = "Wallet") =>
        Expenditure.Reconstitute(1, Email, "ConsumerSpending", "MealOrEatOutExpenses", null, amount, "KRW", payment, "", null, DateTime.UtcNow, DateTime.UtcNow);

    /// <summary>A persisted regular-savings transfer (id 1) from <paramref name="payment"/> into <paramref name="deposit"/>.</summary>
    private static Expenditure ExistingTransfer(decimal amount = 50m, string payment = "Checking", string deposit = "Savings") =>
        Expenditure.Reconstitute(1, Email, "RegularSavings", "Deposit", null, amount, "KRW", payment, deposit, null, DateTime.UtcNow, DateTime.UtcNow);

    /// <summary>A negative amount typed by the user is stored (and withdrawn) as its absolute value.</summary>
    [Fact]
    public async Task CreateAsync_StoresAndWithdrawsTheAbsoluteAmount_WhenTheRequestAmountIsNegative()
    {
        GivenAssets(MakeAsset("Wallet", amount: 1000m));
        Expenditure? stored = null;
        _expenditureRepo.Setup(r => r.CreateAsync(It.IsAny<Expenditure>(), It.IsAny<CancellationToken>()))
            .Callback<Expenditure, CancellationToken>((e, _) => stored = e).Returns(Task.CompletedTask);
        Asset? saved = null;
        _assetBalance.Setup(r => r.SaveAsync(It.IsAny<Asset>(), It.IsAny<CancellationToken>()))
            .Callback<Asset, CancellationToken>((a, _) => saved = a).Returns(Task.CompletedTask);

        ServiceResult result = await CreateSut().CreateAsync(Email, ConsumerRequest("Wallet", -250m), TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(250m, stored?.Amount.Amount);
        Assert.Equal(750m, saved?.Balance.Amount);
    }

    /// <summary>A withdrawal that would push the balance past the column range is refused and nothing is committed.</summary>
    [Fact]
    public async Task CreateAsync_ReturnsTheRangeError_AndRollsBack_WhenTheWithdrawalWouldOverflowTheBalance()
    {
        GivenAssets(MakeAsset("Wallet", amount: -FinanceAmountPolicy.MaxAbsoluteAmount));

        ServiceResult result = await CreateSut().CreateAsync(Email, ConsumerRequest(), TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal("Finance.AmountOutOfRange", result.ErrorCode);
        _assetBalance.Verify(r => r.SaveAsync(It.IsAny<Asset>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>A transfer whose deposit would overflow the destination balance is refused after the source was debited in memory — nothing is saved.</summary>
    [Fact]
    public async Task CreateAsync_ReturnsTheRangeError_AndSavesNoDepositAsset_WhenTheDepositWouldOverflow()
    {
        GivenAssets(MakeAsset("Checking", amount: 1000m), MakeAsset("Savings", amount: FinanceAmountPolicy.MaxAbsoluteAmount));

        ServiceResult result = await CreateSut().CreateAsync(Email, SavingsTransferRequest(amount: 200m), TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal("Finance.AmountOutOfRange", result.ErrorCode);
        _assetBalance.Verify(r => r.SaveAsync(It.Is<Asset>(a => a.ProductName == "Savings"), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>A record the domain rejects (over-long content) is returned as its validation error before any balance moves.</summary>
    [Fact]
    public async Task CreateAsync_ReturnsTheDomainValidationError_BeforeMovingAnyBalance()
    {
        GivenAssets(MakeAsset("Wallet"));
        ExpenditureRequest request = ConsumerRequest();
        request.Content = new string('x', 100_000);

        ServiceResult result = await CreateSut().CreateAsync(Email, request, TestContext.Current.CancellationToken);

        Assert.Equal("Finance.ContentTooLong", result.ErrorCode);
        _expenditureRepo.Verify(r => r.CreateAsync(It.IsAny<Expenditure>(), It.IsAny<CancellationToken>()), Times.Never);
        _assetBalance.Verify(r => r.SaveAsync(It.IsAny<Asset>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>An unexpected failure on create is rolled back and reported without leaking internals.</summary>
    [Fact]
    public async Task CreateAsync_RollsBackAndReportsUnexpectedFailure_WhenThePersistenceThrows()
    {
        GivenAssets(MakeAsset("Wallet"));
        _expenditureRepo.Setup(r => r.CreateAsync(It.IsAny<Expenditure>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("secret detail"));

        ServiceResult result = await CreateSut().CreateAsync(Email, ConsumerRequest(), TestContext.Current.CancellationToken);

        Assert.Equal("Expenditure.Unexpected", result.ErrorCode);
        Assert.DoesNotContain("secret detail", result.ErrorKey);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // -- UpdateAsync: money-affecting failure branches --------------------------------

    /// <summary>Sets <paramref name="r"/>'s id to 1 (an update request) and returns it.</summary>
    private static ExpenditureRequest WithId(ExpenditureRequest r) { r.Id = 1; return r; }

    /// <summary>A transfer edited so that both sides name the same asset is refused before any lookup or lock.</summary>
    [Fact]
    public async Task UpdateAsync_ReturnsFail_WhenPaymentAndDepositAssetAreTheSame_ForTransferType()
    {
        GivenExistingExpenditure(ExistingTransfer());

        ServiceResult result = await CreateSut().UpdateAsync(Email, WithId(SavingsTransferRequest("Checking", "Checking")), TestContext.Current.CancellationToken);

        Assert.Equal("Expenditure.SameAsset", result.ErrorCode);
        _assetBalance.Verify(r => r.GetAssetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _expenditureRepo.Verify(r => r.UpdateEntityAsync(It.IsAny<Expenditure>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>A transfer retargeted to a deposit asset that does not exist is refused, and the row is not updated.</summary>
    [Fact]
    public async Task UpdateAsync_ReturnsAssetNotFound_WhenTheNewDepositAssetDoesNotExist()
    {
        GivenExistingExpenditure(ExistingTransfer());
        GivenAssets(MakeAsset("Checking"), MakeAsset("Savings"));

        ServiceResult result = await CreateSut().UpdateAsync(Email, WithId(SavingsTransferRequest("Checking", "Ghost")), TestContext.Current.CancellationToken);

        Assert.Equal("Expenditure.AssetNotFound", result.ErrorCode);
        _expenditureRepo.Verify(r => r.UpdateEntityAsync(It.IsAny<Expenditure>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>A transfer retargeted onto a since-archived deposit asset is refused (its balance must never be touched).</summary>
    [Fact]
    public async Task UpdateAsync_ReturnsAssetDeleted_WhenTheNewDepositAssetIsArchived()
    {
        GivenExistingExpenditure(ExistingTransfer());
        GivenAssets(MakeAsset("Checking"), MakeAsset("Savings"), MakeAsset("Archived", deleted: true));

        ServiceResult result = await CreateSut().UpdateAsync(Email, WithId(SavingsTransferRequest("Checking", "Archived")), TestContext.Current.CancellationToken);

        Assert.Equal("Expenditure.AssetDeleted", result.ErrorCode);
        _expenditureRepo.Verify(r => r.UpdateEntityAsync(It.IsAny<Expenditure>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>A transfer retargeted to a deposit asset in another currency is refused.</summary>
    [Fact]
    public async Task UpdateAsync_ReturnsCurrencyMismatch_WhenTheNewAssetsUseDifferentCurrencies()
    {
        GivenExistingExpenditure(ExistingTransfer());
        GivenAssets(MakeAsset("Checking"), MakeAsset("Savings"), MakeAsset("UsdSavings", currency: "USD"));

        ServiceResult result = await CreateSut().UpdateAsync(Email, WithId(SavingsTransferRequest("Checking", "UsdSavings")), TestContext.Current.CancellationToken);

        Assert.Equal("Expenditure.CurrencyMismatch", result.ErrorCode);
        _expenditureRepo.Verify(r => r.UpdateEntityAsync(It.IsAny<Expenditure>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>A record whose ORIGINAL deposit asset has since been archived cannot be edited (its old balance impact cannot be reverted).</summary>
    [Fact]
    public async Task UpdateAsync_ReturnsAssetDeleted_WhenThePreviousDepositAssetIsArchived()
    {
        GivenExistingExpenditure(ExistingTransfer(deposit: "OldSavings"));
        GivenAssets(MakeAsset("Checking"), MakeAsset("Savings"), MakeAsset("OldSavings", deleted: true));

        ServiceResult result = await CreateSut().UpdateAsync(Email, WithId(SavingsTransferRequest()), TestContext.Current.CancellationToken);

        Assert.Equal("Expenditure.AssetDeleted", result.ErrorCode);
        _expenditureRepo.Verify(r => r.UpdateEntityAsync(It.IsAny<Expenditure>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>A record edited to content the domain rejects is returned as its validation error and not persisted.</summary>
    [Fact]
    public async Task UpdateAsync_ReturnsTheDomainValidationError_AndDoesNotPersist()
    {
        GivenExistingExpenditure(ExistingConsumer());
        GivenAssets(MakeAsset("Wallet"));
        ExpenditureRequest request = WithId(ConsumerRequest());
        request.Content = new string('x', 100_000);

        ServiceResult result = await CreateSut().UpdateAsync(Email, request, TestContext.Current.CancellationToken);

        Assert.Equal("Finance.ContentTooLong", result.ErrorCode);
        _expenditureRepo.Verify(r => r.UpdateEntityAsync(It.IsAny<Expenditure>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>A balance adjustment the asset refuses (range overflow) rolls the whole update back instead of committing a half-applied edit.</summary>
    [Fact]
    public async Task UpdateAsync_ReturnsTheAdjustmentError_AndRollsBack_WhenTheBalanceWouldOverflow()
    {
        GivenExistingExpenditure(ExistingConsumer(amount: 50m));
        GivenAssets(MakeAsset("Wallet", amount: -FinanceAmountPolicy.MaxAbsoluteAmount));

        ServiceResult result = await CreateSut().UpdateAsync(Email, WithId(ConsumerRequest()), TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>An unexpected failure on update is rolled back and reported without leaking internals.</summary>
    [Fact]
    public async Task UpdateAsync_RollsBackAndReportsUnexpectedFailure_WhenThePersistenceThrows()
    {
        GivenExistingExpenditure(ExistingConsumer());
        GivenAssets(MakeAsset("Wallet"));
        _expenditureRepo.Setup(r => r.UpdateEntityAsync(It.IsAny<Expenditure>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("secret detail"));

        ServiceResult result = await CreateSut().UpdateAsync(Email, WithId(ConsumerRequest("Wallet", 60m)), TestContext.Current.CancellationToken);

        Assert.Equal("Expenditure.Unexpected", result.ErrorCode);
        Assert.DoesNotContain("secret detail", result.ErrorKey);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // -- DeleteAsync: failure branches ------------------------------------------------

    /// <summary>A transfer whose deposit asset has since been archived cannot be deleted (its balance must not be touched).</summary>
    [Fact]
    public async Task DeleteAsync_ReturnsAssetDeleted_WhenTheDepositAssetIsArchived()
    {
        GivenExistingExpenditure(ExistingTransfer());
        GivenAssets(MakeAsset("Checking"), MakeAsset("Savings", deleted: true));

        ServiceResult result = await CreateSut().DeleteAsync(Email, 1, TestContext.Current.CancellationToken);

        Assert.Equal("Expenditure.AssetDeleted", result.ErrorCode);
        _expenditureRepo.Verify(r => r.DeleteByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Reverting the record's impact must keep every balance in range; otherwise the delete is refused and rolled back.</summary>
    [Fact]
    public async Task DeleteAsync_ReturnsTheAdjustmentError_AndKeepsTheRow_WhenRevertingWouldOverflowTheBalance()
    {
        GivenExistingExpenditure(ExistingConsumer(amount: 100m));
        GivenAssets(MakeAsset("Wallet", amount: FinanceAmountPolicy.MaxAbsoluteAmount));

        ServiceResult result = await CreateSut().DeleteAsync(Email, 1, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        _expenditureRepo.Verify(r => r.DeleteByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>An unexpected failure on delete is rolled back and reported without leaking internals.</summary>
    [Fact]
    public async Task DeleteAsync_RollsBackAndReportsUnexpectedFailure_WhenThePersistenceThrows()
    {
        GivenExistingExpenditure(ExistingConsumer());
        GivenAssets(MakeAsset("Wallet"));
        _expenditureRepo.Setup(r => r.DeleteByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("secret detail"));

        ServiceResult result = await CreateSut().DeleteAsync(Email, 1, TestContext.Current.CancellationToken);

        Assert.Equal("Expenditure.Unexpected", result.ErrorCode);
        Assert.DoesNotContain("secret detail", result.ErrorKey);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>A record whose ORIGINAL payment asset has since been archived cannot be moved to another asset (its old balance impact cannot be reverted).</summary>
    [Fact]
    public async Task UpdateAsync_ReturnsAssetDeleted_WhenThePreviousPaymentAssetIsArchived_AndTheNewOneIsNot()
    {
        var previous = Expenditure.Reconstitute(1, Email, "ConsumerSpending", "MealOrEatOutExpenses", null, 50m, "KRW", "OldWallet", "", null, DateTime.UtcNow, DateTime.UtcNow);
        _expenditureRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([
            previous
        ]);
        GivenAssets(MakeAsset("Wallet"), MakeAsset("OldWallet", deleted: true));

        ServiceResult result = await CreateSut().UpdateAsync(Email, WithId(ConsumerRequest()), TestContext.Current.CancellationToken);

        Assert.Equal("Expenditure.AssetDeleted", result.ErrorCode);
        _expenditureRepo.Verify(r => r.UpdateEntityAsync(It.IsAny<Expenditure>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
