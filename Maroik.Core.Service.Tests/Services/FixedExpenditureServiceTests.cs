using Maroik.Core.Contract.Dtos;
using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Domain.Finance;
using Maroik.Core.Service.Services;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace Maroik.Core.Service.Tests.Services;

/// <summary>
/// Unit tests for <see cref="FixedExpenditureService"/>.
/// All repository dependencies are replaced with Moq mocks.
/// Covers fixed-expenditure CRUD, maturity-date expiry detection,
/// notification-window (Noticed) flag computation, and unpunctuality tracking.
/// </summary>
public class FixedExpenditureServiceTests
{
    /// <summary>Mock <c>IFixedExpenditureRepository</c> injected into the system under test.</summary>
    private readonly Mock<IFixedExpenditureRepository> _fixedExpenditureRepo = new();
    /// <summary>Mock <c>IAssetBalanceDomainService</c> injected into the system under test.</summary>
    private readonly Mock<IAssetBalanceDomainService> _assetBalance = new();

    /// <summary>Bridges the id-lookup and no-lock asset read the service now uses to the list-/lock-based
    /// setups these tests already declare.</summary>
    public FixedExpenditureServiceTests()
    {
        _fixedExpenditureRepo.Setup(r => r.FindByEmailAndIdAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .Returns(async (string e, long id, CancellationToken c) =>
                (await _fixedExpenditureRepo.Object.GetByAccountEmailAsync(e, c)).FirstOrDefault(x => x.Id == id));
        _assetBalance.Setup(a => a.GetAssetForReadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string e, string p, CancellationToken c) => _assetBalance.Object.GetAssetAsync(e, p, c));
    }

