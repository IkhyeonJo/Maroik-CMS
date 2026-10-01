using Maroik.Core.Contract.Dtos;
using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Domain.Finance;
using Maroik.Core.Service.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace Maroik.Core.Service.Tests.Services;

/// <summary>
/// Unit tests for <see cref="IncomeService"/>.
/// All repository dependencies are replaced with Moq mocks.
/// Covers income CRUD, asset-balance adjustments on create/update/delete,
/// and category-class validation.
/// </summary>
public class IncomeServiceTests
{
    /// <summary>Mock <c>IIncomeRepository</c> injected into the system under test.</summary>
    private readonly Mock<IIncomeRepository> _incomeRepo = new();
    /// <summary>Mock <c>IAssetBalanceDomainService</c> injected into the system under test.</summary>
    private readonly Mock<IAssetBalanceDomainService> _assetBalance = new();
    /// <summary>Mock <c>IUnitOfWork</c> injected into the system under test.</summary>
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    /// <summary>The fixed "current time" of these tests.</summary>
    private static readonly DateTime Now = new(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);
    /// <summary>The clock the service under test reads, stopped at <see cref="Now"/>.</summary>
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(Now));

    /// <summary>The service under test over the mocked dependencies.</summary>
    private IncomeService CreateSut() => new(
        _incomeRepo.Object,
        _assetBalance.Object,
        _unitOfWork.Object,
        NullLogger<IncomeService>.Instance,
        _time);

    /// <summary>Initializes the test fixture, setting up all required test doubles and the system under test.</summary>
    public IncomeServiceTests()
    {
        _unitOfWork.Setup(u => u.BeginAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _unitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _unitOfWork.Setup(u => u.RollbackAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _unitOfWork.Setup(u => u.DisposeAsync()).Returns(ValueTask.CompletedTask);

        // UpdateAsync/DeleteAsync now fetch a single record by id; bridge it to the list-based
        // setups the tests already declare via GetByAccountEmailAsync. UpdateAsync/DeleteAsync use
        // the FOR UPDATE variant — the row lock itself is covered by the repository tests, so here
        // it resolves identically.
        Func<string, long, CancellationToken, Task<Income?>> findById = async (e, id, c) =>
            (await _incomeRepo.Object.GetByAccountEmailAsync(e, c)).FirstOrDefault(x => x.Id == id);
        _incomeRepo.Setup(r => r.FindByEmailAndIdAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .Returns(findById);
        _incomeRepo.Setup(r => r.FindByEmailAndIdForUpdateAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
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
    private static Asset MakeAsset(string name, decimal amount = 1000m, bool deleted = false)
        => Asset.Reconstitute(
            productName: name,
            accountEmail: "user@example.com",
            item: "Deposit",
            amount: amount,
            monetaryUnit: "KRW",
            note: null,
            deleted: deleted,
            created: DateTime.UtcNow,
            updated: DateTime.UtcNow);

    /// <summary>A valid income request deposited into <paramref name="asset"/>.</summary>
    private static IncomeRequest ValidRequest(string asset = "SavingsAccount") => new()
    {
        MainClass = "RegularIncome",
        SubClass = "LaborIncome",
        DepositMyAssetProductName = asset,
        Amount = 500m,
        Content = "Salary"
    };

    /// <summary>A persisted income deposited into <paramref name="depositAsset"/>.</summary>
    private static Income MakeIncome(long id, string depositAsset = "SavingsAccount", decimal amount = 100m,
        string mainClass = "RegularIncome", string subClass = "LaborIncome")
        => Income.Reconstitute(
            id: id,
            accountEmail: "user@example.com",
            mainClass: mainClass,
            subClass: subClass,
            content: "Salary",
            amount: amount,
            monetaryUnit: "KRW",
            depositMyAssetProductName: depositAsset,
            note: null,
            created: DateTime.UtcNow,
            updated: DateTime.UtcNow);

    // -- CreateAsync ----------------------------------------------------------

    /// <summary>Verifies that <c>CreateAsync</c> returns fail when income class is invalid.</summary>
    [Theory]
    [InlineData("InvalidClass", "LaborIncome")]
    [InlineData("RegularIncome", "InvalidSub")]
    [InlineData("IrregularIncome", "FinancialIncome")] // FinancialIncome not valid for Irregular
    [InlineData(null, "LaborIncome")]
    public async Task CreateAsync_ReturnsFail_WhenIncomeClassIsInvalid(string? mainClass, string? subClass)
    {
        var sut = CreateSut();
        var request = new IncomeRequest { MainClass = mainClass, SubClass = subClass, DepositMyAssetProductName = "Any" };

        ServiceResult result = await sut.CreateAsync("user@example.com", request, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Verifies that <c>CreateAsync</c> returns fail when deposit asset not found.</summary>
    [Fact]
    public async Task CreateAsync_ReturnsFail_WhenDepositAssetNotFound()
    {
        _assetBalance.Setup(r => r.GetAssetAsync("user@example.com", "SavingsAccount", It.IsAny<CancellationToken>())).ReturnsAsync((Asset?)null);
        var sut = CreateSut();

        ServiceResult result = await sut.CreateAsync("user@example.com", ValidRequest(), TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Verifies that <c>CreateAsync</c> returns fail when deposit asset is deleted.</summary>
    [Fact]
    public async Task CreateAsync_ReturnsFail_WhenDepositAssetIsDeleted()
    {
        var asset = MakeAsset("SavingsAccount", deleted: true);
        _assetBalance.Setup(r => r.GetAssetAsync("user@example.com", "SavingsAccount", It.IsAny<CancellationToken>())).ReturnsAsync(asset);
        var sut = CreateSut();

        ServiceResult result = await sut.CreateAsync("user@example.com", ValidRequest(), TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains("deleted", result.ErrorKey, StringComparison.OrdinalIgnoreCase);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Verifies that <c>CreateAsync</c> increments asset amount when valid.</summary>
    [Fact]
    public async Task CreateAsync_IncrementsAssetAmount_WhenValid()
    {
        const string email = "user@example.com";
        var asset = MakeAsset("SavingsAccount", amount: 2000m);
        _assetBalance.Setup(r => r.GetAssetAsync(email, "SavingsAccount", It.IsAny<CancellationToken>())).ReturnsAsync(asset);
        _incomeRepo.Setup(r => r.CreateAsync(It.IsAny<Income>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        Asset? captured = null;
        _assetBalance.Setup(r => r.SaveAsync(It.IsAny<Asset>(), It.IsAny<CancellationToken>()))
            .Callback<Asset, CancellationToken>((a, _) => captured = a)
            .Returns(Task.CompletedTask);
        var sut = CreateSut();

        ServiceResult result = await sut.CreateAsync(email, ValidRequest(), TestContext.Current.CancellationToken); // Amount = 500

        Assert.True(result.Success);
        Assert.NotNull(captured);
        Assert.Equal(2500m, captured.Balance.Amount); // 2000 + 500
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Verifies that <c>CreateAsync</c> returns success for all valid income class combinations.</summary>
    [Theory]
    [InlineData("RegularIncome", "LaborIncome")]
    [InlineData("RegularIncome", "BusinessIncome")]
    [InlineData("RegularIncome", "PensionIncome")]
    [InlineData("RegularIncome", "FinancialIncome")]
    [InlineData("RegularIncome", "RentalIncome")]
    [InlineData("RegularIncome", "OtherIncome")]
    [InlineData("IrregularIncome", "LaborIncome")]
    [InlineData("IrregularIncome", "OtherIncome")]
    public async Task CreateAsync_ReturnsSuccess_ForAllValidIncomeClassCombinations(string mainClass, string subClass)
    {
        const string email = "user@example.com";
        var asset = MakeAsset("SavingsAccount");
        _assetBalance.Setup(r => r.GetAssetAsync(email, "SavingsAccount", It.IsAny<CancellationToken>())).ReturnsAsync(asset);
        _incomeRepo.Setup(r => r.CreateAsync(It.IsAny<Income>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _assetBalance.Setup(r => r.SaveAsync(It.IsAny<Asset>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var sut = CreateSut();

        var request = new IncomeRequest
        {
            MainClass = mainClass,
            SubClass = subClass,
            Content = "Monthly income",
            DepositMyAssetProductName = "SavingsAccount",
            Amount = 100m
        };
        ServiceResult result = await sut.CreateAsync(email, request, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
    }

    // -- UpdateAsync ----------------------------------------------------------

    /// <summary>Verifies that <c>UpdateAsync</c> returns fail when income class is invalid.</summary>
    [Fact]
    public async Task UpdateAsync_ReturnsFail_WhenIncomeClassIsInvalid()
    {
        var sut = CreateSut();
        var request = new IncomeRequest { Id = 1, MainClass = "BadClass", SubClass = "LaborIncome" };

        ServiceResult result = await sut.UpdateAsync("user@example.com", request, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Verifies that <c>UpdateAsync</c> returns fail when income record not found.</summary>
    [Fact]
    public async Task UpdateAsync_ReturnsFail_WhenIncomeRecordNotFound()
    {
        _incomeRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var sut = CreateSut();
        var request = ValidRequest();
        request.Id = 99;

        ServiceResult result = await sut.UpdateAsync("user@example.com", request, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
    }

    /// <summary>Verifies that <c>UpdateAsync</c> returns fail when previous asset is deleted.</summary>
    [Fact]
    public async Task UpdateAsync_ReturnsFail_WhenPreviousAssetIsDeleted()
    {
        const string email = "user@example.com";
        var existing = MakeIncome(1, "OldAsset");
        var deletedAsset = MakeAsset("OldAsset", deleted: true);
        _incomeRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([existing]);
        _assetBalance.Setup(r => r.GetAssetAsync(email, "OldAsset", It.IsAny<CancellationToken>())).ReturnsAsync(deletedAsset);
        var sut = CreateSut();

        var request = ValidRequest("NewAsset");
        request.Id = 1;
        ServiceResult result = await sut.UpdateAsync(email, request, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains("deleted", result.ErrorKey, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Verifies that <c>UpdateAsync</c> adjusts same asset balance when asset name unchanged.</summary>
    [Fact]
    public async Task UpdateAsync_AdjustsSameAssetBalance_WhenAssetNameUnchanged()
    {
        const string email = "user@example.com";
        var existing = MakeIncome(1, "SavingsAccount", 300m);
        var asset = MakeAsset("SavingsAccount", amount: 1300m);
        _incomeRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([existing]);
        _assetBalance.Setup(r => r.GetAssetAsync(email, "SavingsAccount", It.IsAny<CancellationToken>())).ReturnsAsync(asset);
        _incomeRepo.Setup(r => r.UpdateEntityAsync(It.IsAny<Income>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        Asset? captured = null;
        _assetBalance.Setup(r => r.SaveAsync(It.IsAny<Asset>(), It.IsAny<CancellationToken>()))
            .Callback<Asset, CancellationToken>((a, _) => captured = a)
            .Returns(Task.CompletedTask);
        var sut = CreateSut();

        // Same asset, new amount = 500
        var request = new IncomeRequest
        {
            Id = 1,
            MainClass = "RegularIncome",
            SubClass = "LaborIncome",
            Content = "Monthly income",
            DepositMyAssetProductName = "SavingsAccount",
            Amount = 500m
        };
        ServiceResult result = await sut.UpdateAsync(email, request, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.NotNull(captured);
        Assert.Equal(1500m, captured.Balance.Amount); // 1300 - 300 + 500
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Verifies that <c>UpdateAsync</c> adjusts both assets when asset name changes.</summary>
    [Fact]
    public async Task UpdateAsync_AdjustsBothAssets_WhenAssetNameChanges()
    {
        const string email = "user@example.com";
        var existing = MakeIncome(1, "OldAsset", 200m);
        var oldAsset = MakeAsset("OldAsset", amount: 500m);
        var newAsset = MakeAsset("NewAsset", amount: 100m);
        _incomeRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([existing]);
        _assetBalance.Setup(r => r.GetAssetAsync(email, "OldAsset", It.IsAny<CancellationToken>())).ReturnsAsync(oldAsset);
        _assetBalance.Setup(r => r.GetAssetAsync(email, "NewAsset", It.IsAny<CancellationToken>())).ReturnsAsync(newAsset);
        _incomeRepo.Setup(r => r.UpdateEntityAsync(It.IsAny<Income>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var updatedAssets = new List<Asset>();
        _assetBalance.Setup(r => r.SaveAsync(It.IsAny<Asset>(), It.IsAny<CancellationToken>()))
            .Callback<Asset, CancellationToken>((a, _) => updatedAssets.Add(a))
            .Returns(Task.CompletedTask);
        var sut = CreateSut();

        var request = new IncomeRequest
        {
            Id = 1,
            MainClass = "RegularIncome",
            SubClass = "LaborIncome",
            Content = "Monthly income",
            DepositMyAssetProductName = "NewAsset",
            Amount = 400m
        };
        ServiceResult result = await sut.UpdateAsync(email, request, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(2, updatedAssets.Count);
        Assert.Equal(300m, updatedAssets.First(a => a.ProductName == "OldAsset").Balance.Amount); // 500 - 200
        Assert.Equal(500m, updatedAssets.First(a => a.ProductName == "NewAsset").Balance.Amount); // 100 + 400
    }

    /// <summary>Verifies that <c>UpdateAsync</c> locks the previous/new deposit assets in ordinal
    /// name order (not previous-then-new role order) so concurrent transactions on the same
    /// asset pair, including in <see cref="ExpenditureService"/>, cannot deadlock.</summary>
    [Fact]
    public async Task UpdateAsync_LocksAssetsInOrdinalOrder_WhenAssetNameChanges()
    {
        const string email = "user@example.com";
        var existing = MakeIncome(1, depositAsset: "Zebra", amount: 200m);
        _incomeRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([existing]);
        _incomeRepo.Setup(r => r.UpdateEntityAsync(It.IsAny<Income>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var callOrder = new List<string>();
        _assetBalance.Setup(r => r.GetAssetAsync(email, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, name, _) => callOrder.Add(name))
            .Returns<string, string, CancellationToken>((_, name, _) => Task.FromResult<Asset?>(MakeAsset(name)));
        _assetBalance.Setup(r => r.SaveAsync(It.IsAny<Asset>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var sut = CreateSut();

        // Previous asset "Zebra" sorts after the new asset "Apple"; the old code locked them in
        // previous-then-new role order (Zebra, Apple) instead of ordinal order.
        var request = ValidRequest("Apple");
        request.Id = 1;
        ServiceResult result = await sut.UpdateAsync(email, request, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(["Apple", "Zebra"], callOrder.Distinct());
    }

    /// <summary>Verifies that <c>UpdateAsync</c> fails and never withdraws from the old asset when
    /// the new deposit asset name doesn't resolve to an owned asset — regression test for a bug
    /// where the withdrawal from the old asset ran unconditionally while the deposit into the
    /// unresolved new asset was silently skipped, losing money.</summary>
    [Fact]
    public async Task UpdateAsync_ReturnsFail_WhenNewAssetNotFound_AndAssetNameChanges()
    {
        const string email = "user@example.com";
        var existing = MakeIncome(1, "OldAsset", 200m);
        var oldAsset = MakeAsset("OldAsset", amount: 500m);
        _incomeRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([existing]);
        _assetBalance.Setup(r => r.GetAssetAsync(email, "OldAsset", It.IsAny<CancellationToken>())).ReturnsAsync(oldAsset);
        _assetBalance.Setup(r => r.GetAssetAsync(email, "GhostAsset", It.IsAny<CancellationToken>())).ReturnsAsync((Asset?)null);
        var sut = CreateSut();

        var request = ValidRequest("GhostAsset");
        request.Id = 1;
        ServiceResult result = await sut.UpdateAsync(email, request, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        _assetBalance.Verify(r => r.SaveAsync(It.IsAny<Asset>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // -- DeleteAsync ----------------------------------------------------------

    /// <summary>Verifies that <c>DeleteAsync</c> returns fail when income not found.</summary>
    [Fact]
    public async Task DeleteAsync_ReturnsFail_WhenIncomeNotFound()
    {
        _incomeRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var sut = CreateSut();

        ServiceResult result = await sut.DeleteAsync("user@example.com", 99, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Verifies that <c>DeleteAsync</c> returns fail when asset is deleted.</summary>
    [Fact]
    public async Task DeleteAsync_ReturnsFail_WhenAssetIsDeleted()
    {
        const string email = "user@example.com";
        var income = MakeIncome(1);
        var deletedAsset = MakeAsset("SavingsAccount", deleted: true);
        _incomeRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([income]);
        _assetBalance.Setup(r => r.GetAssetAsync(email, "SavingsAccount", It.IsAny<CancellationToken>())).ReturnsAsync(deletedAsset);
        var sut = CreateSut();

        ServiceResult result = await sut.DeleteAsync(email, 1, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains("deleted", result.ErrorKey, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Verifies that <c>DeleteAsync</c> decrements asset amount when valid.</summary>
    [Fact]
    public async Task DeleteAsync_DecrementsAssetAmount_WhenValid()
    {
        const string email = "user@example.com";
        var income = MakeIncome(1, "SavingsAccount", 250m);
        var asset = MakeAsset("SavingsAccount", amount: 1000m);
        _incomeRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([income]);
        _assetBalance.Setup(r => r.GetAssetAsync(email, "SavingsAccount", It.IsAny<CancellationToken>())).ReturnsAsync(asset);
        _incomeRepo.Setup(r => r.DeleteByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        Asset? captured = null;
        _assetBalance.Setup(r => r.SaveAsync(It.IsAny<Asset>(), It.IsAny<CancellationToken>()))
            .Callback<Asset, CancellationToken>((a, _) => captured = a)
            .Returns(Task.CompletedTask);
        var sut = CreateSut();

        ServiceResult result = await sut.DeleteAsync(email, 1, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.NotNull(captured);
        Assert.Equal(750m, captured.Balance.Amount); // 1000 - 250
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // -- GetIncomesAsync ------------------------------------------------------

    /// <summary>Verifies that <c>GetIncomesAsync</c> delegates to repository.</summary>
    [Fact]
    public async Task GetIncomesAsync_DelegatesToRepository()
    {
        const string email = "user@example.com";
        var list = new List<Income> { MakeIncome(1) };
        _incomeRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(list);
        var sut = CreateSut();

        List<IncomeResponse> result = await sut.GetIncomesAsync(email, TestContext.Current.CancellationToken);

        Assert.Single(result);
    }

    // -- SearchIncomesAsync -----------------------------------------------------

    /// <summary>The search is delegated to the repository for the caller's account and its rows are mapped to responses.</summary>
    [Fact]
    public async Task SearchIncomesAsync_DelegatesToRepository_AndMapsTheRows()
    {
        _incomeRepo.Setup(r => r.SearchByAccountEmailAsync("user@example.com", "salary", It.IsAny<CancellationToken>()))
            .ReturnsAsync([MakeIncome(7)]);

        List<IncomeResponse> result = await CreateSut().SearchIncomesAsync("user@example.com", "salary", TestContext.Current.CancellationToken);

        Assert.Equal(7, Assert.Single(result).Id);
    }

    // -- Money-affecting failure branches ------------------------------------------

    /// <summary>Owner e-mail the asset lookups are keyed on.</summary>
    private const string Email = "user@example.com";

    /// <summary>Makes the asset lookup return each of <paramref name="assets"/> by product name.</summary>
    private void GivenAssets(params Asset[] assets)
    {
        foreach (Asset a in assets)
            _assetBalance.Setup(r => r.GetAssetAsync(Email, a.ProductName, It.IsAny<CancellationToken>())).ReturnsAsync(a);
    }

    /// <summary>Makes the owner's income list contain only <paramref name="previous"/>.</summary>
    private void GivenExistingIncome(Income previous) =>
        _incomeRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([previous]);

    /// <summary>Sets <paramref name="r"/>'s id to 1 (an update request) and returns it.</summary>
    private static IncomeRequest WithId(IncomeRequest r) { r.Id = 1; return r; }

    /// <summary>A negative amount typed by the user is stored (and deposited) as its absolute value.</summary>
    [Fact]
    public async Task CreateAsync_StoresAndDepositsTheAbsoluteAmount_WhenTheRequestAmountIsNegative()
    {
        GivenAssets(MakeAsset("SavingsAccount", amount: 1000m));
        Income? stored = null;
        _incomeRepo.Setup(r => r.CreateAsync(It.IsAny<Income>(), It.IsAny<CancellationToken>()))
            .Callback<Income, CancellationToken>((i, _) => stored = i).Returns(Task.CompletedTask);
        Asset? saved = null;
        _assetBalance.Setup(r => r.SaveAsync(It.IsAny<Asset>(), It.IsAny<CancellationToken>()))
            .Callback<Asset, CancellationToken>((a, _) => saved = a).Returns(Task.CompletedTask);
        IncomeRequest request = ValidRequest();
        request.Amount = -250m;

        ServiceResult result = await CreateSut().CreateAsync(Email, request, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(250m, stored?.Amount.Amount);
        Assert.Equal(1250m, saved?.Balance.Amount);
    }

    /// <summary>A deposit that would push the balance past the column range is refused and nothing is committed.</summary>
    [Fact]
    public async Task CreateAsync_ReturnsTheRangeError_AndRollsBack_WhenTheDepositWouldOverflowTheBalance()
    {
        GivenAssets(MakeAsset("SavingsAccount", amount: FinanceAmountPolicy.MaxAbsoluteAmount));

        ServiceResult result = await CreateSut().CreateAsync(Email, ValidRequest(), TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal("Finance.AmountOutOfRange", result.ErrorCode);
        _assetBalance.Verify(r => r.SaveAsync(It.IsAny<Asset>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>An amount with a fifth decimal place is refused (never rounded); nothing is written or committed.</summary>
    [Fact]
    public async Task CreateAsync_ReturnsTheDecimalPlacesError_AndRollsBack_WhenTheAmountHasFiveDecimals()
    {
        GivenAssets(MakeAsset("SavingsAccount"));
        IncomeRequest request = ValidRequest();
        request.Amount = 0.00005m;

        ServiceResult result = await CreateSut().CreateAsync(Email, request, TestContext.Current.CancellationToken);

        Assert.Equal("Finance.AmountTooManyDecimals", result.ErrorCode);
        Assert.Equal("Amount can have up to {0} decimal places.", result.ErrorKey);
        Assert.Equal([4], result.ErrorArgs);
        _incomeRepo.Verify(r => r.CreateAsync(It.IsAny<Income>(), It.IsAny<CancellationToken>()), Times.Never);
        _assetBalance.Verify(r => r.SaveAsync(It.IsAny<Asset>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>A record the domain rejects (over-long content) is returned as its validation error before any balance moves.</summary>
    [Fact]
    public async Task CreateAsync_ReturnsTheDomainValidationError_BeforeMovingAnyBalance()
    {
        GivenAssets(MakeAsset("SavingsAccount"));
        IncomeRequest request = ValidRequest();
        request.Content = new string('x', 100_000);

        ServiceResult result = await CreateSut().CreateAsync(Email, request, TestContext.Current.CancellationToken);

        Assert.Equal("Finance.ContentTooLong", result.ErrorCode);
        _incomeRepo.Verify(r => r.CreateAsync(It.IsAny<Income>(), It.IsAny<CancellationToken>()), Times.Never);
        _assetBalance.Verify(r => r.SaveAsync(It.IsAny<Asset>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>An unexpected failure on create is rolled back and reported without leaking internals.</summary>
    [Fact]
    public async Task CreateAsync_RollsBackAndReportsUnexpectedFailure_WhenThePersistenceThrows()
    {
        GivenAssets(MakeAsset("SavingsAccount"));
        _incomeRepo.Setup(r => r.CreateAsync(It.IsAny<Income>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("secret detail"));

        ServiceResult result = await CreateSut().CreateAsync(Email, ValidRequest(), TestContext.Current.CancellationToken);

        Assert.Equal("Income.Unexpected", result.ErrorCode);
        Assert.DoesNotContain("secret detail", result.ErrorKey);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>An income retargeted onto a since-archived asset is refused (its balance must never be touched).</summary>
    [Fact]
    public async Task UpdateAsync_ReturnsAssetDeleted_WhenTheNewAssetIsArchived()
    {
        GivenExistingIncome(MakeIncome(1));
        GivenAssets(MakeAsset("SavingsAccount"), MakeAsset("Archived", deleted: true));

        ServiceResult result = await CreateSut().UpdateAsync(Email, WithId(ValidRequest("Archived")), TestContext.Current.CancellationToken);

        Assert.Equal("Income.AssetDeleted", result.ErrorCode);
        _incomeRepo.Verify(r => r.UpdateEntityAsync(It.IsAny<Income>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>A record edited to content the domain rejects is returned as its validation error and not persisted.</summary>
    [Fact]
    public async Task UpdateAsync_ReturnsTheDomainValidationError_AndDoesNotPersist()
    {
        GivenExistingIncome(MakeIncome(1));
        GivenAssets(MakeAsset("SavingsAccount"));
        IncomeRequest request = WithId(ValidRequest());
        request.Content = new string('x', 100_000);

        ServiceResult result = await CreateSut().UpdateAsync(Email, request, TestContext.Current.CancellationToken);

        Assert.Equal("Finance.ContentTooLong", result.ErrorCode);
        _incomeRepo.Verify(r => r.UpdateEntityAsync(It.IsAny<Income>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>A balance adjustment the asset refuses (range overflow) rolls the whole update back.</summary>
    [Fact]
    public async Task UpdateAsync_ReturnsTheAdjustmentError_AndRollsBack_WhenTheBalanceWouldOverflow()
    {
        GivenExistingIncome(MakeIncome(1));
        GivenAssets(MakeAsset("SavingsAccount", amount: FinanceAmountPolicy.MaxAbsoluteAmount));

        ServiceResult result = await CreateSut().UpdateAsync(Email, WithId(ValidRequest()), TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>An unexpected failure on update is rolled back and reported without leaking internals.</summary>
    [Fact]
    public async Task UpdateAsync_RollsBackAndReportsUnexpectedFailure_WhenThePersistenceThrows()
    {
        GivenExistingIncome(MakeIncome(1));
        GivenAssets(MakeAsset("SavingsAccount"));
        _incomeRepo.Setup(r => r.UpdateEntityAsync(It.IsAny<Income>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("secret detail"));

        ServiceResult result = await CreateSut().UpdateAsync(Email, WithId(ValidRequest()), TestContext.Current.CancellationToken);

        Assert.Equal("Income.Unexpected", result.ErrorCode);
        Assert.DoesNotContain("secret detail", result.ErrorKey);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Reverting the record's impact must keep the balance in range; otherwise the delete is refused and rolled back.</summary>
    [Fact]
    public async Task DeleteAsync_ReturnsTheAdjustmentError_AndKeepsTheRow_WhenRevertingWouldOverflowTheBalance()
    {
        GivenExistingIncome(MakeIncome(1));
        GivenAssets(MakeAsset("SavingsAccount", amount: -FinanceAmountPolicy.MaxAbsoluteAmount));

        ServiceResult result = await CreateSut().DeleteAsync(Email, 1, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        _incomeRepo.Verify(r => r.DeleteByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>An unexpected failure on delete is rolled back and reported without leaking internals.</summary>
    [Fact]
    public async Task DeleteAsync_RollsBackAndReportsUnexpectedFailure_WhenThePersistenceThrows()
    {
        GivenExistingIncome(MakeIncome(1));
        GivenAssets(MakeAsset("SavingsAccount"));
        _incomeRepo.Setup(r => r.DeleteByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("secret detail"));

        ServiceResult result = await CreateSut().DeleteAsync(Email, 1, TestContext.Current.CancellationToken);

        Assert.Equal("Income.Unexpected", result.ErrorCode);
        Assert.DoesNotContain("secret detail", result.ErrorKey);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
