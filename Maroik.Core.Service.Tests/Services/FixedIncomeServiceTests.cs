using Maroik.Core.Contract.Dtos;
using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Domain.Finance;
using Maroik.Core.Service.Services;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace Maroik.Core.Service.Tests.Services;

/// <summary>
/// Unit tests for <see cref="FixedIncomeService"/>.
/// All repository dependencies are replaced with Moq mocks.
/// Covers fixed-income CRUD, maturity-date expiry detection,
/// notification-window (Noticed) flag computation, and unpunctuality tracking.
/// </summary>
public class FixedIncomeServiceTests
{
    /// <summary>Mock <c>IFixedIncomeRepository</c> injected into the system under test.</summary>
    private readonly Mock<IFixedIncomeRepository> _fixedIncomeRepo = new();
    /// <summary>Mock <c>IAssetBalanceStore</c> injected into the system under test.</summary>
    private readonly Mock<IAssetBalanceStore> _assetBalance = new();

    /// <summary>Bridges the id-lookup and no-lock asset read the service now uses to the list-/lock-based
    /// setups these tests already declare.</summary>
    public FixedIncomeServiceTests()
    {
        _fixedIncomeRepo.Setup(r => r.FindByEmailAndIdAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .Returns(async (string e, long id, CancellationToken c) =>
                (await _fixedIncomeRepo.Object.GetByAccountEmailAsync(e, c)).FirstOrDefault(x => x.Id == id));
        _assetBalance.Setup(a => a.GetAssetForReadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string e, string p, CancellationToken c) => _assetBalance.Object.GetAssetAsync(e, p, c));
    }

