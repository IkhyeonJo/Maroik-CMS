using System.Net;
using Maroik.Core.Contract.Enums;
using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Contract.Messages;
using Maroik.FileStorage.Controllers;
using Maroik.FileStorage.Tests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Moq;

namespace Maroik.FileStorage.Tests.Controllers;

/// <summary>
/// Integration tests for <see cref="FileManagerController"/> (<c>api/FileManager/*</c>) through the real ASP.NET pipeline, with storage
/// in a throwaway root and ClamAV mocked: the chunk / complete / abort / delete endpoints answer with the right status and refusal
/// text, and each outcome is logged at the level it deserves with the upload id or path and the caller's correlation id (S8).
/// </summary>
[Collection("FileStorage host (Program builds a process-wide Serilog logger)")]
public sealed class FileManagerControllerTests(WebApplicationFactory<Program> baseFactory) : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    /// <summary>The throwaway parent of the storage root.</summary>
    private readonly string _base = Path.Combine(Path.GetTempPath(), "fmc-" + Guid.NewGuid().ToString("N"));

    /// <summary>The upload id these tests use.</summary>
    private static readonly string UploadId = Guid.NewGuid().ToString("D");

    /// <summary>The controller's captured log.</summary>
    private readonly FakeLogger<FileManagerController> _logger = new();

    /// <summary>Removes the throwaway storage.</summary>
    public void Dispose()
    {
        if (Directory.Exists(_base)) Directory.Delete(_base, recursive: true);
    }

    /// <summary>A client for a host storing under the throwaway root, with every scan answering <paramref name="scan"/>.</summary>
    private HttpClient Client(ClamavScanResult scan = ClamavScanResult.Clean)
    {
        var factory = baseFactory.WithWebHostBuilder(b =>
        {
            b.UseSetting("ServerSetting:StorageRootPath", Path.Combine(_base, "upload"));
            b.ConfigureServices(services =>
            {
                foreach (var d in services.Where(d => d.ServiceType == typeof(IClamavClient)).ToList()) services.Remove(d);
                var clam = new Mock<IClamavClient>();
                clam.Setup(c => c.ScanWithClamavAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(scan);
                services.AddScoped<IClamavClient>(_ => clam.Object);
                services.AddSingleton<ILogger<FileManagerController>>(_logger);
            });
        });
        return factory.CreateClient();
    }

    /// <summary>POSTs a chunk of <paramref name="bytes"/> at <paramref name="offset"/>.</summary>
    private static async Task<HttpResponseMessage> ChunkAsync(HttpClient client, long offset, byte[] bytes, string uploadId)
    {
        using var form = MultipartForm.Of(("uploadId", uploadId), ("offset", offset.ToString(System.Globalization.CultureInfo.InvariantCulture)), ("correlationId", "corr-1"));
        MultipartForm.AddFile(form, "chunk", "blob", bytes, "application/octet-stream");
        return await client.PostAsync("/api/FileManager/chunk", form, TestContext.Current.CancellationToken);
    }

    /// <summary>POSTs the text fields to <paramref name="action"/>.</summary>
    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string action, params (string, string)[] fields)
    {
        using var form = MultipartForm.Of([.. fields, ("correlationId", "corr-1")]);
        return await client.PostAsync($"/api/FileManager/{action}", form, TestContext.Current.CancellationToken);
    }

    /// <summary>The single log entry at <paramref name="level"/>.</summary>
    private FakeLogRecord Only(LogLevel level) => Assert.Single(_logger.Collector.GetSnapshot(), r => r.Level == level);

    /// <summary>Chunks answer the new length; then complete stores the file (logged at Information) and it can be downloaded back.</summary>
    [Fact]
    public async Task ChunkThenComplete_StoresTheFile()
    {
        using HttpClient client = Client();

        using var first = await ChunkAsync(client, 0, [1, 2, 3], UploadId);
        using var second = await ChunkAsync(client, 3, [4], UploadId);
        using var complete = await PostAsync(client, "complete", ("uploadId", UploadId), ("expectedSize", "4"));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal("3", await first.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("4", await second.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);
        Assert.Equal([1, 2, 3, 4], await File.ReadAllBytesAsync(Path.Combine(_base, "upload", "FileManager", UploadId), TestContext.Current.CancellationToken));
        FakeLogRecord stored = Only(LogLevel.Information);
        Assert.Equal($"File Manager upload {UploadId} stored (4 bytes). CorrelationId=corr-1", stored.Message);
    }

    /// <summary>A chunk at the wrong offset is a conflict, logged at Warning.</summary>
    [Fact]
    public async Task Chunk_AtTheWrongOffset_IsAConflict()
    {
        using HttpClient client = Client();
        using var first = await ChunkAsync(client, 0, [1], UploadId);

        using var response = await ChunkAsync(client, 5, [2], UploadId);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(FileStorageRefusals.OffsetMismatch, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Contains($"File Manager chunk refused: {FileStorageRefusals.OffsetMismatch}. UploadId={UploadId} Offset=5", Only(LogLevel.Warning).Message, StringComparison.Ordinal);
    }

    /// <summary>A chunk naming an invalid upload id, or carrying no file part, is a bad request.</summary>
    [Fact]
    public async Task Chunk_WithAnInvalidUploadIdOrNoFile_IsABadRequest()
    {
        using HttpClient client = Client();

        using var badId = await ChunkAsync(client, 0, [1], "../x");
        using var noFile = await PostAsync(client, "chunk", ("uploadId", UploadId), ("offset", "0"));

        Assert.Equal(HttpStatusCode.BadRequest, badId.StatusCode);
        Assert.Equal(FileStorageRefusals.InvalidUpload, await badId.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(HttpStatusCode.BadRequest, noFile.StatusCode);
    }

    /// <summary>An infected file is refused (400, logged at Warning); a scanner outage is refused too but logged at Error (an operational fault).</summary>
    [Theory]
    [InlineData(ClamavScanResult.Infected, FileStorageRefusals.Infected, LogLevel.Warning)]
    [InlineData(ClamavScanResult.Unavailable, FileStorageRefusals.ScanUnavailable, LogLevel.Error)]
    public async Task Complete_RefusesAnUnsafeFile(ClamavScanResult scan, string refusal, LogLevel level)
    {
        using HttpClient client = Client(scan);
        using var chunk = await ChunkAsync(client, 0, [1], UploadId);

        using var response = await PostAsync(client, "complete", ("uploadId", UploadId), ("expectedSize", "1"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(refusal, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal($"File Manager upload refused: {refusal}. UploadId={UploadId} CorrelationId=corr-1", Only(level).Message);
    }

    /// <summary>Abort answers OK for a valid id and drops its staging file.</summary>
    [Fact]
    public async Task Abort_DropsTheStagingFile()
    {
        using HttpClient client = Client();
        using var chunk = await ChunkAsync(client, 0, [1], UploadId);

        using var response = await PostAsync(client, "abort", ("uploadId", UploadId));
        using var invalid = await PostAsync(client, "abort", ("uploadId", "x"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(File.Exists(Path.Combine(_base, "upload", "FileManager", ".staging", UploadId)));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    /// <summary>Delete removes a stored File Manager file (logged at Information); a missing one is 404; any other path is refused at Warning.</summary>
    [Fact]
    public async Task Delete_RemovesOnlyAFileManagerFile()
    {
        using HttpClient client = Client();
        string stored = Path.Combine(_base, "upload", "FileManager", UploadId);
        Directory.CreateDirectory(Path.GetDirectoryName(stored)!);
        await File.WriteAllBytesAsync(stored, [1], TestContext.Current.CancellationToken);

        using var deleted = await PostAsync(client, "delete", ("filePath", $"upload/FileManager/{UploadId}"));
        using var missing = await PostAsync(client, "delete", ("filePath", $"upload/FileManager/{UploadId}"));
        using var refused = await PostAsync(client, "delete", ("filePath", "upload/Forum/a.zip"));

        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        Assert.False(File.Exists(stored));
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Contains(_logger.Collector.GetSnapshot(), r => r.Level == LogLevel.Information && r.Message == $"File Manager file deleted: upload/FileManager/{UploadId}. CorrelationId=corr-1");
        Assert.Contains(_logger.Collector.GetSnapshot(), r => r.Level == LogLevel.Warning && r.Message == $"File Manager delete refused: {FileStorageRefusals.NotAFileManagerFile}. FilePath=upload/Forum/a.zip CorrelationId=corr-1");
    }

    /// <summary>A failure inside the service is a bad request with a fixed text (never the exception's), logged at Error with the exception.</summary>
    [Fact]
    public async Task AServiceFailure_IsLoggedWithTheException_AndNotLeaked()
    {
        var service = new Mock<Maroik.FileStorage.Interfaces.IFileManagerStorageService>();
        service.Setup(s => s.CompleteAsync(It.IsAny<string?>(), It.IsAny<long>(), It.IsAny<CancellationToken>())).ThrowsAsync(new IOException("disk-secret"));
        var factory = baseFactory.WithWebHostBuilder(b => b.ConfigureServices(services =>
        {
            foreach (var d in services.Where(d => d.ServiceType == typeof(Maroik.FileStorage.Interfaces.IFileManagerStorageService)).ToList()) services.Remove(d);
            services.AddScoped(_ => service.Object);
            services.AddSingleton<ILogger<FileManagerController>>(_logger);
        }));
        using HttpClient client = factory.CreateClient();

        using var response = await PostAsync(client, "complete", ("uploadId", UploadId), ("expectedSize", "1"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain("disk-secret", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
        FakeLogRecord error = Only(LogLevel.Error);
        Assert.Equal("disk-secret", error.Exception?.Message);
    }
}
