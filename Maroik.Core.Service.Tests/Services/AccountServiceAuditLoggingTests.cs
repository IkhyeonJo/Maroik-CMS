using Maroik.Core.Contract.Dtos;
using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Contract.Misc.Messaging;
using Maroik.Core.Contract.Misc.Settings;
using Maroik.Core.Domain.Account;
using Maroik.Core.Service.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;
using Moq;

namespace Maroik.Core.Service.Tests.Services;

/// <summary>
/// The security / audit trail of <see cref="AccountService"/>: every login outcome, registration,
/// email confirmation and password reset leaves a log line an operator can search by account, at the
/// level the CLAUDE.md logging rule prescribes, and never contains a password or token.
/// </summary>
public class AccountServiceAuditLoggingTests
{
    private const string Email = "user@example.com";
    private const string TypedPassword = "Typed-Secret-Pw1!";

    private readonly Mock<IAccountRepository> _accountRepo = new();
    private readonly Mock<IPasswordService> _passwordService = new();
    private readonly Mock<IMailClient> _mailClient = new();
    private readonly Mock<IEmailPublisher> _emailPublisher = new();
    private readonly Mock<IRsaService> _rsa = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly FakeLogger<AccountService> _logger = new();
    private readonly IOptions<ServerSetting> _settings =
        Options.Create(new ServerSetting { MaxLoginAttempt = 3, DomainName = "https://example.com" });

    private static readonly EmailTemplate _emailTemplate = new() { Subject = "s", Title = "t", Content0 = "c0", Content1 = "c1" };

    private AccountService CreateSut() => new(_accountRepo.Object, _passwordService.Object, _mailClient.Object,
        _emailPublisher.Object, _settings, _rsa.Object, _logger, _unitOfWork.Object);

    private static Account ExistingAccount(bool deleted = false, bool locked = false, bool emailConfirmed = true,
        bool agreedServiceTerms = true, long loginAttempt = 0, string? registrationToken = null, string? resetPasswordToken = null) =>
        Account.Reconstitute(Email, "$2a$13$placeholder", "User", null, Role.User, "UTC", null, locked, loginAttempt,
            emailConfirmed, agreedServiceTerms, registrationToken, resetPasswordToken, DateTime.UtcNow, DateTime.UtcNow,
            null, deleted, "stamp", false);

