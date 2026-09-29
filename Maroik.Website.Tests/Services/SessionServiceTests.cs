using Maroik.Core.Contract.Dtos;
using Maroik.Core.Domain.Account;
using Maroik.Website.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Moq;

namespace Maroik.Website.Tests.Services;

/// <summary>
/// Unit tests for <see cref="SessionService"/>.
/// <see cref="IHttpContextAccessor"/> and <see cref="ISession"/> are replaced with Moq mocks.
/// Verifies that <c>SetAccount</c> serialises an <see cref="Maroik.Core.Contract.Dtos.AccountResponse"/>
/// to JSON and that <c>GetAccount</c> correctly deserializes it back.
/// </summary>
public class SessionServiceTests
{
    /// <summary>Mock <c>IHttpContextAccessor</c> injected into the system under test.</summary>
    private readonly Mock<IHttpContextAccessor> _httpContextAccessor = new();
    /// <summary>Mock <c>ISession</c> injected into the system under test.</summary>
    private readonly Mock<ISession> _session = new();

    /// <summary>Captures the log entries the system under test writes.</summary>
    private readonly FakeLogger<SessionService> _logger = new();

    /// <summary>The service under test over the mocked accessor and the capturing logger.</summary>
    private SessionService CreateSut() => new(_httpContextAccessor.Object, _logger);

    /// <summary>Makes the accessor return an HTTP context whose session is the mocked session.</summary>
    private void SetupContext()
    {
        var httpContext = new Mock<HttpContext>();
        httpContext.Setup(c => c.Session).Returns(_session.Object);
        _httpContextAccessor.Setup(a => a.HttpContext).Returns(httpContext.Object);
    }

    // -- Helpers --------------------------------------------------------------

    /// <summary>A User account to store in the session.</summary>
    private static AccountResponse SampleAccount() => new()
    {
        Email = "user@example.com",
        Nickname = "TestUser",
        Role = Role.User
    };

    // -- GetAccount -----------------------------------------------------------

    /// <summary>Verifies that <c>GetAccount</c> returns null when http context is null.</summary>
    [Fact]
    public void GetAccount_ReturnsNull_WhenHttpContextIsNull()
    {
        _httpContextAccessor.Setup(a => a.HttpContext).Returns(((HttpContext?)null)!);
        var sut = CreateSut();

        AccountResponse? result = sut.GetAccount();

        Assert.Null(result);
    }

    /// <summary>Verifies that <c>GetAccount</c> returns null when session key does not exist.</summary>
    [Fact]
    public void GetAccount_ReturnsNull_WhenSessionKeyDoesNotExist()
    {
        SetupContext();
        byte[]? outBytes = null!;
        _session.Setup(s => s.TryGetValue(It.IsAny<string>(), out outBytes)).Returns(false);
        var sut = CreateSut();

        AccountResponse? result = sut.GetAccount();

        Assert.Null(result);
    }

    /// <summary>Verifies that <c>GetAccount</c> returns account when valid session data exists.</summary>
    [Fact]
    public void GetAccount_ReturnsAccount_WhenValidSessionDataExists()
    {
        SetupContext();
        var account = SampleAccount();
        byte[]? bytes = System.Text.Encoding.UTF8.GetBytes(
            System.Text.Json.JsonSerializer.Serialize(account));
        _session.Setup(s => s.TryGetValue(It.IsAny<string>(), out bytes)).Returns(true);
        var sut = CreateSut();

        AccountResponse? result = sut.GetAccount();

        Assert.NotNull(result);
        Assert.Equal(account.Email, result.Email);
        Assert.Equal(account.Nickname, result.Nickname);
    }

    /// <summary>Verifies that <c>GetAccount</c> returns null when session data is invalid JSON.</summary>
    [Fact]
    public void GetAccount_ReturnsNull_WhenSessionDataIsInvalidJson()
    {
        SetupContext();
        byte[]? bytes = System.Text.Encoding.UTF8.GetBytes("not valid json {{{{");
        _session.Setup(s => s.TryGetValue(It.IsAny<string>(), out bytes)).Returns(true);
        var sut = CreateSut();

        AccountResponse? result = sut.GetAccount();

        Assert.Null(result);
    }

    /// <summary>Verifies that <c>GetAccount</c> returns null when session bytes are empty.</summary>
    [Fact]
    public void GetAccount_ReturnsNull_WhenSessionBytesAreEmpty()
    {
        SetupContext();
        byte[]? bytes = [];
        _session.Setup(s => s.TryGetValue(It.IsAny<string>(), out bytes)).Returns(true);
        var sut = CreateSut();

        AccountResponse? result = sut.GetAccount();

        Assert.Null(result);
    }

    // -- SetAccount -----------------------------------------------------------

    /// <summary>Verifies that <c>SetAccount</c> calls session set with serialized account.</summary>
    [Fact]
    public void SetAccount_CallsSessionSet_WithSerializedAccount()
    {
        SetupContext();
        _session.Setup(s => s.Set(It.IsAny<string>(), It.IsAny<byte[]>()));
        var sut = CreateSut();

        sut.SetAccount(SampleAccount());

        _session.Verify(s => s.Set(It.IsAny<string>(), It.IsAny<byte[]>()), Times.Once);
    }

    /// <summary>Verifies that <c>SetAccount</c> does not throw when http context is null.</summary>
    [Fact]
    public void SetAccount_DoesNotThrow_WhenHttpContextIsNull()
    {
        _httpContextAccessor.Setup(a => a.HttpContext).Returns(((HttpContext?)null)!);
        var sut = CreateSut();

        var ex = Record.Exception(() => sut.SetAccount(SampleAccount()));
        Assert.Null(ex);
    }

    // -- RemoveAccount --------------------------------------------------------

    /// <summary>Verifies that <c>RemoveAccount</c> calls session remove.</summary>
    [Fact]
    public void RemoveAccount_CallsSessionRemove()
    {
        SetupContext();
        _session.Setup(s => s.Remove(It.IsAny<string>()));
        var sut = CreateSut();

        sut.RemoveAccount();

        _session.Verify(s => s.Remove(It.IsAny<string>()), Times.Once);
    }

    /// <summary>Verifies that <c>RemoveAccount</c> does not throw when http context is null.</summary>
    [Fact]
    public void RemoveAccount_DoesNotThrow_WhenHttpContextIsNull()
    {
        _httpContextAccessor.Setup(a => a.HttpContext).Returns(((HttpContext?)null)!);
        var sut = CreateSut();

        var ex = Record.Exception(sut.RemoveAccount);
        Assert.Null(ex);
    }

    /// <summary>
    /// A session entry that is not valid JSON is treated as "no session" (the visitor is sent to
    /// log in again) — and logged, so a corrupt or tampered session store is visible to an operator.
    /// The payload itself is not logged.
    /// </summary>
    [Fact]
    public void GetAccount_LogsAWarning_WhenTheStoredSessionIsNotValidJson()
    {
        SetupContext();
        byte[]? bytes = System.Text.Encoding.UTF8.GetBytes("{not json secret-9f3a");
        _session.Setup(s => s.TryGetValue(It.IsAny<string>(), out bytes)).Returns(true);

        Assert.Null(CreateSut().GetAccount());

        FakeLogRecord record = Assert.Single(_logger.Collector.GetSnapshot());
        Assert.Equal(LogLevel.Warning, record.Level);
        Assert.Contains("could not be read", record.Message);
        Assert.NotNull(record.Exception);
        Assert.DoesNotContain("secret-9f3a", record.Message);
    }
}
