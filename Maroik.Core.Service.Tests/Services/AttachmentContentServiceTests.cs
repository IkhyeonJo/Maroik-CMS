using Maroik.Core.Contract.Dtos;
using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Contract.Misc.Settings;
using Maroik.Core.Service.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Maroik.Core.Service.Tests.Services;

/// <summary>
/// Unit tests for <see cref="AttachmentContentService"/>.
/// All external dependencies (file client, RSA, image validation, HTML sanitizing/parsing)
/// are replaced with Moq mocks. Covers attachment validation, Summernote image upload,
/// file download, and HTML display/persist transforms.
/// </summary>
public class AttachmentContentServiceTests
{
    /// <summary>Mock <c>IFileClient</c> injected into the system under test.</summary>
    private readonly Mock<IFileClient> _fileClient = new();
    /// <summary>Mock <c>IRsaService</c> injected into the system under test.</summary>
    private readonly Mock<IRsaService> _rsa = new();
    /// <summary>Mock <c>IImageValidatorService</c> injected into the system under test.</summary>
    private readonly Mock<IImageValidatorService> _imageValidator = new();
    /// <summary>Mock <c>IHtmlContentSanitizerService</c> injected into the system under test.</summary>
    private readonly Mock<IHtmlContentSanitizerService> _htmlSanitizer = new();
    /// <summary>Mock <c>IHtmlParserService</c> injected into the system under test.</summary>
    private readonly Mock<IHtmlParserService> _htmlParser = new();
    /// <summary>Settings with a 1 KB attachment limit and a fake file-storage URL.</summary>
    private readonly IOptions<ServerSetting> _settings = Options.Create(new ServerSetting
    {
        MaxAttachedFileSizeBytes = 1024,
        FileStorageBaseUrl = "http://filestorage.local"
    });

    /// <summary>The service under test over the mocked dependencies.</summary>
    private AttachmentContentService CreateSut() => new(
        _fileClient.Object,
        _rsa.Object,
        _imageValidator.Object,
        _htmlSanitizer.Object,
        _htmlParser.Object,
        _settings,
        NullLogger<AttachmentContentService>.Instance);

    // -- ValidateAttachedFile ---------------------------------------------------

    /// <summary>Verifies that a null file is treated as "nothing to validate".</summary>
    [Fact]
    public void ValidateAttachedFile_ReturnsNull_WhenFileIsNull()
    {
        var result = CreateSut().ValidateAttachedFile(null);

        Assert.Null(result);
    }

    /// <summary>Verifies that non-zip extensions are rejected.</summary>
    [Fact]
    public void ValidateAttachedFile_ReturnsFail_WhenExtensionIsNotZip()
    {
        var file = new AttachedFileDto { FileName = "document.pdf", Size = 100 };

        var result = CreateSut().ValidateAttachedFile(file);

        Assert.NotNull(result);
        Assert.False(result.Success);
    }

    /// <summary>Verifies that files larger than the configured limit are rejected.</summary>
    [Fact]
    public void ValidateAttachedFile_ReturnsFail_WhenSizeExceedsMax()
    {
        var file = new AttachedFileDto { FileName = "archive.zip", Size = 2048 };

        var result = CreateSut().ValidateAttachedFile(file);

        Assert.NotNull(result);
        Assert.False(result.Success);
    }

