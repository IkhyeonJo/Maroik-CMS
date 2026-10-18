using Maroik.Core.Contract.Dtos;
using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Contract.Messages;
using Maroik.Core.Contract.Settings;
using Maroik.Worker.Handlers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
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
    /// <summary>Mock <c>IMailClient</c> injected into the system under test.</summary>
    private readonly Mock<IMailClient> _mailClient = new();
    /// <summary>Mock <c>IAccountMailStatusService</c> injected into the system under test.</summary>
    private readonly Mock<IAccountMailStatusService> _accountMailStatusService = new();
    /// <summary>SMTP settings for the handler under test (the mail client is mocked, so nothing is contacted).</summary>
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

    /// <summary>Records what the handler logs.</summary>
    private readonly FakeLogger<EmailMessageHandler> _logger = new();

    /// <summary>The handler under test over the mocked dependencies.</summary>
    private EmailMessageHandler CreateSut() => new(
        _mailClient.Object,
        _accountMailStatusService.Object,
        _settings,
        _logger);

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
        Assert.Single(_logger.Collector.GetSnapshot(), r => r.Level == LogLevel.Information && r.Message == "Email sent to user@example.com");
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

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.HandleAsync(message, TestContext.Current.CancellationToken));

        _accountMailStatusService.Verify(
            s => s.MarkMailSendFailedAsync("user@example.com", It.IsAny<CancellationToken>()),
            Times.Once);
        _accountMailStatusService.Verify(
            s => s.ClearMailSendFailedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        Assert.Single(_logger.Collector.GetSnapshot(),
            r => r.Level == LogLevel.Warning && r.Message == "SMTP send failed for user@example.com: SMTP connection refused");
        Assert.Equal("SMTP send failed for user@example.com: SMTP connection refused", thrown.Message);
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
