using Maroik.Core.Contract.Dtos;
using Maroik.Core.Domain.Accounts;
using Maroik.Website.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Session;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;
using Moq;

namespace Maroik.Website.Tests.Services;

/// <summary>
/// Unit tests for <see cref="SessionService"/>.
/// <see cref="IHttpContextAccessor"/> and <see cref="ISession"/> are replaced with Moq mocks.
/// Verifies that <c>SetAccount</c> serialises an <see cref="Maroik.Core.Contract.Dtos.AccountResponse"/>
/// to JSON and that <c>GetAccountAsync</c> correctly deserializes it back.
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

    // -- GetAccountAsync ------------------------------------------------------

    /// <summary>Verifies that <c>GetAccountAsync</c> returns null when http context is null.</summary>
    [Fact]
    public async Task GetAccount_ReturnsNull_WhenHttpContextIsNull()
    {
        _httpContextAccessor.Setup(a => a.HttpContext).Returns(((HttpContext?)null)!);
        var sut = CreateSut();

        AccountResponse? result = await sut.GetAccountAsync(TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    /// <summary>Verifies that <c>GetAccountAsync</c> returns null when session key does not exist.</summary>
    [Fact]
    public async Task GetAccount_ReturnsNull_WhenSessionKeyDoesNotExist()
    {
        SetupContext();
        byte[]? outBytes = null!;
        _session.Setup(s => s.TryGetValue(It.IsAny<string>(), out outBytes)).Returns(false);
        var sut = CreateSut();

        AccountResponse? result = await sut.GetAccountAsync(TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    /// <summary>Verifies that <c>GetAccountAsync</c> returns account when valid session data exists.</summary>
    [Fact]
    public async Task GetAccount_ReturnsAccount_WhenValidSessionDataExists()
    {
        SetupContext();
        var account = SampleAccount();
        byte[]? bytes = System.Text.Encoding.UTF8.GetBytes(
            System.Text.Json.JsonSerializer.Serialize(account));
        _session.Setup(s => s.TryGetValue(It.IsAny<string>(), out bytes)).Returns(true);
        var sut = CreateSut();

        AccountResponse? result = await sut.GetAccountAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(account.Email, result.Email);
        Assert.Equal(account.Nickname, result.Nickname);
    }

    /// <summary>Verifies that <c>GetAccountAsync</c> returns null when session data is invalid JSON.</summary>
    [Fact]
    public async Task GetAccount_ReturnsNull_WhenSessionDataIsInvalidJson()
    {
        SetupContext();
        byte[]? bytes = System.Text.Encoding.UTF8.GetBytes("not valid json {{{{");
        _session.Setup(s => s.TryGetValue(It.IsAny<string>(), out bytes)).Returns(true);
        var sut = CreateSut();

        AccountResponse? result = await sut.GetAccountAsync(TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    /// <summary>Verifies that <c>GetAccountAsync</c> returns null when session bytes are empty.</summary>
    [Fact]
    public async Task GetAccount_ReturnsNull_WhenSessionBytesAreEmpty()
    {
        SetupContext();
        byte[]? bytes = [];
        _session.Setup(s => s.TryGetValue(It.IsAny<string>(), out bytes)).Returns(true);
        var sut = CreateSut();

        AccountResponse? result = await sut.GetAccountAsync(TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    // -- GetAccountAsync over a real distributed session -----------------------

    /// <summary>The session cookie's key both helpers below store and read the account under.</summary>
    private const string SessionKey = "session-key";

    /// <summary>
    /// A session store (Valkey in production) whose synchronous read fails while its asynchronous one answers — the
    /// shape of a timed-out blocking read under load. Writes and the asynchronous read reach <paramref name="inner"/>.
    /// </summary>
    private sealed class SyncReadFailsCache(IDistributedCache inner) : IDistributedCache
    {
        public byte[]? Get(string key) => throw new TimeoutException("synchronous session read timed out");
        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => inner.GetAsync(key, token);
        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => inner.Set(key, value, options);
        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) =>
            inner.SetAsync(key, value, options, token);
        public void Refresh(string key) => inner.Refresh(key);
        public Task RefreshAsync(string key, CancellationToken token = default) => inner.RefreshAsync(key, token);
        public void Remove(string key) => inner.Remove(key);
        public Task RemoveAsync(string key, CancellationToken token = default) => inner.RemoveAsync(key, token);
    }

    /// <summary>A session store that is down: every read fails.</summary>
    private sealed class UnreachableCache(IDistributedCache inner) : IDistributedCache
    {
        public byte[]? Get(string key) => throw new TimeoutException("session store unreachable");
        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) =>
            Task.FromException<byte[]?>(new TimeoutException("session store unreachable"));
        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => inner.Set(key, value, options);
        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) =>
            inner.SetAsync(key, value, options, token);
        public void Refresh(string key) => inner.Refresh(key);
        public Task RefreshAsync(string key, CancellationToken token = default) => inner.RefreshAsync(key, token);
        public void Remove(string key) => inner.Remove(key);
        public Task RemoveAsync(string key, CancellationToken token = default) => inner.RemoveAsync(key, token);
    }

    /// <summary>A service whose request carries <paramref name="session"/> as ASP.NET Core's session middleware sets it.</summary>
    private SessionService ServiceOver(ISession session)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Features.Set<ISessionFeature>(new SessionFeature { Session = session });
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(a => a.HttpContext).Returns(httpContext);
        return new SessionService(accessor.Object, _logger);
    }

    /// <summary>A real <see cref="DistributedSession"/> under <see cref="SessionKey"/> over <paramref name="store"/>.</summary>
    private static DistributedSession SessionOver(IDistributedCache store, bool isNewSessionKey) =>
        new(store, SessionKey, TimeSpan.FromMinutes(20), TimeSpan.FromMinutes(1), () => true, NullLoggerFactory.Instance,
            isNewSessionKey);

    /// <summary>Signs <paramref name="account"/> in: stores it in a committed session in <paramref name="store"/>.</summary>
    private async Task SignInAsync(IDistributedCache store, AccountResponse account)
    {
        DistributedSession session = SessionOver(store, isNewSessionKey: true);
        ServiceOver(session).SetAccount(account);
        await session.CommitAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// The production "blank page" bug: a page fires ~35 asset requests at once, each reading the session. Read
    /// synchronously, a read that times out under that load leaves ASP.NET Core with an empty session (it only logs), so
    /// RoleBasedStaticFileMiddleware answered 403 to a signed-in user's /user/** scripts and the page had no jQuery.
    /// Loading the session asynchronously first never takes the synchronous path.
    /// </summary>
    [Fact]
    public async Task GetAccountAsync_ReturnsTheSignedInAccount_WhenOnlyTheSynchronousStoreReadWouldFail()
    {
        var store = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        await SignInAsync(store, SampleAccount());
        SessionService sut = ServiceOver(SessionOver(new SyncReadFailsCache(store), isNewSessionKey: false));

        AccountResponse? result = await sut.GetAccountAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal("user@example.com", result.Email);
    }

    /// <summary>
    /// A session store that cannot be read is a failure, not "signed out": the error reaches the global exception
    /// handler (and its log) instead of quietly refusing a signed-in account's assets and pages.
    /// </summary>
    [Fact]
    public async Task GetAccountAsync_Throws_WhenTheSessionStoreCannotBeRead()
    {
        var store = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        await SignInAsync(store, SampleAccount());
        SessionService sut = ServiceOver(SessionOver(new UnreachableCache(store), isNewSessionKey: false));

        await Assert.ThrowsAsync<TimeoutException>(() => sut.GetAccountAsync(TestContext.Current.CancellationToken));
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
    public async Task GetAccount_LogsAWarning_WhenTheStoredSessionIsNotValidJson()
    {
        SetupContext();
        byte[]? bytes = System.Text.Encoding.UTF8.GetBytes("{not json secret-9f3a");
        _session.Setup(s => s.TryGetValue(It.IsAny<string>(), out bytes)).Returns(true);

        Assert.Null(await CreateSut().GetAccountAsync(TestContext.Current.CancellationToken));

        FakeLogRecord record = Assert.Single(_logger.Collector.GetSnapshot());
        Assert.Equal(LogLevel.Warning, record.Level);
        Assert.Contains("could not be read", record.Message, StringComparison.Ordinal);
        Assert.NotNull(record.Exception);
        Assert.DoesNotContain("secret-9f3a", record.Message, StringComparison.Ordinal);
    }
}
