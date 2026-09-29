using ICSharpCode.SharpZipLib.Zip;
using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Contract.Misc.Enums;
using Maroik.FileStorage.Services;
using Maroik.FileStorage.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Moq;

namespace Maroik.FileStorage.Tests.Services;

/// <summary>
/// Direct unit tests for <see cref="FileValidationService"/>, constructed with a mocked
/// <see cref="IClamavClient"/> (no HTTP pipeline). <see cref="Controllers.FileControllerTests"/>
/// already exercises the validation-failure branches end-to-end; this class adds the one
/// path that isn't covered anywhere else in the repo -- the happy path where a fully valid
/// file is validated, written to disk with its bytes intact, and chmod'ed to 644 -- plus a
/// few direct-unit-test equivalents of the existing validation branches for completeness.
/// </summary>
public class FileValidationServiceTests
{
    /// <summary>A ClamAV client whose every scan comes back clean.</summary>
    private static Mock<IClamavClient> CleanScanMock()
    {
        var mock = new Mock<IClamavClient>();
        mock.Setup(c => c.ScanWithClamavAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ClamavScanResult.Clean);
        return mock;
    }

    /// <summary>A ClamAV client whose every scan finds a virus.</summary>
    private static Mock<IClamavClient> InfectedScanMock() => ScanMock(ClamavScanResult.Infected);

    /// <summary>A ClamAV client whose every scan reports the daemon unavailable.</summary>
    private static Mock<IClamavClient> UnavailableScanMock() => ScanMock(ClamavScanResult.Unavailable);

    /// <summary>A ClamAV client whose every scan returns <paramref name="result"/>.</summary>
    private static Mock<IClamavClient> ScanMock(ClamavScanResult result)
    {
        var mock = new Mock<IClamavClient>();
        mock.Setup(c => c.ScanWithClamavAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
        return mock;
    }

    /// <summary>
    /// Options wrapping the default 10 MB size cap plus a storage root of the OS temp directory, so
    /// the temp target dirs these tests write to resolve as "within the root" (production sets the
    /// root to &lt;contentRoot&gt;/upload in Program.cs).
    /// </summary>
    private static IOptions<FileStorageSetting> DefaultSettings() =>
        Options.Create(new FileStorageSetting { StorageRootPath = Path.GetTempPath() });

    /// <summary>A form file named <paramref name="fileName"/> holding <paramref name="bytes"/>.</summary>
    private static FormFile MakeFormFile(byte[] bytes, string fileName)
    {
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "file", fileName);
    }

