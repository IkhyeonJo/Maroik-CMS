using System.Diagnostics;
using Maroik.Core.Contract.Dtos;
using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Contract.Misc.Helpers;
using Maroik.Core.Contract.Misc.Messaging;
using Maroik.Core.Contract.Misc.Settings;
using Maroik.Core.Domain.Account;
using Maroik.Core.Service.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
// ReSharper disable AccessToDisposedClosure

namespace Maroik.Core.Service.Tests.Services;

/// <summary>
/// Unit tests for <see cref="AccountService"/>.
/// All external dependencies (repository, password service, mail client, email publisher, RSA service)
/// are replaced with Moq mocks so tests run without a database, SMTP server, or RabbitMQ broker.
/// Covers login, registration, email confirmation, and password-reset flows.
/// </summary>
public class AccountServiceTests
{
    /// <summary>Mock <c>IAccountRepository</c> injected into the system under test.</summary>
    private readonly Mock<IAccountRepository> _accountRepo = new();
    /// <summary>Mock <c>IPasswordService</c> injected into the system under test.</summary>
    private readonly Mock<IPasswordService> _passwordService = new();
    /// <summary>Mock <c>IMailClient</c> injected into the system under test.</summary>
    private readonly Mock<IMailClient> _mailClient = new();
    /// <summary>Mock <c>IEmailPublisher</c> injected into the system under test.</summary>
    private readonly Mock<IEmailPublisher> _emailPublisher = new();
    /// <summary>Mock <c>IRsaService</c> injected into the system under test.</summary>
    private readonly Mock<IRsaService> _rsa = new();
    /// <summary>Mock <c>IUnitOfWork</c> injected into the system under test.</summary>
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    /// <summary>Captures the log entries the system under test writes.</summary>
    private readonly FakeLogger<AccountService> _logger = new();
    /// <summary>Settings with a 5-attempt lockout threshold.</summary>
    private readonly IOptions<ServerSetting> _settings =
        Options.Create(new ServerSetting { MaxLoginAttempt = 5, DomainName = "https://example.com" });

    /// <summary>Minimal mail template passed to the mail-sending methods.</summary>
    private static readonly EmailTemplate _emailTemplate = new() { Subject = "subject", Title = "title", Content0 = "c0", Content1 = "c1" };

    /// <summary>
    /// The password the confirmation tests present with the mailed link. Every test accepts it as
    /// the registration password unless it sets its own <see cref="IPasswordService.VerifyPassword"/>
    /// expectation; login tests use other passwords, so this match never leaks into them.
    /// </summary>
    private const string ConfirmPassword = "Confirm-Pass1!";

    /// <summary>Accepts <see cref="ConfirmPassword"/> as the registration password of every mocked account.</summary>
    public AccountServiceTests() =>
        _passwordService.Setup(p => p.VerifyPassword(ConfirmPassword, It.IsAny<string>())).Returns(true);