    private void AccountForLogin(Account? account, bool passwordMatches)
    {
        _accountRepo.Setup(r => r.FindByEmailForUpdateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(account);
        _passwordService.Setup(p => p.VerifyPassword(It.IsAny<string>(), It.IsAny<string>())).Returns(passwordMatches);
        _accountRepo.Setup(r => r.UpdateEntityAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _accountRepo.Setup(r => r.UpdateMessageAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(0);
    }

    private FakeLogRecord Only(LogLevel level, string containing)
    {
        FakeLogRecord record = Assert.Single(_logger.Collector.GetSnapshot(),
            r => r.Level == level && r.Message.Contains(containing, StringComparison.Ordinal));
        Assert.Contains(Email, record.Message);
        return record;
    }

    private void AssertNoSecretLogged(params string[] secrets)
    {
        foreach (FakeLogRecord record in _logger.Collector.GetSnapshot())
        foreach (string secret in secrets)
            Assert.DoesNotContain(secret, record.Message + record.Exception);
    }

    /// <summary>Verifies that a successful login logs one Information entry and never the typed password.</summary>
    [Fact]
    public async Task Login_LogsInformation_WhenItSucceeds()
    {
        AccountForLogin(ExistingAccount(), passwordMatches: true);

        await CreateSut().LoginAsync(Email, TypedPassword, TestContext.Current.CancellationToken);

        Only(LogLevel.Information, "Login succeeded");
        AssertNoSecretLogged(TypedPassword);
    }

    /// <summary>Verifies that a wrong password logs a Warning with the updated attempt count and never the typed password.</summary>
    [Fact]
    public async Task Login_LogsWarningWithAttemptCount_WhenThePasswordIsWrong()
    {
        AccountForLogin(ExistingAccount(loginAttempt: 1), passwordMatches: false);

        await CreateSut().LoginAsync(Email, TypedPassword, TestContext.Current.CancellationToken);

        FakeLogRecord record = Only(LogLevel.Warning, "Login failed");
        Assert.Contains("2", record.Message); // the counter after this failure
        AssertNoSecretLogged(TypedPassword);
    }

    /// <summary>Verifies that the failure that reaches the attempt limit logs an account-locked Warning with the count.</summary>
    [Fact]
    public async Task Login_LogsAnAccountLockedWarning_WhenTheFailureReachesTheLimit()
    {
        AccountForLogin(ExistingAccount(loginAttempt: 2), passwordMatches: false);

        await CreateSut().LoginAsync(Email, TypedPassword, TestContext.Current.CancellationToken);

        FakeLogRecord record = Only(LogLevel.Warning, "locked after");
        Assert.Contains("3", record.Message);
    }

    /// <summary>Verifies that a login for an unknown email logs a Warning and never the typed password.</summary>
    [Fact]
    public async Task Login_LogsWarning_WhenNoAccountHasThatEmail()
    {
        AccountForLogin(null, passwordMatches: false);

        await CreateSut().LoginAsync(Email, TypedPassword, TestContext.Current.CancellationToken);

        Only(LogLevel.Warning, "no account");
        AssertNoSecretLogged(TypedPassword);
    }

    /// <summary>Verifies that a login refused because the account is locked logs a Warning.</summary>
    [Fact]
    public async Task Login_LogsWarning_WhenALockedAccountIsRefused()
    {
        AccountForLogin(ExistingAccount(locked: true, loginAttempt: 3), passwordMatches: true);

        await CreateSut().LoginAsync(Email, TypedPassword, TestContext.Current.CancellationToken);

        Only(LogLevel.Warning, "Login refused: account is locked");
    }

    /// <summary>Verifies that a correct password refused for a deleted, unconfirmed or terms-not-accepted account logs a Warning naming the reason.</summary>
    [Theory]
    [InlineData("deleted")]
    [InlineData("email not confirmed")]
    [InlineData("service terms not accepted")]
    public async Task Login_LogsWarning_WhenAProvenOwnerIsRefusedForItsState(string reason)
    {
        AccountForLogin(ExistingAccount(
            deleted: reason == "deleted",
            emailConfirmed: reason != "email not confirmed",
            agreedServiceTerms: reason != "service terms not accepted"), passwordMatches: true);

        await CreateSut().LoginAsync(Email, TypedPassword, TestContext.Current.CancellationToken);

        Only(LogLevel.Warning, "Login refused: " + reason);
    }

    /// <summary>Verifies that registration logs one Information entry without the password, its hash or the encrypted token.</summary>
    [Fact]
    public async Task Register_LogsInformation_WhenANewAccountIsCreated()
    {
        _accountRepo.Setup(r => r.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Account?)null);
        _passwordService.Setup(p => p.HashPassword(It.IsAny<string>())).Returns("$2a$13$hashed");
        _accountRepo.Setup(r => r.CreateAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _rsa.Setup(r => r.Encrypt(It.IsAny<string>())).Returns("enc-token");
        _mailClient.Setup(m => m.GetMailConfirmationBody(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns("body");
        _emailPublisher.Setup(p => p.PublishAsync(It.IsAny<SendEmailMessage>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await CreateSut().RegisterAsync(
            new AccountRequest { Email = Email, PlainPassword = "PlainPass1!", Nickname = "TestUser", RegistrationToken = GuidToken.Generate() },
            _emailTemplate, TestContext.Current.CancellationToken);

        Only(LogLevel.Information, "Account registered");
        AssertNoSecretLogged("PlainPass1!", "enc-token", "$2a$13$hashed");
    }

    /// <summary>Verifies that a successful email confirmation logs one Information entry without the token or its ciphertext.</summary>
    [Fact]
    public async Task ConfirmEmail_LogsInformation_WhenTheTokenConfirmsTheAccount()
    {
        string token = GuidToken.Generate();
        Account account = ExistingAccount(emailConfirmed: false, registrationToken: token);
        _rsa.Setup(r => r.Decrypt("enc")).Returns(token);
        _accountRepo.Setup(r => r.FindByRegistrationTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(account);
        _accountRepo.Setup(r => r.FindByEmailForUpdateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(account);
        _accountRepo.Setup(r => r.UpdateEmailConfirmationAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(1);

        await CreateSut().ConfirmEmailAsync("enc", TestContext.Current.CancellationToken);

        Only(LogLevel.Information, "Email confirmed");
        AssertNoSecretLogged(token, "enc");
    }

    /// <summary>Verifies that an unknown confirmation token logs a Warning without the token or its ciphertext.</summary>
    [Fact]
    public async Task ConfirmEmail_LogsWarning_WhenTheTokenIsRejected()
    {
        _rsa.Setup(r => r.Decrypt("enc")).Returns("raw-token");
        _accountRepo.Setup(r => r.FindByRegistrationTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Account?)null);

        await CreateSut().ConfirmEmailAsync("enc", TestContext.Current.CancellationToken);

        Assert.Single(_logger.Collector.GetSnapshot(),
            r => r.Level == LogLevel.Warning && r.Message.Contains("Email confirmation rejected", StringComparison.Ordinal));
        AssertNoSecretLogged("raw-token", "enc");
    }

    /// <summary>Verifies that queuing a reset mail logs one Information entry without the encrypted reset token.</summary>
    [Fact]
    public async Task ForgotPassword_LogsInformation_WhenAResetMailIsQueued()
    {
        _accountRepo.Setup(r => r.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(ExistingAccount());
        _accountRepo.Setup(r => r.UpdateResetPasswordTokenAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _accountRepo.Setup(r => r.UpdateMessageAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _rsa.Setup(r => r.Encrypt(It.IsAny<string>())).Returns("enc-reset-token");
        _mailClient.Setup(m => m.GetMailResetPasswordBody(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns("body");
        _emailPublisher.Setup(p => p.PublishAsync(It.IsAny<SendEmailMessage>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await CreateSut().ForgotPasswordAsync(Email, _emailTemplate, TestContext.Current.CancellationToken);

        Only(LogLevel.Information, "Password reset requested");
        AssertNoSecretLogged("enc-reset-token");
    }

    /// <summary>Verifies that a reset request for an unknown address logs a Warning.</summary>
    [Fact]
    public async Task ForgotPassword_LogsWarning_WhenTheAddressIsUnknown()
    {
        _accountRepo.Setup(r => r.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Account?)null);
        _rsa.Setup(r => r.Encrypt(It.IsAny<string>())).Returns("x");
        _accountRepo.Setup(r => r.UpdateResetPasswordTokenAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(0);

        await CreateSut().ForgotPasswordAsync(Email, _emailTemplate, TestContext.Current.CancellationToken);

        Only(LogLevel.Warning, "Password reset ignored");
    }

    /// <summary>Verifies that a completed reset logs one Information entry without the new password, its hash or the token.</summary>
    [Fact]
    public async Task ResetPassword_LogsInformation_WhenThePasswordIsReset()
    {
        string token = GuidToken.Generate();
        Account account = ExistingAccount(locked: true, loginAttempt: 3, resetPasswordToken: token);
        _rsa.Setup(r => r.Decrypt("enc")).Returns(token);
        _accountRepo.Setup(r => r.FindByResetPasswordTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(account);
        _accountRepo.Setup(r => r.FindByEmailForUpdateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(account);
        _passwordService.Setup(p => p.HashPassword("NewPass1!")).Returns("$2a$13$newhash");
        _accountRepo.Setup(r => r.UpdateEntityAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await CreateSut().ResetPasswordAsync("enc", "NewPass1!", TestContext.Current.CancellationToken);

        Only(LogLevel.Information, "Password reset completed");
        AssertNoSecretLogged("NewPass1!", "$2a$13$newhash", token, "enc");
    }

    /// <summary>Verifies that an unknown reset token logs a Warning without the token, its ciphertext or the new password.</summary>
    [Fact]
    public async Task ResetPassword_LogsWarning_WhenTheTokenIsRejected()
    {
        _rsa.Setup(r => r.Decrypt("enc")).Returns("raw-token");
        _accountRepo.Setup(r => r.FindByResetPasswordTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Account?)null);

        await CreateSut().ResetPasswordAsync("enc", "NewPass1!", TestContext.Current.CancellationToken);

        Assert.Single(_logger.Collector.GetSnapshot(),
            r => r.Level == LogLevel.Warning && r.Message.Contains("Password reset rejected", StringComparison.Ordinal));
        AssertNoSecretLogged("raw-token", "enc", "NewPass1!");
    }
}
