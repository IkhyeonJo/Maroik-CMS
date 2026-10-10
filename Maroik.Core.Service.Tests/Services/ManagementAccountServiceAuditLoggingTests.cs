using Maroik.Core.Contract.Dtos;
using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Domain.Account;
using Maroik.Core.Service.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace Maroik.Core.Service.Tests.Services;

/// <summary>
/// The admin audit trail of <see cref="ManagementAccountService"/>: what an administrator created,
/// changed (role, lock, deletion, password reset) or deleted is logged with the values before and
/// after and the administrator who did it, so "who did what to which account" is answerable from the logs. Passwords are never logged.
/// </summary>
public class ManagementAccountServiceAuditLoggingTests
{
    /// <summary>The signed-in administrator performing the change (recorded in the audit log).</summary>
    private const string Actor = "admin@example.com";

    /// <summary>E-mail of the account the admin acts on; audit entries must name it.</summary>
    private const string Email = "target@example.com";

    /// <summary>Mock <c>IAccountRepository</c> injected into the system under test.</summary>
    private readonly Mock<IAccountRepository> _accountRepo = new();
    /// <summary>Mock <c>IPasswordService</c> injected into the system under test.</summary>
    private readonly Mock<IPasswordService> _passwordService = new();
    /// <summary>Mock <c>IUnitOfWork</c> injected into the system under test.</summary>
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    /// <summary>Captures the log entries the system under test writes.</summary>
    private readonly FakeLogger<ManagementAccountService> _logger = new();

    /// <summary>
    /// No active administrator by default (Moq would answer the list with null); the admin-safeguard tests set
    /// their own.
    /// </summary>
    public ManagementAccountServiceAuditLoggingTests() =>
        _accountRepo.Setup(r => r.FindActiveAdminsForUpdateAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

    /// <summary>The fixed "current time" of these tests.</summary>
    private static readonly DateTime Now = new(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);
    /// <summary>The clock the service under test reads, stopped at <see cref="Now"/>.</summary>
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(Now));

    /// <summary>The service under test over the mocked dependencies and the capturing logger.</summary>
    private ManagementAccountService CreateSut() => new(_accountRepo.Object, _passwordService.Object, _unitOfWork.Object, _logger, _time);

    /// <summary>The persisted target account, with the given lockout state.</summary>
    private static Account Existing(bool locked = false, long loginAttempt = 0) =>
        Account.Reconstitute(Email, "$2a$13$placeholder", "Target", null, Role.User, "UTC", null, locked, loginAttempt,
            true, true, null, null, DateTime.UtcNow, DateTime.UtcNow, null, false, "stamp", false);

    /// <summary>Asserts exactly one entry at <paramref name="level"/> contains <paramref name="containing"/>, names the target and the acting admin, and returns it.</summary>
    private FakeLogRecord Only(LogLevel level, string containing)
    {
        FakeLogRecord record = Assert.Single(_logger.Collector.GetSnapshot(),
            r => r.Level == level && r.Message.Contains(containing, StringComparison.Ordinal));
        Assert.Contains(Email, record.Message);
        Assert.Contains($"by admin {Actor}", record.Message);
        return record;
    }

    /// <summary>Asserts no log message or exception text contains any of <paramref name="secrets"/>.</summary>
    private void AssertNoSecretLogged(params string[] secrets)
    {
        foreach (FakeLogRecord record in _logger.Collector.GetSnapshot())
        foreach (string secret in secrets)
            Assert.DoesNotContain(secret, record.Message + record.Exception);
    }

    /// <summary>Verifies that an admin-created account is logged at Information with its role.</summary>
    [Fact]
    public async Task Create_LogsInformationWithTheRole_WhenTheAccountIsCreated()
    {
        _accountRepo.Setup(r => r.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Account?)null);
        _passwordService.Setup(p => p.HashPassword("Plain1234!")).Returns("$2a$13$hashedvalue");
        _accountRepo.Setup(r => r.CreateAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await CreateSut().CreateAccountAsync(new AdminCreateAccountRequest
        {
            Email = Email, PlainPassword = "Plain1234!", Nickname = "NewUser", Role = Role.Admin, EmailConfirmed = true,
        }, Actor, TestContext.Current.CancellationToken);

        FakeLogRecord record = Only(LogLevel.Information, "Admin created account");
        Assert.Contains(Role.Admin, record.Message);
        AssertNoSecretLogged("Plain1234!", "$2a$13$hashedvalue");
    }

