using System.Net;
using System.Net.Http.Headers;
using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Contract.Misc.Enums;
using Maroik.FileStorage.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Maroik.FileStorage.Tests.Controllers;

/// <summary>
/// Integration tests for <see cref="Maroik.FileStorage.Controllers.FileController"/>.
/// Uses <see cref="WebApplicationFactory{TEntryPoint}"/> to spin up the real ASP.NET pipeline
/// in memory, replacing <see cref="IClamavClient"/> with a controllable mock.
///
/// Covers every validation path (extension, size, magic bytes, ClamAV, encrypted ZIP), the happy path
/// (a clean image is written under the storage root and can be downloaded back) and the download edge
/// cases (unknown type, unreadable file).
/// </summary>
[Collection("FileStorage host (Program builds a process-wide Serilog logger)")]
public class FileControllerTests(WebApplicationFactory<Program> baseFactory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    // -- Factory helpers ------------------------------------------------------

    /// <summary>Returns a factory whose ClamAV mock always marks files as clean.</summary>
    private WebApplicationFactory<Program> FactoryWithCleanScan() =>
        baseFactory.WithWebHostBuilder(b => b.ConfigureServices(services =>
        {
            RemoveService<IClamavClient>(services);
            var mock = new Mock<IClamavClient>();
            mock.Setup(c => c.ScanWithClamavAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<int>()))
                .ReturnsAsync(ClamavScanResult.Clean);
            services.AddScoped<IClamavClient>(_ => mock.Object);
        }));

    /// <summary>Returns a factory whose ClamAV mock can never complete a scan (daemon down).</summary>
    private WebApplicationFactory<Program> FactoryWithClamavUnavailable() =>
        baseFactory.WithWebHostBuilder(b =>
        {
            // Storage root = OS temp dir, so the request's target path passes the path check and actually
            // reaches the ClamAV scan (under the default <contentRoot>/upload root it is rejected first).
            b.UseSetting("ServerSetting:StorageRootPath", Path.GetTempPath());
            b.ConfigureServices(services =>
            {
                RemoveService<IClamavClient>(services);
                var mock = new Mock<IClamavClient>();
                mock.Setup(c => c.ScanWithClamavAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<int>()))
                    .ReturnsAsync(ClamavScanResult.Unavailable);
                services.AddScoped<IClamavClient>(_ => mock.Object);
            });
        });

    /// <summary>Returns a factory whose ClamAV mock always reports a virus.</summary>
    private WebApplicationFactory<Program> FactoryWithVirusDetected() =>
        baseFactory.WithWebHostBuilder(b =>
        {
            // Storage root = OS temp dir, so the request's target path passes the path check and actually
            // reaches the ClamAV scan (under the default <contentRoot>/upload root it is rejected first).
            b.UseSetting("ServerSetting:StorageRootPath", Path.GetTempPath());
            b.ConfigureServices(services =>
            {
                RemoveService<IClamavClient>(services);
                var mock = new Mock<IClamavClient>();
                mock.Setup(c => c.ScanWithClamavAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<int>()))
                    .ReturnsAsync(ClamavScanResult.Infected);
                services.AddScoped<IClamavClient>(_ => mock.Object);
            });
        });

    private static void RemoveService<T>(IServiceCollection services)
    {
        var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(T));
        if (descriptor != null) services.Remove(descriptor);
    }

    /// <summary>
    /// A factory whose file-storage root is the OS temp directory, so the download tests below can
    /// point at a real temp file and have it resolve as "within the root" (Program.cs otherwise
    /// confines every path to &lt;contentRoot&gt;/upload).
    /// </summary>
    private WebApplicationFactory<Program> FactoryWithTempStorageRoot() =>
        baseFactory.WithWebHostBuilder(b =>
            b.UseSetting("ServerSetting:StorageRootPath", Path.GetTempPath()));

    /// <summary>A not-yet-existing target directory inside the OS temp dir (the storage root of the scan-failure factories).</summary>
    private static string UniqueTempTarget() => Path.Combine(Path.GetTempPath(), $"maroik_scan_{Guid.NewGuid():N}");

    // -- Upload: invalid input (no ClamAV call needed) ------------------------

    /// <summary>Verifies that <c>Upload</c> returns bad request when no file is provided.</summary>
    [Fact]
    public async Task Upload_ReturnsBadRequest_WhenNoFileIsProvided()
    {
        var client = FactoryWithCleanScan().CreateClient();
 #pragma warning disable IDE0028
        using var content = new MultipartFormDataContent();
 #pragma warning restore IDE0028
        content.Add(new StringContent("/tmp/uploads"), "filePath");
        // Intentionally omit the "file" part

        var response = await client.PostAsync("/api/File/upload", content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>Verifies that <c>Upload</c> returns bad request when file is empty.</summary>
    [Fact]
    public async Task Upload_ReturnsBadRequest_WhenFileIsEmpty()
    {
        var client = FactoryWithCleanScan().CreateClient();
        using var content = new MultipartFormDataContent();
        var emptyFile = new ByteArrayContent([]);
        emptyFile.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg");
        content.Add(emptyFile, "file", "empty.jpg");
        content.Add(new StringContent("/tmp/uploads"), "filePath");

        var response = await client.PostAsync("/api/File/upload", content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>Verifies that <c>Upload</c> returns bad request when extension is not allowed.</summary>
    [Theory]
    [InlineData("malware.exe")]
    [InlineData("script.sh")]
    [InlineData("archive.tar.gz")]
    [InlineData("document.pdf")]
    public async Task Upload_ReturnsBadRequest_WhenExtensionIsNotAllowed(string fileName)
    {
        var client = FactoryWithCleanScan().CreateClient();
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent([0x01, 0x02, 0x03]);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/octet-stream");
        content.Add(fileContent, "file", fileName);
        content.Add(new StringContent("/tmp/uploads"), "filePath");

        var response = await client.PostAsync("/api/File/upload", content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>Verifies that <c>Upload</c> returns bad request when file size exceeds10 mb.</summary>
    [Fact]
    public async Task Upload_ReturnsBadRequest_WhenFileSizeExceeds10MB()
    {
        var client = FactoryWithCleanScan().CreateClient();
        using var content = new MultipartFormDataContent();
        var largeBytes = new byte[11 * 1024 * 1024]; // 11 MB
        var fileContent = new ByteArrayContent(largeBytes);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg");
        content.Add(fileContent, "file", "large.jpg");
        content.Add(new StringContent("/tmp/uploads"), "filePath");

        var response = await client.PostAsync("/api/File/upload", content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // -- Upload: magic-byte validation ----------------------------------------

    /// <summary>Verifies that <c>Upload</c> returns bad request when jpg has wrong magic bytes.</summary>
    [Fact]
    public async Task Upload_ReturnsBadRequest_WhenJpgHasWrongMagicBytes()
    {
        var client = FactoryWithCleanScan().CreateClient();
        using var content = new MultipartFormDataContent();
        // JPEG requires 0xFF 0xD8 as first two bytes
        var fileContent = new ByteArrayContent([0x00, 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06]);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg");
        content.Add(fileContent, "file", "fake.jpg");
        content.Add(new StringContent("/tmp/uploads"), "filePath");

        var response = await client.PostAsync("/api/File/upload", content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>Verifies that <c>Upload</c> returns bad request when jpeg extension has wrong magic bytes.</summary>
    [Fact]
    public async Task Upload_ReturnsBadRequest_WhenJpegExtensionHasWrongMagicBytes()
    {
        var client = FactoryWithCleanScan().CreateClient();
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]); // PNG bytes, not JPEG
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg");
        content.Add(fileContent, "file", "wrong.jpeg");
        content.Add(new StringContent("/tmp/uploads"), "filePath");

        var response = await client.PostAsync("/api/File/upload", content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>Verifies that <c>Upload</c> returns bad request when png has wrong magic bytes.</summary>
    [Fact]
    public async Task Upload_ReturnsBadRequest_WhenPngHasWrongMagicBytes()
    {
        var client = FactoryWithCleanScan().CreateClient();
        using var content = new MultipartFormDataContent();
        // PNG requires: 89 50 4E 47 0D 0A 1A 0A ?? use wrong bytes
        var fileContent = new ByteArrayContent([0x89, 0x50, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00]);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");
        content.Add(fileContent, "file", "fake.png");
        content.Add(new StringContent("/tmp/uploads"), "filePath");

        var response = await client.PostAsync("/api/File/upload", content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>Verifies that <c>Upload</c> returns bad request when zip has wrong magic bytes.</summary>
    [Fact]
    public async Task Upload_ReturnsBadRequest_WhenZipHasWrongMagicBytes()
    {
        var client = FactoryWithCleanScan().CreateClient();
        using var content = new MultipartFormDataContent();
        // ZIP requires: 50 4B 03 04 ?? use wrong bytes
        var fileContent = new ByteArrayContent([0x00, 0x00, 0x00, 0x00, 0x05, 0x06, 0x07, 0x08]);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/zip");
        content.Add(fileContent, "file", "fake.zip");
        content.Add(new StringContent("/tmp/uploads"), "filePath");

        var response = await client.PostAsync("/api/File/upload", content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // -- Upload: ClamAV scan --------------------------------------------------

    /// <summary>Verifies that <c>Upload</c> returns bad request when clam av reports virus.</summary>
    [Fact]
    public async Task Upload_ReturnsBadRequest_WhenClamAvReportsVirus()
    {
        var client = FactoryWithVirusDetected().CreateClient();
        using var content = new MultipartFormDataContent();
        // Valid JPEG magic bytes so it passes the magic-byte check and reaches the scan
        var fileContent = new ByteArrayContent([0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00]);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg");
        content.Add(fileContent, "file", "infected.jpg");
        content.Add(new StringContent(UniqueTempTarget()), "filePath");

        var response = await client.PostAsync("/api/File/upload", content, TestContext.Current.CancellationToken);

        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        // The refusal must come from the scan itself (not from an earlier path / extension check).
        Assert.Contains("infected", body);
    }

    /// <summary>
    /// Verifies that <c>Upload</c> refuses the file with a distinct "could not be scanned" reason —
    /// not the virus message — when the ClamAV daemon cannot complete a scan.
    /// </summary>
    [Fact]
    public async Task Upload_ReturnsBadRequest_WithScanUnavailableReason_WhenClamAvIsDown()
    {
        var client = FactoryWithClamavUnavailable().CreateClient();
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent([0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00]);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg");
        content.Add(fileContent, "file", "unscanned.jpg");
        content.Add(new StringContent(UniqueTempTarget()), "filePath");

        var response = await client.PostAsync("/api/File/upload", content, TestContext.Current.CancellationToken);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("could not be scanned", body);
        Assert.DoesNotContain("infected", body);
    }

    // -- Upload: encrypted ZIP ------------------------------------------------

    /// <summary>Verifies that <c>Upload</c> returns bad request when zip is encrypted.</summary>
    [Fact]
    public async Task Upload_ReturnsBadRequest_WhenZipIsEncrypted()
    {
        // Create a minimal encrypted ZIP in memory using SharpZipLib conventions.
        // The encrypted flag bit (0x0001) in the local file header's general purpose flags indicates encryption.
        // We construct a valid ZIP local file header with the encryption bit set.
        // Format: PK\x03\x04 (4) | version (2) | flags=0x0001 (2, encryption bit) | ...
        byte[] encryptedZipBytes =
        [
            0x50, 0x4B, 0x03, 0x04, // Local file header signature
            0x14, 0x00,             // Version needed: 2.0
            0x01, 0x00,             // General purpose bit flag: bit 0 set = encrypted
            0x00, 0x00,             // Compression method: stored
            0x00, 0x00,             // Last mod time
            0x00, 0x00,             // Last mod date
            0x00, 0x00, 0x00, 0x00, // CRC-32
            0x04, 0x00, 0x00, 0x00, // Compressed size
            0x04, 0x00, 0x00, 0x00, // Uncompressed size
            0x04, 0x00,             // File name length
            0x00, 0x00,             // Extra field length
            0x74, 0x65, 0x73, 0x74, // File name: "test"
            0x01, 0x02, 0x03, 0x04, // Encrypted file data (dummy)
            // Central directory
            0x50, 0x4B, 0x01, 0x02,
            0x14, 0x00, 0x14, 0x00,
            0x01, 0x00,             // Encryption bit set in central directory too
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x04, 0x00, 0x00, 0x00,
            0x04, 0x00, 0x00, 0x00,
            0x04, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x74, 0x65, 0x73, 0x74, // File name
            // End of central directory
            0x50, 0x4B, 0x05, 0x06,
            0x00, 0x00, 0x00, 0x00,
            0x01, 0x00, 0x01, 0x00,
            0x32, 0x00, 0x00, 0x00,
            0x28, 0x00, 0x00, 0x00,
            0x00, 0x00
        ];

        var client = FactoryWithCleanScan().CreateClient();
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(encryptedZipBytes);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/zip");
        content.Add(fileContent, "file", "encrypted.zip");
        content.Add(new StringContent("/tmp/uploads"), "filePath");

        var response = await client.PostAsync("/api/File/upload", content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // -- Upload: unexpected exception (logging path) --------------------------

    /// <summary>Verifies that <c>Upload</c> returns bad request — and doesn't fail DI resolution
    /// now that <see cref="Maroik.FileStorage.Controllers.FileController"/> takes an
    /// <c>ILogger</c> — when the validation service throws, with a correlationId form field present.</summary>
    [Fact]
    public async Task Upload_ReturnsBadRequest_WhenValidationServiceThrows()
    {
        var factory = baseFactory.WithWebHostBuilder(b => b.ConfigureServices(services =>
        {
            RemoveService<IFileValidationService>(services);
            var mock = new Mock<IFileValidationService>();
            mock.Setup(v => v.ValidateAndSaveAsync(It.IsAny<IFormFile>(), It.IsAny<string>()))
                .ThrowsAsync(new InvalidOperationException("disk full"));
            services.AddScoped<IFileValidationService>(_ => mock.Object);
        }));
        var client = factory.CreateClient();
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent([0xFF, 0xD8, 0xFF, 0xE0]);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg");
        content.Add(fileContent, "file", "test.jpg");
        content.Add(new StringContent("/tmp/uploads"), "filePath");
        content.Add(new StringContent("test-correlation-123"), "correlationId");

        var response = await client.PostAsync("/api/File/upload", content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // -- Download -------------------------------------------------------------

    /// <summary>Verifies that <c>Download</c> returns not found when file does not exist.</summary>
    [Fact]
    public async Task Download_ReturnsNotFound_WhenFileDoesNotExist()
    {
        var client = baseFactory.CreateClient();
 #pragma warning disable IDE0028
        using var content = new MultipartFormDataContent();
 #pragma warning restore IDE0028
        content.Add(new StringContent("/nonexistent/path/does_not_exist_xyz.jpg"), "filePath");

        var response = await client.PostAsync("/api/File/download", content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>Verifies that <c>Download</c> returns file bytes when file exists.</summary>
    [Fact]
    public async Task Download_ReturnsFileBytes_WhenFileExists()
    {
        // Arrange: write a small temp file to disk
        var tempPath = Path.Combine(Path.GetTempPath(), $"maroik_test_{Guid.NewGuid()}.jpg");
        byte[] jpegBytes = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10];
        await File.WriteAllBytesAsync(tempPath, jpegBytes, TestContext.Current.CancellationToken);

        try
        {
            var client = FactoryWithTempStorageRoot().CreateClient();
 #pragma warning disable IDE0028
            using var content = new MultipartFormDataContent();
 #pragma warning restore IDE0028
            content.Add(new StringContent(tempPath), "filePath");

            var response = await client.PostAsync("/api/File/download", content, TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var responseBytes = await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
            Assert.Equal(jpegBytes, responseBytes);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    /// <summary>Verifies that <c>Download</c> returns correct content type for png.</summary>
    [Fact]
    public async Task Download_ReturnsCorrectContentType_ForPng()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"maroik_test_{Guid.NewGuid()}.png");
        byte[] pngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        await File.WriteAllBytesAsync(tempPath, pngBytes, TestContext.Current.CancellationToken);

        try
        {
            var client = FactoryWithTempStorageRoot().CreateClient();
 #pragma warning disable IDE0028
            using var content = new MultipartFormDataContent();
 #pragma warning restore IDE0028
            content.Add(new StringContent(tempPath), "filePath");

            var response = await client.PostAsync("/api/File/download", content, TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    // -- Upload: success; Download: unknown type and unreadable file ----------------------

    /// <summary>A valid, clean image is stored under the target directory and the caller is told so; it can then be downloaded back byte for byte.</summary>
    [Fact]
    public async Task Upload_StoresAValidCleanImage_AndItCanBeDownloadedBack()
    {
        string targetDir = UniqueTempTarget();
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00];
        try
        {
            var factory = baseFactory.WithWebHostBuilder(b =>
            {
                b.UseSetting("ServerSetting:StorageRootPath", Path.GetTempPath());
                b.ConfigureServices(services =>
                {
                    RemoveService<IClamavClient>(services);
                    var mock = new Mock<IClamavClient>();
                    mock.Setup(c => c.ScanWithClamavAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<int>()))
                        .ReturnsAsync(ClamavScanResult.Clean);
                    services.AddScoped<IClamavClient>(_ => mock.Object);
                });
            });
            var client = factory.CreateClient();
            using var upload = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(png);
            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");
            upload.Add(fileContent, "file", "stored.png");
            upload.Add(new StringContent(targetDir), "filePath");
            upload.Add(new StringContent("corr-1"), "correlationId");

            var uploaded = await client.PostAsync("/api/File/upload", upload, TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, uploaded.StatusCode);
            Assert.Equal("File uploaded successfully.", await uploaded.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            string stored = Path.Combine(targetDir, "stored.png");
            Assert.Equal(png, await File.ReadAllBytesAsync(stored, TestContext.Current.CancellationToken));

 #pragma warning disable IDE0028
            using var download = new MultipartFormDataContent();
 #pragma warning restore IDE0028
            download.Add(new StringContent(stored), "filePath");
            var downloaded = await client.PostAsync("/api/File/download", download, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, downloaded.StatusCode);
            Assert.Equal(png, await downloaded.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        }
        finally
        {
            if (Directory.Exists(targetDir)) Directory.Delete(targetDir, recursive: true);
        }
    }

    /// <summary>A stored file whose extension has no known media type is served as a generic binary download.</summary>
    [Fact]
    public async Task Download_FallsBackToOctetStream_ForAnUnknownFileType()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"maroik_test_{Guid.NewGuid()}.unknownext");
        await File.WriteAllBytesAsync(tempPath, [1, 2, 3], TestContext.Current.CancellationToken);
        try
        {
            var client = FactoryWithTempStorageRoot().CreateClient();
 #pragma warning disable IDE0028
            using var content = new MultipartFormDataContent();
 #pragma warning restore IDE0028
            content.Add(new StringContent(tempPath), "filePath");

            var response = await client.PostAsync("/api/File/download", content, TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("application/octet-stream", response.Content.Headers.ContentType?.MediaType);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    /// <summary>A file that exists but cannot be read is answered with a plain "Invalid file", not a server error.</summary>
    [Fact]
    public async Task Download_ReturnsBadRequest_WhenTheFileCannotBeRead()
    {
        if (OperatingSystem.IsWindows()) return; // permissions are set through Unix file modes
        var tempPath = Path.Combine(Path.GetTempPath(), $"maroik_test_{Guid.NewGuid()}.png");
        await File.WriteAllBytesAsync(tempPath, [1, 2, 3], TestContext.Current.CancellationToken);
        File.SetUnixFileMode(tempPath, UnixFileMode.None);
        try
        {
            if (CanRead(tempPath)) return; // running as root: file permissions do not apply, so the read cannot be made to fail
            var client = FactoryWithTempStorageRoot().CreateClient();
 #pragma warning disable IDE0028
            using var content = new MultipartFormDataContent();
 #pragma warning restore IDE0028
            content.Add(new StringContent(tempPath), "filePath");

            var response = await client.PostAsync("/api/File/download", content, TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("Invalid file", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        }
        finally
        {
            File.SetUnixFileMode(tempPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            File.Delete(tempPath);
        }
        return;

        static bool CanRead(string path)
        {
            try { _ = File.ReadAllBytes(path); return true; }
            catch (UnauthorizedAccessException) { return false; }
        }
    }
}
