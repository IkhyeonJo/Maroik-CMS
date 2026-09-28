using Maroik.Core.Contract.Dtos;
using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Contract.Misc.Messaging;
using Maroik.Core.Contract.Misc.Settings;
using Maroik.Worker.Handlers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Maroik.Worker.Tests.Handlers;

/// <summary>
/// Unit tests for <see cref="EmailMessageHandler"/>.
/// <see cref="IMailClient"/> and <see cref="IAccountMailStatusService"/> are mocked so no real
/// SMTP server or database is needed.
/// </summary>
public class EmailMessageHandlerTests
{
    private readonly Mock<IMailClient> _mailClient = new();
    private readonly Mock<IAccountMailStatusService> _accountMailStatusService = new();
    private readonly IOptions<ServerSetting> _settings = Options.Create(new ServerSetting
    {
        SmtpHost = "smtp.example.com",
        SmtpPort = 587,
        SmtpSsl = true,
        SmtpUserName = "user",
        SmtpPassword = "pass",
        FromEmail = "noreply@maroik.com",
        FromFullName = "Maroik"
    });

    private EmailMessageHandler CreateSut() => new(
        _mailClient.Object,
        _accountMailStatusService.Object,
        _settings,
        NullLogger<EmailMessageHandler>.Instance);

    /// <summary>Verifies that <c>HandleAsync</c> does not mark the account failed when the SMTP send succeeds.</summary>
    [Fact]
    public async Task HandleAsync_DoesNotUpdateAccount_WhenMailSentSuccessfully()
    {
        _mailClient.Setup(m => m.SendMailAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<ServerSetting>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult.Ok());

        var sut = CreateSut();
        var message = new SendEmailMessage("user@example.com", "subject", "body");

        await sut.HandleAsync(message, TestContext.Current.CancellationToken);

        _accountMailStatusService.Verify(
            s => s.MarkMailSendFailedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>Verifies that <c>HandleAsync</c> clears a previously recorded failure once the send succeeds.</summary>
    [Fact]
    public async Task HandleAsync_ClearsMailSendFailed_WhenMailSentSuccessfully()
    {
        _mailClient.Setup(m => m.SendMailAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<ServerSetting>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult.Ok());

        var sut = CreateSut();
        var message = new SendEmailMessage("user@example.com", "subject", "body");

        await sut.HandleAsync(message, TestContext.Current.CancellationToken);

        _accountMailStatusService.Verify(
            s => s.ClearMailSendFailedAsync("user@example.com", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>Verifies that <c>HandleAsync</c> marks the account as FailToMailSent when SMTP fails.</summary>
    [Fact]
    public async Task HandleAsync_MarksAccountFailToMailSent_WhenSmtpFails()
    {
        _mailClient.Setup(m => m.SendMailAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<ServerSetting>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult.Fail("SMTP connection refused"));

        var sut = CreateSut();
        var message = new SendEmailMessage("user@example.com", "subject", "body");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.HandleAsync(message, TestContext.Current.CancellationToken));

        _accountMailStatusService.Verify(
            s => s.MarkMailSendFailedAsync("user@example.com", It.IsAny<CancellationToken>()),
            Times.Once);
        _accountMailStatusService.Verify(
            s => s.ClearMailSendFailedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>Passes cancellation token through to the account service.</summary>
    [Fact]
    public async Task HandleAsync_PassesCancellationTokenThroughToAccountService()
    {
        _mailClient.Setup(m => m.SendMailAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<ServerSetting>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult.Fail("SMTP connection refused"));

        var sut = CreateSut();
        using var cts = new CancellationTokenSource();
        var message = new SendEmailMessage("user@example.com", "subject", "body");

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.HandleAsync(message, cts.Token));

        _accountMailStatusService.Verify(
            // ReSharper disable once AccessToDisposedClosure
            s => s.MarkMailSendFailedAsync("user@example.com", cts.Token),
            Times.Once);
    }
}