    /// <summary>Verifies that an admin update logs the role and lock changes (before -> after) and the password-reset flag, without the new password or its hash.</summary>
    [Fact]
    public async Task Update_LogsTheChangesBeforeAndAfter_WhenTheRoleLockAndPasswordChange()
    {
        Account account = Existing(locked: false);
        _accountRepo.Setup(r => r.FindByEmailForUpdateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(account);
        _passwordService.Setup(p => p.HashPassword("NewPass1!")).Returns("$2a$13$newhash");
        _accountRepo.Setup(r => r.UpdateEntityAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await CreateSut().UpdateAccountAsync(
            new AdminUpdateAccountRequest { Email = Email, Role = Role.Admin, Locked = true, EmailConfirmed = true, AgreedServiceTerms = true },
            "NewPass1!", Actor, TestContext.Current.CancellationToken);

        FakeLogRecord record = Only(LogLevel.Information, "Admin updated account");
        Assert.Contains("role User -> Admin", record.Message);
        Assert.Contains("locked False -> True", record.Message);
        Assert.Contains("password reset True", record.Message);
        AssertNoSecretLogged("NewPass1!", "$2a$13$newhash");
    }

    /// <summary>Verifies that an admin update logs a deletion, a restore and an unlock as before -> after transitions.</summary>
    [Fact]
    public async Task Update_LogsTheDeletedAndUnlockTransitions()
    {
        Account account = Existing(locked: true, loginAttempt: 5);
        _accountRepo.Setup(r => r.FindByEmailForUpdateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(account);
        _accountRepo.Setup(r => r.UpdateEntityAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await CreateSut().UpdateAccountAsync(
            new AdminUpdateAccountRequest { Email = Email, Locked = false, Deleted = true, EmailConfirmed = true, AgreedServiceTerms = true },
            null, Actor, TestContext.Current.CancellationToken);
        FakeLogRecord deleted = Only(LogLevel.Information, "deleted False -> True");
        Assert.Contains("locked True -> False", deleted.Message);

        _logger.Collector.Clear();
        await CreateSut().UpdateAccountAsync(
            new AdminUpdateAccountRequest { Email = Email, Deleted = false, EmailConfirmed = true, AgreedServiceTerms = true },
            null, Actor, TestContext.Current.CancellationToken);
        Only(LogLevel.Information, "deleted True -> False");
    }

    /// <summary>Verifies that an admin update without a new password logs <c>password reset False</c>.</summary>
    [Fact]
    public async Task Update_LogsThatThePasswordWasNotReset_WhenNoNewPasswordIsGiven()
    {
        _accountRepo.Setup(r => r.FindByEmailForUpdateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(Existing());
        _accountRepo.Setup(r => r.UpdateEntityAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await CreateSut().UpdateAccountAsync(
            new AdminUpdateAccountRequest { Email = Email, Role = Role.User, EmailConfirmed = true, AgreedServiceTerms = true }, null, Actor, TestContext.Current.CancellationToken);

        Assert.Contains("password reset False", Only(LogLevel.Information, "Admin updated account").Message);
    }

    /// <summary>
    /// An empty hash from the password hasher is refused by the domain: the admin update fails,
    /// nothing is written, the transaction is rolled back and the failure is logged as an Error.
    /// </summary>
    [Fact]
    public async Task Update_FailsAndLogsAnError_WhenTheHasherReturnsAnEmptyHash()
    {
        _accountRepo.Setup(r => r.FindByEmailForUpdateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(Existing());
        _passwordService.Setup(p => p.HashPassword("NewPass1!")).Returns(" ");

        ServiceResult result = await CreateSut().UpdateAccountAsync(
            new AdminUpdateAccountRequest { Email = Email, EmailConfirmed = true, AgreedServiceTerms = true },
            "NewPass1!", Actor, TestContext.Current.CancellationToken);

        Assert.Equal("ManagementAccount.UpdateFailed", result.ErrorCode);
        Assert.Equal(ServiceResult.TemporaryErrorKey, result.ErrorKey);
        _accountRepo.Verify(r => r.UpdateEntityAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        Assert.Contains("Account.PasswordEmpty", Only(LogLevel.Error, "Admin password reset failed").Message);
    }

    /// <summary>Verifies that an admin update of a missing account logs a Warning.</summary>
    [Fact]
    public async Task Update_LogsWarning_WhenTheAccountDoesNotExist()
    {
        _accountRepo.Setup(r => r.FindByEmailForUpdateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Account?)null);

        await CreateSut().UpdateAccountAsync(new AdminUpdateAccountRequest { Email = Email }, null, Actor, TestContext.Current.CancellationToken);

        Only(LogLevel.Warning, "Admin update failed: account not found");
    }

    /// <summary>Verifies that an admin deleting an account logs an Information entry.</summary>
    [Fact]
    public async Task Delete_LogsInformation_WhenTheAccountIsDeleted()
    {
        _accountRepo.Setup(r => r.FindByEmailForUpdateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(Existing());
        _accountRepo.Setup(r => r.UpdateEntityAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await CreateSut().DeleteAccountAsync(Email, Actor, TestContext.Current.CancellationToken);

        Only(LogLevel.Information, "Admin deleted account");
    }

    /// <summary>Verifies that an admin deleting a missing account logs a Warning.</summary>
    [Fact]
    public async Task Delete_LogsWarning_WhenTheAccountDoesNotExist()
    {
        _accountRepo.Setup(r => r.FindByEmailForUpdateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Account?)null);

        await CreateSut().DeleteAccountAsync(Email, Actor, TestContext.Current.CancellationToken);

        Only(LogLevel.Warning, "Admin delete failed: account not found");
    }
}
