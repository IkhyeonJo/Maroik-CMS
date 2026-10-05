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
/// Unit tests for <see cref="ManagementAccountService"/>.
/// All repository and password-service dependencies are replaced with Moq mocks.
/// Covers admin-side account management: list retrieval, create, update,
/// password reset, lock/unlock, and delete operations.
/// </summary>
public class ManagementAccountServiceTests
{
    /// <summary>The signed-in administrator performing the change (recorded in the audit log).</summary>
    private const string Actor = "admin@example.com";

    /// <summary>Mock <c>IAccountRepository</c> injected into the system under test.</summary>
    private readonly Mock<IAccountRepository> _accountRepo = new();
    /// <summary>Mock <c>IPasswordService</c> injected into the system under test.</summary>
    private readonly Mock<IPasswordService> _passwordService = new();
    /// <summary>Mock <c>IUnitOfWork</c> injected into the system under test.</summary>
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    /// <summary>Captures the log entries the system under test writes.</summary>
    private readonly FakeLogger<ManagementAccountService> _logger = new();

    /// <summary>The fixed "current time" of these tests.</summary>
    private static readonly DateTime Now = new(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);
    /// <summary>The clock the service under test reads, stopped at <see cref="Now"/>.</summary>
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(Now));

    /// <summary>The service under test over the mocked dependencies.</summary>
    private ManagementAccountService CreateSut() => new(
        _accountRepo.Object,
        _passwordService.Object,
        _unitOfWork.Object,
        _logger,
        _time);

    // -- Helpers --------------------------------------------------------------

    /// <summary>A persisted account whose state is set by the arguments.</summary>
    private static Account ActiveAccount(
        string email = "user@example.com",
        bool locked = false,
        long loginAttempt = 0) =>
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

    // -- GetAllAccountsAsync --------------------------------------------------

    /// <summary>Verifies that <c>GetAllAccountsAsync</c> delegates to repository.</summary>
    [Fact]
    public async Task GetAllAccountsAsync_DelegatesToRepository()
    {
        _accountRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([ActiveAccount()]);
        var sut = CreateSut();

        List<AdminAccountResponse> result = await sut.GetAllAccountsAsync(TestContext.Current.CancellationToken);

        Assert.Single(result);
    }

    // -- GetAccountByEmailAsync -----------------------------------------------

    /// <summary>Verifies that <c>GetAccountByEmailAsync</c> returns null when not found.</summary>
    [Fact]
    public async Task GetAccountByEmailAsync_ReturnsNull_WhenNotFound()
    {
        _accountRepo.Setup(r => r.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Account?)null);
        var sut = CreateSut();

        AccountResponse? result = await sut.GetAccountByEmailAsync("ghost@example.com", TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    /// <summary>
    /// The single-account lookup behind the admin edit form must return the plain
    /// <see cref="AccountResponse"/> — never the <see cref="AdminAccountResponse"/> that carries the
    /// password hash and the registration / reset tokens, which the form does not read.
    /// </summary>
    [Fact]
    public async Task GetAccountByEmailAsync_ReturnsPlainAccountResponse_WithoutSecretColumns()
    {
        var account = ActiveAccount();
        _accountRepo.Setup(r => r.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(account);
        var sut = CreateSut();

        AccountResponse? result = await sut.GetAccountByEmailAsync(account.Email.Value, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(account.Email.Value, result.Email);
        Assert.IsNotType<AdminAccountResponse>(result);
    }

    // -- CreateAccountAsync ---------------------------------------------------

    /// <summary>Verifies that <c>CreateAccountAsync</c> returns fail when email already exists.</summary>
    [Fact]
    public async Task CreateAccountAsync_ReturnsFail_WhenEmailAlreadyExists()
    {
        _accountRepo.Setup(r => r.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(ActiveAccount("existing@example.com"));
        var sut = CreateSut();

        ServiceResult result = await sut.CreateAccountAsync(new AdminCreateAccountRequest
        {
            Email = "existing@example.com",
            PlainPassword = "plain"
        }, Actor, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains("already been created", result.ErrorKey, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Verifies that <c>CreateAccountAsync</c> hashes password and creates account when email is new.</summary>
    [Fact]
    public async Task CreateAccountAsync_HashesPasswordAndCreatesAccount_WhenEmailIsNew()
    {
        _accountRepo.Setup(r => r.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Account?)null);
        _passwordService.Setup(p => p.HashPassword("Plain1234!")).Returns("$2a$13$hashedvalue");
        _accountRepo.Setup(r => r.CreateAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var sut = CreateSut();

        ServiceResult result = await sut.CreateAccountAsync(new AdminCreateAccountRequest
        {
            Email = "new@example.com",
            PlainPassword = "Plain1234!",
            Nickname = "NewUser",
            EmailConfirmed = true
        }, Actor, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        _accountRepo.Verify(r => r.CreateAsync(It.Is<Account>(a => a.HashedPassword == "$2a$13$hashedvalue"), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>An administrator may create an account under a reserved nickname (e.g. a second staff account).</summary>
    [Fact]
    public async Task CreateAccountAsync_AllowsReservedNickname_ForAdministrator()
    {
        _accountRepo.Setup(r => r.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Account?)null);
        _passwordService.Setup(p => p.HashPassword(It.IsAny<string>())).Returns("$2a$13$hashed");
        _accountRepo.Setup(r => r.CreateAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var sut = CreateSut();

        ServiceResult result = await sut.CreateAccountAsync(new AdminCreateAccountRequest
        {
            Email = "staff@example.com",
            PlainPassword = "Plain1234!",
            Nickname = "Admin",
            Role = Role.Admin,
            EmailConfirmed = true
        }, Actor, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        _accountRepo.Verify(r => r.CreateAsync(It.Is<Account>(a => a.Nickname == "Admin"), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>An administrator-created nickname is still normalized and still rejected when it hides invisible characters.</summary>
    [Fact]
    public async Task CreateAccountAsync_ReturnsFail_WhenNicknameHasInvisibleCharacters()
    {
        _accountRepo.Setup(r => r.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Account?)null);
        var sut = CreateSut();

        ServiceResult result = await sut.CreateAccountAsync(new AdminCreateAccountRequest
        {
            Email = "new@example.com",
            PlainPassword = "Plain1234!",
            Nickname = "Bo\u200Bb"
        }, Actor, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal("Account.NicknameInvalidCharacters", result.ErrorCode);
        _passwordService.Verify(p => p.HashPassword(It.IsAny<string>()), Times.Never);
    }

    /// <summary>A nickname that differs from an existing one only by case is reported as a conflict, before hashing.</summary>
    [Fact]
    public async Task CreateAccountAsync_ReturnsNicknameConflict_WhenNicknameExistsIgnoringCase()
    {
        _accountRepo.Setup(r => r.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Account?)null);
        _accountRepo.Setup(r => r.NicknameExistsIgnoreCaseAsync("bob", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var sut = CreateSut();

        ServiceResult result = await sut.CreateAccountAsync(new AdminCreateAccountRequest
        {
            Email = "new@example.com",
            PlainPassword = "Plain1234!",
            Nickname = " bob "
        }, Actor, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(ServiceErrorType.Conflict, result.ErrorType);
        Assert.Equal("Account.NicknameAlreadyExists", result.ErrorCode);
        Assert.Contains("bob", result.ErrorArgs);
        _passwordService.Verify(p => p.HashPassword(It.IsAny<string>()), Times.Never);
    }

    /// <summary>
    /// Regression test: two concurrent admin creates for the same not-yet-registered nickname can
    /// both pass the prior <c>FindByEmailAsync</c> check (it only checks email) and race on
    /// <c>CreateAsync</c>. The nickname-unique-constraint violation must be classified as a
    /// nickname conflict, not fall through to the generic "Input is invalid" failure.
    /// </summary>
    [Fact]
    public async Task CreateAccountAsync_ReturnsNicknameConflict_WhenNicknameUniqueViolationRaces()
    {
        _accountRepo.Setup(r => r.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Account?)null);
        _passwordService.Setup(p => p.HashPassword(It.IsAny<string>())).Returns("$2a$13$hashed");
        // Simulate a unique constraint violation (Npgsql error code 23505) on the Nickname constraint specifically.
        _accountRepo.Setup(r => r.CreateAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("duplicate key", new Exception("23505: duplicate key value violates unique constraint \"Account_Nickname_unique\"")));
        var sut = CreateSut();

        ServiceResult result = await sut.CreateAccountAsync(new AdminCreateAccountRequest
        {
            Email = "new@example.com",
            PlainPassword = "Plain1234!",
            Nickname = "TakenNick"
        }, Actor, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(ServiceErrorType.Conflict, result.ErrorType);
        Assert.Equal("Account.NicknameAlreadyExists", result.ErrorCode);
        Assert.Contains("Nickname", result.ErrorKey);
        Assert.Contains("TakenNick", result.ErrorArgs);
    }

    /// <summary>
    /// The race between "bob" and "Bob" is caught by the case-insensitive unique index
    /// (<c>Account_unique_index_0</c>) and must be reported as a nickname conflict too.
    /// </summary>
    [Fact]
    public async Task CreateAccountAsync_ReturnsNicknameConflict_WhenCaseInsensitiveIndexRaces()
    {
        _accountRepo.Setup(r => r.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Account?)null);
        _passwordService.Setup(p => p.HashPassword(It.IsAny<string>())).Returns("$2a$13$hashed");
        _accountRepo.Setup(r => r.CreateAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("duplicate key", new Exception("23505: duplicate key value violates unique constraint \"Account_unique_index_0\"")));
        var sut = CreateSut();

        ServiceResult result = await sut.CreateAccountAsync(new AdminCreateAccountRequest
        {
            Email = "new@example.com",
            PlainPassword = "Plain1234!",
            Nickname = "bob"
        }, Actor, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(ServiceErrorType.Conflict, result.ErrorType);
        Assert.Equal("Account.NicknameAlreadyExists", result.ErrorCode);
        Assert.Contains("bob", result.ErrorArgs);
    }

    /// <summary>
    /// Regression test: the same race on the Email primary key (<c>Account_pk</c>) must be reported
    /// as an email conflict, not incorrectly blamed on the nickname.
    /// </summary>
    [Fact]
    public async Task CreateAccountAsync_ReturnsEmailAlreadyExists_WhenEmailPrimaryKeyViolationRaces()
    {
        _accountRepo.Setup(r => r.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Account?)null);
        _passwordService.Setup(p => p.HashPassword(It.IsAny<string>())).Returns("$2a$13$hashed");
        // Simulate a unique constraint violation on the Email primary key, not the Nickname constraint.
        _accountRepo.Setup(r => r.CreateAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("duplicate key", new Exception("23505: duplicate key value violates unique constraint \"Account_pk\"")));
        var sut = CreateSut();

        ServiceResult result = await sut.CreateAccountAsync(new AdminCreateAccountRequest
        {
            Email = "raced@example.com",
            PlainPassword = "Plain1234!",
            Nickname = "SomeNick"
        }, Actor, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(ServiceErrorType.Conflict, result.ErrorType);
        Assert.Equal("Account.AlreadyExists", result.ErrorCode);
        Assert.DoesNotContain("Nickname", result.ErrorKey);
    }

    // -- UpdateAccountAsync ---------------------------------------------------

    /// <summary>Verifies that <c>UpdateAccountAsync</c> returns fail when account not found.</summary>
    [Fact]
    public async Task UpdateAccountAsync_ReturnsFail_WhenAccountNotFound()
    {
        _accountRepo.Setup(r => r.FindByEmailForUpdateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Account?)null);
        var sut = CreateSut();

        ServiceResult result = await sut.UpdateAccountAsync(
            new AdminUpdateAccountRequest { Email = "ghost@example.com" }, null, Actor, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains("wrong", result.ErrorKey, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Verifies that <c>UpdateAccountAsync</c> keeps existing password when new password is empty.</summary>
    [Fact]
    public async Task UpdateAccountAsync_KeepsExistingPassword_WhenNewPasswordIsEmpty()
    {
        var account = ActiveAccount();
        _accountRepo.Setup(r => r.FindByEmailForUpdateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(account);
        _accountRepo.Setup(r => r.UpdateEntityAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var sut = CreateSut();

        ServiceResult result = await sut.UpdateAccountAsync(
            new AdminUpdateAccountRequest { Email = account.Email.Value, Role = "User" }, null, Actor, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        _accountRepo.Verify(r => r.UpdateEntityAsync(
            It.Is<Account>(a => a.HashedPassword == account.HashedPassword), It.IsAny<CancellationToken>()), Times.Once);
        _passwordService.Verify(p => p.HashPassword(It.IsAny<string>()), Times.Never);
    }

    /// <summary>Verifies that <c>UpdateAccountAsync</c> hashes new password when new password provided.</summary>
    [Fact]
    public async Task UpdateAccountAsync_HashesNewPassword_WhenNewPasswordProvided()
    {
        var account = ActiveAccount();
        _accountRepo.Setup(r => r.FindByEmailForUpdateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(account);
        _passwordService.Setup(p => p.HashPassword("NewPass1!")).Returns("$2a$13$newhash");
        _accountRepo.Setup(r => r.UpdateEntityAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var sut = CreateSut();

        ServiceResult result = await sut.UpdateAccountAsync(
            new AdminUpdateAccountRequest { Email = account.Email.Value, Role = "User" }, "NewPass1!", Actor, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        _accountRepo.Verify(r => r.UpdateEntityAsync(
            It.Is<Account>(a => a.HashedPassword == "$2a$13$newhash"), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>An admin-set password forces the account to choose its own new one at next login.</summary>
    [Fact]
    public async Task UpdateAccountAsync_ForcesPasswordChange_WhenNewPasswordProvided()
    {
        var account = ActiveAccount();
        string originalStamp = account.SecurityStamp;
        _accountRepo.Setup(r => r.FindByEmailForUpdateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(account);
        _passwordService.Setup(p => p.HashPassword("NewPass1!")).Returns("$2a$13$newhash");
        _accountRepo.Setup(r => r.UpdateEntityAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var sut = CreateSut();

        await sut.UpdateAccountAsync(
            new AdminUpdateAccountRequest { Email = account.Email.Value, Role = "User" }, "NewPass1!", Actor, TestContext.Current.CancellationToken);

        Assert.True(account.MustChangePassword);
        Assert.NotEqual(originalStamp, account.SecurityStamp);
    }

    /// <summary>Verifies that <c>UpdateAccountAsync</c> resets login attempt when unlocking.</summary>
    [Fact]
    public async Task UpdateAccountAsync_ResetsLoginAttempt_WhenUnlocking()
    {
        var account = ActiveAccount(locked: true, loginAttempt: 5);
        _accountRepo.Setup(r => r.FindByEmailForUpdateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(account);
        _accountRepo.Setup(r => r.UpdateEntityAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var sut = CreateSut();

        ServiceResult result = await sut.UpdateAccountAsync(
            new AdminUpdateAccountRequest { Email = account.Email.Value, Locked = false }, null, Actor, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        _accountRepo.Verify(r => r.UpdateEntityAsync(
            It.Is<Account>(a => a.Locked == false && a.LoginAttempt == 0), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>A persisted account in the opposite state of every admin toggle: deleted, unconfirmed, terms not accepted.</summary>
    private static Account DeletedUnconfirmedAccount() =>
        Account.Reconstitute("user@example.com", "$2a$13$placeholder", "User", null, Role.User, "UTC", null, false, 0,
            false, false, "reg-token", null, DateTime.UtcNow, DateTime.UtcNow, null, true, "stamp", false);

    /// <summary>Every admin toggle is applied in both directions: role, time zone, confirmation, terms, deletion and message.</summary>
    [Fact]
    public async Task UpdateAccountAsync_AppliesEveryAdminChange()
    {
        Account account = DeletedUnconfirmedAccount();
        _accountRepo.Setup(r => r.FindByEmailForUpdateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(account);

        ServiceResult result = await CreateSut().UpdateAccountAsync(new AdminUpdateAccountRequest
        {
            Email = "user@example.com", Role = Role.Admin, TimeZoneIanaId = "Asia/Seoul",
            EmailConfirmed = true, AgreedServiceTerms = true, Deleted = false, Message = "note"
        }, null, Actor, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(Role.Admin, account.Role.Value);
        Assert.Equal("Asia/Seoul", account.TimeZone.Value);
        Assert.True(account.EmailConfirmed);
        Assert.True(account.AgreedServiceTerms);
        Assert.False(account.Deleted);
        Assert.Equal("note", account.Message);

        result = await CreateSut().UpdateAccountAsync(new AdminUpdateAccountRequest
        {
            Email = "user@example.com", EmailConfirmed = false, AgreedServiceTerms = false, Deleted = true
        }, null, Actor, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(Role.Admin, account.Role.Value); // a null role keeps the current one
        Assert.Equal("Asia/Seoul", account.TimeZone.Value); // so does a null time zone
        Assert.False(account.EmailConfirmed);
        Assert.False(account.AgreedServiceTerms);
        Assert.True(account.Deleted);
        Assert.Null(account.Message);
    }

    /// <summary>An account that stays locked keeps its failed-login count; the admin path cannot set it.</summary>
    [Fact]
    public async Task UpdateAccountAsync_KeepsTheLoginAttemptCount_WhenTheAccountStaysLocked()
    {
        Account account = ActiveAccount(locked: true, loginAttempt: 4);
        _accountRepo.Setup(r => r.FindByEmailForUpdateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(account);

        await CreateSut().UpdateAccountAsync(
            new AdminUpdateAccountRequest { Email = account.Email.Value, Locked = true, Message = "spam" }, null, Actor, TestContext.Current.CancellationToken);

        Assert.True(account.Locked);
        Assert.Equal(4, account.LoginAttempt);
        Assert.Equal("spam", account.Message);
    }

    /// <summary>An unlocked account the admin locks keeps its count and stores the admin's message.</summary>
    [Fact]
    public async Task UpdateAccountAsync_LocksAnUnlockedAccount()
    {
        Account account = ActiveAccount(locked: false, loginAttempt: 2);
        _accountRepo.Setup(r => r.FindByEmailForUpdateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(account);

        await CreateSut().UpdateAccountAsync(
            new AdminUpdateAccountRequest { Email = account.Email.Value, Locked = true, Message = "spam" }, null, Actor, TestContext.Current.CancellationToken);

        Assert.True(account.Locked);
        Assert.Equal(2, account.LoginAttempt);
        Assert.Equal("spam", account.Message);
    }

    /// <summary>
    /// An administrator locking an unlocked account ends its open sessions (new security stamp); re-saving an
    /// account that is already locked — for example one locked by failed logins — leaves its sessions alone.
    /// </summary>
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task UpdateAccountAsync_EndsTheSessions_OnlyWhenTheAdminLocksAnUnlockedAccount(bool alreadyLocked, bool stampReplaced)
    {
        Account account = ActiveAccount(locked: alreadyLocked);
        _accountRepo.Setup(r => r.FindByEmailForUpdateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(account);

        await CreateSut().UpdateAccountAsync(
            new AdminUpdateAccountRequest { Email = account.Email.Value, Locked = true }, null, Actor, TestContext.Current.CancellationToken);

        Assert.True(account.Locked);
        Assert.Equal(stampReplaced, account.SecurityStamp != "stamp");
    }

    /// <summary>An invalid time zone is returned as the domain's validation error and nothing is written.</summary>
    [Fact]
    public async Task UpdateAccountAsync_ReturnsTheDomainValidationError_ForAnUnknownTimeZone()
    {
        _accountRepo.Setup(r => r.FindByEmailForUpdateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(ActiveAccount());

        ServiceResult result = await CreateSut().UpdateAccountAsync(new AdminUpdateAccountRequest { Email = "user@example.com", TimeZoneIanaId = "Not/AZone" }, null, Actor, TestContext.Current.CancellationToken);

        Assert.Equal("TimeZoneId.Invalid", result.ErrorCode);
        _accountRepo.Verify(r => r.UpdateEntityAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // -- DeleteAccountAsync ---------------------------------------------------

    /// <summary>Verifies that <c>DeleteAccountAsync</c> returns fail when account not found.</summary>
    [Fact]
    public async Task DeleteAccountAsync_ReturnsFail_WhenAccountNotFound()
    {
        _accountRepo.Setup(r => r.FindByEmailForUpdateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Account?)null);
        var sut = CreateSut();

        ServiceResult result = await sut.DeleteAccountAsync("ghost@example.com", Actor, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains("find", result.ErrorKey, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Verifies that <c>DeleteAccountAsync</c> sets deleted flag when account found.</summary>
    [Fact]
    public async Task DeleteAccountAsync_SetsDeletedFlag_WhenAccountFound()
    {
        var account = ActiveAccount();
        _accountRepo.Setup(r => r.FindByEmailForUpdateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(account);
        _accountRepo.Setup(r => r.UpdateEntityAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var sut = CreateSut();

        ServiceResult result = await sut.DeleteAccountAsync(account.Email.Value, Actor, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        _accountRepo.Verify(r => r.UpdateEntityAsync(
            It.Is<Account>(a => a.Deleted == true), It.IsAny<CancellationToken>()), Times.Once);
    }

    // -- SearchAccountsAsync ------------------------------------------------------

    /// <summary>The admin grid search is delegated to the repository and its rows are mapped to the admin response.</summary>
    [Fact]
    public async Task SearchAccountsAsync_DelegatesToRepository_AndMapsTheRows()
    {
        _accountRepo.Setup(r => r.SearchAsync("needle", It.IsAny<CancellationToken>())).ReturnsAsync([ActiveAccount("found@example.com")]);

        List<AdminAccountResponse> result = await CreateSut().SearchAccountsAsync("needle", TestContext.Current.CancellationToken);

        Assert.Equal("found@example.com", Assert.Single(result).Email);
    }

    // -- CreateAccountAsync: validation and failures ---------------------------------

    /// <summary>An admin create-account request with the given e-mail, password and nickname.</summary>
    private static AdminCreateAccountRequest NewAccountRequest(string email = "new@example.com", string password = "Plain1234!", string nickname = "NewUser") => new()
    {
        Email = email, PlainPassword = password, Nickname = nickname, EmailConfirmed = true
    };

    /// <summary>An admin-created account still has to meet the password rule; nothing is hashed or stored otherwise.</summary>
    [Fact]
    public async Task CreateAccountAsync_ReturnsPolicyViolation_AndDoesNotHashOrCreate_WhenThePasswordIsTooWeak()
    {
        _accountRepo.Setup(r => r.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Account?)null);

        ServiceResult result = await CreateSut().CreateAccountAsync(NewAccountRequest(password: "weak"), Actor, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal("Account.PasswordPolicy", result.ErrorCode);
        _passwordService.Verify(p => p.HashPassword(It.IsAny<string>()), Times.Never);
        _accountRepo.Verify(r => r.CreateAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>An account role other than Admin or User (e.g. the menu-only Anonymous) is refused before anything is hashed or stored.</summary>
    [Fact]
    public async Task CreateAccountAsync_RejectsARoleOtherThanAdminOrUser()
    {
        _accountRepo.Setup(r => r.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Account?)null);
        AdminCreateAccountRequest request = NewAccountRequest();
        request.Role = Role.Anonymous;

        ServiceResult result = await CreateSut().CreateAccountAsync(request, Actor, TestContext.Current.CancellationToken);

        Assert.Equal("Account.RoleInvalid", result.ErrorCode);
        _passwordService.Verify(p => p.HashPassword(It.IsAny<string>()), Times.Never);
        _accountRepo.Verify(r => r.CreateAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>A malformed address is returned as the domain's validation error and nothing is stored.</summary>
    [Fact]
    public async Task CreateAccountAsync_ReturnsTheDomainValidationError_ForAMalformedEmail()
    {
        _accountRepo.Setup(r => r.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Account?)null);
        _passwordService.Setup(p => p.HashPassword(It.IsAny<string>())).Returns("$2a$13$hashed");

        ServiceResult result = await CreateSut().CreateAccountAsync(NewAccountRequest(email: "not-an-email"), Actor, TestContext.Current.CancellationToken);

        Assert.Equal("Email.Invalid", result.ErrorCode);
        _accountRepo.Verify(r => r.CreateAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>The admin's "email confirmed" checkbox and status message are applied to the created account.</summary>
    [Fact]
    public async Task CreateAccountAsync_AppliesTheConfirmedFlagAndMessage_FromTheRequest()
    {
        _accountRepo.Setup(r => r.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Account?)null);
        _passwordService.Setup(p => p.HashPassword(It.IsAny<string>())).Returns("$2a$13$hashed");
        AdminCreateAccountRequest request = NewAccountRequest();
        request.Message = "created by admin";
        request.EmailConfirmed = true;

        ServiceResult result = await CreateSut().CreateAccountAsync(request, Actor, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        _accountRepo.Verify(r => r.CreateAsync(It.Is<Account>(a => a.EmailConfirmed && a.Message == "created by admin" && a.RegistrationToken == null), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>An unexpected repository failure is caught and reported without leaking internals.</summary>
    [Fact]
    public async Task CreateAccountAsync_ReportsCreateFailed_WhenTheRepositoryThrows()
    {
        _accountRepo.Setup(r => r.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("secret detail"));

        ServiceResult result = await CreateSut().CreateAccountAsync(NewAccountRequest(), Actor, TestContext.Current.CancellationToken);

        Assert.Equal("ManagementAccount.CreateFailed", result.ErrorCode);
        Assert.Equal(ServiceResult.TemporaryErrorKey, result.ErrorKey);
        Assert.DoesNotContain("secret detail", result.ErrorKey);
    }

    // -- UpdateAccountAsync: validation and failures ----------------------------------

    /// <summary>An admin-set password must meet the policy: refused before hashing and before the row lock is taken.</summary>
    [Fact]
    public async Task UpdateAccountAsync_ReturnsPolicyViolation_BeforeLockingTheRow_WhenTheNewPasswordIsTooWeak()
    {
        ServiceResult result = await CreateSut().UpdateAccountAsync(new AdminUpdateAccountRequest { Email = "user@example.com" }, "weak", Actor, TestContext.Current.CancellationToken);

        Assert.Equal("Account.PasswordPolicy", result.ErrorCode);
        _passwordService.Verify(p => p.HashPassword(It.IsAny<string>()), Times.Never);
        _unitOfWork.Verify(u => u.BeginAsync(null, It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>An unknown role is returned as the domain's validation error; the row is not written and the lock is released.</summary>
    [Fact]
    public async Task UpdateAccountAsync_ReturnsTheDomainValidationError_ForAnUnknownRole()
    {
        _accountRepo.Setup(r => r.FindByEmailForUpdateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(ActiveAccount());

        ServiceResult result = await CreateSut().UpdateAccountAsync(new AdminUpdateAccountRequest { Email = "user@example.com", Role = "SuperUser", TimeZoneIanaId = "UTC" }, null, Actor, TestContext.Current.CancellationToken);

        Assert.Equal("Account.RoleInvalid", result.ErrorCode);
        _accountRepo.Verify(r => r.UpdateEntityAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>An unexpected repository failure on update is rolled back and reported without leaking internals.</summary>
    [Fact]
    public async Task UpdateAccountAsync_RollsBackAndReportsUpdateFailed_WhenTheRepositoryThrows()
    {
        _accountRepo.Setup(r => r.FindByEmailForUpdateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("secret detail"));

        ServiceResult result = await CreateSut().UpdateAccountAsync(new AdminUpdateAccountRequest { Email = "user@example.com" }, null, Actor, TestContext.Current.CancellationToken);

        Assert.Equal("ManagementAccount.UpdateFailed", result.ErrorCode);
        Assert.Equal(ServiceResult.TemporaryErrorKey, result.ErrorKey);
        Assert.DoesNotContain("secret detail", result.ErrorKey);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>An unexpected repository failure on delete is rolled back and reported without leaking internals.</summary>
    [Fact]
    public async Task DeleteAccountAsync_RollsBackAndReportsDeleteFailed_WhenTheRepositoryThrows()
    {
        _accountRepo.Setup(r => r.FindByEmailForUpdateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("secret detail"));

        ServiceResult result = await CreateSut().DeleteAccountAsync("user@example.com", Actor, TestContext.Current.CancellationToken);

        Assert.Equal("ManagementAccount.DeleteFailed", result.ErrorCode);
        Assert.Equal(ServiceResult.TemporaryErrorKey, result.ErrorKey);
        Assert.DoesNotContain("secret detail", result.ErrorKey);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // -- Exact results, lookup keys, defaults, transaction boundaries and failure logging ---

    /// <summary>Asserts the one Error entry carries <paramref name="thrown"/> and reads <paramref name="message"/>.</summary>
    private void AssertLoggedError(string message, Exception thrown)
    {
        FakeLogRecord record = Assert.Single(_logger.Collector.GetSnapshot(), r => r.Level == LogLevel.Error);
        Assert.Same(thrown, record.Exception);
        Assert.Equal(message, record.Message);
    }

    /// <summary>The duplicate-email check looks up the requested e-mail; an existing one is the "already created" Conflict.</summary>
    [Fact]
    public async Task CreateAccountAsync_ReturnsAlreadyExists_WhenTheRequestedEmailIsTaken()
    {
        _accountRepo.Setup(r => r.FindByEmailAsync("taken@example.com", It.IsAny<CancellationToken>())).ReturnsAsync(ActiveAccount("taken@example.com"));

        ServiceResult result = await CreateSut().CreateAccountAsync(NewAccountRequest("taken@example.com"), Actor, TestContext.Current.CancellationToken);

        Assert.Equal(ServiceErrorType.Conflict, result.ErrorType);
        Assert.Equal("Account.AlreadyExists", result.ErrorCode);
        Assert.Equal("This account has already been created.", result.ErrorKey);
        _accountRepo.Verify(r => r.CreateAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>A nickname taken (ignoring case) is a Conflict whose message template takes the nickname as its argument.</summary>
    [Fact]
    public async Task CreateAccountAsync_ReturnsTheNicknameConflictTemplate_WhenTheNicknameIsTaken()
    {
        _accountRepo.Setup(r => r.NicknameExistsIgnoreCaseAsync("NewUser", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        ServiceResult result = await CreateSut().CreateAccountAsync(NewAccountRequest(), Actor, TestContext.Current.CancellationToken);

        Assert.Equal("'{0}' is a Nickname that already exists. Please enter another Nickname.", result.ErrorKey);
        Assert.Equal(["NewUser"], result.ErrorArgs);
    }

    /// <summary>A concurrent create that hits the e-mail primary key is the "already created" Conflict, with its message.</summary>
    [Fact]
    public async Task CreateAccountAsync_ReturnsTheAlreadyExistsMessage_WhenTheEmailPrimaryKeyRaces()
    {
        _passwordService.Setup(p => p.HashPassword(It.IsAny<string>())).Returns("$2a$13$hashed");
        _accountRepo.Setup(r => r.CreateAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("duplicate key", new Exception("23505: duplicate key value violates unique constraint \"Account_pk\"")));

        ServiceResult result = await CreateSut().CreateAccountAsync(NewAccountRequest(), Actor, TestContext.Current.CancellationToken);

        Assert.Equal("This account has already been created.", result.ErrorKey);
    }

    /// <summary>An account created without a time zone gets UTC.</summary>
    [Fact]
    public async Task CreateAccountAsync_DefaultsTheTimeZoneToUtc_WhenNoneIsGiven()
    {
        _passwordService.Setup(p => p.HashPassword(It.IsAny<string>())).Returns("$2a$13$hashed");
        AdminCreateAccountRequest request = NewAccountRequest();
        request.TimeZoneIanaId = null;

        ServiceResult result = await CreateSut().CreateAccountAsync(request, Actor, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        _accountRepo.Verify(r => r.CreateAsync(It.Is<Account>(a => a.TimeZone.Value == "UTC"), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>The requested time zone is stored on the new account.</summary>
    [Fact]
    public async Task CreateAccountAsync_StoresTheRequestedTimeZone()
    {
        _passwordService.Setup(p => p.HashPassword(It.IsAny<string>())).Returns("$2a$13$hashed");
        AdminCreateAccountRequest request = NewAccountRequest();
        request.TimeZoneIanaId = "Asia/Seoul";

        await CreateSut().CreateAccountAsync(request, Actor, TestContext.Current.CancellationToken);

        _accountRepo.Verify(r => r.CreateAsync(It.Is<Account>(a => a.TimeZone.Value == "Asia/Seoul"), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>A create that throws unexpectedly is logged with the exception and the e-mail.</summary>
    [Fact]
    public async Task CreateAccountAsync_LogsTheFailure_WhenTheRepositoryThrows()
    {
        _passwordService.Setup(p => p.HashPassword(It.IsAny<string>())).Returns("$2a$13$hashed");
        var thrown = new InvalidOperationException("boom");
        _accountRepo.Setup(r => r.CreateAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>())).ThrowsAsync(thrown);

        await CreateSut().CreateAccountAsync(NewAccountRequest(), Actor, TestContext.Current.CancellationToken);

        AssertLoggedError("Failed to create account for new@example.com", thrown);
    }

    /// <summary>A successful admin update locks the requested account's row inside a transaction and commits it.</summary>
    [Fact]
    public async Task UpdateAccountAsync_LocksTheRequestedAccountAndCommits_OnSuccess()
    {
        Account account = ActiveAccount();
        _accountRepo.Setup(r => r.FindByEmailForUpdateAsync("user@example.com", It.IsAny<CancellationToken>())).ReturnsAsync(account);

        ServiceResult result = await CreateSut().UpdateAccountAsync(
            new AdminUpdateAccountRequest { Email = "user@example.com" }, null, Actor, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        _unitOfWork.Verify(u => u.BeginAsync(null, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>An unknown account on update is a NotFound ("Email address is wrong"), rolled back.</summary>
    [Fact]
    public async Task UpdateAccountAsync_ReturnsNotFound_AndRollsBack_WhenTheAccountIsUnknown()
    {
        ServiceResult result = await CreateSut().UpdateAccountAsync(
            new AdminUpdateAccountRequest { Email = "ghost@example.com" }, null, Actor, TestContext.Current.CancellationToken);

        Assert.Equal(ServiceErrorType.NotFound, result.ErrorType);
        Assert.Equal("Account.NotFound", result.ErrorCode);
        Assert.Equal("Email address is wrong", result.ErrorKey);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>An update that throws unexpectedly is logged with the exception and the e-mail.</summary>
    [Fact]
    public async Task UpdateAccountAsync_LogsTheFailure_WhenTheRepositoryThrows()
    {
        _accountRepo.Setup(r => r.FindByEmailForUpdateAsync("user@example.com", It.IsAny<CancellationToken>())).ReturnsAsync(ActiveAccount());
        var thrown = new InvalidOperationException("boom");
        _accountRepo.Setup(r => r.UpdateEntityAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>())).ThrowsAsync(thrown);

        await CreateSut().UpdateAccountAsync(new AdminUpdateAccountRequest { Email = "user@example.com" }, null, Actor, TestContext.Current.CancellationToken);

        AssertLoggedError("Failed to update account for user@example.com", thrown);
    }

    /// <summary>A successful admin delete runs inside a transaction and commits it.</summary>
    [Fact]
    public async Task DeleteAccountAsync_BeginsAndCommitsTheTransaction_OnSuccess()
    {
        _accountRepo.Setup(r => r.FindByEmailForUpdateAsync("user@example.com", It.IsAny<CancellationToken>())).ReturnsAsync(ActiveAccount());

        ServiceResult result = await CreateSut().DeleteAccountAsync("user@example.com", Actor, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        _unitOfWork.Verify(u => u.BeginAsync(null, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>An unknown account on delete is a NotFound with its message, rolled back.</summary>
    [Fact]
    public async Task DeleteAccountAsync_ReturnsNotFound_AndRollsBack_WhenTheAccountIsUnknown()
    {
        ServiceResult result = await CreateSut().DeleteAccountAsync("ghost@example.com", Actor, TestContext.Current.CancellationToken);

        Assert.Equal(ServiceErrorType.NotFound, result.ErrorType);
        Assert.Equal("Account.NotFound", result.ErrorCode);
        Assert.Equal("Fail to find the account by given email address", result.ErrorKey);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>A delete that throws unexpectedly is logged with the exception and the e-mail.</summary>
    [Fact]
    public async Task DeleteAccountAsync_LogsTheFailure_WhenTheRepositoryThrows()
    {
        _accountRepo.Setup(r => r.FindByEmailForUpdateAsync("user@example.com", It.IsAny<CancellationToken>())).ReturnsAsync(ActiveAccount());
        var thrown = new InvalidOperationException("boom");
        _accountRepo.Setup(r => r.UpdateEntityAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>())).ThrowsAsync(thrown);

        await CreateSut().DeleteAccountAsync("user@example.com", Actor, TestContext.Current.CancellationToken);

        AssertLoggedError("Failed to delete account for user@example.com", thrown);
    }
}
