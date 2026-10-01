using Maroik.Core.Contract.Dtos;
using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Contract.Misc.Enums;
using Maroik.Core.Contract.Misc.Settings;
using Maroik.Core.Domain.Account;
using Maroik.Core.Domain.Finance;
using Maroik.Core.Service.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace Maroik.Core.Service.Tests.Services;

/// <summary>
/// Unit tests for <see cref="ProfileService"/>.
/// All repository and dependency-service mocks are provided via Moq.
/// Covers profile retrieval, nickname update, avatar update,
/// and password-change validation (current-password verification before update).
/// </summary>
public class ProfileServiceTests
{
    /// <summary>Mock <c>IAccountRepository</c> injected into the system under test.</summary>
    private readonly Mock<IAccountRepository> _accountRepo = new();
    /// <summary>Mock <c>IAssetRepository</c> injected into the system under test.</summary>
    private readonly Mock<IAssetRepository> _assetRepo = new();
    /// <summary>Mock <c>IPasswordService</c> injected into the system under test.</summary>
    private readonly Mock<IPasswordService> _passwordService = new();
    /// <summary>Mock <c>IFileClient</c> injected into the system under test.</summary>
    private readonly Mock<IFileClient> _fileClient = new();
    /// <summary>Mock <c>IImageValidatorService</c> injected into the system under test.</summary>
    private readonly Mock<IImageValidatorService> _imageValidator = new();
    /// <summary>Mock <c>IUnitOfWork</c> injected into the system under test.</summary>
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    /// <summary>Settings with a local file-storage URL.</summary>
    private readonly IOptions<ServerSetting> _settings =
        Options.Create(new ServerSetting { FileStorageBaseUrl = "http://localhost:5001" });