    /// <summary>The fixed "current time" of these tests.</summary>
    private static readonly DateTime Now = new(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);
    /// <summary>The clock the service under test reads, stopped at <see cref="Now"/>.</summary>
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(Now));

    /// <summary>The service under test over the mocked dependencies.</summary>
    private FixedIncomeService CreateSut() => new(_fixedIncomeRepo.Object, _assetBalance.Object, _time);

    // -- Helpers --------------------------------------------------------------

    /// <summary>A persisted, active savings asset named <paramref name="name"/>.</summary>
    private static Asset MakeSavingsAsset(string name = "SavingsAccount", string currency = "KRW") =>
        Asset.Reconstitute(name, "user@example.com", "SavingsAsset", 0m, currency, null, false, DateTime.UtcNow, DateTime.UtcNow);

    /// <summary>A valid fixed-income request with the given classes.</summary>
    private static FixedIncomeRequest ValidRequest(string mainClass = "RegularIncome", string subClass = "LaborIncome") => new()
    {
        MainClass = mainClass,
        SubClass = subClass,
        DepositMyAssetProductName = "SavingsAccount",
        Amount = 500m,
        DepositMonth = 1,
        DepositDay = 15,
        MaturityDate = new DateTime(2030, 12, 31)
    };

    // -- CreateAsync ----------------------------------------------------------

    /// <summary>Verifies that <c>CreateAsync</c> returns fail when class is invalid.</summary>
    [Theory]
    [InlineData("InvalidClass", "LaborIncome")]
    [InlineData("RegularIncome", "InvalidSub")]
    [InlineData("IrregularIncome", "FinancialIncome")] // FinancialIncome not valid for Irregular
    [InlineData(null, "LaborIncome")]
    public async Task CreateAsync_ReturnsFail_WhenClassIsInvalid(string? mainClass, string subClass)
    {
        var sut = CreateSut();
        var request = ValidRequest();
        request.MainClass = mainClass;
        request.SubClass = subClass;

        ServiceResult result = await sut.CreateAsync("user@example.com", request, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
    }

    /// <summary>Verifies that <c>CreateAsync</c> returns fail when deposit month is out of range.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    public async Task CreateAsync_ReturnsFail_WhenDepositMonthIsOutOfRange(short month)
    {
        var sut = CreateSut();
        var request = ValidRequest();
        request.DepositMonth = month;

        ServiceResult result = await sut.CreateAsync("user@example.com", request, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
    }

    /// <summary>Verifies that <c>CreateAsync</c> returns fail when deposit day is out of range.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(32)]
    public async Task CreateAsync_ReturnsFail_WhenDepositDayIsOutOfRange(short day)
    {
        var sut = CreateSut();
        var request = ValidRequest();
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
    public async Task CreateAsync_ReturnsDomainErrorArgs_WhenDepositDayIsOutOfRange()
    {
        var sut = CreateSut();
        var request = ValidRequest();
        request.DepositMonth = 1;
        request.DepositDay = 32;

        ServiceResult result = await sut.CreateAsync("user@example.com", request, TestContext.Current.CancellationToken);

        Assert.Equal("FixedIncome.DepositDay", result.ErrorCode);
        Assert.Equal("Deposit day must be between 1 and {0} for month {1}.", result.ErrorKey);
        Assert.Equal([31, (short)1], result.ErrorArgs);
    }

    /// <summary>Verifies that <c>CreateAsync</c> returns success for all valid class combinations.</summary>
    [Theory]
    [InlineData("RegularIncome", "LaborIncome")]
    [InlineData("RegularIncome", "BusinessIncome")]
    [InlineData("RegularIncome", "PensionIncome")]
    [InlineData("RegularIncome", "FinancialIncome")]
    [InlineData("RegularIncome", "RentalIncome")]
    [InlineData("RegularIncome", "OtherIncome")]
    [InlineData("IrregularIncome", "LaborIncome")]
    [InlineData("IrregularIncome", "OtherIncome")]
    public async Task CreateAsync_ReturnsSuccess_ForAllValidClassCombinations(string mainClass, string subClass)
    {
        _assetBalance.Setup(r => r.GetAssetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeSavingsAsset());
        _fixedIncomeRepo.Setup(r => r.CreateAsync(It.IsAny<FixedIncome>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var sut = CreateSut();

        ServiceResult result = await sut.CreateAsync("user@example.com", ValidRequest(mainClass, subClass), TestContext.Current.CancellationToken);

        Assert.True(result.Success);
    }

    /// <summary>Verifies that <c>CreateAsync</c> sets account email and absolute amount.</summary>
    [Fact]
    public async Task CreateAsync_SetsAccountEmailAndAbsoluteAmount()
    {
        _assetBalance.Setup(r => r.GetAssetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeSavingsAsset());
        FixedIncome? captured = null;
        _fixedIncomeRepo.Setup(r => r.CreateAsync(It.IsAny<FixedIncome>(), It.IsAny<CancellationToken>()))
            .Callback<FixedIncome, CancellationToken>((fi, _) => captured = fi)
            .Returns(Task.CompletedTask);
        var sut = CreateSut();

        var request = ValidRequest();
        request.Amount = -250m;
        await sut.CreateAsync("user@example.com", request, TestContext.Current.CancellationToken);

        Assert.NotNull(captured);
        Assert.Equal("user@example.com", captured.AccountEmail.Value);
        Assert.Equal(250m, captured.Amount.Amount);
        Assert.True(captured.Created > DateTime.MinValue);
    }

    // -- UpdateAsync ----------------------------------------------------------

    /// <summary>Verifies that <c>UpdateAsync</c> returns fail when class is invalid.</summary>
    [Fact]
    public async Task UpdateAsync_ReturnsFail_WhenClassIsInvalid()
    {
        var sut = CreateSut();
        var request = ValidRequest();
        request.MainClass = "BadClass";
        request.Id = 1;

        ServiceResult result = await sut.UpdateAsync("user@example.com", request, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
    }

    /// <summary>Verifies that <c>UpdateAsync</c> returns fail when record not found.</summary>
    [Fact]
    public async Task UpdateAsync_ReturnsFail_WhenRecordNotFound()
    {
        _fixedIncomeRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var sut = CreateSut();

        var request = ValidRequest();
        request.Id = 99;
        ServiceResult result = await sut.UpdateAsync("user@example.com", request, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
    }

    /// <summary>Verifies that <c>UpdateAsync</c> returns success when valid and record found.</summary>
    [Fact]
    public async Task UpdateAsync_ReturnsSuccess_WhenValidAndRecordFound()
    {
        const string email = "user@example.com";
        var existing = FixedIncome.Reconstitute(1, email, "RegularIncome", "LaborIncome", null, 0m, "KRW",
            "SavingsAccount", 1, 15, new DateTime(2025, 12, 31), null, false, DateTime.UtcNow, DateTime.UtcNow);
        _fixedIncomeRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([
            existing
        ]);
        _assetBalance.Setup(r => r.GetAssetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeSavingsAsset());
        _fixedIncomeRepo.Setup(r => r.UpdateEntityAsync(It.IsAny<FixedIncome>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var sut = CreateSut();

        var request = ValidRequest();
        request.Id = 1;
        request.Amount = 600m;
        ServiceResult result = await sut.UpdateAsync(email, request, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        _fixedIncomeRepo.Verify(r => r.UpdateEntityAsync(It.Is<FixedIncome>(fi => fi.Amount.Amount == 600m), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Regression test: the edit form's Unpunctuality checkbox reaches the request, and the service
    /// must persist it. Previously it was silently dropped — <c>FixedIncome.Update</c> does not carry
    /// the flag and nothing called <c>MarkUnpunctual</c> — so it could never be turned on.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_PersistsUnpunctuality_WhenRequested()
    {
        const string email = "user@example.com";
        var existing = FixedIncome.Reconstitute(1, email, "RegularIncome", "LaborIncome", null, 0m, "KRW",
            "SavingsAccount", 1, 15, new DateTime(2030, 12, 31), null, false, DateTime.UtcNow, DateTime.UtcNow);
        _fixedIncomeRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([
            existing
        ]);
        _assetBalance.Setup(r => r.GetAssetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeSavingsAsset());
        _fixedIncomeRepo.Setup(r => r.UpdateEntityAsync(It.IsAny<FixedIncome>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var sut = CreateSut();

        var request = ValidRequest();
        request.Id = 1;
        request.Unpunctuality = true;
        ServiceResult result = await sut.UpdateAsync(email, request, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        _fixedIncomeRepo.Verify(r => r.UpdateEntityAsync(It.Is<FixedIncome>(fi => fi.Unpunctuality), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Unticking the checkbox clears a previously set flag.</summary>
    [Fact]
    public async Task UpdateAsync_ClearsUnpunctuality_WhenNotRequested()
    {
        const string email = "user@example.com";
        var existing = FixedIncome.Reconstitute(1, email, "RegularIncome", "LaborIncome", null, 0m, "KRW",
            "SavingsAccount", 1, 15, new DateTime(2030, 12, 31), null, true, DateTime.UtcNow, DateTime.UtcNow);
        _fixedIncomeRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([
            existing
        ]);
        _assetBalance.Setup(r => r.GetAssetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeSavingsAsset());
        _fixedIncomeRepo.Setup(r => r.UpdateEntityAsync(It.IsAny<FixedIncome>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var sut = CreateSut();

        var request = ValidRequest();
        request.Id = 1;
        request.Unpunctuality = false;
        ServiceResult result = await sut.UpdateAsync(email, request, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        _fixedIncomeRepo.Verify(r => r.UpdateEntityAsync(It.Is<FixedIncome>(fi => !fi.Unpunctuality), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>A create request carrying Unpunctuality is honored too.</summary>
    [Fact]
    public async Task CreateAsync_PersistsUnpunctuality_WhenRequested()
    {
        _assetBalance.Setup(r => r.GetAssetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeSavingsAsset());
        FixedIncome? captured = null;
        _fixedIncomeRepo.Setup(r => r.CreateAsync(It.IsAny<FixedIncome>(), It.IsAny<CancellationToken>()))
            .Callback<FixedIncome, CancellationToken>((fi, _) => captured = fi)
            .Returns(Task.CompletedTask);
        var sut = CreateSut();

        var request = ValidRequest();
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
        _fixedIncomeRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var sut = CreateSut();

        ServiceResult result = await sut.DeleteAsync("user@example.com", 99, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
    }

    /// <summary>Verifies that <c>DeleteAsync</c> returns success when record found.</summary>
    [Fact]
    public async Task DeleteAsync_ReturnsSuccess_WhenRecordFound()
    {
        const string email = "user@example.com";
        var existing = FixedIncome.Reconstitute(1, email, "RegularIncome", "LaborIncome", null, 0m, "KRW",
            "SavingsAccount", 1, 15, new DateTime(2030, 12, 31), null, false, DateTime.UtcNow, DateTime.UtcNow);
        _fixedIncomeRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([
            existing
        ]);
        _fixedIncomeRepo.Setup(r => r.DeleteByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var sut = CreateSut();

        ServiceResult result = await sut.DeleteAsync(email, 1, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        _fixedIncomeRepo.Verify(r => r.DeleteByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // -- GetFixedIncomesAsync -------------------------------------------------

    /// <summary>Verifies that <c>GetFixedIncomesAsync</c> delegates to repository.</summary>
    [Fact]
    public async Task GetFixedIncomesAsync_DelegatesToRepository()
    {
        const string email = "user@example.com";
        var list = new List<FixedIncome>
        {
            FixedIncome.Reconstitute(1, email, "RegularIncome", "LaborIncome", null, 500m, "KRW",
                "SavingsAccount", 1, 15, new DateTime(2030, 12, 31), null, false, DateTime.UtcNow, DateTime.UtcNow)
        };
        _fixedIncomeRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(list);
        var sut = CreateSut();

        List<FixedIncomeResponse> result = await sut.GetFixedIncomesAsync(email, TestContext.Current.CancellationToken);

        Assert.Single(result);
    }

    // -- Search / GetById -------------------------------------------------------

    /// <summary>Owner e-mail used by the existing items and asset lookups.</summary>
    private const string Email = "user@example.com";

    /// <summary>A persisted fixed income deposited into "SavingsAccount".</summary>
    private static FixedIncome ExistingIncome(long id = 1) =>
        FixedIncome.Reconstitute(id, Email, "RegularIncome", "LaborIncome", null, 500m, "KRW", "SavingsAccount",
            1, 15, new DateTime(2030, 12, 31), null, false, DateTime.UtcNow, DateTime.UtcNow);

    /// <summary>A deleted (archived) savings asset named <paramref name="name"/>.</summary>
    private static Asset ArchivedAsset(string name = "SavingsAccount") =>
        Asset.Reconstitute(name, Email, "SavingsAsset", 0m, "KRW", null, true, DateTime.UtcNow, DateTime.UtcNow);

    /// <summary>Sets <paramref name="r"/>'s id to 1 (an update request) and returns it.</summary>
    private static FixedIncomeRequest WithId(FixedIncomeRequest r) { r.Id = 1; return r; }

    /// <summary>The search is delegated to the repository and its rows are mapped to responses.</summary>
    [Fact]
    public async Task SearchFixedIncomesAsync_DelegatesToRepository_AndMapsTheRows()
    {
        _fixedIncomeRepo.Setup(r => r.SearchByAccountEmailAsync(Email, "salary", It.IsAny<CancellationToken>())).ReturnsAsync([ExistingIncome(7)]);

        List<FixedIncomeResponse> result = await CreateSut().SearchFixedIncomesAsync(Email, "salary", TestContext.Current.CancellationToken);

        Assert.Equal(7, Assert.Single(result).Id);
    }

    /// <summary>GetById returns null for an unknown id and the mapped response for a known one.</summary>
    [Fact]
    public async Task GetByIdAsync_ReturnsNullWhenMissing_AndMappedResponseWhenFound()
    {
        _fixedIncomeRepo.Setup(r => r.GetByAccountEmailAsync(Email, It.IsAny<CancellationToken>())).ReturnsAsync([ExistingIncome(3)]);
        var sut = CreateSut();

        Assert.Null(await sut.GetByIdAsync(Email, 99, TestContext.Current.CancellationToken));
        Assert.Equal(3, (await sut.GetByIdAsync(Email, 3, TestContext.Current.CancellationToken))?.Id);
    }

    // -- Asset rules and domain errors -----------------------------------------------

    /// <summary>A schedule cannot be created for an unknown / archived deposit asset.</summary>
    [Theory]
    [InlineData(false, "FixedIncome.AssetNotFound", "The selected asset could not be found.")]
    [InlineData(true, "FixedIncome.AssetDeleted", "Actions cannot be executed with assets that have already been deleted.")]
    public async Task CreateAsync_RefusesAnUnusableDepositAsset(bool archived, string expectedCode, string expectedKey)
    {
        _assetBalance.Setup(r => r.GetAssetAsync(Email, "SavingsAccount", It.IsAny<CancellationToken>())).ReturnsAsync(archived ? ArchivedAsset() : null);

        ServiceResult result = await CreateSut().CreateAsync(Email, ValidRequest(), TestContext.Current.CancellationToken);

        Assert.Equal(expectedCode, result.ErrorCode);
        Assert.Equal(expectedKey, result.ErrorKey);
        _fixedIncomeRepo.Verify(r => r.CreateAsync(It.IsAny<FixedIncome>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>A schedule the domain rejects (over-long content) is returned as its validation error and not persisted.</summary>
    [Fact]
    public async Task CreateAsync_ReturnsTheDomainValidationError_AndDoesNotPersist()
    {
        _assetBalance.Setup(r => r.GetAssetAsync(Email, "SavingsAccount", It.IsAny<CancellationToken>())).ReturnsAsync(MakeSavingsAsset());
        FixedIncomeRequest request = ValidRequest();
        request.Content = new string('x', 100_000);

        ServiceResult result = await CreateSut().CreateAsync(Email, request, TestContext.Current.CancellationToken);

        Assert.Equal("Finance.ContentTooLong", result.ErrorCode);
        _fixedIncomeRepo.Verify(r => r.CreateAsync(It.IsAny<FixedIncome>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>An update naming an unknown / archived deposit asset is refused; the row is not touched.</summary>
    [Theory]
    [InlineData(false, "FixedIncome.AssetNotFound", "The selected asset could not be found.")]
    [InlineData(true, "FixedIncome.AssetDeleted", "Actions cannot be executed with assets that have already been deleted.")]
    public async Task UpdateAsync_RefusesAnUnusableDepositAsset(bool archived, string expectedCode, string expectedKey)
    {
        _fixedIncomeRepo.Setup(r => r.GetByAccountEmailAsync(Email, It.IsAny<CancellationToken>())).ReturnsAsync([ExistingIncome()]);
        _assetBalance.Setup(r => r.GetAssetAsync(Email, "SavingsAccount", It.IsAny<CancellationToken>())).ReturnsAsync(archived ? ArchivedAsset() : null);

        ServiceResult result = await CreateSut().UpdateAsync(Email, WithId(ValidRequest()), TestContext.Current.CancellationToken);

        Assert.Equal(expectedCode, result.ErrorCode);
        Assert.Equal(expectedKey, result.ErrorKey);
        _fixedIncomeRepo.Verify(r => r.UpdateEntityAsync(It.IsAny<FixedIncome>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>An update to content the domain rejects is returned as its validation error and not persisted.</summary>
    [Fact]
    public async Task UpdateAsync_ReturnsTheDomainValidationError_AndDoesNotPersist()
    {
        _fixedIncomeRepo.Setup(r => r.GetByAccountEmailAsync(Email, It.IsAny<CancellationToken>())).ReturnsAsync([ExistingIncome()]);
        _assetBalance.Setup(r => r.GetAssetAsync(Email, "SavingsAccount", It.IsAny<CancellationToken>())).ReturnsAsync(MakeSavingsAsset());
        FixedIncomeRequest request = WithId(ValidRequest());
        request.Content = new string('x', 100_000);

        ServiceResult result = await CreateSut().UpdateAsync(Email, request, TestContext.Current.CancellationToken);

        Assert.Equal("Finance.ContentTooLong", result.ErrorCode);
        _fixedIncomeRepo.Verify(r => r.UpdateEntityAsync(It.IsAny<FixedIncome>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // -- Deposit-month boundaries and not-found results ------------------------------

    /// <summary>The first, second and last months of the year are all valid deposit months.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(12)]
    public async Task CreateAsync_AcceptsEveryMonthFromJanuaryToDecember(short month)
    {
        _assetBalance.Setup(r => r.GetAssetAsync(Email, "SavingsAccount", It.IsAny<CancellationToken>())).ReturnsAsync(MakeSavingsAsset());
        FixedIncomeRequest request = ValidRequest();
        request.DepositMonth = month;

        ServiceResult result = await CreateSut().CreateAsync(Email, request, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
    }

    /// <summary>A month outside 1..12 is the deposit-month validation error, with its message.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    public async Task CreateAsync_ReturnsTheDepositMonthError_WhenTheMonthIsOutOfRange(short month)
    {
        FixedIncomeRequest request = ValidRequest();
        request.DepositMonth = month;

        ServiceResult result = await CreateSut().CreateAsync(Email, request, TestContext.Current.CancellationToken);

        Assert.Equal(ServiceErrorType.Validation, result.ErrorType);
        Assert.Equal("FixedIncome.DepositMonth", result.ErrorCode);
        Assert.Equal("Deposit month must be between 1 and 12.", result.ErrorKey);
    }

    /// <summary>An unknown schedule id on update is a NotFound with the localizable "record not found" message.</summary>
    [Fact]
    public async Task UpdateAsync_ReturnsFixedIncomeNotFound_WithItsMessage_WhenTheRecordIsUnknown()
    {
        _fixedIncomeRepo.Setup(r => r.GetByAccountEmailAsync(Email, It.IsAny<CancellationToken>())).ReturnsAsync([ExistingIncome(2)]);

        ServiceResult result = await CreateSut().UpdateAsync(Email, WithId(ValidRequest()), TestContext.Current.CancellationToken);

        Assert.Equal(ServiceErrorType.NotFound, result.ErrorType);
        Assert.Equal("FixedIncome.NotFound", result.ErrorCode);
        Assert.Equal("The fixed-income record could not be found.", result.ErrorKey);
    }

    /// <summary>An unknown schedule id on delete is a NotFound with the localizable "record not found" message.</summary>
    [Fact]
    public async Task DeleteAsync_ReturnsFixedIncomeNotFound_WithItsMessage_WhenTheRecordIsUnknown()
    {
        _fixedIncomeRepo.Setup(r => r.GetByAccountEmailAsync(Email, It.IsAny<CancellationToken>())).ReturnsAsync([ExistingIncome(2)]);

        ServiceResult result = await CreateSut().DeleteAsync(Email, 1, TestContext.Current.CancellationToken);

        Assert.Equal(ServiceErrorType.NotFound, result.ErrorType);
        Assert.Equal("FixedIncome.NotFound", result.ErrorCode);
        Assert.Equal("The fixed-income record could not be found.", result.ErrorKey);
        _fixedIncomeRepo.Verify(r => r.DeleteByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