    /// <summary>
    /// The "too large" message is a composite-format template with the configured max size (in MB)
    /// as a <see cref="ServiceResult.ErrorArgs"/> value -- not baked into
    /// <see cref="ServiceResult.ErrorKey"/> -- so every caller can localize it via
    /// <c>_localizer[result.ErrorKey, result.ErrorArgs]</c> instead of separately recomputing
    /// maxBytes/(1024*1024) at the call site.
    /// </summary>
    [Fact]
    public void ValidateAttachedFile_ReturnsLocalizableErrorArgs_WhenSizeExceedsMax()
    {
        var settings = Options.Create(new ServerSetting { MaxAttachedFileSizeBytes = 5 * 1024 * 1024, FileStorageBaseUrl = "http://filestorage.local" });
        var sut = new AttachmentContentService(
            _fileClient.Object, _rsa.Object, _imageValidator.Object, _htmlSanitizer.Object, _htmlParser.Object,
            settings, NullLogger<AttachmentContentService>.Instance);
        var file = new AttachedFileDto { FileName = "archive.zip", Size = 10 * 1024 * 1024 };

        var result = sut.ValidateAttachedFile(file);

        Assert.NotNull(result);
        Assert.Equal("File Size must be smaller than {0}MB.", result.ErrorKey);
        Assert.Equal([5L], result.ErrorArgs);
    }

    /// <summary>Verifies that a zero-byte file is rejected.</summary>
    [Fact]
    public void ValidateAttachedFile_ReturnsFail_WhenSizeIsZero()
    {
        var file = new AttachedFileDto { FileName = "archive.zip", Size = 0 };

        var result = CreateSut().ValidateAttachedFile(file);

        Assert.NotNull(result);
        Assert.False(result.Success);
    }

    /// <summary>Verifies that a zip file within the size limit passes validation.</summary>
    [Fact]
    public void ValidateAttachedFile_ReturnsNull_WhenValid()
    {
        var file = new AttachedFileDto { FileName = "archive.zip", Size = 512 };

        var result = CreateSut().ValidateAttachedFile(file);

        Assert.Null(result);
    }

    /// <summary>Verifies that the zip extension check is case-insensitive (e.g. Windows-style "Archive.ZIP").</summary>
    [Fact]
    public void ValidateAttachedFile_ReturnsNull_WhenExtensionIsUppercaseZip()
    {
        var file = new AttachedFileDto { FileName = "Archive.ZIP", Size = 512 };

        var result = CreateSut().ValidateAttachedFile(file);

        Assert.Null(result);
    }

    // -- UploadSummernoteImageAsync ----------------------------------------------

