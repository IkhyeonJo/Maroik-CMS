using System.Diagnostics;
using System.Net;
using Maroik.Core.Client.Clients;
using Maroik.Core.Contract.Misc.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Maroik.Core.Client.Tests.Clients;

/// <summary>
/// Unit tests for <see cref="FileClient"/>.
/// Exercises null/empty input guards, connection-failure handling when the file-storage
/// microservice is not available, and correlation-id propagation.
/// </summary>
public class FileClientTests
{
    /// <summary><see cref="IHttpClientFactory"/> whose clients go through a test-supplied handler.</summary>
    private sealed class FakeHttpClientFactory : IHttpClientFactory
    {
        /// <summary>The handler the created <see cref="HttpClient"/> should use, or null for a default client.</summary>
        public HttpMessageHandler? InnerHandler { get; set; }

        /// <summary>Creates an <see cref="HttpClient"/> wrapping <see cref="InnerHandler"/> when one is set.</summary>
        public HttpClient CreateClient(string name) => InnerHandler is null ? new HttpClient() : new HttpClient(InnerHandler);
    }

    /// <summary>
    /// Reads the request body into a string immediately, since <see cref="FileClient"/>
    /// disposes its <see cref="MultipartFormDataContent"/> in a <c>using</c> block right after
    /// the call returns — inspecting <c>request.Content</c> after the fact would throw
    /// <see cref="ObjectDisposedException"/>.
    /// </summary>
    private sealed class CapturingHandler : HttpMessageHandler
    {
        /// <summary>The body of the most recently captured request, or null if it had no content.</summary>
        public string? LastRequestBody { get; private set; }

        /// <summary>Captures the request body and returns a canned 200 OK response.</summary>
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    /// <summary>Returns a fixed status + plain-text body, like Maroik.FileStorage's <c>BadRequest(error)</c>.</summary>
    private sealed class RefusingHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        /// <inheritdoc />
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
    }

