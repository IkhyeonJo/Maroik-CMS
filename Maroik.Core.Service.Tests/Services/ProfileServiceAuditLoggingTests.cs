using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Contract.Misc.Enums;
using Maroik.Core.Contract.Misc.Settings;
using Maroik.Core.Domain.Account;
using Maroik.Core.Service.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace Maroik.Core.Service.Tests.Services;

/// <summary>
/// The security trail of <see cref="ProfileService"/>: a password change (done, or refused because the
/// current password was wrong) and every avatar upload that is refused — malware, SVG, corrupt image,
/// scanner outage — is logged with the account, and never with a password or the file content.
/// </summary>
public class ProfileServiceAuditLoggingTests
{
    /// <summary>E-mail of the account every test acts on; audit entries must name it.</summary>
    private const string Email = "user@example.com";

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
    /// <summary>Captures the log entries the system under test writes.</summary>
    private readonly FakeLogger<ProfileService> _logger = new();

    /// <summary>The fixed "current time" of these tests.</summary>
    private static readonly DateTime Now = new(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);
    /// <summary>The clock the service under test reads, stopped at <see cref="Now"/>.</summary>
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(Now));

    /// <summary>The service under test over the mocked dependencies and the capturing logger.</summary>
    private ProfileService CreateSut() => new(_accountRepo.Object, _assetRepo.Object, _passwordService.Object,
        _fileClient.Object, _imageValidator.Object,
        Options.Create(new ServerSetting { FileStorageBaseUrl = "http://localhost:5001" }), _unitOfWork.Object, _logger, _time);

    /// <summary>The persisted account for <see cref="Email"/>.</summary>
    private static Account Existing() =>
        Account.Reconstitute(Email, "$2a$13$placeholder", "TestUser", null, "User", "UTC", null, false, 0, true, true,
            null, null, DateTime.UtcNow, DateTime.UtcNow, null, false, "stamp", false);

    /// <summary>Asserts exactly one entry at <paramref name="level"/> contains <paramref name="containing"/> and names <see cref="Email"/>, and returns it.</summary>
    private FakeLogRecord Only(LogLevel level, string containing)
    {
        FakeLogRecord record = Assert.Single(_logger.Collector.GetSnapshot(),
            r => r.Level == level && r.Message.Contains(containing, StringComparison.Ordinal));
        Assert.Contains(Email, record.Message);
        return record;
    }

    /// <summary>Arranges a password change from "oldpass" to "NewPass1!"; the current-password check returns <paramref name="currentPasswordMatches"/>.</summary>
    private void SetupPasswordChange(bool currentPasswordMatches)
    {
        _accountRepo.Setup(r => r.FindByEmailForUpdateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(Existing());
        _passwordService.Setup(p => p.VerifyPassword("oldpass", It.IsAny<string>())).Returns(currentPasswordMatches);
        _passwordService.Setup(p => p.HashPassword("NewPass1!")).Returns("$2a$13$newhash");
        _accountRepo.Setup(r => r.UpdatePasswordAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(),
            It.IsAny<string?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(1);
    }

    /// <summary>
    /// An empty hash from the password hasher is refused by the domain: nothing is written, the
    /// transaction is rolled back, the failure is logged as an Error and no success is logged.
    /// </summary>
    [Fact]
    public async Task UpdatePassword_FailsAndLogsAnError_WhenTheHasherReturnsAnEmptyHash()
    {
        SetupPasswordChange(currentPasswordMatches: true);
        _passwordService.Setup(p => p.HashPassword("NewPass1!")).Returns("");

        var result = await CreateSut().UpdatePasswordAsync(Email, "oldpass", "NewPass1!", TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal("Profile.UpdatePasswordFailed", result.ErrorCode);
        _accountRepo.Verify(r => r.UpdatePasswordAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(),
            It.IsAny<string?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        Assert.Contains("Account.PasswordEmpty", Only(LogLevel.Error, "Password change failed").Message);
        Assert.DoesNotContain(_logger.Collector.GetSnapshot(), r => r.Message.Contains("Password changed", StringComparison.Ordinal));
    }

    /// <summary>Verifies that a password change logs an Information entry without the old or new password or the new hash.</summary>
    [Fact]
    public async Task UpdatePassword_LogsInformation_WhenThePasswordIsChanged()
    {
        SetupPasswordChange(currentPasswordMatches: true);

        await CreateSut().UpdatePasswordAsync(Email, "oldpass", "NewPass1!", TestContext.Current.CancellationToken);

        FakeLogRecord record = Only(LogLevel.Information, "Password changed");
        Assert.DoesNotContain("NewPass1!", record.Message);
        Assert.DoesNotContain("oldpass", record.Message);
        Assert.DoesNotContain("$2a$13$newhash", record.Message);
    }

    /// <summary>Verifies that a password change with a wrong current password logs a Warning without that password.</summary>
    [Fact]
    public async Task UpdatePassword_LogsWarning_WhenTheCurrentPasswordIsWrong()
    {
        SetupPasswordChange(currentPasswordMatches: false);

        await CreateSut().UpdatePasswordAsync(Email, "oldpass", "NewPass1!", TestContext.Current.CancellationToken);

        FakeLogRecord record = Only(LogLevel.Warning, "Password change refused: wrong current password");
        Assert.DoesNotContain("oldpass", record.Message);
    }

    /// <summary>Arranges the image validator verdicts and, when <paramref name="uploaded"/> is given, the upload result.</summary>
    private void SetupUpload(bool valid, bool svg, FileUploadResult? uploaded = null)
    {
        _imageValidator.Setup(v => v.IsValidImage(It.IsAny<byte[]>())).Returns(valid);
        _imageValidator.Setup(v => v.IsSvg(It.IsAny<byte[]>())).Returns(svg);
        if (uploaded != null)
            _fileClient.Setup(c => c.UploadWithResultAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(uploaded.Value);
    }

    /// <summary>Verifies that an avatar that is not a valid image is refused with a Warning.</summary>
    [Fact]
    public async Task UploadAvatar_LogsWarning_WhenTheFileIsNotAValidImage()
    {
        SetupUpload(valid: false, svg: false);

        await CreateSut().UploadAndUpdateAvatarAsync(Email, [1, 2, 3], ".png", TestContext.Current.CancellationToken);

        Only(LogLevel.Warning, "Avatar upload refused: not a valid image");
    }

    /// <summary>Verifies that an SVG avatar is refused with a Warning.</summary>
    [Fact]
    public async Task UploadAvatar_LogsWarning_WhenTheFileIsAnSvg()
    {
        SetupUpload(valid: true, svg: true);

        await CreateSut().UploadAndUpdateAvatarAsync(Email, [1, 2, 3], ".png", TestContext.Current.CancellationToken);

        Only(LogLevel.Warning, "Avatar upload refused: SVG is not allowed");
    }

    /// <summary>Verifies that an avatar with a disallowed extension is refused with a Warning naming the extension.</summary>
    [Fact]
    public async Task UploadAvatar_LogsWarning_WhenTheExtensionIsNotAllowed()
    {
        await CreateSut().UploadAndUpdateAvatarAsync(Email, [1, 2, 3], ".exe", TestContext.Current.CancellationToken);

        FakeLogRecord record = Only(LogLevel.Warning, "Avatar upload refused: extension not allowed");
        Assert.Contains(".exe", record.Message);
    }

    /// <summary>Verifies that an avatar the virus scanner flags is refused with a Warning.</summary>
    [Fact]
    public async Task UploadAvatar_LogsWarning_WhenMalwareIsDetected()
    {
        SetupUpload(valid: true, svg: false, FileUploadResult.Infected);

        await CreateSut().UploadAndUpdateAvatarAsync(Email, [0xFF, 0xD8, 0xFF], ".png", TestContext.Current.CancellationToken);

        Only(LogLevel.Warning, "Avatar upload refused: virus detected");
    }

    /// <summary>Verifies that an unavailable virus scanner logs an Error.</summary>
    [Fact]
    public async Task UploadAvatar_LogsError_WhenTheScannerIsUnavailable()
    {
        SetupUpload(valid: true, svg: false, FileUploadResult.ScanUnavailable);

        await CreateSut().UploadAndUpdateAvatarAsync(Email, [0xFF, 0xD8, 0xFF], ".png", TestContext.Current.CancellationToken);

        Only(LogLevel.Error, "Avatar upload failed: virus scanner unavailable");
    }

    /// <summary>Verifies that a file storage refusal logs an Error.</summary>
    [Fact]
    public async Task UploadAvatar_LogsError_WhenTheStorageRefusesTheUpload()
    {
        SetupUpload(valid: true, svg: false, FileUploadResult.Failed);

        await CreateSut().UploadAndUpdateAvatarAsync(Email, [0xFF, 0xD8, 0xFF], ".png", TestContext.Current.CancellationToken);

        Only(LogLevel.Error, "Avatar upload failed: file storage refused the upload");
    }

    /// <summary>Verifies that a stored avatar logs an Information entry.</summary>
    [Fact]
    public async Task UploadAvatar_LogsInformation_WhenTheAvatarIsStored()
    {
        SetupUpload(valid: true, svg: false, FileUploadResult.Stored);
        _accountRepo.Setup(r => r.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(Existing());
        _accountRepo.Setup(r => r.UpdateAvatarPathAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(1);

        await CreateSut().UploadAndUpdateAvatarAsync(Email, [0xFF, 0xD8, 0xFF], ".png", TestContext.Current.CancellationToken);

        Only(LogLevel.Information, "Avatar updated");
    }
}
