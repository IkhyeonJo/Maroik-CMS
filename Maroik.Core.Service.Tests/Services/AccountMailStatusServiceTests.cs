using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Contract.Misc.Helpers;
using Maroik.Core.Domain.Account;
using Maroik.Core.Service.Services;
using Moq;
// ReSharper disable AccessToDisposedClosure

namespace Maroik.Core.Service.Tests.Services;

/// <summary>
/// Unit tests for <see cref="AccountMailStatusService"/>.
/// <see cref="IAccountRepository"/> is replaced with a Moq mock so tests run without a database.
/// </summary>
public class AccountMailStatusServiceTests
{
    private readonly Mock<IAccountRepository> _accountRepo = new();

    private AccountMailStatusService CreateSut() => new(_accountRepo.Object);

    private static Account ActiveAccount(string email = "user@example.com", string? message = null) => Account.Reconstitute(
        email: email,
        hashedPassword: "$2a$13$placeholder",
        nickname: "User",
        avatarImagePath: null,
        role: Role.User,
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
        message: message,
        deleted: false,
        securityStamp: "stamp",
        mustChangePassword: false);

    /// <summary>Verifies that <c>MarkMailSendFailedAsync</c> sets the FailToMailSent message and persists it.</summary>
    [Fact]
    public async Task MarkMailSendFailedAsync_SetsFailToMailSentMessage_WhenAccountFound()
    {
        var account = ActiveAccount();
        _accountRepo.Setup(r => r.FindByEmailAsync(account.Email.Value, It.IsAny<CancellationToken>())).ReturnsAsync(account);
        var sut = CreateSut();

        await sut.MarkMailSendFailedAsync(account.Email.Value, TestContext.Current.CancellationToken);

        _accountRepo.Verify(r => r.UpdateMessageAsync(
            account.Email.Value, EnumHelper.GetDescription(AccountMessage.FailToMailSent), It.IsAny<DateTime>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Verifies that <c>MarkMailSendFailedAsync</c> does nothing when no matching account exists.</summary>
    [Fact]
    public async Task MarkMailSendFailedAsync_DoesNothing_WhenAccountNotFound()
    {
        _accountRepo.Setup(r => r.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Account?)null);
        var sut = CreateSut();

        await sut.MarkMailSendFailedAsync("ghost@example.com", TestContext.Current.CancellationToken);

        _accountRepo.Verify(r => r.UpdateMessageAsync(
            It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Passes cancellation token through to the repository.</summary>
    [Fact]
    public async Task MarkMailSendFailedAsync_PassesCancellationTokenThroughToRepository()
    {
        var account = ActiveAccount();
        using var cts = new CancellationTokenSource();
        _accountRepo.Setup(r => r.FindByEmailAsync(account.Email.Value, cts.Token)).ReturnsAsync(account);
        var sut = CreateSut();

        await sut.MarkMailSendFailedAsync(account.Email.Value, cts.Token);

        _accountRepo.Verify(r => r.UpdateMessageAsync(account.Email.Value, It.IsAny<string?>(), It.IsAny<DateTime>(), cts.Token), Times.Once);
    }

    /// <summary>Verifies that <c>ClearMailSendFailedAsync</c> clears a FailToMailSent message and persists it.</summary>
    [Fact]
    public async Task ClearMailSendFailedAsync_ClearsMessage_WhenAccountWasFailToMailSent()
    {
        var account = ActiveAccount(message: EnumHelper.GetDescription(AccountMessage.FailToMailSent));
        _accountRepo.Setup(r => r.FindByEmailAsync(account.Email.Value, It.IsAny<CancellationToken>())).ReturnsAsync(account);
        var sut = CreateSut();

        await sut.ClearMailSendFailedAsync(account.Email.Value, TestContext.Current.CancellationToken);

        _accountRepo.Verify(r => r.UpdateMessageAsync(
            account.Email.Value, null, It.IsAny<DateTime>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Verifies that <c>ClearMailSendFailedAsync</c> does nothing when no matching account exists.</summary>
    [Fact]
    public async Task ClearMailSendFailedAsync_DoesNothing_WhenAccountNotFound()
    {
        _accountRepo.Setup(r => r.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Account?)null);
        var sut = CreateSut();

        await sut.ClearMailSendFailedAsync("ghost@example.com", TestContext.Current.CancellationToken);

        _accountRepo.Verify(r => r.UpdateMessageAsync(
            It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Verifies that <c>ClearMailSendFailedAsync</c> leaves an unrelated message untouched.</summary>
    [Fact]
    public async Task ClearMailSendFailedAsync_DoesNothing_WhenAccountMessageIsNotFailToMailSent()
    {
        var account = ActiveAccount(message: EnumHelper.GetDescription(AccountMessage.MailSent));
        _accountRepo.Setup(r => r.FindByEmailAsync(account.Email.Value, It.IsAny<CancellationToken>())).ReturnsAsync(account);
        var sut = CreateSut();

        await sut.ClearMailSendFailedAsync(account.Email.Value, TestContext.Current.CancellationToken);

        _accountRepo.Verify(r => r.UpdateMessageAsync(
            It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
