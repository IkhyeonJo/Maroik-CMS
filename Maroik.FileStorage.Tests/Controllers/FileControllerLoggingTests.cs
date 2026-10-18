using System.Net;
using Maroik.Core.Contract.Enums;
using Maroik.Core.Contract.Interfaces;
using Maroik.FileStorage.Controllers;
using Maroik.FileStorage.Tests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Moq;

namespace Maroik.FileStorage.Tests.Controllers;

/// <summary>
/// The file-storage service's own security trail: every refused upload (virus, scanner outage,
/// traversal path, bad extension …) and every refused / missing download is logged with the path and
/// the caller's correlation id, at the level the reason deserves. (The Website logs the refusal too, but
/// only this service knows the exact reason and sees a direct call that bypassed the Website.)
/// The controller's logger is replaced with a <see cref="FakeLogger{T}"/> — the host uses Serilog,
/// which does not forward to Microsoft.Extensions.Logging providers.
/// </summary>
[Collection("FileStorage host (Program builds a process-wide Serilog logger)")]
public class FileControllerLoggingTests(WebApplicationFactory<Program> baseFactory) : IClassFixture<WebApplicationFactory<Program>>
{
    /// <summary>The leading bytes of a JPEG (JFIF) file.</summary>
    private static readonly byte[] _jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00];

    /// <summary>A client for the host with storage under the temp folder, ClamAV mocked to return <paramref name="scan"/>, and the controller logger captured.</summary>
    private (HttpClient Client, FakeLogger<FileController> Logger) CreateClient(ClamavScanResult scan)
    {
        var logger = new FakeLogger<FileController>();
        var factory = baseFactory.WithWebHostBuilder(b =>
        {
            b.UseSetting("ServerSetting:StorageRootPath", Path.GetTempPath());
            b.ConfigureServices(services =>
            {
                foreach (var d in services.Where(d => d.ServiceType == typeof(IClamavClient)).ToList()) services.Remove(d);
                var clam = new Mock<IClamavClient>();
                clam.Setup(c => c.ScanWithClamavAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<int>())).ReturnsAsync(scan);
                services.AddScoped<IClamavClient>(_ => clam.Object);
                services.AddSingleton<ILogger<FileController>>(logger);
            });
        });
        return (factory.CreateClient(), logger);
    }

    /// <summary>A fresh, not-yet-existing path under the temp folder.</summary>
    private static string UniqueTarget() => Path.Combine(Path.GetTempPath(), $"maroik_log_{Guid.NewGuid():N}");

    /// <summary>An upload form with a JPEG-typed file part plus the <c>filePath</c> and <c>correlationId</c> fields.</summary>
    private static MultipartFormDataContent Upload(string fileName, byte[] bytes, string filePath, string correlationId)
    {
        var content = new MultipartFormDataContent();
        MultipartForm.AddFile(content, "file", fileName, bytes, "image/jpeg");
        MultipartForm.AddText(content, "filePath", filePath);
        MultipartForm.AddText(content, "correlationId", correlationId);
        return content;
    }

    /// <summary>Verifies that an infected upload is refused (400) with one Warning naming the path and correlation ID.</summary>
    [Fact]
    public async Task Upload_LogsAWarningWithPathAndCorrelationId_WhenAVirusIsDetected()
    {
        var (client, logger) = CreateClient(ClamavScanResult.Infected);
        string target = UniqueTarget();

        using var form = Upload("evil.jpg", _jpeg, target, "corr-virus-1");
        var response = await client.PostAsync("/api/File/upload", form, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        FakeLogRecord record = Assert.Single(logger.Collector.GetSnapshot(), r => r.Level == LogLevel.Warning);
        Assert.Contains("File upload refused", record.Message, StringComparison.Ordinal);
        Assert.Contains("infected", record.Message, StringComparison.Ordinal);
        Assert.Contains(target, record.Message, StringComparison.Ordinal);
        Assert.Contains("corr-virus-1", record.Message, StringComparison.Ordinal);
    }

    /// <summary>Verifies that an upload that could not be scanned logs one Error with the correlation ID.</summary>
    [Fact]
    public async Task Upload_LogsAnError_WhenTheScannerIsUnavailable()
    {
        var (client, logger) = CreateClient(ClamavScanResult.Unavailable);

        using var form = Upload("x.jpg", _jpeg, UniqueTarget(), "corr-scan-1");
        await client.PostAsync("/api/File/upload", form, TestContext.Current.CancellationToken);

        FakeLogRecord record = Assert.Single(logger.Collector.GetSnapshot(), r => r.Level == LogLevel.Error);
        Assert.Contains("File upload refused", record.Message, StringComparison.Ordinal);
        Assert.Contains("could not be scanned", record.Message, StringComparison.Ordinal);
        Assert.Contains("corr-scan-1", record.Message, StringComparison.Ordinal);
    }

    /// <summary>Verifies that an upload to a path outside the storage root is refused (400) with one Warning naming the path.</summary>
    [Fact]
    public async Task Upload_LogsAWarning_WhenThePathEscapesTheStorageRoot()
    {
        var (client, logger) = CreateClient(ClamavScanResult.Clean);
        const string traversal = "../../etc/cron.d";

        using var form = Upload("x.jpg", _jpeg, traversal, "corr-path-1");
        var response = await client.PostAsync("/api/File/upload", form, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        FakeLogRecord record = Assert.Single(logger.Collector.GetSnapshot(), r => r.Level == LogLevel.Warning);
        Assert.Contains("Invalid file path", record.Message, StringComparison.Ordinal);
        Assert.Contains(traversal, record.Message, StringComparison.Ordinal);
    }

    /// <summary>Verifies that a clean upload logs only an Information entry.</summary>
    [Fact]
    public async Task Upload_LogsNoWarningOrError_WhenTheUploadSucceeds()
    {
        var (client, logger) = CreateClient(ClamavScanResult.Clean);

        using var form = Upload("ok.jpg", _jpeg, UniqueTarget(), "corr-ok");
        var response = await client.PostAsync("/api/File/upload", form, TestContext.Current.CancellationToken);

        Assert.True(response.IsSuccessStatusCode);
        Assert.DoesNotContain(logger.Collector.GetSnapshot(), r => r.Level >= LogLevel.Warning);
        Assert.Contains(logger.Collector.GetSnapshot(), r => r.Level == LogLevel.Information && r.Message.Contains("File uploaded", StringComparison.Ordinal));
    }

    /// <summary>Verifies that a download from a path outside the storage root returns 404 with one Warning naming the path and correlation ID.</summary>
    [Fact]
    public async Task Download_LogsAWarning_WhenThePathEscapesTheStorageRoot()
    {
        var (client, logger) = CreateClient(ClamavScanResult.Clean);
        using var content = MultipartForm.Of(("filePath", "../../etc/passwd"), ("correlationId", "corr-dl-1"));

        var response = await client.PostAsync("/api/File/download", content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        FakeLogRecord record = Assert.Single(logger.Collector.GetSnapshot(), r => r.Level == LogLevel.Warning);
        Assert.Contains("File download refused: path outside the storage root", record.Message, StringComparison.Ordinal);
        Assert.Contains("../../etc/passwd", record.Message, StringComparison.Ordinal);
        Assert.Contains("corr-dl-1", record.Message, StringComparison.Ordinal);
    }

    /// <summary>Verifies that a download of a missing file returns 404 with one Warning naming the path.</summary>
    [Fact]
    public async Task Download_LogsAWarning_WhenTheFileDoesNotExist()
    {
        var (client, logger) = CreateClient(ClamavScanResult.Clean);
        string missing = Path.Combine(Path.GetTempPath(), $"maroik_missing_{Guid.NewGuid():N}.jpg");
        using var content = MultipartForm.Of(("filePath", missing));

        var response = await client.PostAsync("/api/File/download", content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        FakeLogRecord record = Assert.Single(logger.Collector.GetSnapshot(), r => r.Level == LogLevel.Warning);
        Assert.Contains("File download failed: file not found", record.Message, StringComparison.Ordinal);
        Assert.Contains(missing, record.Message, StringComparison.Ordinal);
    }
}