    /// <summary>A refusal whose body cannot be decoded (a charset .NET does not know) — reading the reason throws.</summary>
    private sealed class UnreadableBodyHandler : HttpMessageHandler
    {
        /// <inheritdoc />
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var content = new StringContent("refused");
            content.Headers.Remove("Content-Type");
            content.Headers.TryAddWithoutValidation("Content-Type", "text/plain; charset=not-a-real-charset");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = content });
        }
    }

    /// <summary>Records every formatted log message.</summary>
    private sealed class RecordingLogger : ILogger<FileClient>
    {
        /// <summary>Every logged entry: its level and formatted message.</summary>
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        /// <summary>Scopes are not recorded.</summary>
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        /// <summary>Every level is enabled.</summary>
        public bool IsEnabled(LogLevel logLevel) => true;

        /// <summary>Records the level and formatted message.</summary>
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Entries.Add((logLevel, formatter(state, exception)));
    }

    /// <summary>Factory handed to the client under test; each test sets its handler.</summary>
    private readonly FakeHttpClientFactory _httpClientFactory = new();
    /// <summary>Builds the client under test over <see cref="_httpClientFactory"/> (logging discarded).</summary>
    private FileClient CreateSut() => new(_httpClientFactory, NullLogger<FileClient>.Instance);

    // -- UploadAsync ----------------------------------------------------------

    /// <summary>Verifies that <c>UploadAsync</c> returns false when file data is null.</summary>
    [Fact]
    public async Task UploadAsync_ReturnsFalse_WhenFileDataIsNull()
    {
        bool result = await CreateSut().UploadAsync(null!, "image/png", "test.png", "http://localhost", TestContext.Current.CancellationToken);

        Assert.False(result);
    }

    /// <summary>Verifies that <c>UploadAsync</c> returns false when file data is empty.</summary>
    [Fact]
    public async Task UploadAsync_ReturnsFalse_WhenFileDataIsEmpty()
    {
        bool result = await CreateSut().UploadAsync([], "image/png", "test.png", "http://localhost", TestContext.Current.CancellationToken);

        Assert.False(result);
    }

    /// <summary>Verifies that <c>UploadAsync</c> returns false when http call fails.</summary>
    [Fact]
    public async Task UploadAsync_ReturnsFalse_WhenHttpCallFails()
    {
        // Any non-empty byte array will attempt a real HTTP call to an invalid host → returns false (caught exception)
        byte[] data = [0x89, 0x50, 0x4E, 0x47];
        bool result = await CreateSut().UploadAsync(data, "image/png", "test.png", "http://invalid.host.that.does.not.exist.local", TestContext.Current.CancellationToken);

        Assert.False(result);
    }

    // -- Refusal reason logging -------------------------------------------------

    /// <summary>
    /// Verifies that when the storage service refuses an upload, the reason it gave in the response
    /// body ("could not be scanned", "infected", ...) is logged — a bare "400" would not tell an
    /// operator a ClamAV outage from a virus detection.
    /// </summary>
    [Fact]
    public async Task UploadAsync_LogsTheStorageServicesRefusalReason_WhenUploadIsRefused()
    {
        _httpClientFactory.InnerHandler = new RefusingHandler(HttpStatusCode.BadRequest, "The file could not be scanned for viruses. Please try again later.");
        var logger = new RecordingLogger();
        var sut = new FileClient(_httpClientFactory, logger);

        bool result = await sut.UploadAsync([0x89, 0x50, 0x4E, 0x47], "image/png", "test.png", "http://filestorage.local", TestContext.Current.CancellationToken);

        Assert.False(result);
        var (_, message) = Assert.Single(logger.Entries, e => e.Level == LogLevel.Warning);
        Assert.Contains("400", message);
        Assert.Contains("could not be scanned", message);
    }

    /// <summary>A refusal whose body cannot be decoded does not turn into an exception: the upload just fails, with an empty reason logged.</summary>
    [Fact]
    public async Task UploadAsync_ReturnsFalse_WhenTheRefusalBodyCannotBeRead()
    {
        _httpClientFactory.InnerHandler = new UnreadableBodyHandler();
        var logger = new RecordingLogger();
        var sut = new FileClient(_httpClientFactory, logger);

        bool result = await sut.UploadAsync([0x89, 0x50, 0x4E, 0x47], "image/png", "test.png", "http://filestorage.local", TestContext.Current.CancellationToken);

        Assert.False(result);
        Assert.Single(logger.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("400"));
        // The unreadable body itself leaves a trace too (diagnostic level: the refusal is already warned about).
        Assert.Single(logger.Entries, e => e.Level == LogLevel.Debug && e.Message.Contains("could not be read"));
    }

    /// <summary>An over-long refusal body is truncated in the log.</summary>
    [Fact]
    public async Task UploadAsync_TruncatesAnOverlongRefusalReason()
    {
        _httpClientFactory.InnerHandler = new RefusingHandler(HttpStatusCode.BadRequest, new string('x', 5000));
        var logger = new RecordingLogger();
        var sut = new FileClient(_httpClientFactory, logger);

        await sut.UploadAsync([0x89, 0x50, 0x4E, 0x47], "image/png", "test.png", "http://filestorage.local", TestContext.Current.CancellationToken);

        var (_, message) = Assert.Single(logger.Entries, e => e.Level == LogLevel.Warning);
        Assert.True(message.Length < 500);
    }

    // -- UploadWithResultAsync: why a refused file was refused ------------------

    /// <summary>The storage service's refusal reasons map to distinct outcomes: detection, scanner outage, anything else.</summary>
    [Theory]
    [InlineData("File may be infected with a virus.", FileUploadResult.Infected)]
    [InlineData("The file could not be scanned for viruses. Please try again later.", FileUploadResult.ScanUnavailable)]
    [InlineData("Unsupported file extension.", FileUploadResult.Failed)]
    [InlineData("", FileUploadResult.Failed)]
    public async Task UploadWithResultAsync_MapsTheRefusalReason_ToAnOutcome(string reason, FileUploadResult expected)
    {
        _httpClientFactory.InnerHandler = new RefusingHandler(HttpStatusCode.BadRequest, reason);
        var sut = CreateSut();

        FileUploadResult result = await sut.UploadWithResultAsync([0x89, 0x50, 0x4E, 0x47], "image/png", "test.png", "http://filestorage.local", TestContext.Current.CancellationToken);

        Assert.Equal(expected, result);
    }

    /// <summary>A 2xx reply is <see cref="FileUploadResult.Stored"/> (and <c>UploadAsync</c> stays true for it).</summary>
    [Fact]
    public async Task UploadWithResultAsync_ReturnsStored_ForASuccessfulUpload()
    {
        _httpClientFactory.InnerHandler = new RefusingHandler(HttpStatusCode.OK, "File uploaded successfully.");
        var sut = CreateSut();

        FileUploadResult result = await sut.UploadWithResultAsync([0x89, 0x50, 0x4E, 0x47], "image/png", "test.png", "http://filestorage.local", TestContext.Current.CancellationToken);
        bool legacy = await sut.UploadAsync([0x89, 0x50, 0x4E, 0x47], "image/png", "test.png", "http://filestorage.local", TestContext.Current.CancellationToken);

        Assert.Equal(FileUploadResult.Stored, result);
        Assert.True(legacy);
    }

    /// <summary>Empty input, an unsafe path and a transport error are all <see cref="FileUploadResult.Failed"/>.</summary>
    [Fact]
    public async Task UploadWithResultAsync_ReturnsFailed_ForEmptyInputAndUnsafePath()
    {
        var sut = CreateSut();

        Assert.Equal(FileUploadResult.Failed, await sut.UploadWithResultAsync([], "image/png", "test.png", "http://localhost", TestContext.Current.CancellationToken));
        Assert.Equal(FileUploadResult.Failed, await sut.UploadWithResultAsync([1], "image/png", "../escape.png", "http://localhost", TestContext.Current.CancellationToken));
    }

    // -- Correlation id form field ----------------------------------------------

    /// <summary>Verifies that <c>UploadAsync</c> includes the ambient Activity id as a form field.</summary>
    [Fact]
    public async Task UploadAsync_IncludesCorrelationId_WhenActivityIsCurrent()
    {
        using var activity = new Activity("test-request").Start();
        var capturing = new CapturingHandler();
        _httpClientFactory.InnerHandler = capturing;
        byte[] data = [0x89, 0x50, 0x4E, 0x47];

        await CreateSut().UploadAsync(data, "image/png", "test.png", "http://filestorage.local", TestContext.Current.CancellationToken);

        Assert.Contains("name=correlationId", capturing.LastRequestBody);
        Assert.Contains(activity.Id!, capturing.LastRequestBody);
    }

    /// <summary>Verifies that <c>DownloadAsync</c> includes the ambient Activity id as a form field.</summary>
    [Fact]
    public async Task DownloadAsync_IncludesCorrelationId_WhenActivityIsCurrent()
    {
        using var activity = new Activity("test-request").Start();
        var capturing = new CapturingHandler();
        _httpClientFactory.InnerHandler = capturing;

        await CreateSut().DownloadAsync("some/path.png", "http://filestorage.local", TestContext.Current.CancellationToken);

        Assert.Contains("name=correlationId", capturing.LastRequestBody);
        Assert.Contains(activity.Id!, capturing.LastRequestBody);
    }

    /// <summary>Verifies that <c>DownloadAsync</c> sends an empty correlation id when there is no ambient Activity.</summary>
    [Fact]
    public async Task DownloadAsync_SendsEmptyCorrelationId_WhenNoActivityIsCurrent()
    {
        Activity.Current = null;
        var capturing = new CapturingHandler();
        _httpClientFactory.InnerHandler = capturing;

        await CreateSut().DownloadAsync("some/path.png", "http://filestorage.local", TestContext.Current.CancellationToken);

        Assert.Contains("name=correlationId", capturing.LastRequestBody);
    }

    // -- Path safety ------------------------------------------------------------

    /// <summary>
    /// Regression test: a leading backslash is not "rooted" per <see cref="Path.IsPathRooted(string)"/>
    /// on this (Linux) runtime, but becomes a rooted Unix path ("/etc/passwd") once normalized for
    /// the storage service — the safety check must normalize separators first, or this form of
    /// input slips past it into an absolute path outside the storage root.
    /// </summary>
    [Theory]
    [InlineData(@"\etc\passwd")]
    [InlineData("/etc/passwd")]
    [InlineData(@"..\..\etc\passwd")]
    [InlineData("../../etc/passwd")]
    [InlineData("")]
    [InlineData("   ")]
    public async Task DownloadAsync_ThrowsArgumentException_ForUnsafePath(string unsafePath)
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => CreateSut().DownloadAsync(unsafePath, "http://filestorage.local", TestContext.Current.CancellationToken));
    }

    /// <summary>Same unsafe-path cases as <c>DownloadAsync</c>, but <c>UploadAsync</c> reports failure via its bool return instead of throwing.</summary>
    [Theory]
    [InlineData(@"\etc\passwd")]
    [InlineData("/etc/passwd")]
    [InlineData(@"..\..\etc\passwd")]
    [InlineData("../../etc/passwd")]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UploadAsync_ReturnsFalse_ForUnsafePath(string unsafePath)
    {
        byte[] data = [0x89, 0x50, 0x4E, 0x47];

        bool result = await CreateSut().UploadAsync(data, "image/png", unsafePath, "http://filestorage.local", TestContext.Current.CancellationToken);

        Assert.False(result);
    }
}