    /// <summary>Verifies that an invalid image is rejected before ever calling the file client.</summary>
    [Fact]
    public async Task UploadSummernoteImageAsync_ReturnsFail_WhenImageIsInvalid()
    {
        _imageValidator.Setup(v => v.IsValidImage(It.IsAny<byte[]>())).Returns(false);
        var file = new AttachedFileDto { Bytes = [1, 2, 3], ContentType = "image/png", FileName = "x.png" };

        var result = await CreateSut().UploadSummernoteImageAsync(file, "board", "post", TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        _fileClient.Verify(f => f.UploadAsync(
            It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>A file whose extension / content type is outside the jpg-png allow-list is rejected before the decode check.</summary>
    [Theory]
    [InlineData("photo.gif", "image/gif")]
    [InlineData("photo.svg", "image/svg+xml")]
    [InlineData("photo.png", "text/html")]
    [InlineData("photo.bmp", "image/bmp")]
    public async Task UploadSummernoteImageAsync_ReturnsFail_WhenExtensionOrContentTypeNotAllowed(string fileName, string contentType)
    {
        _imageValidator.Setup(v => v.IsValidImage(It.IsAny<byte[]>())).Returns(true);
        var file = new AttachedFileDto { Bytes = [1, 2, 3], ContentType = contentType, FileName = fileName };

        var result = await CreateSut().UploadSummernoteImageAsync(file, "board", "post", TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        _fileClient.Verify(f => f.UploadAsync(
            It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>A jpg/png-labeled file that decodes as SVG is rejected (SVG can carry script).</summary>
    [Fact]
    public async Task UploadSummernoteImageAsync_ReturnsFail_WhenBytesAreSvg()
    {
        _imageValidator.Setup(v => v.IsValidImage(It.IsAny<byte[]>())).Returns(true);
        _imageValidator.Setup(v => v.IsSvg(It.IsAny<byte[]>())).Returns(true);
        var file = new AttachedFileDto { Bytes = [1, 2, 3], ContentType = "image/png", FileName = "x.png" };

        var result = await CreateSut().UploadSummernoteImageAsync(file, "board", "post", TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        _fileClient.Verify(f => f.UploadAsync(
            It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Verifies that a failed upload short-circuits before attempting the re-download.</summary>
    [Fact]
    public async Task UploadSummernoteImageAsync_ReturnsFail_WhenUploadFails()
    {
        _imageValidator.Setup(v => v.IsValidImage(It.IsAny<byte[]>())).Returns(true);
        _fileClient.Setup(f => f.UploadAsync(
            It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var file = new AttachedFileDto { Bytes = [1, 2, 3], ContentType = "image/png", FileName = "x.png" };

        var result = await CreateSut().UploadSummernoteImageAsync(file, "board", "post", TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(ServiceResult.TemporaryErrorKey, result.ErrorKey);
        _fileClient.Verify(f => f.DownloadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// A stored editor image is returned from the bytes already in hand: the upload's success means
    /// file storage kept them as sent, so nothing is downloaded back.
    /// </summary>
    [Fact]
    public async Task UploadSummernoteImageAsync_ReturnsTheUploadedBytes_WithoutDownloadingThemBack()
    {
        _imageValidator.Setup(v => v.IsValidImage(It.IsAny<byte[]>())).Returns(true);
        _imageValidator.Setup(v => v.StripMetadata(It.IsAny<byte[]>())).Returns<byte[]>(bytes => bytes);
        string? storedPath = null;
        _fileClient.Setup(f => f.UploadAsync(
            It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<byte[], string, string, string, CancellationToken>((_, _, path, _, _) => storedPath = path)
            .ReturnsAsync(true);
        var file = new AttachedFileDto { Bytes = [1, 2, 3], ContentType = "image/png", FileName = "x.png" };

        var result = await CreateSut().UploadSummernoteImageAsync(file, "board", "post", TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(file.Bytes, result.FileBytes);
        Assert.Equal("image/png", result.ContentType);
        Assert.Equal(storedPath, result.FilePath);
        _fileClient.Verify(f => f.DownloadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// The editor image is stored — and handed back to the editor — as the validator's metadata-free
    /// re-encoding, so the EXIF of a phone photo (GPS position, camera, capture time) never reaches the
    /// readers of the post or shared calendar.
    /// </summary>
    [Fact]
    public async Task UploadSummernoteImageAsync_StoresAndReturnsTheMetadataFreeImage_NotTheUploadedBytes()
    {
        byte[] uploadedBytes = [0xFF, 0xD8, 0xFF, 0xE1, 0x45, 0x78, 0x69, 0x66];
        byte[] strippedBytes = [0xFF, 0xD8, 0xFF, 0xDB];
        _imageValidator.Setup(v => v.IsValidImage(uploadedBytes)).Returns(true);
        _imageValidator.Setup(v => v.StripMetadata(uploadedBytes)).Returns(strippedBytes);
        byte[]? stored = null;
        _fileClient.Setup(f => f.UploadAsync(
            It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<byte[], string, string, string, CancellationToken>((bytes, _, _, _, _) => stored = bytes)
            .ReturnsAsync(true);
        var file = new AttachedFileDto { Bytes = uploadedBytes, ContentType = "image/jpeg", FileName = "photo.jpg" };

        var result = await CreateSut().UploadSummernoteImageAsync(file, "board", "post", TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(strippedBytes, stored);
        Assert.Equal(strippedBytes, result.FileBytes);
    }

    /// <summary>A refused editor image is never re-encoded: the decoder only sees bytes the validator accepted.</summary>
    [Fact]
    public async Task UploadSummernoteImageAsync_DoesNotReencode_AnImageThatFailedValidation()
    {
        _imageValidator.Setup(v => v.IsValidImage(It.IsAny<byte[]>())).Returns(false);
        var file = new AttachedFileDto { Bytes = [1, 2, 3], ContentType = "image/png", FileName = "x.png" };

        await CreateSut().UploadSummernoteImageAsync(file, "board", "post", TestContext.Current.CancellationToken);

        _imageValidator.Verify(v => v.StripMetadata(It.IsAny<byte[]>()), Times.Never);
    }

    // -- DownloadFileAsync ---------------------------------------------------------

    /// <summary>Verifies that downloaded bytes are returned as-is on success.</summary>
    [Fact]
    public async Task DownloadFileAsync_ReturnsBytes_WhenSuccessful()
    {
        byte[] data = [1, 2, 3];
        _fileClient.Setup(f => f.DownloadAsync("path", It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(data);

        var result = await CreateSut().DownloadFileAsync("path", TestContext.Current.CancellationToken);

        Assert.Equal(data, result);
    }

    /// <summary>Verifies that a file-client failure is swallowed (and logged) rather than thrown.</summary>
    [Fact]
    public async Task DownloadFileAsync_ReturnsNull_WhenFileClientThrows()
    {
        _fileClient.Setup(f => f.DownloadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var result = await CreateSut().DownloadFileAsync("path", TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    // -- OpenFileAsync -------------------------------------------------------------

    /// <summary>The stream the file client opened is handed back unread.</summary>
    [Fact]
    public async Task OpenFileAsync_ReturnsTheOpenedStream()
    {
        var stream = new MemoryStream([1, 2, 3]);
        _fileClient.Setup(f => f.OpenReadAsync("upload/a.zip", "http://filestorage.local", It.IsAny<CancellationToken>())).ReturnsAsync(stream);

        Assert.Same(stream, await CreateSut().OpenFileAsync("upload/a.zip", TestContext.Current.CancellationToken));
    }

    /// <summary>A file the storage service cannot hand out yields null and one Error entry naming the path, with the exception attached.</summary>
    [Fact]
    public async Task OpenFileAsync_ReturnsNullAndLogsTheFailure_WhenTheFileCannotBeOpened()
    {
        var logger = new Microsoft.Extensions.Logging.Testing.FakeLogger<AttachmentContentService>();
        var failure = new HttpRequestException("404");
        _fileClient.Setup(f => f.OpenReadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(failure);
        var sut = new AttachmentContentService(_fileClient.Object, _rsa.Object, _imageValidator.Object, _htmlSanitizer.Object,
            _htmlParser.Object, _settings, logger);

        Assert.Null(await sut.OpenFileAsync("upload/a.zip", TestContext.Current.CancellationToken));

        var record = Assert.Single(logger.Collector.GetSnapshot());
        Assert.Equal(Microsoft.Extensions.Logging.LogLevel.Error, record.Level);
        Assert.Contains("upload/a.zip", record.Message);
        Assert.Same(failure, record.Exception);
    }

    // -- PrepareHtmlForDisplayAsync -------------------------------------------------

    /// <summary>
    /// Verifies the patch-factory callback passed to <see cref="IHtmlParserService"/>: it should
    /// download the image at the given (encrypted) path and re-encrypt that same path for the
    /// new alt attribute, embedding the downloaded bytes as base64 with the correct content type.
    /// </summary>
    [Fact]
    public async Task PrepareHtmlForDisplayAsync_PatchFactory_DownloadsAndEncryptsAlt()
    {
        Func<string, CancellationToken, Task<HtmlImgPatch?>>? capturedFactory = null;
        _htmlParser.Setup(p => p.TransformImageAttributesAsync(
                It.IsAny<string>(), It.IsAny<Func<string, CancellationToken, Task<HtmlImgPatch?>>>(), It.IsAny<CancellationToken>()))
            .Callback<string, Func<string, CancellationToken, Task<HtmlImgPatch?>>, CancellationToken>(
                (_, factory, _) => capturedFactory = factory)
            .ReturnsAsync(("<html/>", true));
        byte[] fileData = [5, 6, 7];
        _fileClient.Setup(f => f.DownloadAsync("upload/img.png", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileData);
        _rsa.Setup(r => r.Encrypt("upload/img.png")).Returns("encrypted-path");

        (string html, bool hasImages) = await CreateSut().PrepareHtmlForDisplayAsync("<html/>", TestContext.Current.CancellationToken);

        Assert.NotNull(capturedFactory);
        HtmlImgPatch? patch = await capturedFactory!("upload/img.png", TestContext.Current.CancellationToken);
        Assert.Equal("encrypted-path", patch!.NewAlt);
        Assert.Equal(Convert.ToBase64String(fileData), patch.DataFile);
        Assert.Equal("image/png", patch.DataContentType);
        Assert.True(hasImages);
        Assert.Equal("<html/>", html);
    }

    /// <summary>
    /// When the file-storage download fails (transient outage, missing blob, …) the patch must
    /// mark the tag for removal rather than returning null: a null patch leaves the tag's `alt`
    /// untouched, which is still the plaintext storage path at that point, so it would leak the
    /// internal storage layout to the client instead of degrading gracefully.
    /// </summary>
    [Fact]
    public async Task PrepareHtmlForDisplayAsync_PatchFactory_MarksImageForRemoval_WhenDownloadFails()
    {
        Func<string, CancellationToken, Task<HtmlImgPatch?>>? capturedFactory = null;
        _htmlParser.Setup(p => p.TransformImageAttributesAsync(
                It.IsAny<string>(), It.IsAny<Func<string, CancellationToken, Task<HtmlImgPatch?>>>(), It.IsAny<CancellationToken>()))
            .Callback<string, Func<string, CancellationToken, Task<HtmlImgPatch?>>, CancellationToken>(
                (_, factory, _) => capturedFactory = factory)
            .ReturnsAsync(("<html/>", true));
        _fileClient.Setup(f => f.DownloadAsync("upload/img.png", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("storage unreachable"));

        await CreateSut().PrepareHtmlForDisplayAsync("<html/>", TestContext.Current.CancellationToken);

        Assert.NotNull(capturedFactory);
        HtmlImgPatch? patch = await capturedFactory!("upload/img.png", TestContext.Current.CancellationToken);
        Assert.NotNull(patch);
        Assert.True(patch.Remove);
        Assert.Null(patch.NewAlt);
    }

    // -- SanitizeAndDecryptContent ---------------------------------------------------

    /// <summary>
    /// Verifies that content is sanitized first, then handed to the parser with a patch factory
    /// that decrypts each image's stored (encrypted) alt path.
    /// </summary>
    [Fact]
    public void SanitizeAndDecryptContent_SanitizesThenDecryptsAlt()
    {
        _htmlSanitizer.Setup(s => s.Sanitize("<raw/>")).Returns("<clean/>");
        Func<string, HtmlImgPatch?>? capturedFactory = null;
        _htmlParser.Setup(p => p.TransformImageAttributes("<clean/>", It.IsAny<Func<string, HtmlImgPatch?>>()))
            .Callback<string, Func<string, HtmlImgPatch?>>((_, factory) => capturedFactory = factory)
            .Returns("<final/>");
        _rsa.Setup(r => r.Decrypt("encrypted-alt")).Returns("decrypted-alt");

        string result = CreateSut().SanitizeAndDecryptContent("<raw/>");

        Assert.Equal("<final/>", result);
        Assert.NotNull(capturedFactory);
        HtmlImgPatch? patch = capturedFactory!("encrypted-alt");
        Assert.Equal("decrypted-alt", patch!.NewAlt);
        Assert.False(patch.Remove);
    }

    /// <summary>
    /// An <c>&lt;img&gt;</c> whose <c>alt</c> is not a decryptable storage token — e.g. a
    /// hand-crafted plaintext path posted straight to the write endpoint rather than produced by
    /// the editor — is marked for removal, not persisted. Otherwise, the stored tag would drive a
    /// server-side file fetch every time the post is rendered.
    /// </summary>
    [Fact]
    public void SanitizeAndDecryptContent_MarksImageForRemoval_WhenAltDoesNotDecrypt()
    {
        _htmlSanitizer.Setup(s => s.Sanitize("<raw/>")).Returns("<clean/>");
        Func<string, HtmlImgPatch?>? capturedFactory = null;
        _htmlParser.Setup(p => p.TransformImageAttributes("<clean/>", It.IsAny<Func<string, HtmlImgPatch?>>()))
            .Callback<string, Func<string, HtmlImgPatch?>>((_, factory) => capturedFactory = factory)
            .Returns("<final/>");
        _rsa.Setup(r => r.Decrypt("upload/Forum/whatever.png")).Throws<FormatException>();

        CreateSut().SanitizeAndDecryptContent("<raw/>");

        Assert.NotNull(capturedFactory);
        HtmlImgPatch? patch = capturedFactory!("upload/Forum/whatever.png");
        Assert.True(patch!.Remove);
        Assert.Null(patch.NewAlt);
    }

    /// <summary>A bare <c>&lt;img&gt;</c> with no <c>alt</c> yields a null patch (left untouched), not a removal.</summary>
    [Fact]
    public void SanitizeAndDecryptContent_LeavesImage_WhenAltIsEmpty()
    {
        _htmlSanitizer.Setup(s => s.Sanitize("<raw/>")).Returns("<clean/>");
        Func<string, HtmlImgPatch?>? capturedFactory = null;
        _htmlParser.Setup(p => p.TransformImageAttributes("<clean/>", It.IsAny<Func<string, HtmlImgPatch?>>()))
            .Callback<string, Func<string, HtmlImgPatch?>>((_, factory) => capturedFactory = factory)
            .Returns("<final/>");

        CreateSut().SanitizeAndDecryptContent("<raw/>");

        Assert.NotNull(capturedFactory);
        Assert.Null(capturedFactory!(""));
    }

    // -- SanitizeContent -----------------------------------------------------------

    /// <summary>Verifies that content with no image handling is passed straight to the sanitizer.</summary>
    [Fact]
    public void SanitizeContent_DelegatesToSanitizer()
    {
        _htmlSanitizer.Setup(s => s.Sanitize("<raw/>")).Returns("<clean/>");

        string result = CreateSut().SanitizeContent("<raw/>");

        Assert.Equal("<clean/>", result);
    }

    /// <summary>The embedded image's content type follows its stored file's extension (unknown extensions are served as generic binary).</summary>
    [Theory]
    [InlineData("upload/a.jpg", "image/jpeg")]
    [InlineData("upload/a.JPEG", "image/jpeg")]
    [InlineData("upload/a.gif", "image/gif")]
    [InlineData("upload/a.webp", "image/webp")]
    [InlineData("upload/a.bmp", "application/octet-stream")]
    [InlineData("upload/noextension", "application/octet-stream")]
    public async Task PrepareHtmlForDisplayAsync_PatchFactory_PicksTheContentTypeFromTheExtension(string path, string expected)
    {
        Func<string, CancellationToken, Task<HtmlImgPatch?>>? capturedFactory = null;
        _htmlParser.Setup(p => p.TransformImageAttributesAsync(
                It.IsAny<string>(), It.IsAny<Func<string, CancellationToken, Task<HtmlImgPatch?>>>(), It.IsAny<CancellationToken>()))
            .Callback<string, Func<string, CancellationToken, Task<HtmlImgPatch?>>, CancellationToken>((_, factory, _) => capturedFactory = factory)
            .ReturnsAsync(("<html/>", true));
        _fileClient.Setup(f => f.DownloadAsync(path, It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([1, 2, 3]);
        _rsa.Setup(r => r.Encrypt(path)).Returns("enc");

        await CreateSut().PrepareHtmlForDisplayAsync("<html/>", TestContext.Current.CancellationToken);
        HtmlImgPatch? patch = await capturedFactory!(path, TestContext.Current.CancellationToken);

        Assert.Equal(expected, patch!.DataContentType);
    }
}