    /// <summary>The fixed "current time" of these tests.</summary>
    private static readonly DateTime Now = new(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);
    /// <summary>The clock the service under test reads, stopped at <see cref="Now"/>.</summary>
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(Now));

    /// <summary>The service under test over the mocked dependencies.</summary>
    private ProfileService CreateSut() => new(
        _accountRepo.Object,
        _assetRepo.Object,
        _passwordService.Object,
        _fileClient.Object,
        _imageValidator.Object,
        _settings,
        _unitOfWork.Object,
        NullLogger<ProfileService>.Instance,
        _time);

    // -- Helpers --------------------------------------------------------------

    /// <summary>A persisted asset named <paramref name="name"/> in <paramref name="unit"/>.</summary>
    private static Asset MakeAsset(string name, string unit = "KRW", bool deleted = false) =>
        Asset.Reconstitute(
            productName: name,
            accountEmail: "user@example.com",
            item: "Deposit",
            amount: 1000m,
            monetaryUnit: unit,
            note: null,
            deleted: deleted,
            created: DateTime.UtcNow,
            updated: DateTime.UtcNow);

    /// <summary>A confirmed, unlocked account for <paramref name="email"/>.</summary>
    private static Account ActiveAccount(string email = "user@example.com") =>
        Account.Reconstitute(
            email: email,
            hashedPassword: "$2a$13$placeholder",
            nickname: "TestUser",
            avatarImagePath: "/upload/Management/Profile/default-avatar.jpg",
            role: "User",
            timeZoneIanaId: "UTC",
            defaultMonetaryUnit: null,
            locked: false,
            loginAttempt: 0,
            emailConfirmed: true,
            agreedServiceTerms: true,
            registrationToken: null,
            resetPasswordToken: null,
            created: DateTime.UtcNow,
            updated: DateTime.UtcNow,
            message: null,
            deleted: false,
            securityStamp: "stamp",
            mustChangePassword: false);

    // -- GetProfileAsync ------------------------------------------------------

    /// <summary>Verifies that <c>GetProfileAsync</c> delegates to repository.</summary>
    [Fact]
    public async Task GetProfileAsync_DelegatesToRepository()
    {
        var account = ActiveAccount();
        _accountRepo.Setup(r => r.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(account);
        var sut = CreateSut();

        AccountResponse? result = await sut.GetProfileAsync(account.Email.Value, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(account.Email.Value, result.Email);
    }

    // -- UpdateAvatarAsync ----------------------------------------------------

    /// <summary>Verifies that <c>UpdateAvatarAsync</c> returns fail when account not found.</summary>
    [Fact]
    public async Task UpdateAvatarAsync_ReturnsFail_WhenAccountNotFound()
    {
        _accountRepo.Setup(r => r.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Account?)null);
        var sut = CreateSut();

        ServiceResult result = await sut.UpdateAvatarAsync("ghost@example.com", "/new/path.jpg", TestContext.Current.CancellationToken);

        Assert.False(result.Success);
    }

    /// <summary>Verifies that <c>UpdateAvatarAsync</c> updates avatar path when account found.</summary>
    [Fact]
    public async Task UpdateAvatarAsync_UpdatesAvatarPath_WhenAccountFound()
    {
        var account = ActiveAccount();
        _accountRepo.Setup(r => r.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(account);
        _accountRepo.Setup(r => r.UpdateAvatarPathAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(1);
        var sut = CreateSut();

        ServiceResult result = await sut.UpdateAvatarAsync(account.Email.Value, "/new/avatar.jpg", TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        _accountRepo.Verify(r => r.UpdateAvatarPathAsync(
            account.Email.Value, "/new/avatar.jpg", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // -- UploadAndUpdateAvatarAsync -----------------------------------------------

    /// <summary>Rejects a payload the image validator does not recognize, without touching storage.</summary>
    [Fact]
    public async Task UploadAndUpdateAvatarAsync_ReturnsInvalidImage_WhenNotAnImage()
    {
        _imageValidator.Setup(v => v.IsValidImage(It.IsAny<byte[]>())).Returns(false);
        var sut = CreateSut();

        ServiceResult result = await sut.UploadAndUpdateAvatarAsync("user@example.com", [1, 2, 3], ".png", TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal("invalid-image", result.ErrorKey);
        _fileClient.Verify(c => c.UploadWithResultAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Rejects an SVG even though it is a "valid image", without touching storage.</summary>
    [Fact]
    public async Task UploadAndUpdateAvatarAsync_ReturnsSvgNotAllowed_WhenSvg()
    {
        _imageValidator.Setup(v => v.IsValidImage(It.IsAny<byte[]>())).Returns(true);
        _imageValidator.Setup(v => v.IsSvg(It.IsAny<byte[]>())).Returns(true);
        var sut = CreateSut();

        ServiceResult result = await sut.UploadAndUpdateAvatarAsync("user@example.com", [1, 2, 3], ".png", TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal("svg-not-allowed", result.ErrorKey);
        _fileClient.Verify(c => c.UploadWithResultAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>A valid image is stored under the avatar directory with the right MIME type, and the account path is updated.</summary>
    [Fact]
    public async Task UploadAndUpdateAvatarAsync_StoresImageAndUpdatesAccountPath_WhenValid()
    {
        var account = ActiveAccount();
        _imageValidator.Setup(v => v.IsValidImage(It.IsAny<byte[]>())).Returns(true);
        _imageValidator.Setup(v => v.IsSvg(It.IsAny<byte[]>())).Returns(false);
        _fileClient.Setup(c => c.UploadWithResultAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(FileUploadResult.Stored);
        _accountRepo.Setup(r => r.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(account);
        _accountRepo.Setup(r => r.UpdateAvatarPathAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(1);
        var sut = CreateSut();

        ServiceResult result = await sut.UploadAndUpdateAvatarAsync(account.Email.Value, [0xFF, 0xD8, 0xFF], ".JPG", TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        _fileClient.Verify(c => c.UploadWithResultAsync(
            It.IsAny<byte[]>(), "image/jpeg",
            It.Is<string>(p => p.StartsWith("upload/Management/Profile/Avatar/") && p.EndsWith(".jpg")),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        _accountRepo.Verify(r => r.UpdateAvatarPathAsync(
            account.Email.Value, It.Is<string>(p => p.StartsWith("/upload/Management/Profile/Avatar/")),
            It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>A storage upload failure returns a failure result and never updates the account.</summary>
    [Fact]
    public async Task UploadAndUpdateAvatarAsync_ReturnsFailure_WhenStorageUploadFails()
    {
        _imageValidator.Setup(v => v.IsValidImage(It.IsAny<byte[]>())).Returns(true);
        _imageValidator.Setup(v => v.IsSvg(It.IsAny<byte[]>())).Returns(false);
        _fileClient.Setup(c => c.UploadWithResultAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(FileUploadResult.Failed);
        var sut = CreateSut();

        ServiceResult result = await sut.UploadAndUpdateAvatarAsync("user@example.com", [0xFF, 0xD8, 0xFF], ".png", TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal("Profile.UploadAvatarFailed", result.ErrorCode);
        _accountRepo.Verify(r => r.UpdateAvatarPathAsync(
            It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
        _accountRepo.Verify(r => r.UpdateEntityAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Regression: a virus detection is reported as such (not as a generic "invalid input") and never updates the account.</summary>
    [Fact]
    public async Task UploadAndUpdateAvatarAsync_ReturnsVirusDetected_WhenStorageFlagsTheFile()
    {
        _imageValidator.Setup(v => v.IsValidImage(It.IsAny<byte[]>())).Returns(true);
        _imageValidator.Setup(v => v.IsSvg(It.IsAny<byte[]>())).Returns(false);
        _fileClient.Setup(c => c.UploadWithResultAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(FileUploadResult.Infected);
        var sut = CreateSut();

        ServiceResult result = await sut.UploadAndUpdateAvatarAsync("user@example.com", [0xFF, 0xD8, 0xFF], ".png", TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal("virus-detected", result.ErrorKey);
        _accountRepo.Verify(r => r.UpdateAvatarPathAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>A scanner outage is reported separately from a detection and never updates the account.</summary>
    [Fact]
    public async Task UploadAndUpdateAvatarAsync_ReturnsScanUnavailable_WhenTheScannerCouldNotRun()
    {
        _imageValidator.Setup(v => v.IsValidImage(It.IsAny<byte[]>())).Returns(true);
        _imageValidator.Setup(v => v.IsSvg(It.IsAny<byte[]>())).Returns(false);
        _fileClient.Setup(c => c.UploadWithResultAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(FileUploadResult.ScanUnavailable);
        var sut = CreateSut();

        ServiceResult result = await sut.UploadAndUpdateAvatarAsync("user@example.com", [0xFF, 0xD8, 0xFF], ".png", TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal("scan-unavailable", result.ErrorKey);
        _accountRepo.Verify(r => r.UpdateAvatarPathAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // -- DownloadAvatarAsync ----------------------------------------------------

    /// <summary>Returns the bytes file storage hands back for a known avatar.</summary>
    [Fact]
    public async Task DownloadAvatarAsync_ReturnsBytes_WhenStorageHasFile()
    {
        byte[] payload = [1, 2, 3, 4];
        _fileClient.Setup(c => c.DownloadAsync("upload/Management/Profile/Avatar/abc.png", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(payload);
        var sut = CreateSut();

        byte[]? result = await sut.DownloadAvatarAsync("abc.png", TestContext.Current.CancellationToken);

        Assert.Equal(payload, result);
    }

    /// <summary>Returns null (not an exception) when file storage is unreachable / errors.</summary>
    [Fact]
    public async Task DownloadAvatarAsync_ReturnsNull_WhenStorageThrows()
    {
        _fileClient.Setup(c => c.DownloadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("connection refused"));
        var sut = CreateSut();

        byte[]? result = await sut.DownloadAvatarAsync("abc.png", TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    /// <summary>Treats an empty response as "not found".</summary>
    [Fact]
    public async Task DownloadAvatarAsync_ReturnsNull_WhenStorageReturnsEmpty()
    {
        _fileClient.Setup(c => c.DownloadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var sut = CreateSut();

        byte[]? result = await sut.DownloadAvatarAsync("abc.png", TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    // -- UpdateTimeZoneAsync --------------------------------------------------

    /// <summary>Verifies that <c>UpdateTimeZoneAsync</c> returns fail when account not found.</summary>
    [Fact]
    public async Task UpdateTimeZoneAsync_ReturnsFail_WhenAccountNotFound()
    {
        _accountRepo.Setup(r => r.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Account?)null);
        var sut = CreateSut();

        ServiceResult result = await sut.UpdateTimeZoneAsync("ghost@example.com", "Asia/Seoul", TestContext.Current.CancellationToken);

        Assert.False(result.Success);
    }

    /// <summary>Verifies that <c>UpdateTimeZoneAsync</c> updates time zone when account found.</summary>
    [Fact]
    public async Task UpdateTimeZoneAsync_UpdatesTimeZone_WhenAccountFound()
    {
        var account = ActiveAccount();
        _accountRepo.Setup(r => r.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(account);
        _accountRepo.Setup(r => r.UpdateTimeZoneAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(1);
        var sut = CreateSut();

        ServiceResult result = await sut.UpdateTimeZoneAsync(account.Email.Value, "Asia/Seoul", TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        _accountRepo.Verify(r => r.UpdateTimeZoneAsync(
            account.Email.Value, "Asia/Seoul", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // -- UpdatePasswordAsync --------------------------------------------------

    /// <summary>Verifies that <c>UpdatePasswordAsync</c> returns fail when account not found.</summary>
    [Fact]
    public async Task UpdatePasswordAsync_ReturnsFail_WhenAccountNotFound()
    {
        _accountRepo.Setup(r => r.FindByEmailForUpdateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Account?)null);
        var sut = CreateSut();

        ServiceResult result = await sut.UpdatePasswordAsync("ghost@example.com", "old", "new", TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Verifies that <c>UpdatePasswordAsync</c> returns fail when current password is wrong.</summary>
    [Fact]
    public async Task UpdatePasswordAsync_ReturnsFail_WhenCurrentPasswordIsWrong()
    {
        var account = ActiveAccount();
        _accountRepo.Setup(r => r.FindByEmailForUpdateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(account);
        _passwordService.Setup(p => p.VerifyPassword("wrongpass", account.HashedPassword)).Returns(false);
        var sut = CreateSut();

        ServiceResult result = await sut.UpdatePasswordAsync(account.Email.Value, "wrongpass", "newpass", TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains("Invalid password", result.ErrorKey, StringComparison.OrdinalIgnoreCase);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Verifies that <c>UpdatePasswordAsync</c> hashes and updates when current password is correct.</summary>
    [Fact]
    public async Task UpdatePasswordAsync_HashesAndUpdates_WhenCurrentPasswordIsCorrect()
    {
        var account = ActiveAccount();
        _accountRepo.Setup(r => r.FindByEmailForUpdateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(account);
        _passwordService.Setup(p => p.VerifyPassword("oldpass", account.HashedPassword)).Returns(true);
        _passwordService.Setup(p => p.HashPassword("NewPass1!")).Returns("$2a$13$newhash");
        _accountRepo.Setup(r => r.UpdatePasswordAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(1);
        var sut = CreateSut();

        ServiceResult result = await sut.UpdatePasswordAsync(account.Email.Value, "oldpass", "NewPass1!", TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        _accountRepo.Verify(r => r.UpdatePasswordAsync(
            account.Email.Value, "$2a$13$newhash", It.IsAny<string>(), false, null, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.BeginAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// A self-service password change must also persist the discarded reset token: this path writes
    /// column-scoped, so without passing the token column a reset link mailed earlier would stay
    /// valid in the database even though the domain object had cleared it.
    /// </summary>
    [Fact]
    public async Task UpdatePasswordAsync_PersistsClearedResetPasswordToken()
    {
        var account = ActiveAccount();
        Assert.False(account.RequestPasswordReset(GuidToken.Generate(Now), Now).IsError);
        _accountRepo.Setup(r => r.FindByEmailForUpdateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(account);
        _passwordService.Setup(p => p.VerifyPassword("oldpass", account.HashedPassword)).Returns(true);
        _passwordService.Setup(p => p.HashPassword("NewPass1!")).Returns("$2a$13$newhash");
        _accountRepo.Setup(r => r.UpdatePasswordAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(1);
        var sut = CreateSut();

        ServiceResult result = await sut.UpdatePasswordAsync(account.Email.Value, "oldpass", "NewPass1!", TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        _accountRepo.Verify(r => r.UpdatePasswordAsync(
            account.Email.Value, "$2a$13$newhash", It.IsAny<string>(), false, null, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Regression test for the read-verify-write race this method used to have: the account lookup
    /// must be the locked (FOR UPDATE) variant, not the plain unlocked lookup, so a concurrent
    /// password reset on the same row cannot be silently overwritten by a stale verification.
    /// </summary>
    [Fact]
    public async Task UpdatePasswordAsync_UsesLockedLookup_NotUnlockedFindByEmail()
    {
        var account = ActiveAccount();
        _accountRepo.Setup(r => r.FindByEmailForUpdateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(account);
        _passwordService.Setup(p => p.VerifyPassword("oldpass", account.HashedPassword)).Returns(true);
        _passwordService.Setup(p => p.HashPassword("NewPass1!")).Returns("$2a$13$newhash");
        _accountRepo.Setup(r => r.UpdatePasswordAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(1);
        var sut = CreateSut();

        await sut.UpdatePasswordAsync(account.Email.Value, "oldpass", "NewPass1!", TestContext.Current.CancellationToken);

        _accountRepo.Verify(r => r.FindByEmailForUpdateAsync(account.Email.Value, It.IsAny<CancellationToken>()), Times.Once);
        _accountRepo.Verify(r => r.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // -- UpdateDefaultMonetaryUnitAsync --------------------------------------

    /// <summary>A non-existent account resolves to a no-op column update (0 rows) and does not throw.</summary>
    [Fact]
    public async Task UpdateDefaultMonetaryUnitAsync_IssuesColumnUpdate_EvenWhenAccountMissing()
    {
        _assetRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _accountRepo.Setup(r => r.UpdateDefaultMonetaryUnitAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync(0);
        var sut = CreateSut();

        await sut.UpdateDefaultMonetaryUnitAsync("missing@example.com", "KRW", TestContext.Current.CancellationToken);

        _accountRepo.Verify(r => r.UpdateEntityAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()), Times.Never);
        _accountRepo.Verify(r => r.UpdateDefaultMonetaryUnitAsync("missing@example.com", null, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Verifies that <c>UpdateDefaultMonetaryUnitAsync</c> persists unit when it exists in assets.</summary>
    [Fact]
    public async Task UpdateDefaultMonetaryUnitAsync_SetsUnit_WhenUnitExistsInAssets()
    {
        var account = ActiveAccount();
        _assetRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([MakeAsset("Wallet")]);
        _accountRepo.Setup(r => r.UpdateDefaultMonetaryUnitAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync(1);
        var sut = CreateSut();

        await sut.UpdateDefaultMonetaryUnitAsync(account.Email.Value, "KRW", TestContext.Current.CancellationToken);

        _accountRepo.Verify(r => r.UpdateDefaultMonetaryUnitAsync(account.Email.Value, "KRW", It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Verifies that <c>UpdateDefaultMonetaryUnitAsync</c> clears unit when it is not in assets.</summary>
    [Fact]
    public async Task UpdateDefaultMonetaryUnitAsync_SetsNull_WhenUnitNotInAssets()
    {
        var account = ActiveAccount();
        _assetRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([MakeAsset("Wallet", "USD")]);
        _accountRepo.Setup(r => r.UpdateDefaultMonetaryUnitAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync(1);
        var sut = CreateSut();

        await sut.UpdateDefaultMonetaryUnitAsync(account.Email.Value, "KRW", TestContext.Current.CancellationToken);

        _accountRepo.Verify(r => r.UpdateDefaultMonetaryUnitAsync(account.Email.Value, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Verifies that <c>UpdateDefaultMonetaryUnitAsync</c> ignores a soft-deleted asset's currency.</summary>
    [Fact]
    public async Task UpdateDefaultMonetaryUnitAsync_SetsNull_WhenUnitOnlyExistsOnDeletedAsset()
    {
        var account = ActiveAccount();
        _assetRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([MakeAsset("OldForeignAccount", "USD", deleted: true)]);
        _accountRepo.Setup(r => r.UpdateDefaultMonetaryUnitAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync(1);
        var sut = CreateSut();

        await sut.UpdateDefaultMonetaryUnitAsync(account.Email.Value, "USD", TestContext.Current.CancellationToken);

        _accountRepo.Verify(r => r.UpdateDefaultMonetaryUnitAsync(account.Email.Value, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    // -- Failure branches ---------------------------------------------------------

    /// <summary>A repository failure while saving the avatar path is caught and reported generically.</summary>
    [Fact]
    public async Task UpdateAvatarAsync_ReportsUpdateAvatarFailed_WhenTheRepositoryThrows()
    {
        _accountRepo.Setup(r => r.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("secret detail"));

        ServiceResult result = await CreateSut().UpdateAvatarAsync("user@example.com", "/a.png", TestContext.Current.CancellationToken);

        Assert.Equal("Profile.UpdateAvatarFailed", result.ErrorCode);
        Assert.DoesNotContain("secret detail", result.ErrorKey);
    }

    /// <summary>An unknown IANA time zone is refused with the domain's validation error and nothing is written.</summary>
    [Fact]
    public async Task UpdateTimeZoneAsync_ReturnsTheDomainValidationError_ForAnUnknownTimeZone()
    {
        _accountRepo.Setup(r => r.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(ActiveAccount());

        ServiceResult result = await CreateSut().UpdateTimeZoneAsync("user@example.com", "Mars/Olympus_Mons", TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.NotEqual("Profile.UpdateTimezoneFailed", result.ErrorCode);
        _accountRepo.Verify(r => r.UpdateTimeZoneAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>A repository failure while saving the time zone is caught and reported generically.</summary>
    [Fact]
    public async Task UpdateTimeZoneAsync_ReportsUpdateTimezoneFailed_WhenTheRepositoryThrows()
    {
        _accountRepo.Setup(r => r.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("secret detail"));

        ServiceResult result = await CreateSut().UpdateTimeZoneAsync("user@example.com", "UTC", TestContext.Current.CancellationToken);

        Assert.Equal("Profile.UpdateTimezoneFailed", result.ErrorCode);
        Assert.DoesNotContain("secret detail", result.ErrorKey);
    }

    /// <summary>A new password that breaks the complexity rule is refused before hashing and before any write; the lock is released.</summary>
    [Fact]
    public async Task UpdatePasswordAsync_ReturnsPolicyViolation_AndDoesNotHashOrWrite_WhenTheNewPasswordIsTooWeak()
    {
        _accountRepo.Setup(r => r.FindByEmailForUpdateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(ActiveAccount());
        _passwordService.Setup(p => p.VerifyPassword(It.IsAny<string>(), It.IsAny<string>())).Returns(true);

        ServiceResult result = await CreateSut().UpdatePasswordAsync("user@example.com", "current", "weak", TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal("Account.PasswordPolicy", result.ErrorCode);
        _passwordService.Verify(p => p.HashPassword(It.IsAny<string>()), Times.Never);
        _accountRepo.Verify(r => r.UpdatePasswordAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>A failure while persisting the new password is rolled back and reported generically.</summary>
    [Fact]
    public async Task UpdatePasswordAsync_RollsBackAndReportsUpdatePasswordFailed_WhenTheRepositoryThrows()
    {
        _accountRepo.Setup(r => r.FindByEmailForUpdateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(ActiveAccount());
        _passwordService.Setup(p => p.VerifyPassword(It.IsAny<string>(), It.IsAny<string>())).Returns(true);
        _passwordService.Setup(p => p.HashPassword(It.IsAny<string>())).Returns("$2a$13$new");
        _accountRepo.Setup(r => r.UpdatePasswordAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("secret detail"));

        ServiceResult result = await CreateSut().UpdatePasswordAsync("user@example.com", "current", "NewPass1!", TestContext.Current.CancellationToken);

        Assert.Equal("Profile.UpdatePasswordFailed", result.ErrorCode);
        Assert.DoesNotContain("secret detail", result.ErrorKey);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>The extension allow-list is re-asserted in the service: a disallowed extension never reaches the validator or storage.</summary>
    [Theory]
    [InlineData(".gif")]
    [InlineData(".svg")]
    [InlineData("")]
    [InlineData(".png.exe")]
    public async Task UploadAndUpdateAvatarAsync_RefusesADisallowedExtension_BeforeDecodingOrStoring(string extension)
    {
        ServiceResult result = await CreateSut().UploadAndUpdateAvatarAsync("user@example.com", [1, 2, 3], extension, TestContext.Current.CancellationToken);

        Assert.Equal("invalid-image", result.ErrorKey);
        _imageValidator.Verify(v => v.IsValidImage(It.IsAny<byte[]>()), Times.Never);
        _fileClient.Verify(c => c.UploadWithResultAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>The stored MIME type follows the (case-insensitive) extension.</summary>
    [Theory]
    [InlineData(".jpg", "image/jpeg")]
    [InlineData(".JPEG", "image/jpeg")]
    [InlineData(".png", "image/png")]
    [InlineData(".PNG", "image/png")]
    public async Task UploadAndUpdateAvatarAsync_UploadsWithTheMimeTypeOfTheExtension(string extension, string expectedContentType)
    {
        _imageValidator.Setup(v => v.IsValidImage(It.IsAny<byte[]>())).Returns(true);
        _fileClient.Setup(c => c.UploadWithResultAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(FileUploadResult.Stored);
        _accountRepo.Setup(r => r.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(ActiveAccount());

        await CreateSut().UploadAndUpdateAvatarAsync("user@example.com", [1, 2, 3], extension, TestContext.Current.CancellationToken);

        _fileClient.Verify(c => c.UploadWithResultAsync(It.IsAny<byte[]>(), expectedContentType, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>An unexpected failure while validating/storing the avatar is caught and reported generically.</summary>
    [Fact]
    public async Task UploadAndUpdateAvatarAsync_ReportsUploadAvatarFailed_WhenTheValidatorThrows()
    {
        _imageValidator.Setup(v => v.IsSvg(It.IsAny<byte[]>())).Throws(new InvalidOperationException("secret detail"));

        ServiceResult result = await CreateSut().UploadAndUpdateAvatarAsync("user@example.com", [1, 2, 3], ".png", TestContext.Current.CancellationToken);

        Assert.Equal("Profile.UploadAvatarFailed", result.ErrorCode);
        Assert.DoesNotContain("secret detail", result.ErrorKey);
    }
}