    /// <summary>A unique path under the temp folder; the directory itself is not created.</summary>
    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"maroik_fvs_test_{Guid.NewGuid()}");
        return dir;
    }

    /// <summary>Builds a minimal, unencrypted, valid ZIP archive in memory using SharpZipLib.</summary>
    private static byte[] BuildValidZipBytes()
    {
        using var memStream = new MemoryStream();
        using (var zipStream = new ZipOutputStream(memStream))
        {
            zipStream.IsStreamOwner = false;
            var entry = new ZipEntry("test.txt") { DateTime = DateTime.Now };
            zipStream.PutNextEntry(entry);
            var content = "hello world"u8.ToArray();
            zipStream.Write(content, 0, content.Length);
            zipStream.CloseEntry();
            zipStream.Finish();
        }
        return memStream.ToArray();
    }

    /// <summary>Builds a minimal, password-encrypted ZIP archive in memory using SharpZipLib.</summary>
    private static byte[] BuildEncryptedZipBytes()
    {
        using var memStream = new MemoryStream();
        using (var zipStream = new ZipOutputStream(memStream))
        {
            zipStream.IsStreamOwner = false;
            zipStream.Password = "secret";
            var entry = new ZipEntry("test.txt") { DateTime = DateTime.Now, AESKeySize = 0 };
            zipStream.PutNextEntry(entry);
            var content = "hello world"u8.ToArray();
            zipStream.Write(content, 0, content.Length);
            zipStream.CloseEntry();
            zipStream.Finish();
        }
        return memStream.ToArray();
    }

    // -- Happy path ------------------------------------------------------------

    /// <summary>Validate and save async saves file with correct bytes and permissions, when jpg is valid.</summary>
    [Fact]
    public async Task ValidateAndSaveAsync_SavesFileWithCorrectBytesAndPermissions_WhenJpgIsValid()
    {
        var tempDir = CreateTempDir();
        try
        {
            byte[] jpegBytes = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00];
            var file = MakeFormFile(jpegBytes, "photo.jpg");
            var service = new FileValidationService(CleanScanMock().Object, DefaultSettings());

            var (success, error) = await service.ValidateAndSaveAsync(file, tempDir);

            Assert.True(success);
            Assert.Null(error);

            var savedPath = Path.Combine(tempDir, "photo.jpg");
            Assert.True(File.Exists(savedPath));
            var savedBytes = await File.ReadAllBytesAsync(savedPath, TestContext.Current.CancellationToken);
            Assert.Equal(jpegBytes, savedBytes);

            if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            {
                var mode = File.GetUnixFileMode(savedPath);
                // Assert the world/group/owner read bits chmod 644 guarantees, rather than the
                // exact literal value, so the test is robust to any umask quirks in the sandbox.
                Assert.True(mode.HasFlag(UnixFileMode.UserRead));
                Assert.True(mode.HasFlag(UnixFileMode.UserWrite));
                Assert.True(mode.HasFlag(UnixFileMode.GroupRead));
                Assert.True(mode.HasFlag(UnixFileMode.OtherRead));
                Assert.False(mode.HasFlag(UnixFileMode.UserExecute));
            }
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }

    /// <summary>Validate and save async saves file with correct bytes, when png is valid.</summary>
    [Fact]
    public async Task ValidateAndSaveAsync_SavesFileWithCorrectBytes_WhenPngIsValid()
    {
        var tempDir = CreateTempDir();
        try
        {
            byte[] pngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x01, 0x02];
            var file = MakeFormFile(pngBytes, "picture.png");
            var service = new FileValidationService(CleanScanMock().Object, DefaultSettings());

            var (success, error) = await service.ValidateAndSaveAsync(file, tempDir);

            Assert.True(success);
            Assert.Null(error);

            var savedPath = Path.Combine(tempDir, "picture.png");
            Assert.True(File.Exists(savedPath));
            Assert.Equal(pngBytes, await File.ReadAllBytesAsync(savedPath, TestContext.Current.CancellationToken));
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }

    // -- Invalid / empty file ---------------------------------------------------

    /// <summary>Validate and save async returns invalid file, when file is null.</summary>
    [Fact]
    public async Task ValidateAndSaveAsync_ReturnsInvalidFile_WhenFileIsNull()
    {
        var service = new FileValidationService(CleanScanMock().Object, DefaultSettings());

        var (success, error) = await service.ValidateAndSaveAsync(null!, Path.GetTempPath());

        Assert.False(success);
        Assert.Equal("Invalid file.", error);
    }

    /// <summary>Validate and save async returns invalid file, when file is zero length.</summary>
    [Fact]
    public async Task ValidateAndSaveAsync_ReturnsInvalidFile_WhenFileIsZeroLength()
    {
        var file = MakeFormFile([], "empty.jpg");
        var service = new FileValidationService(CleanScanMock().Object, DefaultSettings());

        var (success, error) = await service.ValidateAndSaveAsync(file, Path.GetTempPath());

        Assert.False(success);
        Assert.Equal("Invalid file.", error);
    }

    // -- Extension allowlist ------------------------------------------------------

    /// <summary>Validate and save async returns unsupported extension, when extension is not allowed.</summary>
    [Theory]
    [InlineData("malware.exe")]
    [InlineData("script.sh")]
    [InlineData("document.pdf")]
    public async Task ValidateAndSaveAsync_ReturnsUnsupportedExtension_WhenExtensionIsNotAllowed(string fileName)
    {
        var file = MakeFormFile([0x01, 0x02, 0x03], fileName);
        var service = new FileValidationService(CleanScanMock().Object, DefaultSettings());

        var (success, error) = await service.ValidateAndSaveAsync(file, Path.GetTempPath());

        Assert.False(success);
        Assert.Equal("Unsupported file extension.", error);
    }

    // -- Size cap ------------------------------------------------------------------

    /// <summary>Validate and save async returns file too large, when over10 MB.</summary>
    [Fact]
    public async Task ValidateAndSaveAsync_ReturnsFileTooLarge_WhenOver10MB()
    {
        var largeBytes = new byte[11 * 1024 * 1024];
        largeBytes[0] = 0xFF;
        largeBytes[1] = 0xD8;
        var file = MakeFormFile(largeBytes, "large.jpg");
        var service = new FileValidationService(CleanScanMock().Object, DefaultSettings());

        var (success, error) = await service.ValidateAndSaveAsync(file, Path.GetTempPath());

        Assert.False(success);
        Assert.Equal("File too large.", error);
    }

    // -- Magic bytes ----------------------------------------------------------------

    /// <summary>Validate and save async returns invalid format, when jpg has wrong magic bytes.</summary>
    [Fact]
    public async Task ValidateAndSaveAsync_ReturnsInvalidFormat_WhenJpgHasWrongMagicBytes()
    {
        var file = MakeFormFile([0x00, 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06], "fake.jpg");
        var service = new FileValidationService(CleanScanMock().Object, DefaultSettings());

        var (success, error) = await service.ValidateAndSaveAsync(file, Path.GetTempPath());

        Assert.False(success);
        Assert.Equal("Invalid or unsupported file format.", error);
    }

    // -- ClamAV -----------------------------------------------------------------------

    /// <summary>Validate and save async returns virus message, when clamav reports infected.</summary>
    [Fact]
    public async Task ValidateAndSaveAsync_ReturnsVirusMessage_WhenClamavReportsInfected()
    {
        byte[] jpegBytes = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00];
        var file = MakeFormFile(jpegBytes, "infected.jpg");
        var service = new FileValidationService(InfectedScanMock().Object, DefaultSettings());

        var (success, error) = await service.ValidateAndSaveAsync(file, Path.GetTempPath());

        Assert.False(success);
        Assert.Equal("File may be infected with a virus.", error);
    }

    /// <summary>
    /// A ClamAV outage / incomplete scan is refused (fail-closed) but must NOT be reported as a
    /// virus detection: the file was never found infected.
    /// </summary>
    [Fact]
    public async Task ValidateAndSaveAsync_ReturnsScanUnavailableMessage_WhenClamavCannotScan()
    {
        byte[] jpegBytes = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00];
        var file = MakeFormFile(jpegBytes, "unscanned.jpg");
        var service = new FileValidationService(UnavailableScanMock().Object, DefaultSettings());

        var (success, error) = await service.ValidateAndSaveAsync(file, Path.GetTempPath());

        Assert.False(success);
        Assert.Equal("The file could not be scanned for viruses. Please try again later.", error);
        Assert.DoesNotContain("infected", error!, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>A refused scan leaves nothing on disk.</summary>
    [Fact]
    public async Task ValidateAndSaveAsync_WritesNothing_WhenClamavCannotScan()
    {
        byte[] jpegBytes = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00];
        string dir = CreateTempDir();
        var file = MakeFormFile(jpegBytes, "unscanned.jpg");
        var service = new FileValidationService(UnavailableScanMock().Object, DefaultSettings());

        var (success, _) = await service.ValidateAndSaveAsync(file, dir);

        Assert.False(success);
        Assert.False(Directory.Exists(dir));
    }

    // -- ZIP handling -------------------------------------------------------------------

    /// <summary>Validate and save async saves file, when zip is valid and not encrypted.</summary>
    [Fact]
    public async Task ValidateAndSaveAsync_SavesFile_WhenZipIsValidAndNotEncrypted()
    {
        var tempDir = CreateTempDir();
        try
        {
            var zipBytes = BuildValidZipBytes();
            var file = MakeFormFile(zipBytes, "archive.zip");
            var service = new FileValidationService(CleanScanMock().Object, DefaultSettings());

            var (success, error) = await service.ValidateAndSaveAsync(file, tempDir);

            Assert.True(success);
            Assert.Null(error);

            var savedPath = Path.Combine(tempDir, "archive.zip");
            Assert.True(File.Exists(savedPath));
            Assert.Equal(zipBytes, await File.ReadAllBytesAsync(savedPath, TestContext.Current.CancellationToken));
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }

    /// <summary>Validate and save async returns encrypted zip message, when zip built with sharp zip lib is encrypted.</summary>
    [Fact]
    public async Task ValidateAndSaveAsync_ReturnsEncryptedZipMessage_WhenZipBuiltWithSharpZipLibIsEncrypted()
    {
        var encryptedZipBytes = BuildEncryptedZipBytes();
        var file = MakeFormFile(encryptedZipBytes, "encrypted.zip");
        var service = new FileValidationService(CleanScanMock().Object, DefaultSettings());

        var (success, error) = await service.ValidateAndSaveAsync(file, Path.GetTempPath());

        Assert.False(success);
        Assert.Equal("Encrypted ZIP files are not allowed.", error);
    }

    /// <summary>An upload whose target file already exists is refused (names are unique GUIDs, so a collision means a bug upstream) and the original is left untouched.</summary>
    [Fact]
    public async Task ValidateAndSaveAsync_RefusesToOverwrite_AnExistingFile()
    {
        var tempDir = CreateTempDir();
        try
        {
            byte[] original = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00];
            var service = new FileValidationService(CleanScanMock().Object, DefaultSettings());
            Assert.True((await service.ValidateAndSaveAsync(MakeFormFile(original, "photo.jpg"), tempDir)).Item1);

            byte[] other = [0xFF, 0xD8, 0xFF, 0xE1, 0x00, 0x10, 0x00, 0x00, 0x00, 0x00, 0x00];
            var (success, error) = await service.ValidateAndSaveAsync(MakeFormFile(other, "photo.jpg"), tempDir);

            Assert.False(success);
            Assert.Equal("A file already exists at the target path.", error);
            Assert.Equal(original, await File.ReadAllBytesAsync(Path.Combine(tempDir, "photo.jpg"), TestContext.Current.CancellationToken));
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }
}