    /// <summary>The fixed "current time" of these tests.</summary>
    private static readonly DateTime Now = new(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);
    /// <summary>The clock the service under test reads, stopped at <see cref="Now"/>.</summary>
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(Now));

    /// <summary>The service under test over the mocked dependencies.</summary>
    private AccountService CreateSut() => new(
        _accountRepo.Object,
        _passwordService.Object,
        _mailClient.Object,
        _emailPublisher.Object,
        _settings,
        _rsa.Object,
        _logger,
        _unitOfWork.Object,
        _time);

    // -- Helpers --------------------------------------------------------------

    /// <summary>A persisted account (confirmed, unlocked, terms agreed unless overridden) whose state is set by the arguments.</summary>
    private static Account ActiveAccount(
        string email = "user@example.com",
        bool deleted = false,
        bool locked = false,
        bool emailConfirmed = true,
        bool agreedServiceTerms = true,
        long loginAttempt = 0,
        string? registrationToken = null,
        string? resetPasswordToken = null) =>
        Account.Reconstitute(
            email: email,
            hashedPassword: "$2a$13$placeholder",
            nickname: "User",
            avatarImagePath: null,
            role: Role.User,
            timeZoneIanaId: "UTC",
            defaultMonetaryUnit: null,
            locked: locked,
            loginAttempt: loginAttempt,
            emailConfirmed: emailConfirmed,
            agreedServiceTerms: agreedServiceTerms,
            registrationToken: registrationToken,
            resetPasswordToken: resetPasswordToken,
            created: DateTime.UtcNow,
            updated: DateTime.UtcNow,
            message: null,
            deleted: deleted,
            securityStamp: "stamp",
            mustChangePassword: false);

    /// <summary>Arranges mail bodies to render and the e-mail queue publish to succeed.</summary>
    private void SetupMailSuccess()
    {
        _mailClient.Setup(m => m.GetMailConfirmationBody(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns("email-body");
        _mailClient.Setup(m => m.GetMailResetPasswordBody(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns("email-body");
        _emailPublisher.Setup(p => p.PublishAsync(It.IsAny<SendEmailMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    /// <summary>Arranges mail bodies to render but the e-mail queue publish to throw (broker unreachable).</summary>
    private void SetupMailFailure()
    {
        _mailClient.Setup(m => m.GetMailConfirmationBody(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns("email-body");
        _mailClient.Setup(m => m.GetMailResetPasswordBody(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns("email-body");
        _emailPublisher.Setup(p => p.PublishAsync(It.IsAny<SendEmailMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("RabbitMQ unreachable"));
    }

    // -- LoginAsync -----------------------------------------------------------

    /// <summary>Verifies that <c>LoginAsync</c> returns fail when account not found.</summary>
    [Fact]
    public async Task LoginAsync_ReturnsFail_WhenAccountNotFound()
    {
        _accountRepo.Setup(r => r.FindByEmailForUpdateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Account?)null);
        var sut = CreateSut();

        LoginResult result = await sut.LoginAsync("missing@example.com", "any", TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains("wrong", result.ErrorKey, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Verifies that <c>LoginAsync</c> returns fail when account is deleted.</summary>
    [Fact]
    public async Task LoginAsync_ReturnsFail_WhenAccountIsDeleted()
    {
        var account = ActiveAccount(deleted: true);
        _accountRepo.Setup(r => r.FindByEmailForUpdateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(account);
        // The account-state message is only disclosed once the password proves account ownership.
        _passwordService.Setup(p => p.VerifyPassword(It.IsAny<string>(), It.IsAny<string>())).Returns(true);
        var sut = CreateSut();

        LoginResult result = await sut.LoginAsync(account.Email.Value, "correct", TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains("Deleted", result.ErrorKey, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Verifies that <c>LoginAsync</c> does not disclose a deleted account to a wrong password.</summary>
    [Fact]
    public async Task LoginAsync_ReturnsGenericError_WhenAccountIsDeleted_AndPasswordIsWrong()
    {
        var account = ActiveAccount(deleted: true);
        _accountRepo.Setup(r => r.FindByEmailForUpdateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(account);
        _passwordService.Setup(p => p.VerifyPassword(It.IsAny<string>(), It.IsAny<string>())).Returns(false);
        _accountRepo.Setup(r => r.UpdateEntityAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var sut = CreateSut();

        LoginResult result = await sut.LoginAsync(account.Email.Value, "wrong", TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains("wrong", result.ErrorKey, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Deleted", result.ErrorKey, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A locked account is refused as "locked" whatever password is supplied — right or wrong — and
    /// the password is never even verified, so the lock really ends the guessing and a correct
    /// guess is indistinguishable from a wrong one.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task LoginAsync_ReturnsLocked_WithoutVerifyingThePassword_WhenAccountIsLocked(bool passwordIsCorrect)
    {
        var account = ActiveAccount(locked: true, loginAttempt: 3);
        _accountRepo.Setup(r => r.FindByEmailForUpdateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(account);
        _passwordService.Setup(p => p.VerifyPassword(It.IsAny<string>(), It.IsAny<string>())).Returns(passwordIsCorrect);
        var sut = CreateSut();

        LoginResult result = await sut.LoginAsync(account.Email.Value, "whatever", TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains("Locked", result.ErrorKey, StringComparison.OrdinalIgnoreCase);
        _passwordService.Verify(p => p.VerifyPassword(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        // Refused up front: no failed-attempt write and no commit of a mutated row.
        _accountRepo.Verify(r => r.UpdateEntityAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Verifies that <c>LoginAsync</c> returns a failure result and persists the incremented counter when the password is wrong.</summary>
    [Fact]
    public async Task LoginAsync_ReturnsFail_AndPersistsIncrementedCounter_WhenPasswordIsWrong()
    {
        var account = ActiveAccount(loginAttempt: 1);
        _accountRepo.Setup(r => r.FindByEmailForUpdateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(account);
        _passwordService.Setup(p => p.VerifyPassword(It.IsAny<string>(), It.IsAny<string>())).Returns(false);
        _accountRepo.Setup(r => r.UpdateEntityAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var sut = CreateSut();

        LoginResult result = await sut.LoginAsync(account.Email.Value, "wrong", TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains("wrong", result.ErrorKey, StringComparison.OrdinalIgnoreCase);
        // The row-locked account is mutated in memory and persisted within the same transaction,
        // not via a separate atomic UPDATE.
        _accountRepo.Verify(r => r.UpdateEntityAsync(
            It.Is<Account>(a => a.LoginAttempt == 2 && !a.Locked), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Verifies that <c>LoginAsync</c> locks the account when the failed attempt reaches max login attempts.</summary>
    [Fact]
    public async Task LoginAsync_LocksAccount_WhenMaxLoginAttemptsReached()
    {
        var account = ActiveAccount(loginAttempt: _settings.Value.MaxLoginAttempt - 1);
        _accountRepo.Setup(r => r.FindByEmailForUpdateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(account);
        _passwordService.Setup(p => p.VerifyPassword(It.IsAny<string>(), It.IsAny<string>())).Returns(false);
        _accountRepo.Setup(r => r.UpdateEntityAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var sut = CreateSut();

        LoginResult result = await sut.LoginAsync(account.Email.Value, "wrong", TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        // The wrong-password response is always the generic error — it must not reveal that this
        // failed attempt just locked the account (that would enumerate valid emails). The account
        // is still locked in persisted state; the user learns that on their next attempt.
        Assert.Contains("wrong", result.ErrorKey, StringComparison.OrdinalIgnoreCase);
        _accountRepo.Verify(r => r.UpdateEntityAsync(
            It.Is<Account>(a => a.Locked && a.LoginAttempt == _settings.Value.MaxLoginAttempt), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Verifies that <c>LoginAsync</c> returns fail when email not confirmed.</summary>
    [Fact]
    public async Task LoginAsync_ReturnsFail_WhenEmailNotConfirmed()
    {
        var account = ActiveAccount(emailConfirmed: false);
        _accountRepo.Setup(r => r.FindByEmailForUpdateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(account);
        _passwordService.Setup(p => p.VerifyPassword(It.IsAny<string>(), It.IsAny<string>())).Returns(true);
        _accountRepo.Setup(r => r.UpdateEntityAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var sut = CreateSut();

        LoginResult result = await sut.LoginAsync(account.Email.Value, "correct", TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains("Email verification", result.ErrorKey, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Verifies that <c>LoginAsync</c> returns fail when service terms not agreed.</summary>
    [Fact]
    public async Task LoginAsync_ReturnsFail_WhenServiceTermsNotAgreed()
    {
        var account = ActiveAccount(agreedServiceTerms: false);
        _accountRepo.Setup(r => r.FindByEmailForUpdateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(account);
        _passwordService.Setup(p => p.VerifyPassword(It.IsAny<string>(), It.IsAny<string>())).Returns(true);
        _accountRepo.Setup(r => r.UpdateEntityAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var sut = CreateSut();

        LoginResult result = await sut.LoginAsync(account.Email.Value, "correct", TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains("Service Terms", result.ErrorKey, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Verifies that <c>LoginAsync</c> returns a success result with account data when credentials are valid.</summary>
    [Fact]
    public async Task LoginAsync_ReturnsSuccessWithAccount_WhenCredentialsValid()
    {
        var account = ActiveAccount();
        _accountRepo.Setup(r => r.FindByEmailForUpdateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(account);
        _passwordService.Setup(p => p.VerifyPassword(It.IsAny<string>(), It.IsAny<string>())).Returns(true);
        _accountRepo.Setup(r => r.UpdateEntityAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var sut = CreateSut();

        LoginResult result = await sut.LoginAsync(account.Email.Value, "correct", TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.NotNull(result.Account);
        Assert.Equal(account.Email.Value, result.Account.Email);
        // AccountResponse has no HashedPassword member at all, so the absence of a password hash
        // in the session payload is a compile-time guarantee, not a runtime assertion.
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // -- RegisterAsync --------------------------------------------------------

    /// <summary>Verifies that <c>RegisterAsync</c> creates account and sends email when email does not exist.</summary>
    [Fact]
    public async Task RegisterAsync_CreatesAccountAndSendsEmail_WhenEmailDoesNotExist()
    {
        _accountRepo.Setup(r => r.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Account?)null);
        _passwordService.Setup(p => p.HashPassword(It.IsAny<string>())).Returns("$2a$13$hashed");
        _accountRepo.Setup(r => r.CreateAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _rsa.Setup(r => r.Encrypt(It.IsAny<string>())).Returns("enc-token");
        SetupMailSuccess();
        var sut = CreateSut();

        var request = new RegisterAccountRequest
        {
            Email = "new@example.com",
            PlainPassword = "PlainPass1!",
            Nickname = "TestUser"
        };

        RegisterResult result = await sut.RegisterAsync(request, _emailTemplate, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.True(result.ShowResendEmail);
        _accountRepo.Verify(r => r.CreateAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Regression test (privilege escalation): self-registration always creates an unconfirmed
    /// <see cref="Role.User"/> account with a fresh registration token — the request type has no
    /// role, lock, confirmation or deletion field for a caller to set.
    /// </summary>
    [Fact]
    public async Task RegisterAsync_AlwaysCreatesAnUnconfirmedUserAccount()
    {
        _accountRepo.Setup(r => r.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Account?)null);
        _passwordService.Setup(p => p.HashPassword(It.IsAny<string>())).Returns("$2a$13$hashed");
        Account? created = null;
        _accountRepo.Setup(r => r.CreateAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()))
            .Callback<Account, CancellationToken>((a, _) => created = a)
            .Returns(Task.CompletedTask);
        _rsa.Setup(r => r.Encrypt(It.IsAny<string>())).Returns("enc-token");
        SetupMailSuccess();

        var request = new RegisterAccountRequest
        {
            Email = "new@example.com",
            PlainPassword = "PlainPass1!",
            Nickname = "TestUser",
            TimeZoneIanaId = "UTC",
            AgreedServiceTerms = true
        };

        RegisterResult result = await CreateSut().RegisterAsync(request, _emailTemplate, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.NotNull(created);
        Assert.Equal(Role.User, created.Role.Value);
        Assert.False(created.Locked);
        Assert.False(created.EmailConfirmed);
        Assert.False(created.Deleted);
        Assert.True(GuidToken.IsTokenAlive(created.RegistrationToken!, Now));
    }

    /// <summary>
    /// Verifies that <c>RegisterAsync</c> rejects a reserved nickname (a staff / system name, or the
    /// anonymous placeholder) before doing any of the expensive or persisting work.
    /// </summary>
    [Theory]
    [InlineData("Admin")]
    [InlineData("login")]
    [InlineData("A d m i n")]
    public async Task RegisterAsync_ReturnsFail_WhenNicknameIsReserved(string nickname)
    {
        _accountRepo.Setup(r => r.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Account?)null);
        var sut = CreateSut();

        RegisterResult result = await sut.RegisterAsync(
            new RegisterAccountRequest { Email = "new@example.com", PlainPassword = "PlainPass1!", Nickname = nickname },
            _emailTemplate, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal("'{0}' cannot be used as a Nickname.", result.ErrorKey);
        _passwordService.Verify(p => p.HashPassword(It.IsAny<string>()), Times.Never);
        _accountRepo.Verify(r => r.CreateAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Verifies that <c>RegisterAsync</c> rejects a nickname containing invisible characters.</summary>
    [Fact]
    public async Task RegisterAsync_ReturnsFail_WhenNicknameHasInvisibleCharacters()
    {
        _accountRepo.Setup(r => r.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Account?)null);
        var sut = CreateSut();

        RegisterResult result = await sut.RegisterAsync(
            new RegisterAccountRequest { Email = "new@example.com", PlainPassword = "PlainPass1!", Nickname = "Bo\u200Bb" },
            _emailTemplate, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal("Nickname contains characters that are not allowed.", result.ErrorKey);
        _accountRepo.Verify(r => r.CreateAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Verifies that <c>RegisterAsync</c> reports a nickname already taken ignoring case (the DB
    /// constraint is case-sensitive, so this check is what stops "bob" registering next to "Bob"),
    /// and does so before hashing the password.
    /// </summary>
    [Fact]
    public async Task RegisterAsync_ReturnsFail_WhenNicknameExistsIgnoringCase()
    {
        _accountRepo.Setup(r => r.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Account?)null);
        _accountRepo.Setup(r => r.NicknameExistsIgnoreCaseAsync("bob", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var sut = CreateSut();

        RegisterResult result = await sut.RegisterAsync(
            new RegisterAccountRequest { Email = "new@example.com", PlainPassword = "PlainPass1!", Nickname = "  bob " },
            _emailTemplate, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal("'{0}' is a Nickname that already exists. Please enter another Nickname.", result.ErrorKey);
        Assert.Equal(["bob"], result.ErrorArgs);
        _passwordService.Verify(p => p.HashPassword(It.IsAny<string>()), Times.Never);
        _accountRepo.Verify(r => r.CreateAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Verifies that a concurrent "bob" / "Bob" registration — passing the app-level check but caught
    /// by the case-insensitive unique index — is reported as a nickname conflict, not an email one.
    /// </summary>
    [Fact]
    public async Task RegisterAsync_ReturnsNicknameConflict_WhenCaseInsensitiveIndexRaces()
    {
        _accountRepo.Setup(r => r.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Account?)null);
        _passwordService.Setup(p => p.HashPassword(It.IsAny<string>())).Returns("$2a$13$hashed");
        _accountRepo.Setup(r => r.CreateAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("duplicate key", new Exception("23505: duplicate key value violates unique constraint \"Account_unique_index_0\"")));
        var sut = CreateSut();

        RegisterResult result = await sut.RegisterAsync(
            new RegisterAccountRequest { Email = "new@example.com", PlainPassword = "PlainPass1!", Nickname = "bob" },
            _emailTemplate, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal("'{0}' is a Nickname that already exists. Please enter another Nickname.", result.ErrorKey);
        Assert.Equal(["bob"], result.ErrorArgs);
    }

    /// <summary>Verifies that <c>RegisterAsync</c> persists the normalized nickname, not the raw input.</summary>
    [Fact]
    public async Task RegisterAsync_StoresNormalizedNickname()
    {
        _accountRepo.Setup(r => r.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Account?)null);
        _passwordService.Setup(p => p.HashPassword(It.IsAny<string>())).Returns("$2a$13$hashed");
        Account? created = null;
        _accountRepo.Setup(r => r.CreateAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()))
            .Callback<Account, CancellationToken>((a, _) => created = a)
            .Returns(Task.CompletedTask);
        _rsa.Setup(r => r.Encrypt(It.IsAny<string>())).Returns("enc-token");
        SetupMailSuccess();
        var sut = CreateSut();

        RegisterResult result = await sut.RegisterAsync(
            new RegisterAccountRequest { Email = "new@example.com", PlainPassword = "PlainPass1!", Nickname = "  Ｂｏｂ   Smith " },
            _emailTemplate, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.NotNull(created);
        Assert.Equal("Bob Smith", created.Nickname);
    }

    /// <summary>Verifies that <c>RegisterAsync</c> stamps the published email with the ambient
    /// <see cref="Activity.Current"/> id, so Worker-side logs for that message can be traced
    /// back to this request.</summary>
    [Fact]
    public async Task RegisterAsync_PublishesEmail_WithCurrentActivityId()
    {
        _accountRepo.Setup(r => r.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Account?)null);
        _passwordService.Setup(p => p.HashPassword(It.IsAny<string>())).Returns("$2a$13$hashed");
        _accountRepo.Setup(r => r.CreateAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _rsa.Setup(r => r.Encrypt(It.IsAny<string>())).Returns("enc-token");
        SetupMailSuccess();
        using var activity = new Activity("test-request").Start();
        var sut = CreateSut();

        var request = new RegisterAccountRequest
        {
            Email = "new@example.com",
            PlainPassword = "PlainPass1!",
            Nickname = "TestUser"
        };

        await sut.RegisterAsync(request, _emailTemplate, TestContext.Current.CancellationToken);

        _emailPublisher.Verify(p => p.PublishAsync(
            It.Is<SendEmailMessage>(m => m.CorrelationId == activity.Id),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Verifies that <c>RegisterAsync</c> returns fail when email sending fails.</summary>
    [Fact]
    public async Task RegisterAsync_ReturnsFail_WhenEmailSendingFails()
    {
        _accountRepo.Setup(r => r.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Account?)null);
        _passwordService.Setup(p => p.HashPassword(It.IsAny<string>())).Returns("$2a$13$hashed");
        _accountRepo.Setup(r => r.CreateAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _accountRepo.Setup(r => r.UpdateEntityAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _rsa.Setup(r => r.Encrypt(It.IsAny<string>())).Returns("enc-token");
        SetupMailFailure();
        var sut = CreateSut();

        var request = new RegisterAccountRequest
        {
            Email = "new@example.com",
            PlainPassword = "PlainPass1!"
        };

        RegisterResult result = await sut.RegisterAsync(request, _emailTemplate, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
    }

    /// <summary>
    /// Regression test: a brand-new account is already durably committed via <c>CreateAsync</c>
    /// before this call, with no surrounding try/catch in <c>RegisterAsync</c>. If building the
    /// confirmation mail body (including the RSA encryption of the registration token) throws, it
    /// must be caught and turned into a graceful <see cref="RegisterResult.Fail"/> rather than
    /// propagating as a raw unhandled exception out of <c>RegisterAsync</c>.
    /// </summary>
    [Fact]
    public async Task RegisterAsync_ReturnsFail_WhenRsaEncryptThrows()
    {
        _accountRepo.Setup(r => r.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Account?)null);
        _passwordService.Setup(p => p.HashPassword(It.IsAny<string>())).Returns("$2a$13$hashed");
        _accountRepo.Setup(r => r.CreateAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _rsa.Setup(r => r.Encrypt(It.IsAny<string>())).Throws(new InvalidOperationException("key misconfigured"));
        var sut = CreateSut();

        var request = new RegisterAccountRequest
        {
            Email = "new@example.com",
            PlainPassword = "PlainPass1!",
            Nickname = "TestUser"
        };

        RegisterResult result = await sut.RegisterAsync(request, _emailTemplate, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        _emailPublisher.Verify(p => p.PublishAsync(It.IsAny<SendEmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Verifies that <c>RegisterAsync</c> returns fail when nickname already exists.</summary>
    [Fact]
    public async Task RegisterAsync_ReturnsFail_WhenNicknameAlreadyExists()
    {
        _accountRepo.Setup(r => r.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Account?)null);
        _passwordService.Setup(p => p.HashPassword(It.IsAny<string>())).Returns("$2a$13$hashed");
        // Simulate a unique constraint violation (Npgsql error code 23505) on the Nickname constraint specifically.
        _accountRepo.Setup(r => r.CreateAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("duplicate key", new Exception("23505: duplicate key value violates unique constraint \"Account_Nickname_unique\"")));
        var sut = CreateSut();

        var request = new RegisterAccountRequest
        {
            Email = "new@example.com",
            PlainPassword = "PlainPass1!",
            Nickname = "TakenNick"
        };

        RegisterResult result = await sut.RegisterAsync(request, _emailTemplate, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains("Nickname", result.ErrorKey);
        Assert.Contains("TakenNick", result.ErrorArgs);
    }

    /// <summary>
    /// Regression test: two concurrent registrations for the same not-yet-registered email can both
    /// pass the prior <c>FindByEmailAsync</c> check and race on <c>CreateAsync</c>. The loser hits the
    /// Email primary-key violation (<c>Account_pk</c>), not the Nickname constraint, and must be told
    /// the email is taken — not incorrectly blamed on the nickname.
    /// </summary>
    [Fact]
    public async Task RegisterAsync_ReturnsEmailAlreadyExists_WhenEmailPrimaryKeyViolationRaces()
    {
        _accountRepo.Setup(r => r.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Account?)null);
        _passwordService.Setup(p => p.HashPassword(It.IsAny<string>())).Returns("$2a$13$hashed");
        // Simulate a unique constraint violation on the Email primary key, not the Nickname constraint.
        _accountRepo.Setup(r => r.CreateAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("duplicate key", new Exception("23505: duplicate key value violates unique constraint \"Account_pk\"")));
        var sut = CreateSut();

        var request = new RegisterAccountRequest
        {
            Email = "raced@example.com",
            PlainPassword = "PlainPass1!",
            Nickname = "SomeNick"
        };

        RegisterResult result = await sut.RegisterAsync(request, _emailTemplate, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains("raced@example.com", result.ErrorArgs);
        Assert.DoesNotContain("Nickname", result.ErrorKey);
    }

    /// <summary>Verifies that <c>RegisterAsync</c> resends confirmation email when account exists but not confirmed.</summary>
    [Fact]
    public async Task RegisterAsync_ResendsConfirmationEmail_WhenAccountExistsButNotConfirmed()
    {
        string token = GuidToken.Generate(Now);
        var existing = ActiveAccount(emailConfirmed: false, registrationToken: token);
        SetupUnconfirmedAccountForRegister(existing, lockedRead: existing);
        _rsa.Setup(r => r.Encrypt(It.IsAny<string>())).Returns("enc-token");
        SetupMailSuccess();
        var sut = CreateSut();

        RegisterResult result = await sut.RegisterAsync(NewRegistration(), _emailTemplate, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.True(result.ShowResendEmail);
        _emailPublisher.Verify(p => p.PublishAsync(It.IsAny<SendEmailMessage>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Verifies that <c>RegisterAsync</c> returns fail when account already confirmed.</summary>
    [Fact]
    public async Task RegisterAsync_ReturnsFail_WhenAccountAlreadyConfirmed()
    {
        var existing = ActiveAccount(emailConfirmed: true);
        _accountRepo.Setup(r => r.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        _accountRepo.Setup(r => r.UpdateEntityAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var sut = CreateSut();

        var request = new RegisterAccountRequest { Email = existing.Email.Value, PlainPassword = "pw" };
        RegisterResult result = await sut.RegisterAsync(request, _emailTemplate, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains("already", result.ErrorKey, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Regression test (L-2): the "already confirmed" branch runs off an unauthenticated,
    /// address-only request. Without the caller proving ownership (current password), it must NOT
    /// mutate the row — no AgreedServiceTerms / Message write on a stranger's account.
    /// </summary>
    [Fact]
    public async Task RegisterAsync_ConfirmedAccount_DoesNotTouchTheRow_WhenPasswordIsWrong()
    {
        var existing = ActiveAccount(emailConfirmed: true, agreedServiceTerms: false);
        _accountRepo.Setup(r => r.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        _passwordService.Setup(p => p.VerifyPassword(It.IsAny<string>(), It.IsAny<string>())).Returns(false);
        var sut = CreateSut();

        var request = new RegisterAccountRequest { Email = existing.Email.Value, PlainPassword = "not-the-password" };
        RegisterResult result = await sut.RegisterAsync(request, _emailTemplate, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains("already", result.ErrorKey, StringComparison.OrdinalIgnoreCase);
        _accountRepo.Verify(r => r.UpdateAgreedServiceTermsAsync(
            It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
        _accountRepo.Verify(r => r.UpdateMessageAsync(
            It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Companion to <see cref="RegisterAsync_ConfirmedAccount_DoesNotTouchTheRow_WhenPasswordIsWrong"/>:
    /// the legit owner re-running registration (correct password) still recovers a legacy
    /// "confirmed but terms not accepted" account.
    /// </summary>
    [Fact]
    public async Task RegisterAsync_ConfirmedAccount_ReconcilesTerms_WhenPasswordMatches()
    {
        var existing = ActiveAccount(emailConfirmed: true, agreedServiceTerms: false);
        _accountRepo.Setup(r => r.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        _accountRepo.Setup(r => r.UpdateAgreedServiceTermsAsync(
            It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _accountRepo.Setup(r => r.UpdateMessageAsync(
            It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _passwordService.Setup(p => p.VerifyPassword(It.IsAny<string>(), It.IsAny<string>())).Returns(true);
        var sut = CreateSut();

        var request = new RegisterAccountRequest { Email = existing.Email.Value, PlainPassword = "correct" };
        RegisterResult result = await sut.RegisterAsync(request, _emailTemplate, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        _accountRepo.Verify(r => r.UpdateAgreedServiceTermsAsync(
            existing.Email.Value, true, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // -- ResendConfirmationEmailAsync ------------------------------------------

    /// <summary>
    /// Regression test: this endpoint is address-only (the resend form has no password field), so
    /// an already-confirmed account must never be written to -- otherwise anyone who knows a
    /// registered address could churn a stranger's account row for free, the same class of issue
    /// RegisterAsync's equivalent branch was hardened against.
    /// </summary>
    [Fact]
    public async Task ResendConfirmationEmailAsync_ConfirmedAccount_DoesNotTouchTheRow()
    {
        var existing = ActiveAccount(emailConfirmed: true);
        _accountRepo.Setup(r => r.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        var sut = CreateSut();

        RegisterResult result = await sut.ResendConfirmationEmailAsync(existing.Email.Value, _emailTemplate, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains("already", result.ErrorKey, StringComparison.OrdinalIgnoreCase);
        _accountRepo.Verify(r => r.UpdateMessageAsync(
            It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // -- ConfirmEmailAsync ----------------------------------------------------

    /// <summary>Verifies that <c>ConfirmEmailAsync</c> returns invalid token when decryption fails.</summary>
    [Fact]
    public async Task ConfirmEmailAsync_ReturnsInvalidToken_WhenDecryptionFails()
    {
        _rsa.Setup(r => r.Decrypt(It.IsAny<string>())).Throws(new Exception("bad cipher"));
        var sut = CreateSut();

        ConfirmEmailResult result = await sut.ConfirmEmailAsync("bad-encrypted-token", ConfirmPassword, TestContext.Current.CancellationToken);

        Assert.True(result.InvalidToken);
        Assert.False(result.AccountCreated);
    }

    /// <summary>Verifies that <c>ConfirmEmailAsync</c> returns invalid token when account not found.</summary>
    [Fact]
    public async Task ConfirmEmailAsync_ReturnsInvalidToken_WhenAccountNotFound()
    {
        string freshToken = GuidToken.Generate(Now);
        _rsa.Setup(r => r.Decrypt("enc")).Returns(freshToken);
        _accountRepo.Setup(r => r.FindByRegistrationTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Account?)null);
        var sut = CreateSut();

        ConfirmEmailResult result = await sut.ConfirmEmailAsync("enc", ConfirmPassword, TestContext.Current.CancellationToken);

        Assert.True(result.InvalidToken);
        Assert.False(result.AccountCreated);
    }

    /// <summary>Verifies that <c>ConfirmEmailAsync</c> returns invalid token when token is expired.</summary>
    [Fact]
    public async Task ConfirmEmailAsync_ReturnsInvalidToken_WhenTokenIsExpired()
    {
        string expiredToken = BuildExpiredToken();
        _rsa.Setup(r => r.Decrypt("enc-expired")).Returns(expiredToken);
        _accountRepo.Setup(r => r.FindByRegistrationTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActiveAccount());
        var sut = CreateSut();

        ConfirmEmailResult result = await sut.ConfirmEmailAsync("enc-expired", ConfirmPassword, TestContext.Current.CancellationToken);

        Assert.True(result.InvalidToken);
        Assert.False(result.AccountCreated);
    }

    /// <summary>Verifies that <c>ConfirmEmailAsync</c> confirms account when token is valid.</summary>
    [Fact]
    public async Task ConfirmEmailAsync_ConfirmsAccount_WhenTokenIsValid()
    {
        string freshToken = GuidToken.Generate(Now);
        var account = ActiveAccount(emailConfirmed: false, registrationToken: freshToken);
        _rsa.Setup(r => r.Decrypt("enc")).Returns(freshToken);
        _accountRepo.Setup(r => r.FindByRegistrationTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(account);
        _accountRepo.Setup(r => r.FindByEmailForUpdateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(account);
        _accountRepo.Setup(r => r.UpdateEmailConfirmationAsync(
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        var sut = CreateSut();

        ConfirmEmailResult result = await sut.ConfirmEmailAsync("enc", ConfirmPassword, TestContext.Current.CancellationToken);

        Assert.False(result.InvalidToken);
        Assert.True(result.AccountCreated);
        _accountRepo.Verify(r => r.UpdateEmailConfirmationAsync(
            account.Email.Value, null, It.IsAny<string?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Regression: <c>UpdateEmailConfirmationAsync</c> is an <c>ExecuteUpdateAsync</c>-based,
    /// column-scoped write that matches zero rows -- without throwing -- when the account row was
    /// deleted concurrently between the <c>FindByRegistrationTokenAsync</c> lookup and this write.
    /// <c>ConfirmEmailAsync</c> must treat that as a failed confirmation (mirroring the "account not
    /// found" branch) rather than reporting <c>AccountCreated = true</c> for write that persisted
    /// nothing.
    /// </summary>
    [Fact]
    public async Task ConfirmEmailAsync_ReturnsInvalidTokenNotCreated_WhenAccountVanishesBeforeWrite()
    {
        string freshToken = GuidToken.Generate(Now);
        var account = ActiveAccount(emailConfirmed: false, registrationToken: freshToken);
        _rsa.Setup(r => r.Decrypt("enc")).Returns(freshToken);
        _accountRepo.Setup(r => r.FindByRegistrationTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(account);
        _accountRepo.Setup(r => r.FindByEmailForUpdateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(account);
        _accountRepo.Setup(r => r.UpdateEmailConfirmationAsync(
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        var sut = CreateSut();

        ConfirmEmailResult result = await sut.ConfirmEmailAsync("enc", ConfirmPassword, TestContext.Current.CancellationToken);

        Assert.True(result.InvalidToken);
        Assert.False(result.AccountCreated);
    }

    /// <summary>
    /// Verifies that <c>ConfirmEmailAsync</c> never falls back to a full-row <c>UpdateEntityAsync</c>
    /// write on the success path — it must use the column-scoped <c>UpdateEmailConfirmationAsync</c>
    /// so a concurrent write to another column on the same account row is not silently reverted by a
    /// stale full-row snapshot read from the un-locked token lookup.
    /// </summary>
    [Fact]
    public async Task ConfirmEmailAsync_DoesNotUseFullRowUpdate_WhenTokenIsValid()
    {
        string freshToken = GuidToken.Generate(Now);
        var account = ActiveAccount(emailConfirmed: false, registrationToken: freshToken);
        _rsa.Setup(r => r.Decrypt("enc")).Returns(freshToken);
        _accountRepo.Setup(r => r.FindByRegistrationTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(account);
        _accountRepo.Setup(r => r.FindByEmailForUpdateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(account);
        _accountRepo.Setup(r => r.UpdateEmailConfirmationAsync(
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        var sut = CreateSut();

        await sut.ConfirmEmailAsync("enc", ConfirmPassword, TestContext.Current.CancellationToken);

        _accountRepo.Verify(r => r.UpdateEntityAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Verifies that <c>ConfirmEmailAsync</c> returns not created when account already confirmed.</summary>
    [Fact]
    public async Task ConfirmEmailAsync_ReturnsNotCreated_WhenAccountAlreadyConfirmed()
    {
        string freshToken = GuidToken.Generate(Now);
        var account = ActiveAccount(emailConfirmed: true);
        _rsa.Setup(r => r.Decrypt("enc")).Returns(freshToken);
        _accountRepo.Setup(r => r.FindByRegistrationTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(account);
        _accountRepo.Setup(r => r.FindByEmailForUpdateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(account);
        _accountRepo.Setup(r => r.UpdateMessageAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        var sut = CreateSut();

        ConfirmEmailResult result = await sut.ConfirmEmailAsync("enc", ConfirmPassword, TestContext.Current.CancellationToken);

        Assert.False(result.InvalidToken);
        Assert.False(result.AccountCreated);
    }

    // -- ForgotPasswordAsync --------------------------------------------------

    /// <summary>Verifies that <c>ForgotPasswordAsync</c> returns true silently when account not found.</summary>
    [Fact]
    public async Task ForgotPasswordAsync_ReturnsTrueSilently_WhenAccountNotFound()
    {
        _accountRepo.Setup(r => r.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Account?)null);
        var sut = CreateSut();

        bool result = await sut.ForgotPasswordAsync("ghost@example.com", _emailTemplate, TestContext.Current.CancellationToken);

        Assert.True(result);
        _emailPublisher.Verify(p => p.PublishAsync(It.IsAny<SendEmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Verifies that <c>ForgotPasswordAsync</c> returns true silently when account email not confirmed.</summary>
    [Fact]
    public async Task ForgotPasswordAsync_ReturnsTrueSilently_WhenAccountEmailNotConfirmed()
    {
        var account = ActiveAccount(emailConfirmed: false);
        _accountRepo.Setup(r => r.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(account);
        var sut = CreateSut();

        bool result = await sut.ForgotPasswordAsync(account.Email.Value, _emailTemplate, TestContext.Current.CancellationToken);

        Assert.True(result);
        _emailPublisher.Verify(p => p.PublishAsync(It.IsAny<SendEmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
        VerifyNoWriteToRegisteredRow(account.Email.Value);
    }

    /// <summary>Verifies that <c>ForgotPasswordAsync</c> returns true silently for a deleted account and leaves its row alone.</summary>
    [Fact]
    public async Task ForgotPasswordAsync_ReturnsTrueSilently_AndLeavesRowAlone_WhenAccountDeleted()
    {
        var account = ActiveAccount(deleted: true);
        _accountRepo.Setup(r => r.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(account);
        var sut = CreateSut();

        bool result = await sut.ForgotPasswordAsync(account.Email.Value, _emailTemplate, TestContext.Current.CancellationToken);

        Assert.True(result);
        _emailPublisher.Verify(p => p.PublishAsync(It.IsAny<SendEmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
        VerifyNoWriteToRegisteredRow(account.Email.Value);
    }

    /// <summary>
    /// The timing-equalization write of the "silently succeed" branch must never target the address the
    /// (anonymous) caller typed: for a registered-but-unconfirmed/deleted account that would rewrite a
    /// stranger's <c>Updated</c> stamp and clear its token. It still runs — against an address no
    /// account has — so the branch costs what the real path costs.
    /// </summary>
    private void VerifyNoWriteToRegisteredRow(string registeredEmail)
    {
        _accountRepo.Verify(r => r.UpdateResetPasswordTokenAsync(
            registeredEmail, It.IsAny<string?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
        _accountRepo.Verify(r => r.UpdateResetPasswordTokenAsync(
            "", null, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
        _accountRepo.Verify(r => r.UpdateMessageAsync(
            registeredEmail, It.IsAny<string?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
        _accountRepo.Verify(r => r.UpdateEntityAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Verifies that <c>ForgotPasswordAsync</c> sends reset email when account is valid.</summary>
    [Fact]
    public async Task ForgotPasswordAsync_SendsResetEmail_WhenAccountIsValid()
    {
        var account = ActiveAccount();
        _accountRepo.Setup(r => r.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(account);
        _accountRepo.Setup(r => r.UpdateResetPasswordTokenAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _accountRepo.Setup(r => r.UpdateMessageAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _rsa.Setup(r => r.Encrypt(It.IsAny<string>())).Returns("enc-reset-token");
        SetupMailSuccess();
        var sut = CreateSut();

        bool result = await sut.ForgotPasswordAsync(account.Email.Value, _emailTemplate, TestContext.Current.CancellationToken);

        Assert.True(result);
        // Column-scoped writes (not a full-row UpdateEntityAsync): the reset token first, then the
        // "mail sent" status message.
        _accountRepo.Verify(r => r.UpdateResetPasswordTokenAsync(
            account.Email.Value, It.Is<string?>(t => t != null), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
        _accountRepo.Verify(r => r.UpdateMessageAsync(
            account.Email.Value, It.IsAny<string?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
        _accountRepo.Verify(r => r.UpdateEntityAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Verifies that <c>ForgotPasswordAsync</c> still sends a reset email when the account is locked,
    /// since resetting the password is the intended way to recover from a login lockout.</summary>
    [Fact]
    public async Task ForgotPasswordAsync_SendsResetEmail_WhenAccountIsLocked()
    {
        var account = ActiveAccount(locked: true);
        _accountRepo.Setup(r => r.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(account);
        _accountRepo.Setup(r => r.UpdateResetPasswordTokenAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _accountRepo.Setup(r => r.UpdateMessageAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _rsa.Setup(r => r.Encrypt(It.IsAny<string>())).Returns("enc-reset-token");
        SetupMailSuccess();
        var sut = CreateSut();

        bool result = await sut.ForgotPasswordAsync(account.Email.Value, _emailTemplate, TestContext.Current.CancellationToken);

        Assert.True(result);
        _accountRepo.Verify(r => r.UpdateResetPasswordTokenAsync(
            account.Email.Value, It.Is<string?>(t => t != null), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
        _accountRepo.Verify(r => r.UpdateMessageAsync(
            account.Email.Value, It.IsAny<string?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
        _accountRepo.Verify(r => r.UpdateEntityAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // -- ValidateResetPasswordTokenAsync -------------------------------------

    /// <summary>Verifies that <c>ValidateResetPasswordTokenAsync</c> returns fail to reset when decryption fails.</summary>
    [Fact]
    public async Task ValidateResetPasswordTokenAsync_ReturnsFailToReset_WhenDecryptionFails()
    {
        _rsa.Setup(r => r.Decrypt(It.IsAny<string>())).Throws(new Exception("bad cipher"));
        var sut = CreateSut();

        ResetPasswordValidationResult result = await sut.ValidateResetPasswordTokenAsync("bad", TestContext.Current.CancellationToken);

        Assert.True(result.FailToReset);
    }

    /// <summary>Verifies that <c>ValidateResetPasswordTokenAsync</c> returns fail to reset when token expired.</summary>
    [Fact]
    public async Task ValidateResetPasswordTokenAsync_ReturnsFailToReset_WhenTokenExpired()
    {
        string expiredToken = BuildExpiredToken();
        _rsa.Setup(r => r.Decrypt("enc")).Returns(expiredToken);
        _accountRepo.Setup(r => r.FindByResetPasswordTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActiveAccount());
        var sut = CreateSut();

        ResetPasswordValidationResult result = await sut.ValidateResetPasswordTokenAsync("enc", TestContext.Current.CancellationToken);

        Assert.True(result.FailToReset);
    }

    /// <summary>Verifies that <c>ValidateResetPasswordTokenAsync</c> returns token when valid.</summary>
    [Fact]
    public async Task ValidateResetPasswordTokenAsync_ReturnsToken_WhenValid()
    {
        string freshToken = GuidToken.Generate(Now);
        _rsa.Setup(r => r.Decrypt("enc")).Returns(freshToken);
        _accountRepo.Setup(r => r.FindByResetPasswordTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActiveAccount());
        var sut = CreateSut();

        ResetPasswordValidationResult result = await sut.ValidateResetPasswordTokenAsync("enc", TestContext.Current.CancellationToken);

        Assert.False(result.FailToReset);
        Assert.Equal("enc", result.ResetPasswordToken);
    }

    // -- ResetPasswordAsync ---------------------------------------------------

    /// <summary>Verifies that <c>ResetPasswordAsync</c> returns fail when decryption fails.</summary>
    [Fact]
    public async Task ResetPasswordAsync_ReturnsFail_WhenDecryptionFails()
    {
        _rsa.Setup(r => r.Decrypt(It.IsAny<string>())).Throws(new Exception("bad cipher"));
        var sut = CreateSut();

        (ServiceResult result, _) = await sut.ResetPasswordAsync("bad", "newpass", TestContext.Current.CancellationToken);

        Assert.False(result.Success);
    }

    /// <summary>Verifies that <c>ResetPasswordAsync</c> returns fail when token expired.</summary>
    [Fact]
    public async Task ResetPasswordAsync_ReturnsFail_WhenTokenExpired()
    {
        string expiredToken = BuildExpiredToken();
        _rsa.Setup(r => r.Decrypt("enc")).Returns(expiredToken);
        _accountRepo.Setup(r => r.FindByResetPasswordTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActiveAccount());
        var sut = CreateSut();

        (ServiceResult result, _) = await sut.ResetPasswordAsync("enc", "newpass", TestContext.Current.CancellationToken);

        Assert.False(result.Success);
    }

    /// <summary>Verifies that <c>ResetPasswordAsync</c> updates password and unlocks account when valid.</summary>
    [Fact]
    public async Task ResetPasswordAsync_UpdatesPasswordAndUnlocksAccount_WhenValid()
    {
        string freshToken = GuidToken.Generate(Now);
        var account = ActiveAccount(locked: true, loginAttempt: 3, resetPasswordToken: freshToken);
        _rsa.Setup(r => r.Decrypt("enc")).Returns(freshToken);
        _accountRepo.Setup(r => r.FindByResetPasswordTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(account);
        _accountRepo.Setup(r => r.FindByEmailForUpdateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(account);
        _passwordService.Setup(p => p.HashPassword("NewPass1!")).Returns("$2a$13$newhash");
        _accountRepo.Setup(r => r.UpdateEntityAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var sut = CreateSut();

        (ServiceResult result, _) = await sut.ResetPasswordAsync("enc", "NewPass1!", TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        _accountRepo.Verify(r => r.UpdateEntityAsync(It.Is<Account>(a =>
            a.HashedPassword == "$2a$13$newhash" &&
            a.Locked == false &&
            a.LoginAttempt == 0 &&
            a.ResetPasswordToken == null), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// A reset that succeeds hands back the account to sign in with: unlocked, with the NEW security stamp (a
    /// session carrying the old one would be thrown out on its next request) and no forced password change.
    /// </summary>
    [Fact]
    public async Task ResetPasswordAsync_ReturnsTheAccountToSignIn_WithItsNewSecurityStamp()
    {
        string freshToken = GuidToken.Generate(Now);
        var account = ActiveAccount(email: "reset@example.com", locked: true, loginAttempt: 3, resetPasswordToken: freshToken);
        string oldStamp = account.SecurityStamp;
        _rsa.Setup(r => r.Decrypt("enc")).Returns(freshToken);
        _accountRepo.Setup(r => r.FindByResetPasswordTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(account);
        _accountRepo.Setup(r => r.FindByEmailForUpdateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(account);
        _passwordService.Setup(p => p.HashPassword("NewPass1!")).Returns("$2a$13$newhash");
        _accountRepo.Setup(r => r.UpdateEntityAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        (ServiceResult result, AccountResponse? signIn) = await CreateSut().ResetPasswordAsync("enc", "NewPass1!", TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.NotNull(signIn);
        Assert.Equal("reset@example.com", signIn.Email);
        Assert.NotEqual(oldStamp, signIn.SecurityStamp);
        Assert.Equal(account.SecurityStamp, signIn.SecurityStamp);
        Assert.False(signIn.Locked);
        Assert.False(signIn.MustChangePassword);
    }

    /// <summary>
    /// An account that has not accepted the service terms gets its new password but is not signed in: LoginAsync
    /// refuses it too, so a reset must not be a way around that.
    /// </summary>
    [Fact]
    public async Task ResetPasswordAsync_ReturnsNoAccountToSignIn_WhenTheServiceTermsAreNotAccepted()
    {
        string freshToken = GuidToken.Generate(Now);
        var account = ActiveAccount(agreedServiceTerms: false, resetPasswordToken: freshToken);
        _rsa.Setup(r => r.Decrypt("enc")).Returns(freshToken);
        _accountRepo.Setup(r => r.FindByResetPasswordTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(account);
        _accountRepo.Setup(r => r.FindByEmailForUpdateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(account);
        _passwordService.Setup(p => p.HashPassword("NewPass1!")).Returns("$2a$13$newhash");
        _accountRepo.Setup(r => r.UpdateEntityAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        (ServiceResult result, AccountResponse? signIn) = await CreateSut().ResetPasswordAsync("enc", "NewPass1!", TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Null(signIn);
    }

    // -- GetAccountsByNicknamesAsync ------------------------------------------

    /// <summary>Verifies that <c>GetAccountsByNicknamesAsync</c> delegates to repository.</summary>
    [Fact]
    public async Task GetAccountsByNicknamesAsync_DelegatesToRepository()
    {
        var nicknames = new[] { "Alice", "Bob" };
        var expected = new List<Account>
        {
            ActiveAccount("alice@example.com"),
            ActiveAccount("bob@example.com")
        };
        _accountRepo.Setup(r => r.FindByNicknamesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await CreateSut().GetAccountsByNicknamesAsync(nicknames, TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Count);
        _accountRepo.Verify(r => r.FindByNicknamesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // -- Concurrency / failure branches ----------------------------------------

    /// <summary>A registration request with the given nickname and password.</summary>
    private static RegisterAccountRequest NewRegistration(string nickname = "TestUser", string password = "PlainPass1!") => new()
    {
        Email = "new@example.com",
        PlainPassword = password,
        Nickname = nickname
    };

    /// <summary>Hash returned for the new password when an unconfirmed registration is re-submitted.</summary>
    private const string ReplacementHash = "$2a$13$replacement";

    /// <summary>Arranges a re-registration: the plain read returns <paramref name="existing"/>, the locked re-read returns <paramref name="lockedRead"/>, and hashing yields <see cref="ReplacementHash"/>.</summary>
    private void SetupUnconfirmedAccountForRegister(Account existing, Account? lockedRead)
    {
        _accountRepo.Setup(r => r.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        _accountRepo.Setup(r => r.FindByEmailForUpdateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(lockedRead);
        _passwordService.Setup(p => p.HashPassword(It.IsAny<string>())).Returns(ReplacementHash);
    }

    /// <summary>Verifies that <c>GetAccountByEmailAsync</c> returns null for an unknown address and a mapped response otherwise.</summary>
    [Fact]
    public async Task GetAccountByEmailAsync_ReturnsNullWhenMissing_AndMappedResponseWhenFound()
    {
        var known = ActiveAccount("known@example.com");
        _accountRepo.Setup(r => r.FindByEmailAsync("known@example.com", It.IsAny<CancellationToken>())).ReturnsAsync(known);
        _accountRepo.Setup(r => r.FindByEmailAsync("ghost@example.com", It.IsAny<CancellationToken>())).ReturnsAsync((Account?)null);
        var sut = CreateSut();

        Assert.Null(await sut.GetAccountByEmailAsync("ghost@example.com", TestContext.Current.CancellationToken));
        Assert.Equal("known@example.com", (await sut.GetAccountByEmailAsync("known@example.com", TestContext.Current.CancellationToken))?.Email);
    }

    /// <summary>An unexpected repository failure during login must release the row lock (rollback) and propagate.</summary>
    [Fact]
    public async Task LoginAsync_RollsBackAndRethrows_WhenTheRepositoryThrows()
    {
        _accountRepo.Setup(r => r.FindByEmailForUpdateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db down"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateSut().LoginAsync("user@example.com", "pw", TestContext.Current.CancellationToken));

        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>The complexity rule is enforced at the service boundary, before the (slow) hash and before anything is stored.</summary>
    [Fact]
    public async Task RegisterAsync_ReturnsPasswordViolation_AndDoesNotHashOrCreate_WhenPasswordIsTooWeak()
    {
        _accountRepo.Setup(r => r.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Account?)null);

        RegisterResult result = await CreateSut().RegisterAsync(NewRegistration(password: "weak"), _emailTemplate, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(PasswordPolicy.ViolationMessage, result.ErrorKey);
        _passwordService.Verify(p => p.HashPassword(It.IsAny<string>()), Times.Never);
        _accountRepo.Verify(r => r.CreateAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>A failure that is not a unique-key race is logged and reported as a generic registration error.</summary>
    [Fact]
    public async Task RegisterAsync_ReturnsGenericError_WhenCreateFailsForAnyOtherReason()
    {
        _accountRepo.Setup(r => r.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Account?)null);
        _passwordService.Setup(p => p.HashPassword(It.IsAny<string>())).Returns("$2a$13$hashed");
        _accountRepo.Setup(r => r.CreateAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("connection reset"));

        RegisterResult result = await CreateSut().RegisterAsync(NewRegistration(), _emailTemplate, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal("Error occurred while processing about account registration", result.ErrorKey);
        _emailPublisher.Verify(p => p.PublishAsync(It.IsAny<SendEmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>The account vanishing between the unlocked read and the FOR UPDATE re-read is reported, not dereferenced.</summary>
    [Fact]
    public async Task RegisterAsync_Unconfirmed_ReturnsGenericError_WhenTheRowVanishesUnderTheLock()
    {
        SetupUnconfirmedAccountForRegister(ActiveAccount(emailConfirmed: false), lockedRead: null);

        RegisterResult result = await CreateSut().RegisterAsync(NewRegistration(), _emailTemplate, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal("Error occurred while processing about account registration", result.ErrorKey);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        _emailPublisher.Verify(p => p.PublishAsync(It.IsAny<SendEmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>A concurrent confirmation that wins the lock turns the re-registration into "already exists", with no mail.</summary>
    [Fact]
    public async Task RegisterAsync_Unconfirmed_ReportsAlreadyCreated_WhenConfirmedConcurrentlyWhileWaitingForTheLock()
    {
        SetupUnconfirmedAccountForRegister(ActiveAccount(emailConfirmed: false), lockedRead: ActiveAccount(emailConfirmed: true));

        RegisterResult result = await CreateSut().RegisterAsync(NewRegistration(), _emailTemplate, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(EnumHelper.GetDescription(AccountMessage.UserAlreadyCreated), result.ErrorKey);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        _emailPublisher.Verify(p => p.PublishAsync(It.IsAny<SendEmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>An expired registration token is regenerated on re-registration so the mailed link is alive.</summary>
    [Fact]
    public async Task RegisterAsync_Unconfirmed_RegeneratesAnExpiredRegistrationToken()
    {
        string expired = BuildExpiredToken();
        var existing = ActiveAccount(emailConfirmed: false, registrationToken: expired);
        SetupUnconfirmedAccountForRegister(existing, lockedRead: existing);
        _rsa.Setup(r => r.Encrypt(It.IsAny<string>())).Returns("enc-token");
        SetupMailSuccess();

        RegisterResult result = await CreateSut().RegisterAsync(NewRegistration(), _emailTemplate, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        _accountRepo.Verify(r => r.UpdateRegistrationTokenAsync(
            existing.Email.Value,
            It.Is<string?>(t => t != null && t != expired && GuidToken.IsTokenAlive(t, Now)),
            It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    /// <summary>A failing write of the replaced registration under the lock releases it and propagates instead of mailing a dead link.</summary>
    [Fact]
    public async Task RegisterAsync_Unconfirmed_RollsBackAndRethrows_WhenPersistingTheReplacementFails()
    {
        var existing = ActiveAccount(emailConfirmed: false, registrationToken: GuidToken.Generate(Now));
        SetupUnconfirmedAccountForRegister(existing, lockedRead: existing);
        _accountRepo.Setup(r => r.UpdateEntityAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db down"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateSut().RegisterAsync(NewRegistration(), _emailTemplate, TestContext.Current.CancellationToken));

        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        _emailPublisher.Verify(p => p.PublishAsync(It.IsAny<SendEmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Pre-hijacking guard: re-registering an address nobody has confirmed replaces the earlier
    /// registrant's password, nickname and time zone with the new submission and issues a fresh
    /// token, so the confirmation mail the owner receives activates the owner's credentials.
    /// </summary>
    [Fact]
    public async Task RegisterAsync_Unconfirmed_ReplacesTheEarlierRegistrantsCredentials()
    {
        string squatterToken = GuidToken.Generate(Now);
        var existing = ActiveAccount(emailConfirmed: false, registrationToken: squatterToken); // nickname "User", time zone "UTC"
        SetupUnconfirmedAccountForRegister(existing, lockedRead: existing);
        _rsa.Setup(r => r.Encrypt(It.IsAny<string>())).Returns("enc-token");
        SetupMailSuccess();
        Account? saved = null;
        _accountRepo.Setup(r => r.UpdateEntityAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()))
            .Callback<Account, CancellationToken>((a, _) => saved = a).Returns(Task.CompletedTask);

        var request = NewRegistration(nickname: "Owner", password: "OwnerPass1!");
        request.TimeZoneIanaId = "Asia/Seoul";
        RegisterResult result = await CreateSut().RegisterAsync(request, _emailTemplate, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        _passwordService.Verify(p => p.HashPassword("OwnerPass1!"), Times.Once);
        Assert.NotNull(saved);
        Assert.Equal(ReplacementHash, saved.HashedPassword);
        Assert.Equal("Owner", saved.Nickname);
        Assert.Equal("Asia/Seoul", saved.TimeZone.Value);
        Assert.NotEqual(squatterToken, saved.RegistrationToken);
        Assert.True(GuidToken.IsTokenAlive(saved.RegistrationToken!, Now));
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        _emailPublisher.Verify(p => p.PublishAsync(It.IsAny<SendEmailMessage>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Re-registering with a nickname another account already holds is refused before anything is written or mailed.</summary>
    [Fact]
    public async Task RegisterAsync_Unconfirmed_ReturnsNicknameConflict_WhenAnotherAccountHoldsTheNickname()
    {
        var existing = ActiveAccount(emailConfirmed: false, registrationToken: GuidToken.Generate(Now));
        SetupUnconfirmedAccountForRegister(existing, lockedRead: existing);
        _accountRepo.Setup(r => r.NicknameExistsIgnoreCaseAsync("Taken", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        RegisterResult result = await CreateSut().RegisterAsync(NewRegistration(nickname: "Taken"), _emailTemplate, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal("'{0}' is a Nickname that already exists. Please enter another Nickname.", result.ErrorKey);
        Assert.Equal(["Taken"], result.ErrorArgs);
        _accountRepo.Verify(r => r.UpdateEntityAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        _emailPublisher.Verify(p => p.PublishAsync(It.IsAny<SendEmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// The account's own current nickname is not a conflict: keeping it (or changing only its case)
    /// must not be refused by the uniqueness check that the account itself would trip.
    /// </summary>
    [Fact]
    public async Task RegisterAsync_Unconfirmed_KeepsItsOwnNickname_WithoutAConflictCheck()
    {
        var existing = ActiveAccount(emailConfirmed: false, registrationToken: GuidToken.Generate(Now)); // nickname "User"
        SetupUnconfirmedAccountForRegister(existing, lockedRead: existing);
        _accountRepo.Setup(r => r.NicknameExistsIgnoreCaseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _rsa.Setup(r => r.Encrypt(It.IsAny<string>())).Returns("enc-token");
        SetupMailSuccess();

        RegisterResult result = await CreateSut().RegisterAsync(NewRegistration(nickname: "user"), _emailTemplate, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal("user", existing.Nickname);
        _accountRepo.Verify(r => r.NicknameExistsIgnoreCaseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>A concurrent registration taking the same nickname surfaces at commit as the unique index; it is reported as a nickname conflict.</summary>
    [Fact]
    public async Task RegisterAsync_Unconfirmed_ReturnsNicknameConflict_WhenTheUniqueIndexRacesAtCommit()
    {
        var existing = ActiveAccount(emailConfirmed: false, registrationToken: GuidToken.Generate(Now));
        SetupUnconfirmedAccountForRegister(existing, lockedRead: existing);
        _unitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("duplicate key", new Exception("23505: duplicate key value violates unique constraint \"Account_unique_index_0\"")));

        RegisterResult result = await CreateSut().RegisterAsync(NewRegistration(nickname: "Racer"), _emailTemplate, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal("'{0}' is a Nickname that already exists. Please enter another Nickname.", result.ErrorKey);
        Assert.Equal(["Racer"], result.ErrorArgs);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        _emailPublisher.Verify(p => p.PublishAsync(It.IsAny<SendEmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>A weak password or an invalid nickname is refused before hashing or taking the row lock.</summary>
    [Theory]
    [InlineData("TestUser", "weak")]
    [InlineData("admin", "PlainPass1!")]
    public async Task RegisterAsync_Unconfirmed_RefusesInvalidInput_BeforeHashingOrLocking(string nickname, string password)
    {
        var existing = ActiveAccount(emailConfirmed: false, registrationToken: GuidToken.Generate(Now));
        SetupUnconfirmedAccountForRegister(existing, lockedRead: existing);

        RegisterResult result = await CreateSut().RegisterAsync(NewRegistration(nickname, password), _emailTemplate, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        _passwordService.Verify(p => p.HashPassword(It.IsAny<string>()), Times.Never);
        _accountRepo.Verify(r => r.FindByEmailForUpdateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>A failure while reconciling a confirmed account's status is logged and reported as a status error.</summary>
    [Fact]
    public async Task RegisterAsync_ConfirmedAccount_ReportsStatusError_WhenReconcilingTheRowFails()
    {
        var existing = ActiveAccount(emailConfirmed: true, agreedServiceTerms: false);
        _accountRepo.Setup(r => r.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        _passwordService.Setup(p => p.VerifyPassword(It.IsAny<string>(), It.IsAny<string>())).Returns(true);
        _accountRepo.Setup(r => r.UpdateAgreedServiceTermsAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db down"));

        RegisterResult result = await CreateSut().RegisterAsync(NewRegistration(), _emailTemplate, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal("Error occurred while processing about account status", result.ErrorKey);
    }

    /// <summary>Resending for an unknown address asks the UI to keep showing the resend button.</summary>
    [Fact]
    public async Task ResendConfirmationEmailAsync_ReturnsFailWithResendButton_WhenAccountNotFound()
    {
        _accountRepo.Setup(r => r.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Account?)null);

        RegisterResult result = await CreateSut().ResendConfirmationEmailAsync("ghost@example.com", _emailTemplate, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.True(result.ShowResendEmail);
        Assert.Equal("Failed to resend email", result.ErrorKey);
    }

    /// <summary>The row vanishing under the lock is reported like "not found" and the lock is released.</summary>
    [Fact]
    public async Task ResendConfirmationEmailAsync_ReturnsFail_WhenTheRowVanishesUnderTheLock()
    {
        SetupUnconfirmedAccountForRegister(ActiveAccount(emailConfirmed: false), lockedRead: null);

        RegisterResult result = await CreateSut().ResendConfirmationEmailAsync("user@example.com", _emailTemplate, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.True(result.ShowResendEmail);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>A concurrent confirmation that wins the lock stops the resend with "already exists" and sends nothing.</summary>
    [Fact]
    public async Task ResendConfirmationEmailAsync_ReportsAlreadyCreated_WhenConfirmedConcurrentlyWhileWaitingForTheLock()
    {
        SetupUnconfirmedAccountForRegister(ActiveAccount(emailConfirmed: false), lockedRead: ActiveAccount(emailConfirmed: true));

        RegisterResult result = await CreateSut().ResendConfirmationEmailAsync("user@example.com", _emailTemplate, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(EnumHelper.GetDescription(AccountMessage.UserAlreadyCreated), result.ErrorKey);
        _emailPublisher.Verify(p => p.PublishAsync(It.IsAny<SendEmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>A resend regenerates a missing/expired token, and marks the outcome as a repeat.</summary>
    [Fact]
    public async Task ResendConfirmationEmailAsync_RegeneratesAnExpiredToken_AndFlagsTheResultAsARepeat()
    {
        string expired = BuildExpiredToken();
        var account = ActiveAccount(emailConfirmed: false, registrationToken: expired);
        SetupUnconfirmedAccountForRegister(account, lockedRead: account);
        _rsa.Setup(r => r.Encrypt(It.IsAny<string>())).Returns("enc-token");
        SetupMailSuccess();

        RegisterResult result = await CreateSut().ResendConfirmationEmailAsync(account.Email.Value, _emailTemplate, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.True(result.RepeatEmailSend);
        _accountRepo.Verify(r => r.UpdateRegistrationTokenAsync(
            account.Email.Value, It.Is<string?>(t => t != null && t != expired && GuidToken.IsTokenAlive(t, Now)),
            It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    /// <summary>A failing token write under the lock releases it and propagates.</summary>
    [Fact]
    public async Task ResendConfirmationEmailAsync_RollsBackAndRethrows_WhenPersistingTheTokenFails()
    {
        var account = ActiveAccount(emailConfirmed: false, registrationToken: GuidToken.Generate(Now));
        SetupUnconfirmedAccountForRegister(account, lockedRead: account);
        _accountRepo.Setup(r => r.UpdateRegistrationTokenAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db down"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateSut().ResendConfirmationEmailAsync(account.Email.Value, _emailTemplate, TestContext.Current.CancellationToken));

        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>The confirmation-mail send reports a status error (and sends nothing) when the token cannot be persisted first.</summary>
    [Fact]
    public async Task RegisterAsync_NewAccount_ReportsStatusError_WhenThePostCreateTokenWriteFails()
    {
        _accountRepo.Setup(r => r.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Account?)null);
        _passwordService.Setup(p => p.HashPassword(It.IsAny<string>())).Returns("$2a$13$hashed");
        _accountRepo.Setup(r => r.CreateAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _accountRepo.Setup(r => r.UpdateRegistrationTokenAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db down"));

        RegisterResult result = await CreateSut().RegisterAsync(NewRegistration(), _emailTemplate, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal("Error occurred while processing about account status", result.ErrorKey);
        _emailPublisher.Verify(p => p.PublishAsync(It.IsAny<SendEmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>A failing final "verify e-mail" status write after a successful send is reported as a status error.</summary>
    [Fact]
    public async Task ResendConfirmationEmailAsync_ReportsStatusError_WhenTheFinalStatusWriteFails()
    {
        var account = ActiveAccount(emailConfirmed: false, registrationToken: GuidToken.Generate(Now));
        SetupUnconfirmedAccountForRegister(account, lockedRead: account);
        _rsa.Setup(r => r.Encrypt(It.IsAny<string>())).Returns("enc-token");
        SetupMailSuccess();
        _accountRepo.Setup(r => r.UpdateMessageAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db down"));

        RegisterResult result = await CreateSut().ResendConfirmationEmailAsync(account.Email.Value, _emailTemplate, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal("Error occurred while processing about account status", result.ErrorKey);
    }

    /// <summary>A mail-send failure whose "FailToMailSent" status write also fails is still reported as a send failure.</summary>
    [Fact]
    public async Task ResendConfirmationEmailAsync_ReportsSendFailure_EvenWhenRecordingTheFailureAlsoFails()
    {
        var account = ActiveAccount(emailConfirmed: false, registrationToken: GuidToken.Generate(Now));
        SetupUnconfirmedAccountForRegister(account, lockedRead: account);
        _rsa.Setup(r => r.Encrypt(It.IsAny<string>())).Returns("enc-token");
        SetupMailFailure();
        _accountRepo.Setup(r => r.UpdateMessageAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db down"));

        RegisterResult result = await CreateSut().ResendConfirmationEmailAsync(account.Email.Value, _emailTemplate, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal("Error occurred while processing about sending account authentication mail", result.ErrorKey);
    }

    // -- ConfirmEmailAsync: registration password -------------------------------

    /// <summary>
    /// The link alone does not activate the account: a password that does not match the one chosen at
    /// registration leaves it unconfirmed, keeps the token for a retry, and writes nothing.
    /// </summary>
    [Fact]
    public async Task ConfirmEmailAsync_ReturnsWrongPassword_AndConfirmsNothing_WhenThePasswordDoesNotMatch()
    {
        string token = GuidToken.Generate(Now);
        var account = ActiveAccount(emailConfirmed: false, registrationToken: token);
        SetupConfirmLookup(account, account, token);
        _passwordService.Setup(p => p.VerifyPassword("Not-The-Password1!", account.HashedPassword)).Returns(false);

        ConfirmEmailResult result = await CreateSut().ConfirmEmailAsync("enc", "Not-The-Password1!", TestContext.Current.CancellationToken);

        Assert.True(result.WrongPassword);
        Assert.False(result.InvalidToken);
        Assert.False(result.AccountCreated);
        Assert.Equal("enc", result.RegistrationToken);
        Assert.False(account.EmailConfirmed);
        _accountRepo.Verify(r => r.UpdateEmailConfirmationAsync(
            It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>The registration password is checked against the locked row's hash — the credentials actually being activated.</summary>
    [Fact]
    public async Task ConfirmEmailAsync_ChecksThePasswordAgainstTheLockedRow()
    {
        string token = GuidToken.Generate(Now);
        var account = ActiveAccount(emailConfirmed: false, registrationToken: token);
        SetupConfirmLookup(account, account, token);
        _accountRepo.Setup(r => r.UpdateEmailConfirmationAsync(
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        ConfirmEmailResult result = await CreateSut().ConfirmEmailAsync("enc", ConfirmPassword, TestContext.Current.CancellationToken);

        Assert.True(result.AccountCreated);
        _passwordService.Verify(p => p.VerifyPassword(ConfirmPassword, account.HashedPassword), Times.Once);
    }

    // -- ValidateRegistrationTokenAsync --------------------------------------------

    /// <summary>A live link for an unconfirmed account shows the password form (its token is echoed back) and changes nothing.</summary>
    [Fact]
    public async Task ValidateRegistrationTokenAsync_ReturnsTheTokenForTheForm_WhenTheLinkIsLive()
    {
        string token = GuidToken.Generate(Now);
        _rsa.Setup(r => r.Decrypt("enc")).Returns(token);
        _accountRepo.Setup(r => r.FindByRegistrationTokenAsync(token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActiveAccount(emailConfirmed: false, registrationToken: token));

        ConfirmEmailResult result = await CreateSut().ValidateRegistrationTokenAsync("enc", TestContext.Current.CancellationToken);

        Assert.False(result.InvalidToken);
        Assert.False(result.AccountCreated);
        Assert.Equal("enc", result.RegistrationToken);
        _unitOfWork.VerifyNoOtherCalls();
    }

    /// <summary>An undecryptable, unknown or expired link is invalid and shows no form.</summary>
    [Theory]
    [InlineData("undecryptable")]
    [InlineData("unknown")]
    [InlineData("expired")]
    public async Task ValidateRegistrationTokenAsync_ReturnsInvalidToken_WhenTheLinkIsDead(string kind)
    {
        string token = kind == "expired" ? BuildExpiredToken() : GuidToken.Generate(Now);
        if (kind == "undecryptable")
            _rsa.Setup(r => r.Decrypt("enc")).Throws(new FormatException("bad cipher"));
        else
            _rsa.Setup(r => r.Decrypt("enc")).Returns(token);
        _accountRepo.Setup(r => r.FindByRegistrationTokenAsync(token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(kind == "unknown" ? null : ActiveAccount(emailConfirmed: false, registrationToken: token));

        ConfirmEmailResult result = await CreateSut().ValidateRegistrationTokenAsync("enc", TestContext.Current.CancellationToken);

        Assert.True(result.InvalidToken);
        Assert.False(result.AccountCreated);
        Assert.Null(result.RegistrationToken);
    }

    // -- ConfirmEmailAsync: locked re-read branches ------------------------------

    /// <summary>Arranges e-mail confirmation: <c>"enc"</c> decrypts to <paramref name="rawToken"/>, the token lookup returns <paramref name="found"/>, and the locked re-read returns <paramref name="locked"/>.</summary>
    private void SetupConfirmLookup(Account found, Account? locked, string rawToken)
    {
        _rsa.Setup(r => r.Decrypt("enc")).Returns(rawToken);
        _accountRepo.Setup(r => r.FindByRegistrationTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(found);
        _accountRepo.Setup(r => r.FindByEmailForUpdateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(locked);
    }

    /// <summary>The row vanishing between the token lookup and the FOR UPDATE re-read is an invalid token, nothing created.</summary>
    [Fact]
    public async Task ConfirmEmailAsync_ReturnsInvalidTokenNotCreated_WhenTheRowVanishesUnderTheLock()
    {
        string token = GuidToken.Generate(Now);
        SetupConfirmLookup(ActiveAccount(emailConfirmed: false, registrationToken: token), locked: null, token);

        ConfirmEmailResult result = await CreateSut().ConfirmEmailAsync("enc", ConfirmPassword, TestContext.Current.CancellationToken);

        Assert.True(result.InvalidToken);
        Assert.False(result.AccountCreated);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>A token superseded by a concurrent resend (no longer matches the locked row) is invalid but the account exists.</summary>
    [Fact]
    public async Task ConfirmEmailAsync_ReturnsInvalidTokenButCreated_WhenTheTokenWasSupersededUnderTheLock()
    {
        string token = GuidToken.Generate(Now);
        var stale = ActiveAccount(emailConfirmed: false, registrationToken: token);
        var superseded = ActiveAccount(emailConfirmed: false, registrationToken: GuidToken.Generate(Now));
        SetupConfirmLookup(stale, superseded, token);

        ConfirmEmailResult result = await CreateSut().ConfirmEmailAsync("enc", ConfirmPassword, TestContext.Current.CancellationToken);

        Assert.True(result.InvalidToken);
        Assert.True(result.AccountCreated);
        _accountRepo.Verify(r => r.UpdateEmailConfirmationAsync(
            It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>When the status write for an already-confirmed account fails, the transaction is rolled back and the token is still reported valid.</summary>
    [Fact]
    public async Task ConfirmEmailAsync_RollsBack_WhenWritingTheAlreadyConfirmedMessageFails()
    {
        string token = GuidToken.Generate(Now);
        var found = ActiveAccount(emailConfirmed: false, registrationToken: token);
        SetupConfirmLookup(found, ActiveAccount(emailConfirmed: true), token);
        _accountRepo.Setup(r => r.UpdateMessageAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db down"));

        ConfirmEmailResult result = await CreateSut().ConfirmEmailAsync("enc", ConfirmPassword, TestContext.Current.CancellationToken);

        Assert.False(result.InvalidToken);
        Assert.False(result.AccountCreated);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>A failing confirmation write is rolled back and surfaced with its error key so the page can explain.</summary>
    [Fact]
    public async Task ConfirmEmailAsync_ReturnsErrorKey_WhenTheConfirmationWriteThrows()
    {
        string token = GuidToken.Generate(Now);
        var account = ActiveAccount(emailConfirmed: false, registrationToken: token);
        SetupConfirmLookup(account, account, token);
        _accountRepo.Setup(r => r.UpdateEmailConfirmationAsync(
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db down"));

        ConfirmEmailResult result = await CreateSut().ConfirmEmailAsync("enc", ConfirmPassword, TestContext.Current.CancellationToken);

        Assert.True(result.InvalidToken);
        Assert.True(result.AccountCreated);
        Assert.Equal("Error occurred while processing about confirm email account status", result.ErrorKey);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>An unexpected failure taking the lock releases it and propagates.</summary>
    [Fact]
    public async Task ConfirmEmailAsync_RollsBackAndRethrows_WhenTheLockedReadThrows()
    {
        string token = GuidToken.Generate(Now);
        _rsa.Setup(r => r.Decrypt("enc")).Returns(token);
        _accountRepo.Setup(r => r.FindByRegistrationTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActiveAccount(emailConfirmed: false, registrationToken: token));
        _accountRepo.Setup(r => r.FindByEmailForUpdateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db down"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateSut().ConfirmEmailAsync("enc", ConfirmPassword, TestContext.Current.CancellationToken));

        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // -- ForgotPasswordAsync / ResetPasswordAsync: failure branches ---------------

    /// <summary>The timing-equalization work of an unknown address never surfaces a failure to the caller.</summary>
    [Fact]
    public async Task ForgotPasswordAsync_StillReturnsTrue_WhenTheEqualizationWorkThrows()
    {
        _accountRepo.Setup(r => r.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Account?)null);
        _rsa.Setup(r => r.Encrypt(It.IsAny<string>())).Throws(new InvalidOperationException("rsa down"));

        bool result = await CreateSut().ForgotPasswordAsync("ghost@example.com", _emailTemplate, TestContext.Current.CancellationToken);

        Assert.True(result);
        _emailPublisher.Verify(p => p.PublishAsync(It.IsAny<SendEmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>A failure persisting the reset token is swallowed (enumeration-safe reply) and no mail is sent.</summary>
    [Fact]
    public async Task ForgotPasswordAsync_ReturnsTrueAndSendsNothing_WhenPersistingTheResetTokenFails()
    {
        var account = ActiveAccount();
        _accountRepo.Setup(r => r.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(account);
        _accountRepo.Setup(r => r.UpdateResetPasswordTokenAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db down"));

        bool result = await CreateSut().ForgotPasswordAsync(account.Email.Value, _emailTemplate, TestContext.Current.CancellationToken);

        Assert.True(result);
        _emailPublisher.Verify(p => p.PublishAsync(It.IsAny<SendEmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>A new password that breaks the complexity rule is refused before hashing, leaving the reset token usable.</summary>
    [Fact]
    public async Task ResetPasswordAsync_ReturnsPolicyViolation_AndDoesNotHash_WhenPasswordIsTooWeak()
    {
        string token = GuidToken.Generate(Now);
        var account = ActiveAccount(resetPasswordToken: token);
        _rsa.Setup(r => r.Decrypt("enc")).Returns(token);
        _accountRepo.Setup(r => r.FindByResetPasswordTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(account);

        (ServiceResult result, _) = await CreateSut().ResetPasswordAsync("enc", "weak", TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        _passwordService.Verify(p => p.HashPassword(It.IsAny<string>()), Times.Never);
        _unitOfWork.Verify(u => u.BeginAsync(null, It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>The account vanishing (or being soft-deleted) between the token lookup and the lock refuses the reset.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ResetPasswordAsync_ReturnsInvalid_WhenTheLockedRowIsMissingOrDeleted(bool rowExistsButDeleted)
    {
        string token = GuidToken.Generate(Now);
        var found = ActiveAccount(resetPasswordToken: token);
        _rsa.Setup(r => r.Decrypt("enc")).Returns(token);
        _accountRepo.Setup(r => r.FindByResetPasswordTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(found);
        _passwordService.Setup(p => p.HashPassword(It.IsAny<string>())).Returns("$2a$13$newhash");
        _accountRepo.Setup(r => r.FindByEmailForUpdateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(rowExistsButDeleted ? ActiveAccount(resetPasswordToken: token, deleted: true) : null);

        (ServiceResult result, _) = await CreateSut().ResetPasswordAsync("enc", "NewPass1!", TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        _accountRepo.Verify(r => r.UpdateEntityAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>A reset token consumed by a concurrent reset between the lookup and the lock is rejected (compare-and-swap).</summary>
    [Fact]
    public async Task ResetPasswordAsync_ReturnsInvalid_WhenTheTokenWasConsumedConcurrently()
    {
        string token = GuidToken.Generate(Now);
        var found = ActiveAccount(resetPasswordToken: token);
        var consumed = ActiveAccount(resetPasswordToken: null);
        _rsa.Setup(r => r.Decrypt("enc")).Returns(token);
        _accountRepo.Setup(r => r.FindByResetPasswordTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(found);
        _accountRepo.Setup(r => r.FindByEmailForUpdateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(consumed);
        _passwordService.Setup(p => p.HashPassword(It.IsAny<string>())).Returns("$2a$13$newhash");

        (ServiceResult result, _) = await CreateSut().ResetPasswordAsync("enc", "NewPass1!", TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        _accountRepo.Verify(r => r.UpdateEntityAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>A persistence failure during the reset is rolled back and reported with its own failure code.</summary>
    [Fact]
    public async Task ResetPasswordAsync_RollsBackAndReportsFailure_WhenTheWriteThrows()
    {
        string token = GuidToken.Generate(Now);
        var account = ActiveAccount(resetPasswordToken: token);
        _rsa.Setup(r => r.Decrypt("enc")).Returns(token);
        _accountRepo.Setup(r => r.FindByResetPasswordTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(account);
        _accountRepo.Setup(r => r.FindByEmailForUpdateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(account);
        _passwordService.Setup(p => p.HashPassword(It.IsAny<string>())).Returns("$2a$13$newhash");
        _accountRepo.Setup(r => r.UpdateEntityAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db down"));

        (ServiceResult result, _) = await CreateSut().ResetPasswordAsync("enc", "NewPass1!", TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // -- Email lookups ignore surrounding whitespace and letter case ------------------------

    /// <summary>
    /// The address typed for a login, password reset or confirmation resend is looked up in the
    /// stored (trimmed, lower-case) form, so a mobile autocomplete's trailing space still finds the account.
    /// </summary>
    [Fact]
    public async Task EmailLookups_UseTheTrimmedLowerCaseAddress()
    {
        const string typed = " User@Example.com\t";
        var sut = CreateSut();

        await sut.LoginAsync(typed, "pw", TestContext.Current.CancellationToken);
        await sut.ForgotPasswordAsync(typed, _emailTemplate, TestContext.Current.CancellationToken);
        await sut.ResendConfirmationEmailAsync(typed, _emailTemplate, TestContext.Current.CancellationToken);
        await sut.RegisterAsync(new RegisterAccountRequest { Email = typed, PlainPassword = "weak" }, _emailTemplate, TestContext.Current.CancellationToken);

        _accountRepo.Verify(r => r.FindByEmailForUpdateAsync("user@example.com", It.IsAny<CancellationToken>()), Times.Once);
        _accountRepo.Verify(r => r.FindByEmailAsync("user@example.com", It.IsAny<CancellationToken>()), Times.Exactly(3));
        _accountRepo.Verify(r => r.FindByEmailAsync(It.Is<string>(e => e != "user@example.com"), It.IsAny<CancellationToken>()), Times.Never);
        _accountRepo.Verify(r => r.FindByEmailForUpdateAsync(It.Is<string>(e => e != "user@example.com"), It.IsAny<CancellationToken>()), Times.Never);
    }

    // -- Private helpers ------------------------------------------------------

    /// <summary>A token minted 25 hours before <see cref="Now"/>, past its 24h validity window.</summary>
    private static string BuildExpiredToken() => GuidToken.Generate(Now.AddHours(-25));

    // -- Login transaction, reset/confirmation flows, account messages and logging -------------

    /// <summary>Asserts exactly one entry at <paramref name="level"/> reads <paramref name="message"/> and carries <paramref name="thrown"/> (or none).</summary>
    private void AssertLogged(LogLevel level, string message, Exception? thrown = null)
    {
        FakeLogRecord record = Assert.Single(_logger.Collector.GetSnapshot(), r => r.Level == level && r.Message == message);
        Assert.Same(thrown, record.Exception);
    }

    /// <summary>Arranges a confirmed account whose live reset token <c>"enc"</c> decrypts to; returns the account.</summary>
    private Account GivenAResettableAccount(bool locked = false)
    {
        string token = GuidToken.Generate(Now);
        Account account = ActiveAccount(locked: locked, loginAttempt: locked ? 5 : 0, resetPasswordToken: token);
        _rsa.Setup(r => r.Decrypt("enc")).Returns(token);
        _accountRepo.Setup(r => r.FindByResetPasswordTokenAsync(token, It.IsAny<CancellationToken>())).ReturnsAsync(account);
        _accountRepo.Setup(r => r.FindByEmailForUpdateAsync(account.Email.Value, It.IsAny<CancellationToken>())).ReturnsAsync(account);
        _passwordService.Setup(p => p.HashPassword(It.IsAny<string>())).Returns("$2a$13$newhash");
        return account;
    }

    /// <summary>A successful login runs in its own transaction, resets the failed-attempt counter and persists it.</summary>
    [Fact]
    public async Task LoginAsync_ResetsAndPersistsTheFailedAttemptCounter_InACommittedTransaction()
    {
        Account account = ActiveAccount(loginAttempt: 3);
        _accountRepo.Setup(r => r.FindByEmailForUpdateAsync(account.Email.Value, It.IsAny<CancellationToken>())).ReturnsAsync(account);
        _passwordService.Setup(p => p.VerifyPassword("Right1234!", account.HashedPassword)).Returns(true);

        LoginResult result = await CreateSut().LoginAsync(account.Email.Value, "Right1234!", TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        _accountRepo.Verify(r => r.UpdateEntityAsync(It.Is<Account>(a => a.LoginAttempt == 0), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.BeginAsync(null, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>An unknown address commits its equalizing (0-row) write, so it costs what a wrong password costs.</summary>
    [Fact]
    public async Task LoginAsync_CommitsTheEqualizingWrite_ForAnUnknownAddress()
    {
        await CreateSut().LoginAsync("ghost@example.com", "any", TestContext.Current.CancellationToken);

        _accountRepo.Verify(r => r.UpdateMessageAsync("ghost@example.com", null, Now, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>A refusal after the right password (deleted, unconfirmed, terms not accepted) writes nothing and is rolled back.</summary>
    [Theory]
    [InlineData(true, true, true)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    public async Task LoginAsync_RollsBack_ARefusalAfterTheRightPassword(bool deleted, bool emailConfirmed, bool agreedServiceTerms)
    {
        Account account = ActiveAccount(deleted: deleted, emailConfirmed: emailConfirmed, agreedServiceTerms: agreedServiceTerms);
        _accountRepo.Setup(r => r.FindByEmailForUpdateAsync(account.Email.Value, It.IsAny<CancellationToken>())).ReturnsAsync(account);
        _passwordService.Setup(p => p.VerifyPassword("Right1234!", account.HashedPassword)).Returns(true);

        LoginResult result = await CreateSut().LoginAsync(account.Email.Value, "Right1234!", TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>An undecryptable registration link is logged as a Warning with the decryption failure.</summary>
    [Fact]
    public async Task ValidateRegistrationTokenAsync_LogsAWarning_WhenTheTokenDoesNotDecrypt()
    {
        var thrown = new FormatException("bad");
        _rsa.Setup(r => r.Decrypt("enc")).Throws(thrown);

        ConfirmEmailResult result = await CreateSut().ValidateRegistrationTokenAsync("enc", TestContext.Current.CancellationToken);

        Assert.True(result.InvalidToken);
        AssertLogged(LogLevel.Warning, "Failed to decrypt registration token", thrown);
    }

    /// <summary>A dead registration link is logged as a Warning.</summary>
    [Fact]
    public async Task ValidateRegistrationTokenAsync_LogsAWarning_WhenTheLinkIsDead()
    {
        _rsa.Setup(r => r.Decrypt("enc")).Returns(BuildExpiredToken());

        await CreateSut().ValidateRegistrationTokenAsync("enc", TestContext.Current.CancellationToken);

        AssertLogged(LogLevel.Warning, "Email confirmation link rejected: invalid or expired token");
    }

    /// <summary>A failure of the timing-equalization work for an unknown address is logged as a Warning.</summary>
    [Fact]
    public async Task ForgotPasswordAsync_LogsAWarning_WhenTheEqualizationWorkThrows()
    {
        var thrown = new InvalidOperationException("rsa down");
        _rsa.Setup(r => r.Encrypt(It.IsAny<string>())).Throws(thrown);

        await CreateSut().ForgotPasswordAsync("ghost@example.com", _emailTemplate, TestContext.Current.CancellationToken);

        AssertLogged(LogLevel.Warning, "ForgotPassword equalization failed for ghost@example.com", thrown);
    }

    /// <summary>A failure while issuing the reset for a real account is logged as a Warning (the reply stays the same).</summary>
    [Fact]
    public async Task ForgotPasswordAsync_LogsAWarning_WhenIssuingTheResetFails()
    {
        Account account = ActiveAccount();
        _accountRepo.Setup(r => r.FindByEmailAsync(account.Email.Value, It.IsAny<CancellationToken>())).ReturnsAsync(account);
        var thrown = new InvalidOperationException("db down");
        _accountRepo.Setup(r => r.UpdateResetPasswordTokenAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ThrowsAsync(thrown);

        await CreateSut().ForgotPasswordAsync(account.Email.Value, _emailTemplate, TestContext.Current.CancellationToken);

        AssertLogged(LogLevel.Warning, $"ForgotPassword failed for {account.Email.Value}", thrown);
    }

    /// <summary>
    /// The reset mail is built for the configured domain, and the account then records whether it went out ("Email has
    /// been sent…" or "Fail to mail sent").
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ForgotPasswordAsync_BuildsTheMailForTheDomain_AndRecordsWhetherItWentOut(bool published)
    {
        Account account = ActiveAccount();
        _accountRepo.Setup(r => r.FindByEmailAsync(account.Email.Value, It.IsAny<CancellationToken>())).ReturnsAsync(account);
        _rsa.Setup(r => r.Encrypt(It.IsAny<string>())).Returns("enc-token");
        if (published) SetupMailSuccess(); else SetupMailFailure();

        await CreateSut().ForgotPasswordAsync(account.Email.Value, _emailTemplate, TestContext.Current.CancellationToken);

        _mailClient.Verify(m => m.GetMailResetPasswordBody("enc-token", "title", "c0", "c1", "https://example.com"), Times.Once);
        string expected = published ? EnumHelper.GetDescription(AccountMessage.ResetPasswordMail) : "Fail to mail sent";
        _accountRepo.Verify(r => r.UpdateMessageAsync(account.Email.Value, expected, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>An undecryptable reset link is logged as a Warning and reported as dead.</summary>
    [Fact]
    public async Task ValidateResetPasswordTokenAsync_LogsAWarning_WhenTheTokenDoesNotDecrypt()
    {
        var thrown = new FormatException("bad");
        _rsa.Setup(r => r.Decrypt("enc")).Throws(thrown);

        await CreateSut().ValidateResetPasswordTokenAsync("enc", TestContext.Current.CancellationToken);

        AssertLogged(LogLevel.Warning, "Failed to decrypt reset password token", thrown);
    }

    /// <summary>A reset link that matches no account is dead (and logged as such).</summary>
    [Fact]
    public async Task ValidateResetPasswordTokenAsync_ReportsADeadLink_WhenNoAccountHoldsTheToken()
    {
        _rsa.Setup(r => r.Decrypt("enc")).Returns(GuidToken.Generate(Now));

        ResetPasswordValidationResult result = await CreateSut().ValidateResetPasswordTokenAsync("enc", TestContext.Current.CancellationToken);

        Assert.True(result.FailToReset);
        AssertLogged(LogLevel.Warning, "Password reset link rejected: invalid or expired token");
    }

    /// <summary>An undecryptable reset is refused as "reset-password-invalid" and logged.</summary>
    [Fact]
    public async Task ResetPasswordAsync_RefusesAndLogs_WhenTheTokenDoesNotDecrypt()
    {
        var thrown = new FormatException("bad");
        _rsa.Setup(r => r.Decrypt("enc")).Throws(thrown);

        (ServiceResult result, _) = await CreateSut().ResetPasswordAsync("enc", "NewPass1!", TestContext.Current.CancellationToken);

        Assert.Equal("reset-password-invalid", result.ErrorKey);
        AssertLogged(LogLevel.Warning, "Failed to decrypt reset password token", thrown);
    }

    /// <summary>A live token held by a deleted or unconfirmed account is refused as "reset-password-invalid", before any hashing.</summary>
    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task ResetPasswordAsync_RefusesALiveTokenOfADeletedOrUnconfirmedAccount(bool deleted, bool emailConfirmed)
    {
        string token = GuidToken.Generate(Now);
        _rsa.Setup(r => r.Decrypt("enc")).Returns(token);
        _accountRepo.Setup(r => r.FindByResetPasswordTokenAsync(token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActiveAccount(deleted: deleted, emailConfirmed: emailConfirmed, resetPasswordToken: token));

        (ServiceResult result, _) = await CreateSut().ResetPasswordAsync("enc", "NewPass1!", TestContext.Current.CancellationToken);

        Assert.Equal("reset-password-invalid", result.ErrorKey);
        _passwordService.Verify(p => p.HashPassword(It.IsAny<string>()), Times.Never);
    }

    /// <summary>A weak new password is the password-policy Validation error.</summary>
    [Fact]
    public async Task ResetPasswordAsync_ReturnsThePasswordPolicyError_ForAWeakPassword()
    {
        GivenAResettableAccount();

        (ServiceResult result, _) = await CreateSut().ResetPasswordAsync("enc", "weak", TestContext.Current.CancellationToken);

        Assert.Equal(ServiceErrorType.Validation, result.ErrorType);
        Assert.Equal("Account.PasswordPolicy", result.ErrorCode);
        Assert.Equal(PasswordPolicy.ViolationMessage, result.ErrorKey);
    }

    /// <summary>A successful reset records "Success to reset password" on the account, in a committed transaction.</summary>
    [Fact]
    public async Task ResetPasswordAsync_RecordsTheSuccessMessage_InACommittedTransaction()
    {
        GivenAResettableAccount();

        (ServiceResult result, _) = await CreateSut().ResetPasswordAsync("enc", "NewPass1!", TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        _accountRepo.Verify(r => r.UpdateEntityAsync(It.Is<Account>(a => a.Message == "Success to reset password"), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.BeginAsync(null, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>A row that vanished under the lock is refused as "reset-password-invalid" and logged with the address.</summary>
    [Fact]
    public async Task ResetPasswordAsync_RefusesAndLogs_WhenTheRowVanishedUnderTheLock()
    {
        Account account = GivenAResettableAccount();
        _accountRepo.Setup(r => r.FindByEmailForUpdateAsync(account.Email.Value, It.IsAny<CancellationToken>())).ReturnsAsync((Account?)null);

        (ServiceResult result, _) = await CreateSut().ResetPasswordAsync("enc", "NewPass1!", TestContext.Current.CancellationToken);

        Assert.Equal("reset-password-invalid", result.ErrorKey);
        AssertLogged(LogLevel.Warning, $"Password reset rejected: account {account.Email.Value} no longer exists or is deleted");
    }

    /// <summary>A token consumed concurrently is refused as "reset-password-invalid", rolled back, and logged with the address.</summary>
    [Fact]
    public async Task ResetPasswordAsync_RefusesRollsBackAndLogs_WhenTheTokenWasConsumedUnderTheLock()
    {
        Account found = GivenAResettableAccount();
        _accountRepo.Setup(r => r.FindByEmailForUpdateAsync(found.Email.Value, It.IsAny<CancellationToken>())).ReturnsAsync(ActiveAccount(resetPasswordToken: null));

        (ServiceResult result, _) = await CreateSut().ResetPasswordAsync("enc", "NewPass1!", TestContext.Current.CancellationToken);

        Assert.Equal("reset-password-invalid", result.ErrorKey);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        AssertLogged(LogLevel.Warning, $"Password reset rejected: invalid or expired token for {found.Email.Value}");
    }

    /// <summary>A reset whose write throws is logged with the exception and reported with its own message.</summary>
    [Fact]
    public async Task ResetPasswordAsync_LogsAndReportsResetPasswordFailed_WhenTheWriteThrows()
    {
        Account account = GivenAResettableAccount();
        var thrown = new InvalidOperationException("db down");
        _accountRepo.Setup(r => r.UpdateEntityAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>())).ThrowsAsync(thrown);

        (ServiceResult result, _) = await CreateSut().ResetPasswordAsync("enc", "NewPass1!", TestContext.Current.CancellationToken);

        Assert.Equal(ServiceErrorType.Failure, result.ErrorType);
        Assert.Equal("Account.ResetPasswordFailed", result.ErrorCode);
        Assert.Equal("Error occurred while processing about reset password", result.ErrorKey);
        AssertLogged(LogLevel.Error, $"Failed to reset password for {account.Email.Value}", thrown);
    }

    /// <summary>Arranges a resend for an unconfirmed account with a live token; returns the account.</summary>
    private Account GivenAResendableAccount()
    {
        Account account = ActiveAccount(emailConfirmed: false, registrationToken: GuidToken.Generate(Now));
        SetupUnconfirmedAccountForRegister(account, lockedRead: account);
        _rsa.Setup(r => r.Encrypt(It.IsAny<string>())).Returns("enc-token");
        return account;
    }

    /// <summary>A registration token that cannot be persisted before the mail is logged with the exception and the address.</summary>
    [Fact]
    public async Task RegisterAsync_LogsTheFailure_WhenThePostCreateTokenWriteFails()
    {
        _passwordService.Setup(p => p.HashPassword(It.IsAny<string>())).Returns("$2a$13$hashed");
        var thrown = new InvalidOperationException("db down");
        _accountRepo.Setup(r => r.UpdateRegistrationTokenAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ThrowsAsync(thrown);

        await CreateSut().RegisterAsync(NewRegistration(), _emailTemplate, TestContext.Current.CancellationToken);

        AssertLogged(LogLevel.Error, "Failed to persist registration token for new@example.com", thrown);
    }

    /// <summary>The confirmation mail is built for the configured domain.</summary>
    [Fact]
    public async Task ResendConfirmationEmailAsync_BuildsTheMailForTheDomain()
    {
        Account account = GivenAResendableAccount();
        SetupMailSuccess();

        await CreateSut().ResendConfirmationEmailAsync(account.Email.Value, _emailTemplate, TestContext.Current.CancellationToken);

        _mailClient.Verify(m => m.GetMailConfirmationBody("enc-token", "title", "c0", "c1", "https://example.com"), Times.Once);
    }

    /// <summary>A confirmation mail body that cannot be built is logged and reported as a send failure.</summary>
    [Fact]
    public async Task ResendConfirmationEmailAsync_LogsAndReportsASendFailure_WhenTheMailBodyCannotBeBuilt()
    {
        Account account = GivenAResendableAccount();
        var thrown = new InvalidOperationException("rsa down");
        _rsa.Setup(r => r.Encrypt(It.IsAny<string>())).Throws(thrown);

        RegisterResult result = await CreateSut().ResendConfirmationEmailAsync(account.Email.Value, _emailTemplate, TestContext.Current.CancellationToken);

        Assert.Equal("Error occurred while processing about sending account authentication mail", result.ErrorKey);
        AssertLogged(LogLevel.Error, $"Failed to build confirmation mail body for {account.Email.Value}", thrown);
    }

    /// <summary>
    /// A confirmation mail that cannot be queued records "Fail to mail sent" on the account, and the queue failure is
    /// logged with the address.
    /// </summary>
    [Fact]
    public async Task ResendConfirmationEmailAsync_RecordsTheSendFailure_AndLogsIt()
    {
        Account account = GivenAResendableAccount();
        SetupMailFailure();

        await CreateSut().ResendConfirmationEmailAsync(account.Email.Value, _emailTemplate, TestContext.Current.CancellationToken);

        _accountRepo.Verify(r => r.UpdateMessageAsync(account.Email.Value, "Fail to mail sent", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
        FakeLogRecord error = Assert.Single(_logger.Collector.GetSnapshot(), r => r.Level == LogLevel.Error);
        Assert.Equal($"Failed to publish email message for {account.Email.Value}", error.Message);
        Assert.IsType<InvalidOperationException>(error.Exception);
    }

    /// <summary>Failing to record the send failure as well is logged as a Warning.</summary>
    [Fact]
    public async Task ResendConfirmationEmailAsync_LogsAWarning_WhenRecordingTheSendFailureAlsoFails()
    {
        Account account = GivenAResendableAccount();
        SetupMailFailure();
        var thrown = new InvalidOperationException("db down");
        _accountRepo.Setup(r => r.UpdateMessageAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ThrowsAsync(thrown);

        await CreateSut().ResendConfirmationEmailAsync(account.Email.Value, _emailTemplate, TestContext.Current.CancellationToken);

        AssertLogged(LogLevel.Warning, $"Failed to update account message after mail send failure for {account.Email.Value}", thrown);
    }

    /// <summary>A queued confirmation mail records "verify your mail" on the account, and the publish is logged with an empty correlation id outside a request.</summary>
    [Fact]
    public async Task ResendConfirmationEmailAsync_RecordsVerifyEmail_AndLogsThePublish()
    {
        Account account = GivenAResendableAccount();
        SetupMailSuccess();
        Activity.Current = null;

        await CreateSut().ResendConfirmationEmailAsync(account.Email.Value, _emailTemplate, TestContext.Current.CancellationToken);

        _accountRepo.Verify(r => r.UpdateMessageAsync(account.Email.Value, "User already created, please verify your given mail Id", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
        AssertLogged(LogLevel.Information, $"Publishing email to {account.Email.Value}. CorrelationId=");
        _emailPublisher.Verify(p => p.PublishAsync(It.Is<SendEmailMessage>(m => m.CorrelationId == ""), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>A failing final status write after a queued mail is logged with the exception and the address.</summary>
    [Fact]
    public async Task ResendConfirmationEmailAsync_LogsTheFailure_WhenTheFinalStatusWriteFails()
    {
        Account account = GivenAResendableAccount();
        SetupMailSuccess();
        var thrown = new InvalidOperationException("db down");
        _accountRepo.Setup(r => r.UpdateMessageAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ThrowsAsync(thrown);

        await CreateSut().ResendConfirmationEmailAsync(account.Email.Value, _emailTemplate, TestContext.Current.CancellationToken);

        AssertLogged(LogLevel.Error, $"Failed to update account status for {account.Email.Value}", thrown);
    }
}