    /// <summary>The fixed "current time" of these tests.</summary>
    private static readonly DateTime Now = new(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);
    /// <summary>The clock the service under test reads, stopped at <see cref="Now"/>.</summary>
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(Now));

    /// <summary>The service under test over the mocked dependencies.</summary>
    private FixedExpenditureService CreateSut() => new(
        _fixedExpenditureRepo.Object,
        _assetBalance.Object,
        _time);

    // -- Helpers --------------------------------------------------------------

    /// <summary>A persisted, active asset named <paramref name="name"/> in <paramref name="unit"/>.</summary>
    private static Asset MakeAsset(string name, string unit = "KRW") =>
        Asset.Reconstitute(name, "user@example.com", "FreeDepositAndWithdrawal", 1000m, unit, null, false, DateTime.UtcNow, DateTime.UtcNow);

    /// <summary>A valid consumer-spending request (no deposit target).</summary>
    private static FixedExpenditureRequest ConsumerRequest() => new()
    {
        MainClass = "ConsumerSpending",
        SubClass = "MealOrEatOutExpenses",
        PaymentMethod = "Wallet",
        Amount = 100m,
        DepositMonth = 1,
        DepositDay = 15,
        MaturityDate = new DateTime(2030, 12, 31)
    };

    /// <summary>A valid regular-savings request with a deposit target.</summary>
    private static FixedExpenditureRequest SavingsRequest() => new()
    {
        MainClass = "RegularSavings",
        SubClass = "Deposit",
        PaymentMethod = "Checking",
        MyDepositAsset = "Savings",
        Amount = 200m,
        DepositMonth = 1,
        DepositDay = 15,
        MaturityDate = new DateTime(2030, 12, 31)
    };

    // -- CreateAsync ----------------------------------------------------------

    /// <summary>Verifies that <c>CreateAsync</c> returns fail when class is invalid.</summary>
    [Theory]
    [InlineData("InvalidClass", "MealOrEatOutExpenses")]
    [InlineData("ConsumerSpending", "Deposit")] // Deposit is not valid for ConsumerSpending
    [InlineData(null, "MealOrEatOutExpenses")]
    public async Task CreateAsync_ReturnsFail_WhenClassIsInvalid(string? mainClass, string subClass)
    {
        var sut = CreateSut();
        var request = new FixedExpenditureRequest
        {
            MainClass = mainClass,
            SubClass = subClass,
            PaymentMethod = "Wallet",
            DepositMonth = 1,
            DepositDay = 15,
            MaturityDate = new DateTime(2030, 12, 31)
        };

        ServiceResult result = await sut.CreateAsync("user@example.com", request, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
    }

    /// <summary>Verifies that <c>CreateAsync</c> returns fail when deposit month is invalid.</summary>
    [Theory]
    [InlineData(0)]   // out of range
    [InlineData(13)]  // out of range
    public async Task CreateAsync_ReturnsFail_WhenDepositMonthIsInvalid(short month)
    {
        var sut = CreateSut();
        var request = ConsumerRequest();
        request.DepositMonth = month;

        ServiceResult result = await sut.CreateAsync("user@example.com", request, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
    }

    /// <summary>Verifies that <c>CreateAsync</c> returns fail when deposit day is invalid.</summary>
    [Theory]
    [InlineData(0)]   // below 1
    [InlineData(32)]  // above 31
    public async Task CreateAsync_ReturnsFail_WhenDepositDayIsInvalid(short day)
    {
        var sut = CreateSut();
        var request = ConsumerRequest();
        request.DepositDay = day;

        ServiceResult result = await sut.CreateAsync("user@example.com", request, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
    }

    /// <summary>
    /// The out-of-range message is a composite-format template with the computed max day and the
    /// requested month as separate <see cref="ServiceResult.ErrorArgs"/> -- not baked into
    /// <see cref="ServiceResult.ErrorKey"/> -- so the UI can localize it via resx.
    /// </summary>
    [Fact]
    public async Task CreateAsync_ReturnsLocalizableErrorArgs_WhenDepositDayIsInvalid()
    {
        var sut = CreateSut();
        var request = ConsumerRequest();
        request.DepositMonth = 1;
        request.DepositDay = 32;

        ServiceResult result = await sut.CreateAsync("user@example.com", request, TestContext.Current.CancellationToken);

        Assert.Equal("Deposit day must be between 1 and {0} for month {1}.", result.ErrorKey);
        Assert.Equal([31, (short)1], result.ErrorArgs);
    }

    /// <summary>Verifies that <c>CreateAsync</c> returns fail when payment method asset not found.</summary>
    [Fact]
    public async Task CreateAsync_ReturnsFail_WhenPaymentMethodAssetNotFound()
    {
        _assetBalance.Setup(r => r.GetAssetAsync("user@example.com", "Wallet", It.IsAny<CancellationToken>())).ReturnsAsync((Asset?)null);
        var sut = CreateSut();

        ServiceResult result = await sut.CreateAsync("user@example.com", ConsumerRequest(), TestContext.Current.CancellationToken);

        Assert.False(result.Success);
    }

    /// <summary>Verifies that <c>CreateAsync</c> returns fail when same payment and deposit asset.</summary>
    [Fact]
    public async Task CreateAsync_ReturnsFail_WhenSamePaymentAndDepositAsset()
    {
        _assetBalance.Setup(r => r.GetAssetAsync("user@example.com", "Wallet", It.IsAny<CancellationToken>())).ReturnsAsync(MakeAsset("Wallet"));
        var sut = CreateSut();
        var request = SavingsRequest();
        request.PaymentMethod = "Wallet";
        request.MyDepositAsset = "Wallet";

        ServiceResult result = await sut.CreateAsync("user@example.com", request, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains("same", result.ErrorKey, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Verifies that <c>CreateAsync</c> returns fail when deposit asset not found.</summary>
    [Fact]
    public async Task CreateAsync_ReturnsFail_WhenDepositAssetNotFound()
    {
        _assetBalance.Setup(r => r.GetAssetAsync("user@example.com", "Checking", It.IsAny<CancellationToken>())).ReturnsAsync(MakeAsset("Checking"));
        _assetBalance.Setup(r => r.GetAssetAsync("user@example.com", "Savings", It.IsAny<CancellationToken>())).ReturnsAsync((Asset?)null);
        var sut = CreateSut();

        ServiceResult result = await sut.CreateAsync("user@example.com", SavingsRequest(), TestContext.Current.CancellationToken);

        Assert.False(result.Success);
    }

    /// <summary>Verifies that <c>CreateAsync</c> returns fail when currencies mismatch.</summary>
    [Fact]
    public async Task CreateAsync_ReturnsFail_WhenCurrenciesMismatch()
    {
        _assetBalance.Setup(r => r.GetAssetAsync("user@example.com", "Checking", It.IsAny<CancellationToken>())).ReturnsAsync(MakeAsset("Checking"));
        _assetBalance.Setup(r => r.GetAssetAsync("user@example.com", "Savings", It.IsAny<CancellationToken>())).ReturnsAsync(MakeAsset("Savings", "USD"));
        var sut = CreateSut();

        ServiceResult result = await sut.CreateAsync("user@example.com", SavingsRequest(), TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains("MonetaryUnit", result.ErrorKey);
    }

    /// <summary>Verifies that <c>CreateAsync</c> returns success for consumer spending with valid asset.</summary>
    [Fact]
    public async Task CreateAsync_ReturnsSuccess_ForConsumerSpendingWithValidAsset()
    {
        _assetBalance.Setup(r => r.GetAssetAsync("user@example.com", "Wallet", It.IsAny<CancellationToken>())).ReturnsAsync(MakeAsset("Wallet"));
        _fixedExpenditureRepo.Setup(r => r.CreateAsync(It.IsAny<FixedExpenditure>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var sut = CreateSut();

        ServiceResult result = await sut.CreateAsync("user@example.com", ConsumerRequest(), TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        _fixedExpenditureRepo.Verify(r => r.CreateAsync(It.IsAny<FixedExpenditure>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Verifies that <c>CreateAsync</c> returns success for regular savings with matching currencies.</summary>
    [Fact]
    public async Task CreateAsync_ReturnsSuccess_ForRegularSavingsWithMatchingCurrencies()
    {
        _assetBalance.Setup(r => r.GetAssetAsync("user@example.com", "Checking", It.IsAny<CancellationToken>())).ReturnsAsync(MakeAsset("Checking"));
        _assetBalance.Setup(r => r.GetAssetAsync("user@example.com", "Savings", It.IsAny<CancellationToken>())).ReturnsAsync(MakeAsset("Savings"));
        _fixedExpenditureRepo.Setup(r => r.CreateAsync(It.IsAny<FixedExpenditure>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var sut = CreateSut();

        ServiceResult result = await sut.CreateAsync("user@example.com", SavingsRequest(), TestContext.Current.CancellationToken);

        Assert.True(result.Success);
    }

    /// <summary>Verifies that <c>CreateAsync</c> sets amount to absolute value.</summary>
    [Fact]
    public async Task CreateAsync_SetsAmountToAbsoluteValue()
    {
        _assetBalance.Setup(r => r.GetAssetAsync("user@example.com", "Wallet", It.IsAny<CancellationToken>())).ReturnsAsync(MakeAsset("Wallet"));

        FixedExpenditure? captured = null;
        _fixedExpenditureRepo.Setup(r => r.CreateAsync(It.IsAny<FixedExpenditure>(), It.IsAny<CancellationToken>()))
            .Callback<FixedExpenditure, CancellationToken>((fe, _) => captured = fe)
            .Returns(Task.CompletedTask);
        var sut = CreateSut();

        var request = ConsumerRequest();
        request.Amount = -150m;
        await sut.CreateAsync("user@example.com", request, TestContext.Current.CancellationToken);

        Assert.NotNull(captured);
        Assert.Equal(150m, captured.Amount.Amount);
    }

    // -- UpdateAsync ----------------------------------------------------------

    /// <summary>Verifies that <c>UpdateAsync</c> returns fail when class is invalid.</summary>
    [Fact]
    public async Task UpdateAsync_ReturnsFail_WhenClassIsInvalid()
    {
        var sut = CreateSut();
        var request = new FixedExpenditureRequest
        {
            Id = 1,
            MainClass = "InvalidClass",
            SubClass = "MealOrEatOutExpenses",
            PaymentMethod = "Wallet",
            DepositMonth = 1,
            DepositDay = 15,
            MaturityDate = new DateTime(2030, 12, 31)
        };

        ServiceResult result = await sut.UpdateAsync("user@example.com", request, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
    }

    /// <summary>Verifies that <c>UpdateAsync</c> returns fail when existing record not found.</summary>
    [Fact]
    public async Task UpdateAsync_ReturnsFail_WhenExistingRecordNotFound()
    {
        _assetBalance.Setup(r => r.GetAssetAsync("user@example.com", "Wallet", It.IsAny<CancellationToken>())).ReturnsAsync(MakeAsset("Wallet"));
        _fixedExpenditureRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var sut = CreateSut();

        var request = ConsumerRequest();
        request.Id = 99;
        ServiceResult result = await sut.UpdateAsync("user@example.com", request, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
    }

    /// <summary>Verifies that <c>UpdateAsync</c> returns success when valid and record found.</summary>
    [Fact]
    public async Task UpdateAsync_ReturnsSuccess_WhenValidAndRecordFound()
    {
        const string email = "user@example.com";
        var existing = FixedExpenditure.Reconstitute(1, email, "ConsumerSpending", "MealOrEatOutExpenses", null, 100m, "KRW",
            "Wallet", null, 1, 15, new DateTime(2030, 12, 31), null, false, DateTime.UtcNow, DateTime.UtcNow);
        _assetBalance.Setup(r => r.GetAssetAsync(email, "Wallet", It.IsAny<CancellationToken>())).ReturnsAsync(MakeAsset("Wallet"));
        _fixedExpenditureRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([
            existing
        ]);
        _fixedExpenditureRepo.Setup(r => r.UpdateEntityAsync(It.IsAny<FixedExpenditure>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var sut = CreateSut();

        var request = ConsumerRequest();
        request.Id = 1;
        ServiceResult result = await sut.UpdateAsync(email, request, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        _fixedExpenditureRepo.Verify(r => r.UpdateEntityAsync(It.IsAny<FixedExpenditure>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Regression test: the edit form's Unpunctuality checkbox reaches the request and must be
    /// persisted (it used to be silently dropped — see FixedIncomeServiceTests).
    /// </summary>
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task UpdateAsync_AppliesUnpunctuality_FromTheRequest(bool requested, bool existingFlag)
    {
        const string email = "user@example.com";
        var existing = FixedExpenditure.Reconstitute(1, email, "ConsumerSpending", "MealOrEatOutExpenses", null, 100m, "KRW",
            "Wallet", null, 1, 15, new DateTime(2030, 12, 31), null, existingFlag, DateTime.UtcNow, DateTime.UtcNow);
        _assetBalance.Setup(r => r.GetAssetAsync(email, "Wallet", It.IsAny<CancellationToken>())).ReturnsAsync(MakeAsset("Wallet"));
        _fixedExpenditureRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([
            existing
        ]);
        _fixedExpenditureRepo.Setup(r => r.UpdateEntityAsync(It.IsAny<FixedExpenditure>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var sut = CreateSut();

        var request = ConsumerRequest();
        request.Id = 1;
        request.Unpunctuality = requested;
        ServiceResult result = await sut.UpdateAsync(email, request, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        _fixedExpenditureRepo.Verify(r => r.UpdateEntityAsync(It.Is<FixedExpenditure>(fe => fe.Unpunctuality == requested), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>A create request carrying Unpunctuality is honored too.</summary>
    [Fact]
    public async Task CreateAsync_PersistsUnpunctuality_WhenRequested()
    {
        _assetBalance.Setup(r => r.GetAssetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(MakeAsset("Wallet"));
        FixedExpenditure? captured = null;
        _fixedExpenditureRepo.Setup(r => r.CreateAsync(It.IsAny<FixedExpenditure>(), It.IsAny<CancellationToken>()))
            .Callback<FixedExpenditure, CancellationToken>((fe, _) => captured = fe)
            .Returns(Task.CompletedTask);
        var sut = CreateSut();

        var request = ConsumerRequest();
        request.Unpunctuality = true;
        await sut.CreateAsync("user@example.com", request, TestContext.Current.CancellationToken);

        Assert.NotNull(captured);
        Assert.True(captured.Unpunctuality);
    }

    // -- DeleteAsync ----------------------------------------------------------

    /// <summary>Verifies that <c>DeleteAsync</c> returns fail when record not found.</summary>
    [Fact]
    public async Task DeleteAsync_ReturnsFail_WhenRecordNotFound()
    {
        _fixedExpenditureRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var sut = CreateSut();

        ServiceResult result = await sut.DeleteAsync("user@example.com", 99, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
    }

    /// <summary>Verifies that <c>DeleteAsync</c> returns success when record found.</summary>
    [Fact]
    public async Task DeleteAsync_ReturnsSuccess_WhenRecordFound()
    {
        const string email = "user@example.com";
        var existing = FixedExpenditure.Reconstitute(1, email, "ConsumerSpending", "MealOrEatOutExpenses", null, 100m, "KRW",
            "Wallet", null, 1, 15, new DateTime(2030, 12, 31), null, false, DateTime.UtcNow, DateTime.UtcNow);
        _fixedExpenditureRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([
            existing
        ]);
        _fixedExpenditureRepo.Setup(r => r.DeleteByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var sut = CreateSut();

        ServiceResult result = await sut.DeleteAsync(email, 1, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        _fixedExpenditureRepo.Verify(r => r.DeleteByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // -- GetFixedExpendituresAsync --------------------------------------------

    /// <summary>Verifies that <c>GetFixedExpendituresAsync</c> delegates to repository.</summary>
    [Fact]
    public async Task GetFixedExpendituresAsync_DelegatesToRepository()
    {
        const string email = "user@example.com";
        var list = new List<FixedExpenditure>
        {
            FixedExpenditure.Reconstitute(1, email, "ConsumerSpending", "MealOrEatOutExpenses", null, 100m, "KRW",
                "Wallet", null, 1, 15, new DateTime(2030, 12, 31), null, false, DateTime.UtcNow, DateTime.UtcNow)
        };
        _fixedExpenditureRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(list);
        var sut = CreateSut();

        List<FixedExpenditureResponse> result = await sut.GetFixedExpendituresAsync(email, TestContext.Current.CancellationToken);

        var item = Assert.Single(result);
        Assert.Equal(1, item.Id);
    }

    // -- Search / GetById -------------------------------------------------------

    /// <summary>Owner e-mail the asset lookups are keyed on.</summary>
    private const string Email = "user@example.com";

    /// <summary>A persisted consumer-spending fixed expenditure paid from <paramref name="payment"/>.</summary>
    private static FixedExpenditure ExistingConsumer(long id = 1, string payment = "Wallet") =>
        FixedExpenditure.Reconstitute(id, Email, "ConsumerSpending", "MealOrEatOutExpenses", null, 100m, "KRW",
            payment, null, 1, 15, new DateTime(2030, 12, 31), null, false, DateTime.UtcNow, DateTime.UtcNow);

    /// <summary>Makes the asset lookup return each of <paramref name="assets"/> by product name.</summary>
    private void GivenAssets(params Asset[] assets)
    {
        foreach (Asset a in assets)
            _assetBalance.Setup(r => r.GetAssetAsync(Email, a.ProductName, It.IsAny<CancellationToken>())).ReturnsAsync(a);
    }

    /// <summary>A deleted (archived) asset named <paramref name="name"/>.</summary>
    private static Asset ArchivedAsset(string name) =>
        Asset.Reconstitute(name, Email, "FreeDepositAndWithdrawal", 1000m, "KRW", null, true, DateTime.UtcNow, DateTime.UtcNow);

    /// <summary>The search is delegated to the repository and its rows are mapped to responses.</summary>
    [Fact]
    public async Task SearchFixedExpendituresAsync_DelegatesToRepository_AndMapsTheRows()
    {
        _fixedExpenditureRepo.Setup(r => r.SearchByAccountEmailAsync(Email, "rent", It.IsAny<CancellationToken>())).ReturnsAsync([ExistingConsumer(7)]);

        List<FixedExpenditureResponse> result = await CreateSut().SearchFixedExpendituresAsync(Email, "rent", TestContext.Current.CancellationToken);

        Assert.Equal(7, Assert.Single(result).Id);
    }

    /// <summary>GetById returns null for an unknown id and the mapped response for a known one.</summary>
    [Fact]
    public async Task GetByIdAsync_ReturnsNullWhenMissing_AndMappedResponseWhenFound()
    {
        _fixedExpenditureRepo.Setup(r => r.GetByAccountEmailAsync(Email, It.IsAny<CancellationToken>())).ReturnsAsync([ExistingConsumer(3)]);
        var sut = CreateSut();

        Assert.Null(await sut.GetByIdAsync(Email, 99, TestContext.Current.CancellationToken));
        Assert.Equal(3, (await sut.GetByIdAsync(Email, 3, TestContext.Current.CancellationToken))?.Id);
    }

    // -- CreateAsync: archived asset / domain error ---------------------------------

    /// <summary>A schedule cannot be created against an archived payment asset.</summary>
    [Fact]
    public async Task CreateAsync_ReturnsAssetDeleted_WhenThePaymentAssetIsArchived()
    {
        GivenAssets(ArchivedAsset("Wallet"));

        ServiceResult result = await CreateSut().CreateAsync(Email, ConsumerRequest(), TestContext.Current.CancellationToken);

        Assert.Equal("FixedExpenditure.AssetDeleted", result.ErrorCode);
        _fixedExpenditureRepo.Verify(r => r.CreateAsync(It.IsAny<FixedExpenditure>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>A transfer schedule cannot be created against an archived deposit asset.</summary>
    [Fact]
    public async Task CreateAsync_ReturnsAssetDeleted_WhenTheDepositAssetIsArchived()
    {
        GivenAssets(MakeAsset("Checking"), ArchivedAsset("Savings"));

        ServiceResult result = await CreateSut().CreateAsync(Email, SavingsRequest(), TestContext.Current.CancellationToken);

        Assert.Equal("FixedExpenditure.AssetDeleted", result.ErrorCode);
        _fixedExpenditureRepo.Verify(r => r.CreateAsync(It.IsAny<FixedExpenditure>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>A record the domain rejects (over-long content) is returned as its validation error and not persisted.</summary>
    [Fact]
    public async Task CreateAsync_ReturnsTheDomainValidationError_AndDoesNotPersist()
    {
        GivenAssets(MakeAsset("Wallet"));
        FixedExpenditureRequest request = ConsumerRequest();
        request.Content = new string('x', 100_000);

        ServiceResult result = await CreateSut().CreateAsync(Email, request, TestContext.Current.CancellationToken);

        Assert.Equal("Finance.ContentTooLong", result.ErrorCode);
        _fixedExpenditureRepo.Verify(r => r.CreateAsync(It.IsAny<FixedExpenditure>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // -- UpdateAsync: asset rules ---------------------------------------------------

    /// <summary>Sets <paramref name="r"/>'s id to 1 (an update request) and returns it.</summary>
    private static FixedExpenditureRequest WithId(FixedExpenditureRequest r) { r.Id = 1; return r; }

    /// <summary>An update naming an archived payment asset is refused before the record is looked up.</summary>
    [Fact]
    public async Task UpdateAsync_ReturnsAssetDeleted_WhenThePaymentAssetIsArchived()
    {
        GivenAssets(ArchivedAsset("Wallet"));

        ServiceResult result = await CreateSut().UpdateAsync(Email, WithId(ConsumerRequest()), TestContext.Current.CancellationToken);

        Assert.Equal("FixedExpenditure.AssetDeleted", result.ErrorCode);
        _fixedExpenditureRepo.Verify(r => r.UpdateEntityAsync(It.IsAny<FixedExpenditure>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>An update to an unknown payment asset is refused.</summary>
    [Fact]
    public async Task UpdateAsync_ReturnsAssetNotFound_WhenThePaymentAssetDoesNotExist()
    {
        GivenAssets();

        ServiceResult result = await CreateSut().UpdateAsync(Email, WithId(ConsumerRequest()), TestContext.Current.CancellationToken);

        Assert.Equal("FixedExpenditure.AssetNotFound", result.ErrorCode);
    }

    /// <summary>A transfer schedule cannot name the same asset on both sides.</summary>
    [Fact]
    public async Task UpdateAsync_ReturnsSameAsset_WhenPaymentAndDepositAssetAreTheSame()
    {
        GivenAssets(MakeAsset("Checking"));

        ServiceResult result = await CreateSut().UpdateAsync(Email, WithId(new FixedExpenditureRequest
        {
            MainClass = "RegularSavings", SubClass = "Deposit", PaymentMethod = "Checking", MyDepositAsset = "Checking",
            Amount = 200m, DepositMonth = 1, DepositDay = 15, MaturityDate = new DateTime(2030, 12, 31)
        }), TestContext.Current.CancellationToken);

        Assert.Equal("FixedExpenditure.SameAsset", result.ErrorCode);
    }

    /// <summary>A transfer update whose deposit asset does not exist / is archived / uses another currency is refused; the row is not touched.</summary>
    [Theory]
    [InlineData("missing", "FixedExpenditure.AssetNotFound")]
    [InlineData("archived", "FixedExpenditure.AssetDeleted")]
    [InlineData("usd", "FixedExpenditure.CurrencyMismatch")]
    public async Task UpdateAsync_RefusesATransferWhoseDepositAssetIsUnusable(string scenario, string expectedCode)
    {
        GivenAssets(MakeAsset("Checking"));
        switch (scenario)
        {
            case "archived": GivenAssets(ArchivedAsset("Savings")); break;
            case "usd": GivenAssets(MakeAsset("Savings", "USD")); break;
        }
        _fixedExpenditureRepo.Setup(r => r.GetByAccountEmailAsync(Email, It.IsAny<CancellationToken>())).ReturnsAsync([ExistingConsumer()]);

        ServiceResult result = await CreateSut().UpdateAsync(Email, WithId(SavingsRequest()), TestContext.Current.CancellationToken);

        Assert.Equal(expectedCode, result.ErrorCode);
        _fixedExpenditureRepo.Verify(r => r.UpdateEntityAsync(It.IsAny<FixedExpenditure>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>An update to content the domain rejects is returned as its validation error and not persisted.</summary>
    [Fact]
    public async Task UpdateAsync_ReturnsTheDomainValidationError_AndDoesNotPersist()
    {
        GivenAssets(MakeAsset("Wallet"));
        _fixedExpenditureRepo.Setup(r => r.GetByAccountEmailAsync(Email, It.IsAny<CancellationToken>())).ReturnsAsync([ExistingConsumer()]);
        FixedExpenditureRequest request = WithId(ConsumerRequest());
        request.Content = new string('x', 100_000);

        ServiceResult result = await CreateSut().UpdateAsync(Email, request, TestContext.Current.CancellationToken);

        Assert.Equal("Finance.ContentTooLong", result.ErrorCode);
        _fixedExpenditureRepo.Verify(r => r.UpdateEntityAsync(It.IsAny<FixedExpenditure>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Changing a transfer schedule into a non-transfer one drops the deposit asset from the stored record.</summary>
    [Fact]
    public async Task UpdateAsync_ClearsTheDepositAsset_WhenTheClassIsNoLongerATransfer()
    {
        var existingTransfer = FixedExpenditure.Reconstitute(1, Email, "RegularSavings", "Deposit", null, 100m, "KRW",
            "Checking", "Savings", 1, 15, new DateTime(2030, 12, 31), null, false, DateTime.UtcNow, DateTime.UtcNow);
        GivenAssets(MakeAsset("Wallet"));
        _fixedExpenditureRepo.Setup(r => r.GetByAccountEmailAsync(Email, It.IsAny<CancellationToken>())).ReturnsAsync([existingTransfer]);
        FixedExpenditureRequest request = WithId(ConsumerRequest());
        request.MyDepositAsset = "Savings";

        ServiceResult result = await CreateSut().UpdateAsync(Email, request, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        _fixedExpenditureRepo.Verify(r => r.UpdateEntityAsync(It.Is<FixedExpenditure>(f => f.MyDepositAsset == null && f.PaymentMethod == "Wallet"), It.IsAny<CancellationToken>()), Times.Once);
    }
}
