using Maroik.Core.Contract.Dtos;
using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Domain.Finance;
using Maroik.Core.Service.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace Maroik.Core.Service.Tests.Services;

/// <summary>
/// Unit tests for <see cref="AssetService"/>.
/// All repository dependencies are replaced with Moq mocks.
/// Covers asset CRUD, balance adjustments triggered by income/expenditure changes,
/// and product-name rename propagation.
/// </summary>
public class AssetServiceTests
{
    /// <summary>Mock <c>IAssetRepository</c> injected into the system under test.</summary>
    private readonly Mock<IAssetRepository> _assetRepo = new();
    /// <summary>Mock <c>IUnitOfWork</c> injected into the system under test.</summary>
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    /// <summary>Captures the log entries the system under test writes.</summary>
    private readonly FakeLogger<AssetService> _logger = new();

    /// <summary>The fixed "current time" of these tests.</summary>
    private static readonly DateTime Now = new(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);
    /// <summary>The clock the service under test reads, stopped at <see cref="Now"/>.</summary>
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(Now));

    /// <summary>The service under test over the mocked dependencies.</summary>
    private AssetService CreateSut() => new(_assetRepo.Object, _unitOfWork.Object, _logger, _time);

    /// <summary>Initializes the test fixture, setting up all required test doubles and the system under test.</summary>
    public AssetServiceTests()
    {
        _unitOfWork.Setup(u => u.BeginAsync(null, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _unitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _unitOfWork.Setup(u => u.RollbackAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _unitOfWork.Setup(u => u.DisposeAsync()).Returns(ValueTask.CompletedTask);
    }

    // -- Helpers --------------------------------------------------------------

    /// <summary>A persisted asset named <paramref name="name"/>.</summary>
    private static Asset MakeAsset(string name, decimal amount = 1000m, string currency = "KRW", bool deleted = false) =>
        Asset.Reconstitute(name, "user@example.com", "FreeDepositAndWithdrawal", amount, currency, null, deleted, DateTime.UtcNow, DateTime.UtcNow);

    /// <summary>Makes both the plain and the row-locking lookup return <paramref name="asset"/>.</summary>
    private void SetupFindAsset(Asset? asset)
    {
        _assetRepo.Setup(r => r.FindByEmailAndProductNameAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(asset);
        // UpdateAsync row-locks its read via the ForUpdate variant (see AssetService.UpdateAsync);
        // stub it too so Update-path tests using this same helper still find the asset.
        _assetRepo.Setup(r => r.FindByEmailAndProductNameForUpdateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(asset);
    }

    /// <summary>A valid asset request for product <paramref name="name"/>.</summary>
    private static AssetRequest ValidRequest(string name = "MyBank") => new()
    {
        ProductName = name,
        Item = "FreeDepositAndWithdrawal",
        Amount = 0m,
        MonetaryUnit = "KRW"
    };

    // -- CreateAsync ----------------------------------------------------------

    /// <summary>Verifies that <c>CreateAsync</c> returns fail when item is invalid.</summary>
    [Theory]
    [InlineData("InvalidItem")]
    [InlineData("")]
    [InlineData(null)]
    public async Task CreateAsync_ReturnsFail_WhenItemIsInvalid(string? item)
    {
        var sut = CreateSut();
        var request = new AssetRequest { ProductName = "MyBank", Item = item };

        ServiceResult result = await sut.CreateAsync("user@example.com", request, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal("Asset.ItemInvalid", result.ErrorCode);
    }

    /// <summary>
    /// Regression test: a database failure other than a duplicate key used to escape
    /// <c>CreateAsync</c> as an unhandled exception (an HTTP 500); like every other write in
    /// this service it must come back as a failed result.
    /// </summary>
    [Fact]
    public async Task CreateAsync_ReturnsFailure_WhenTheRepositoryThrowsANonDuplicateError()
    {
        _assetRepo.Setup(r => r.CreateAsync(It.IsAny<Asset>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("22001: value too long for type character varying(255)"));
        var sut = CreateSut();

        ServiceResult result = await sut.CreateAsync("user@example.com", ValidRequest(), TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal("Asset.CreateFailed", result.ErrorCode);
        Assert.Equal(ServiceResult.TemporaryErrorKey, result.ErrorKey);
    }

    /// <summary>An over-long product name is now a validation error before it reaches the database.</summary>
    [Fact]
    public async Task CreateAsync_ReturnsValidationError_WhenProductNameIsTooLong()
    {
        var sut = CreateSut();

        ServiceResult result = await sut.CreateAsync("user@example.com", ValidRequest(new string('a', 256)), TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal("Asset.ProductNameTooLong", result.ErrorCode);
        _assetRepo.Verify(r => r.CreateAsync(It.IsAny<Asset>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Verifies that <c>CreateAsync</c> returns fail when asset with same name already exists.</summary>
    [Fact]
    public async Task CreateAsync_ReturnsFail_WhenAssetWithSameNameAlreadyExists()
    {
        SetupFindAsset(MakeAsset("MyBank"));
        var sut = CreateSut();

        ServiceResult result = await sut.CreateAsync("user@example.com", ValidRequest(), TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains("already exists", result.ErrorKey, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies that <c>CreateAsync</c> rejects a name that belongs to a soft-deleted asset rather
    /// than reviving it — a freed-up-looking name must never be reusable, since reviving the row
    /// would silently re-attach its old transaction history to what the user believes is a new,
    /// unrelated asset.
    /// </summary>
    [Fact]
    public async Task CreateAsync_ReturnsFail_WhenSameNameIsSoftDeleted()
    {
        SetupFindAsset(MakeAsset("MyBank", amount: 500m, currency: "KRW", deleted: true));
        var sut = CreateSut();

        ServiceResult result = await sut.CreateAsync("user@example.com", ValidRequest(), TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains("already exists", result.ErrorKey, StringComparison.OrdinalIgnoreCase);
        _assetRepo.Verify(r => r.UpdateEntityAsync(It.IsAny<Asset>(), It.IsAny<CancellationToken>()), Times.Never);
        _assetRepo.Verify(r => r.CreateAsync(It.IsAny<Asset>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Verifies that <c>CreateAsync</c> returns success for all valid item types.</summary>
    [Theory]
    [InlineData("FreeDepositAndWithdrawal")]
    [InlineData("TrustAsset")]
    [InlineData("CashAsset")]
    [InlineData("SavingsAsset")]
    [InlineData("InvestmentAsset")]
    [InlineData("RealEstate")]
    [InlineData("Movables")]
    [InlineData("OtherPhysicalAsset")]
    [InlineData("InsuranceAsset")]
    public async Task CreateAsync_ReturnsSuccess_ForAllValidItemTypes(string item)
    {
        SetupFindAsset(null);
        _assetRepo.Setup(r => r.CreateAsync(It.IsAny<Asset>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var sut = CreateSut();

        var request = new AssetRequest { ProductName = "MyBank", Item = item, MonetaryUnit = "KRW" };
        ServiceResult result = await sut.CreateAsync("user@example.com", request, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
    }

    /// <summary>
    /// Regression test: FindAssetAsync + CreateAsync is a read-then-write race (two concurrent
    /// requests for the same product name can both pass the prior existence check). The DB's
    /// primary key on (ProductName, AccountEmail) still rejects the second insert — verify that
    /// failure is translated to the same friendly "already exists" result instead of propagating
    /// the raw DB exception to the caller.
    /// </summary>
    [Fact]
    public async Task CreateAsync_ReturnsFail_WhenConcurrentInsertViolatesUniqueConstraint()
    {
        SetupFindAsset(null);
        var pgException = new Exception("23505: duplicate key value violates unique constraint \"Asset_pk\"");
        _assetRepo.Setup(r => r.CreateAsync(It.IsAny<Asset>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("insert failed", pgException));
        var sut = CreateSut();

        ServiceResult result = await sut.CreateAsync("user@example.com", ValidRequest(), TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains("already exists", result.ErrorKey, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Verifies that <c>CreateAsync</c> sets account email and timestamps before inserting.</summary>
    [Fact]
    public async Task CreateAsync_SetsAccountEmailAndTimestamps_BeforeInserting()
    {
        SetupFindAsset(null);

        Asset? captured = null;
        _assetRepo.Setup(r => r.CreateAsync(It.IsAny<Asset>(), It.IsAny<CancellationToken>()))
            .Callback<Asset, CancellationToken>((asset, _) => captured = asset)
            .Returns(Task.CompletedTask);
        var sut = CreateSut();

        await sut.CreateAsync("user@example.com", ValidRequest(), TestContext.Current.CancellationToken);

        Assert.NotNull(captured);
        Assert.Equal("user@example.com", captured.AccountEmail.Value);
        Assert.False(captured.Deleted);
        Assert.True(captured.Created > DateTime.MinValue);
        Assert.True(captured.Updated > DateTime.MinValue);
    }

    // -- UpdateAsync ----------------------------------------------------------

    /// <summary>Verifies that <c>UpdateAsync</c> returns fail when item is invalid.</summary>
    [Fact]
    public async Task UpdateAsync_ReturnsFail_WhenItemIsInvalid()
    {
        var sut = CreateSut();
        var request = new AssetRequest { ProductName = "MyBank", Item = "InvalidItem" };

        ServiceResult result = await sut.UpdateAsync("user@example.com", request, "MyBank", TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal("Asset.ItemInvalid", result.ErrorCode);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Verifies that <c>UpdateAsync</c> returns fail when original asset not found.</summary>
    [Fact]
    public async Task UpdateAsync_ReturnsFail_WhenOriginalAssetNotFound()
    {
        SetupFindAsset(null);
        var sut = CreateSut();

        ServiceResult result = await sut.UpdateAsync("user@example.com", ValidRequest("NewName"), "OldName", TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains("find", result.ErrorKey, StringComparison.OrdinalIgnoreCase);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Verifies that <c>UpdateAsync</c> returns fail when rename returns zero rows affected.</summary>
    [Fact]
    public async Task UpdateAsync_ReturnsFail_WhenRenameReturnsZeroRowsAffected()
    {
        SetupFindAsset(MakeAsset("OldName"));
        _assetRepo.Setup(r => r.UpdateAssetWithProductNameAsync(It.IsAny<Asset>(), "OldName", It.IsAny<CancellationToken>())).ReturnsAsync(0);
        var sut = CreateSut();

        ServiceResult result = await sut.UpdateAsync("user@example.com", ValidRequest("NewName"), "OldName", TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal("Input is invalid", result.ErrorKey); // a request that matches nothing is not a server fault
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Verifies that <c>UpdateAsync</c> commits the rename when successful.</summary>
    [Fact]
    public async Task UpdateAsync_Commits_WhenSuccessful()
    {
        const string email = "user@example.com";
        SetupFindAsset(MakeAsset("OldName"));
        _assetRepo.Setup(r => r.UpdateAssetWithProductNameAsync(It.IsAny<Asset>(), "OldName", It.IsAny<CancellationToken>())).ReturnsAsync(1);
        var sut = CreateSut();

        ServiceResult result = await sut.UpdateAsync(email, ValidRequest("NewName"), "OldName", TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Regression test: renaming an asset to a product name that already exists for this account
    /// hits the same (ProductName, AccountEmail) primary key CreateAsync guards against. Verify
    /// that failure is translated to the same friendly "already exists" result instead of the
    /// generic fallback message.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_ReturnsFail_WhenRenameViolatesUniqueConstraint()
    {
        SetupFindAsset(MakeAsset("OldName"));
        var pgException = new Exception("23505: duplicate key value violates unique constraint \"Asset_pk\"");
        _assetRepo.Setup(r => r.UpdateAssetWithProductNameAsync(It.IsAny<Asset>(), "OldName", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("rename failed", pgException));
        var sut = CreateSut();

        ServiceResult result = await sut.UpdateAsync("user@example.com", ValidRequest("NewName"), "OldName", TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains("already exists", result.ErrorKey, StringComparison.OrdinalIgnoreCase);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// A rename goes through the composite-key raw-SQL update keyed on the original product name;
    /// propagation to referencing Income/Expenditure rows is the database's ON UPDATE CASCADE job,
    /// not the service's.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_Rename_UpdatesAssetKeyedOnOriginalName_AndCommits()
    {
        const string email = "user@example.com";
        SetupFindAsset(MakeAsset("OldName"));
        _assetRepo.Setup(r => r.UpdateAssetWithProductNameAsync(It.IsAny<Asset>(), "OldName", It.IsAny<CancellationToken>())).ReturnsAsync(1);
        var sut = CreateSut();

        ServiceResult result = await sut.UpdateAsync(email, ValidRequest("NewName"), "OldName", TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        _assetRepo.Verify(
            r => r.UpdateAssetWithProductNameAsync(
                It.Is<Asset>(a => a.ProductName == "NewName"), "OldName", It.IsAny<CancellationToken>()),
            Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // -- DeleteAsync ----------------------------------------------------------

    /// <summary>Verifies that <c>DeleteAsync</c> returns fail when asset not found.</summary>
    [Fact]
    public async Task DeleteAsync_ReturnsFail_WhenAssetNotFound()
    {
        SetupFindAsset(null);
        var sut = CreateSut();

        ServiceResult result = await sut.DeleteAsync("user@example.com", "MyBank", TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains("find", result.ErrorKey, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Verifies that <c>DeleteAsync</c> sets deleted flag and updates when asset found.</summary>
    [Fact]
    public async Task DeleteAsync_SetsDeletedFlagAndUpdates_WhenAssetFound()
    {
        const string email = "user@example.com";
        SetupFindAsset(MakeAsset("MyBank"));

        Asset? captured = null;
        _assetRepo.Setup(r => r.UpdateEntityAsync(It.IsAny<Asset>(), It.IsAny<CancellationToken>()))
            .Callback<Asset, CancellationToken>((asset, _) => captured = asset)
            .Returns(Task.CompletedTask);
        var sut = CreateSut();

        ServiceResult result = await sut.DeleteAsync(email, "MyBank", TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.NotNull(captured);
        Assert.True(captured.Deleted);
    }

    // -- GetAssetsAsync / GetAssetAsync ---------------------------------------

    /// <summary>Verifies that <c>GetAssetsAsync</c> delegates to repository.</summary>
    [Fact]
    public async Task GetAssetsAsync_DelegatesToRepository()
    {
        var list = new List<Asset> { MakeAsset("MyBank") };
        _assetRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(list);
        var sut = CreateSut();

        List<AssetResponse> result = await sut.GetAssetsAsync("user@example.com", TestContext.Current.CancellationToken);

        Assert.Single(result);
    }

    /// <summary>Verifies that <c>GetAssetAsync</c> delegates to repository.</summary>
    [Fact]
    public async Task GetAssetAsync_DelegatesToRepository()
    {
        var asset = MakeAsset("MyBank");
        SetupFindAsset(asset);
        var sut = CreateSut();

        AssetResponse? result = await sut.GetAssetAsync("user@example.com", "MyBank", TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal("MyBank", result.ProductName);
    }

    // -- SearchAssetsAsync ------------------------------------------------------

    /// <summary>The search is delegated to the repository and its rows are mapped to responses.</summary>
    [Fact]
    public async Task SearchAssetsAsync_DelegatesToRepository_AndMapsTheRows()
    {
        _assetRepo.Setup(r => r.SearchByAccountEmailAsync("user@example.com", "bank", It.IsAny<CancellationToken>()))
            .ReturnsAsync([MakeAsset("MyBank")]);

        List<AssetResponse> result = await CreateSut().SearchAssetsAsync("user@example.com", "bank", TestContext.Current.CancellationToken);

        Assert.Equal("MyBank", Assert.Single(result).ProductName);
    }

    // -- UpdateAsync / DeleteAsync: domain errors and unexpected failures --------------

    /// <summary>A rename the domain rejects (blank name) is returned as its validation error and never written.</summary>
    [Fact]
    public async Task UpdateAsync_ReturnsTheDomainValidationError_AndDoesNotWrite()
    {
        SetupFindAsset(MakeAsset("MyBank"));

        ServiceResult result = await CreateSut().UpdateAsync("user@example.com",
            new AssetRequest { ProductName = "   ", Item = "FreeDepositAndWithdrawal", Amount = 10m, MonetaryUnit = "KRW" }, "MyBank", TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.NotEqual("Asset.UpdateFailed", result.ErrorCode);
        _assetRepo.Verify(r => r.UpdateAssetWithProductNameAsync(It.IsAny<Asset>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>An unexpected repository failure on update is rolled back and reported without leaking internals.</summary>
    [Fact]
    public async Task UpdateAsync_RollsBackAndReportsUpdateFailed_WhenTheRepositoryThrows()
    {
        SetupFindAsset(MakeAsset("MyBank"));
        _assetRepo.Setup(r => r.UpdateAssetWithProductNameAsync(It.IsAny<Asset>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("secret detail"));

        ServiceResult result = await CreateSut().UpdateAsync("user@example.com", ValidRequest(), "MyBank", TestContext.Current.CancellationToken);

        Assert.Equal("Asset.UpdateFailed", result.ErrorCode);
        Assert.Equal(ServiceResult.TemporaryErrorKey, result.ErrorKey);
        Assert.DoesNotContain("secret detail", result.ErrorKey);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>An unexpected repository failure on delete is rolled back and reported without leaking internals.</summary>
    [Fact]
    public async Task DeleteAsync_RollsBackAndReportsDeleteFailed_WhenTheRepositoryThrows()
    {
        SetupFindAsset(MakeAsset("MyBank"));
        _assetRepo.Setup(r => r.UpdateEntityAsync(It.IsAny<Asset>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("secret detail"));

        ServiceResult result = await CreateSut().DeleteAsync("user@example.com", "MyBank", TestContext.Current.CancellationToken);

        Assert.Equal("Asset.DeleteFailed", result.ErrorCode);
        Assert.Equal(ServiceResult.TemporaryErrorKey, result.ErrorKey);
        Assert.DoesNotContain("secret detail", result.ErrorKey);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // -- Transaction boundaries, exact results, field fall-backs and failure logging -------

    /// <summary>Owner e-mail of every asset in these tests.</summary>
    private const string Email = "user@example.com";

    /// <summary>Asserts the one Error entry names <see cref="Email"/> and <paramref name="assetName"/>, carries the thrown exception and starts with <paramref name="prefix"/>.</summary>
    private void AssertLoggedFailure(string prefix, string assetName, Exception thrown)
    {
        FakeLogRecord record = Assert.Single(_logger.Collector.GetSnapshot());
        Assert.Equal(LogLevel.Error, record.Level);
        Assert.Same(thrown, record.Exception);
        Assert.StartsWith(prefix, record.Message, StringComparison.Ordinal);
        Assert.Contains(assetName, record.Message);
        Assert.Contains(Email, record.Message);
    }

    /// <summary>Asserts <paramref name="result"/> is the "asset already exists" Conflict.</summary>
    private static void AssertDuplicate(ServiceResult result)
    {
        Assert.Equal(ServiceErrorType.Conflict, result.ErrorType);
        Assert.Equal("Asset.Duplicate", result.ErrorCode);
        Assert.Equal("The asset already exists.", result.ErrorKey);
    }

    /// <summary>Asserts <paramref name="result"/> is the "asset not found" NotFound.</summary>
    private static void AssertAssetNotFound(ServiceResult result)
    {
        Assert.Equal(ServiceErrorType.NotFound, result.ErrorType);
        Assert.Equal("Asset.NotFound", result.ErrorCode);
        Assert.Equal("Fail to find the asset by given product name", result.ErrorKey);
    }

    /// <summary>An unknown product name resolves to null.</summary>
    [Fact]
    public async Task GetAssetAsync_ReturnsNull_WhenTheAssetDoesNotExist()
    {
        SetupFindAsset(null);

        Assert.Null(await CreateSut().GetAssetAsync(Email, "Ghost", TestContext.Current.CancellationToken));
    }

    /// <summary>An unknown asset category is a Validation error with its localizable message.</summary>
    [Fact]
    public async Task CreateAsync_ReturnsItemInvalid_WithItsMessage_WhenTheItemIsUnknown()
    {
        AssetRequest request = ValidRequest();
        request.Item = "Unknown";

        ServiceResult result = await CreateSut().CreateAsync(Email, request, TestContext.Current.CancellationToken);

        Assert.Equal(ServiceErrorType.Validation, result.ErrorType);
        Assert.Equal("Asset.ItemInvalid", result.ErrorCode);
        Assert.Equal("Asset category (item) is not a recognised value.", result.ErrorKey);
    }

    /// <summary>The duplicate check looks up the requested product name; an existing one is the "already exists" Conflict.</summary>
    [Fact]
    public async Task CreateAsync_ReturnsDuplicate_WhenTheRequestedNameIsAlreadyTaken()
    {
        _assetRepo.Setup(r => r.FindByEmailAndProductNameAsync(Email, "MyBank", It.IsAny<CancellationToken>())).ReturnsAsync(MakeAsset("MyBank"));

        ServiceResult result = await CreateSut().CreateAsync(Email, ValidRequest("MyBank"), TestContext.Current.CancellationToken);

        AssertDuplicate(result);
        _assetRepo.Verify(r => r.CreateAsync(It.IsAny<Asset>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>A concurrent insert that hits the primary key is the same "already exists" Conflict.</summary>
    [Fact]
    public async Task CreateAsync_ReturnsDuplicate_WhenAConcurrentInsertHitsThePrimaryKey()
    {
        SetupFindAsset(null);
        _assetRepo.Setup(r => r.CreateAsync(It.IsAny<Asset>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("insert failed", new Exception("23505: duplicate key value violates unique constraint \"Asset_pk\"")));

        AssertDuplicate(await CreateSut().CreateAsync(Email, ValidRequest(), TestContext.Current.CancellationToken));
        Assert.Empty(_logger.Collector.GetSnapshot());
    }

    /// <summary>A create that throws anything else logs the failure with the exception, the asset and the account.</summary>
    [Fact]
    public async Task CreateAsync_LogsTheFailureWithTheException_WhenTheRepositoryThrows()
    {
        SetupFindAsset(null);
        var thrown = new InvalidOperationException("boom");
        _assetRepo.Setup(r => r.CreateAsync(It.IsAny<Asset>(), It.IsAny<CancellationToken>())).ThrowsAsync(thrown);

        await CreateSut().CreateAsync(Email, ValidRequest("MyBank"), TestContext.Current.CancellationToken);

        AssertLoggedFailure("Failed to create asset", "MyBank", thrown);
    }

    /// <summary>A successful update opens the transaction and commits it.</summary>
    [Fact]
    public async Task UpdateAsync_BeginsAndCommitsTheTransaction_OnSuccess()
    {
        SetupFindAsset(MakeAsset("MyBank"));
        _assetRepo.Setup(r => r.UpdateAssetWithProductNameAsync(It.IsAny<Asset>(), "MyBank", It.IsAny<CancellationToken>())).ReturnsAsync(1);

        ServiceResult result = await CreateSut().UpdateAsync(Email, ValidRequest("MyBank"), "MyBank", TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        _unitOfWork.Verify(u => u.BeginAsync(null, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>An unknown asset category on update is a Validation error with its localizable message.</summary>
    [Fact]
    public async Task UpdateAsync_ReturnsItemInvalid_WithItsMessage_WhenTheItemIsUnknown()
    {
        AssetRequest request = ValidRequest();
        request.Item = "Unknown";

        ServiceResult result = await CreateSut().UpdateAsync(Email, request, "MyBank", TestContext.Current.CancellationToken);

        Assert.Equal("Asset.ItemInvalid", result.ErrorCode);
        Assert.Equal("Asset category (item) is not a recognised value.", result.ErrorKey);
    }

    /// <summary>An unknown original product name on update is the "asset not found" NotFound.</summary>
    [Fact]
    public async Task UpdateAsync_ReturnsAssetNotFound_WithItsMessage_WhenTheAssetIsUnknown()
    {
        SetupFindAsset(null);

        AssertAssetNotFound(await CreateSut().UpdateAsync(Email, ValidRequest(), "Ghost", TestContext.Current.CancellationToken));
    }

    /// <summary>A rename that hits an existing product name is the "already exists" Conflict.</summary>
    [Fact]
    public async Task UpdateAsync_ReturnsDuplicate_WhenTheRenameHitsAnExistingName()
    {
        SetupFindAsset(MakeAsset("OldName"));
        _assetRepo.Setup(r => r.UpdateAssetWithProductNameAsync(It.IsAny<Asset>(), "OldName", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("rename failed", new Exception("23505: duplicate key value violates unique constraint \"Asset_pk\"")));

        AssertDuplicate(await CreateSut().UpdateAsync(Email, ValidRequest("NewName"), "OldName", TestContext.Current.CancellationToken));
    }

    /// <summary>An update that matches no row is the "Input is invalid" failure.</summary>
    [Fact]
    public async Task UpdateAsync_ReturnsUpdateFailed_WithInputIsInvalid_WhenNoRowIsUpdated()
    {
        SetupFindAsset(MakeAsset("MyBank"));
        _assetRepo.Setup(r => r.UpdateAssetWithProductNameAsync(It.IsAny<Asset>(), "MyBank", It.IsAny<CancellationToken>())).ReturnsAsync(0);

        ServiceResult result = await CreateSut().UpdateAsync(Email, ValidRequest("MyBank"), "MyBank", TestContext.Current.CancellationToken);

        Assert.Equal(ServiceErrorType.Failure, result.ErrorType);
        Assert.Equal("Asset.UpdateFailed", result.ErrorCode);
        Assert.Equal("Input is invalid", result.ErrorKey);
    }

    /// <summary>The requested currency label and category replace the stored ones (a currency label can be relabelled at any time).</summary>
    [Fact]
    public async Task UpdateAsync_StoresTheRequestedCurrencyAndItem()
    {
        SetupFindAsset(MakeAsset("MyBank", currency: "원"));
        Asset? written = null;
        _assetRepo.Setup(r => r.UpdateAssetWithProductNameAsync(It.IsAny<Asset>(), "MyBank", It.IsAny<CancellationToken>()))
            .Callback<Asset, string, CancellationToken>((a, _, _) => written = a).ReturnsAsync(1);
        var request = new AssetRequest { ProductName = "MyBank", Item = "SavingsAsset", Amount = 10m, MonetaryUnit = "KRW" };

        ServiceResult result = await CreateSut().UpdateAsync(Email, request, "MyBank", TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal("KRW", written?.Balance.Currency.Value);
        Assert.Equal("SavingsAsset", written?.Item);
    }

    /// <summary>A request without a currency keeps the stored currency label.</summary>
    [Fact]
    public async Task UpdateAsync_KeepsTheStoredCurrency_WhenTheRequestHasNone()
    {
        SetupFindAsset(MakeAsset("MyBank", currency: "USD"));
        Asset? written = null;
        _assetRepo.Setup(r => r.UpdateAssetWithProductNameAsync(It.IsAny<Asset>(), "MyBank", It.IsAny<CancellationToken>()))
            .Callback<Asset, string, CancellationToken>((a, _, _) => written = a).ReturnsAsync(1);
        var request = new AssetRequest { ProductName = "MyBank", Item = "FreeDepositAndWithdrawal", Amount = 10m, MonetaryUnit = null };

        ServiceResult result = await CreateSut().UpdateAsync(Email, request, "MyBank", TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal("USD", written?.Balance.Currency.Value);
    }

    /// <summary>An update that throws anything else logs the failure with the exception, the asset and the account.</summary>
    [Fact]
    public async Task UpdateAsync_LogsTheFailureWithTheException_WhenTheRepositoryThrows()
    {
        SetupFindAsset(MakeAsset("MyBank"));
        var thrown = new InvalidOperationException("boom");
        _assetRepo.Setup(r => r.UpdateAssetWithProductNameAsync(It.IsAny<Asset>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(thrown);

        await CreateSut().UpdateAsync(Email, ValidRequest("MyBank"), "MyBank", TestContext.Current.CancellationToken);

        AssertLoggedFailure("Failed to update asset", "MyBank", thrown);
    }

    /// <summary>A successful soft-delete opens the transaction and commits it.</summary>
    [Fact]
    public async Task DeleteAsync_BeginsAndCommitsTheTransaction_OnSuccess()
    {
        SetupFindAsset(MakeAsset("MyBank"));

        ServiceResult result = await CreateSut().DeleteAsync(Email, "MyBank", TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        _unitOfWork.Verify(u => u.BeginAsync(null, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>An unknown asset on delete is the "asset not found" NotFound, and the transaction is rolled back.</summary>
    [Fact]
    public async Task DeleteAsync_ReturnsAssetNotFound_AndRollsBack_WhenTheAssetIsUnknown()
    {
        SetupFindAsset(null);

        AssertAssetNotFound(await CreateSut().DeleteAsync(Email, "Ghost", TestContext.Current.CancellationToken));
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>A delete that throws logs the failure with the exception, the asset and the account.</summary>
    [Fact]
    public async Task DeleteAsync_LogsTheFailureWithTheException_WhenTheRepositoryThrows()
    {
        SetupFindAsset(MakeAsset("MyBank"));
        var thrown = new InvalidOperationException("boom");
        _assetRepo.Setup(r => r.UpdateEntityAsync(It.IsAny<Asset>(), It.IsAny<CancellationToken>())).ThrowsAsync(thrown);

        await CreateSut().DeleteAsync(Email, "MyBank", TestContext.Current.CancellationToken);

        AssertLoggedFailure("Failed to delete asset", "MyBank", thrown);
    }
}
