using System.Net;
using Maroik.Core.Client.Clients;
using Maroik.Core.Contract.Enums;
using Maroik.Core.Contract.Messages;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace Maroik.Core.Client.Tests.Clients;

/// <summary>
/// Unit tests for the File Manager calls of <see cref="FileClient"/> (Implementation note [A]): each sends its multipart form to the
/// right <c>api/FileManager/*</c> endpoint, reads the storage service's answer back into a result, and logs a refusal or a transport
/// failure with its target — never throws to the caller.
/// </summary>
public class FileClientFileManagerTests
{
    /// <summary>The base URL of the fake storage service.</summary>
    private const string BaseUrl = "http://files.local";

    /// <summary>The upload id these tests use.</summary>
    private static readonly Guid UploadId = Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e");

    /// <summary>Answers every request with a fixed status and body, recording what was sent.</summary>
    private sealed class FakeStorage(HttpStatusCode status, string body) : HttpMessageHandler
    {
        /// <summary>The URL of the last request.</summary>
        public string? Url { get; private set; }

        /// <summary>The body of the last request, read when it was sent.</summary>
        public string? Body { get; private set; }

        /// <inheritdoc />
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Url = request.RequestUri?.ToString();
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            HttpResponseMessage? response = new(status);
            try
            {
                response.Content = new StringContent(body);
                HttpResponseMessage built = response;
                response = null;
                return built;
            }
            finally
            {
                response?.Dispose();
            }
        }
    }

    /// <summary>Fails every request as an unreachable storage service would.</summary>
    private sealed class Unreachable : HttpMessageHandler
    {
        /// <inheritdoc />
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new HttpRequestException("connection refused");
    }

    /// <summary>Hands out clients over one handler (which the clients do not own).</summary>
    private sealed class Factory(HttpMessageHandler handler) : IHttpClientFactory
    {
        /// <inheritdoc />
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    /// <summary>The captured log.</summary>
    private readonly FakeLogger<FileClient> _logger = new();

    /// <summary>A client over <paramref name="handler"/>.</summary>
    private FileClient Sut(HttpMessageHandler handler) => new(new Factory(handler), _logger);

    // -- AppendFileManagerChunkAsync ------------------------------------------------

    /// <summary>A chunk goes to api/FileManager/chunk with the upload id, the offset and the bytes, and the new length is read back.</summary>
    [Fact]
    public async Task AppendFileManagerChunkAsync_SendsTheChunk_AndReturnsTheNewLength()
    {
        using var storage = new FakeStorage(HttpStatusCode.OK, "16777219");

        long? length = await Sut(storage).AppendFileManagerChunkAsync(UploadId, 16777216, new MemoryStream("abc"u8.ToArray()), BaseUrl, TestContext.Current.CancellationToken);

        Assert.Equal(16777219, length);
        Assert.Equal($"{BaseUrl}/api/FileManager/chunk", storage.Url);
        Assert.Contains(UploadId.ToString("D"), storage.Body, StringComparison.Ordinal);
        Assert.Contains("16777216", storage.Body, StringComparison.Ordinal);
        Assert.Contains("abc", storage.Body, StringComparison.Ordinal);
        Assert.Contains("name=chunk", storage.Body, StringComparison.Ordinal);
    }

    /// <summary>A refused chunk (wrong offset) is null and logged at Warning with the storage service's reason.</summary>
    [Fact]
    public async Task AppendFileManagerChunkAsync_ReturnsNull_AndLogs_WhenRefused()
    {
        using var storage = new FakeStorage(HttpStatusCode.Conflict, FileStorageRefusals.OffsetMismatch);

        Assert.Null(await Sut(storage).AppendFileManagerChunkAsync(UploadId, 5, new MemoryStream([1]), BaseUrl, TestContext.Current.CancellationToken));

        FakeLogRecord warning = Assert.Single(_logger.Collector.GetSnapshot(), r => r.Level == LogLevel.Warning);
        Assert.Equal($"File Manager chunk of {UploadId} at offset 5 returned 409: {FileStorageRefusals.OffsetMismatch}", warning.Message);
    }

    /// <summary>An unreachable storage service is null, logged at Error with the exception.</summary>
    [Fact]
    public async Task AppendFileManagerChunkAsync_ReturnsNull_AndLogs_WhenUnreachable()
    {
        using var handler = new Unreachable();

        Assert.Null(await Sut(handler).AppendFileManagerChunkAsync(UploadId, 0, new MemoryStream([1]), BaseUrl, TestContext.Current.CancellationToken));

        FakeLogRecord error = Assert.Single(_logger.Collector.GetSnapshot(), r => r.Level == LogLevel.Error);
        Assert.Equal($"File Manager chunk request failed for {UploadId}", error.Message);
        Assert.NotNull(error.Exception);
    }

    // -- CompleteFileManagerUploadAsync ----------------------------------------------

    /// <summary>A completed upload goes to api/FileManager/complete with the id and the declared size; OK is Stored.</summary>
    [Fact]
    public async Task CompleteFileManagerUploadAsync_ReturnsStored_WhenAccepted()
    {
        using var storage = new FakeStorage(HttpStatusCode.OK, "");

        Assert.Equal(FileUploadResult.Stored, await Sut(storage).CompleteFileManagerUploadAsync(UploadId, 1234, BaseUrl, TestContext.Current.CancellationToken));
        Assert.Equal($"{BaseUrl}/api/FileManager/complete", storage.Url);
        Assert.Contains("1234", storage.Body, StringComparison.Ordinal);
        Assert.Contains(UploadId.ToString("D"), storage.Body, StringComparison.Ordinal);
    }

    /// <summary>The storage service's refusal is mapped back: a virus is Infected, a scanner outage or exceeded limit ScanUnavailable, anything else Failed.</summary>
    [Theory]
    [InlineData(FileStorageRefusals.Infected, FileUploadResult.Infected)]
    [InlineData(FileStorageRefusals.ScanUnavailable, FileUploadResult.ScanUnavailable)]
    [InlineData(FileStorageRefusals.SizeMismatch, FileUploadResult.Failed)]
    public async Task CompleteFileManagerUploadAsync_MapsTheRefusal(string reason, FileUploadResult expected)
    {
        using var storage = new FakeStorage(HttpStatusCode.BadRequest, reason);

        Assert.Equal(expected, await Sut(storage).CompleteFileManagerUploadAsync(UploadId, 1, BaseUrl, TestContext.Current.CancellationToken));
        Assert.Contains(_logger.Collector.GetSnapshot(), r => r.Level == LogLevel.Warning && r.Message == $"File Manager upload {UploadId} returned 400: {reason}");
    }

    /// <summary>An unreachable storage service is Failed, logged at Error.</summary>
    [Fact]
    public async Task CompleteFileManagerUploadAsync_ReturnsFailed_WhenUnreachable()
    {
        using var handler = new Unreachable();

        Assert.Equal(FileUploadResult.Failed, await Sut(handler).CompleteFileManagerUploadAsync(UploadId, 1, BaseUrl, TestContext.Current.CancellationToken));
        Assert.Contains(_logger.Collector.GetSnapshot(), r => r.Level == LogLevel.Error && r.Exception != null);
    }

    // -- Abort / Delete ----------------------------------------------------------------

    /// <summary>Abort and delete go to their endpoints with the upload id / the storage path; OK is true.</summary>
    [Fact]
    public async Task AbortAndDelete_SendTheirRequests_AndReturnTrue_WhenAccepted()
    {
        using var storage = new FakeStorage(HttpStatusCode.OK, "");
        FileClient sut = Sut(storage);

        Assert.True(await sut.AbortFileManagerUploadAsync(UploadId, BaseUrl, TestContext.Current.CancellationToken));
        Assert.Equal($"{BaseUrl}/api/FileManager/abort", storage.Url);
        Assert.Contains(UploadId.ToString("D"), storage.Body, StringComparison.Ordinal);

        Assert.True(await sut.DeleteFileManagerFileAsync($"upload/FileManager/{UploadId:D}", BaseUrl, TestContext.Current.CancellationToken));
        Assert.Equal($"{BaseUrl}/api/FileManager/delete", storage.Url);
        Assert.Contains($"upload/FileManager/{UploadId:D}", storage.Body, StringComparison.Ordinal);
    }

    /// <summary>A refused delete is false, logged at Warning naming the path; an unreachable service is false, logged at Error.</summary>
    [Fact]
    public async Task Delete_ReturnsFalse_AndLogs_WhenRefusedOrUnreachable()
    {
        using var storage = new FakeStorage(HttpStatusCode.NotFound, FileStorageRefusals.FileNotFound);
        using var handler = new Unreachable();

        Assert.False(await Sut(storage).DeleteFileManagerFileAsync("upload/FileManager/x", BaseUrl, TestContext.Current.CancellationToken));
        Assert.False(await Sut(handler).DeleteFileManagerFileAsync("upload/FileManager/x", BaseUrl, TestContext.Current.CancellationToken));
        Assert.False(await Sut(handler).AbortFileManagerUploadAsync(UploadId, BaseUrl, TestContext.Current.CancellationToken));

        Assert.Contains(_logger.Collector.GetSnapshot(), r => r.Level == LogLevel.Warning && r.Message == $"File Manager delete of upload/FileManager/x returned 404: {FileStorageRefusals.FileNotFound}");
        Assert.Equal(2, _logger.Collector.GetSnapshot().Count(r => r.Level == LogLevel.Error && r.Exception != null));
    }
}
