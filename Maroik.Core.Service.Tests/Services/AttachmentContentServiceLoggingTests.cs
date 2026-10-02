using System.Diagnostics;
using Maroik.Core.Contract.Dtos;
using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Contract.Misc.Settings;
using Maroik.Core.Service.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;
using Moq;

namespace Maroik.Core.Service.Tests.Services;

/// <summary>
/// A file-storage failure is logged once, by <see cref="AttachmentContentService"/> where it is
/// handled (the file client only throws it): one Error entry carrying the exception, the storage
/// path and the request's correlation id, so it can be matched with Maroik.FileStorage's own log.
/// </summary>
public class AttachmentContentServiceLoggingTests
{
    /// <summary>Mock <c>IFileClient</c> whose calls fail.</summary>
    private readonly Mock<IFileClient> _fileClient = new();
    /// <summary>Mock <c>IHtmlParserService</c> that hands its patch factory to the test.</summary>
    private readonly Mock<IHtmlParserService> _htmlParser = new();
    /// <summary>Captures the log entries the system under test writes.</summary>
    private readonly FakeLogger<AttachmentContentService> _logger = new();
    /// <summary>The storage failure every call fails with.</summary>
    private readonly HttpRequestException _failure = new("File storage answered 404 for 'upload/x.png'.");

    /// <summary>The service under test, over a file client whose every download and open fails.</summary>
    private AttachmentContentService CreateSut()
    {
        _fileClient.Setup(f => f.DownloadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(_failure);
        _fileClient.Setup(f => f.OpenReadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(_failure);
        return new AttachmentContentService(_fileClient.Object, Mock.Of<IRsaService>(), Mock.Of<IImageValidatorService>(),
            Mock.Of<IHtmlContentSanitizerService>(), _htmlParser.Object,
            Options.Create(new ServerSetting { FileStorageBaseUrl = "http://filestorage.local" }), _logger);
    }

    /// <summary>Asserts exactly one Error was logged, with the failure attached, naming <paramref name="path"/> and the current correlation id.</summary>
    private void AssertLoggedOnce(string path, Activity activity)
    {
        FakeLogRecord record = Assert.Single(_logger.Collector.GetSnapshot(), r => r.Level >= LogLevel.Warning);
        Assert.Equal(LogLevel.Error, record.Level);
        Assert.Same(_failure, record.Exception);
        Assert.Contains(path, record.Message);
        Assert.Contains($"CorrelationId={activity.Id}", record.Message);
    }

    /// <summary>A failed attachment open.</summary>
    [Fact]
    public async Task OpenFileAsync_LogsTheFailureOnce_WithPathAndCorrelationId()
    {
        using var activity = new Activity("request").Start();

        Assert.Null(await CreateSut().OpenFileAsync("upload/a.zip", TestContext.Current.CancellationToken));

        AssertLoggedOnce("upload/a.zip", activity);
    }

    /// <summary>A body image that cannot be fetched while a post is rendered.</summary>
    [Fact]
    public async Task PrepareHtmlForDisplayAsync_LogsAMissingImageOnce_WithPathAndCorrelationId()
    {
        using var activity = new Activity("request").Start();
        Func<string, CancellationToken, Task<HtmlImgPatch?>>? factory = null;
        _htmlParser.Setup(p => p.TransformImageAttributesAsync(It.IsAny<string>(), It.IsAny<Func<string, CancellationToken, Task<HtmlImgPatch?>>>(), It.IsAny<CancellationToken>()))
            .Callback<string, Func<string, CancellationToken, Task<HtmlImgPatch?>>, CancellationToken>((_, f, _) => factory = f)
            .ReturnsAsync(("", true));

        await CreateSut().PrepareHtmlForDisplayAsync("<img>", TestContext.Current.CancellationToken);
        HtmlImgPatch? patch = await factory!("upload/img.png", TestContext.Current.CancellationToken);

        Assert.True(patch!.Remove);
        AssertLoggedOnce("upload/img.png", activity);
    }
    // -- Refused editor images ------------------------------------------------------

    /// <summary>The service under test for an editor-image upload, over <paramref name="imageValidator"/>.</summary>
    private AttachmentContentService CreateUploadSut(Mock<IImageValidatorService> imageValidator) =>
        new(_fileClient.Object, Mock.Of<IRsaService>(), imageValidator.Object,
            Mock.Of<IHtmlContentSanitizerService>(), _htmlParser.Object,
            Options.Create(new ServerSetting { FileStorageBaseUrl = "http://filestorage.local" }), _logger);

    /// <summary>
    /// Asserts the refusal was logged as exactly one Warning naming the uploader, the editor area and
    /// <paramref name="reason"/>, and that nothing was sent to file storage.
    /// </summary>
    private void AssertRefusalLoggedOnce(string reason)
    {
        FakeLogRecord record = Assert.Single(_logger.Collector.GetSnapshot(), r => r.Level >= LogLevel.Warning);
        Assert.Equal(LogLevel.Warning, record.Level);
        Assert.Contains("Editor image upload refused", record.Message);
        Assert.Contains(reason, record.Message);
        Assert.Contains("uploader@test.com", record.Message);
        Assert.Contains("Forum/FreeForum", record.Message);
        _fileClient.Verify(f => f.UploadAsync(
            It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>An image refused for its extension or declared content type is logged with both.</summary>
    [Fact]
    public async Task UploadSummernoteImageAsync_LogsARefusedExtensionOrContentType()
    {
        var file = new AttachedFileDto { Bytes = [1, 2, 3], ContentType = "image/gif", FileName = "x.gif" };

        SummernoteUploadResult result = await CreateUploadSut(ImageValidatorMock.Create())
            .UploadSummernoteImageAsync(file, "Forum", "FreeForum", "uploader@test.com", TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        AssertRefusalLoggedOnce("image/gif");
    }

    /// <summary>An SVG document is logged as refused for being SVG.</summary>
    [Fact]
    public async Task UploadSummernoteImageAsync_LogsARefusedSvg()
    {
        Mock<IImageValidatorService> validator = ImageValidatorMock.Create();
        validator.Setup(v => v.IsSvg(It.IsAny<byte[]>())).Returns(true);
        var file = new AttachedFileDto { Bytes = [1, 2, 3], ContentType = "image/png", FileName = "x.png" };

        SummernoteUploadResult result = await CreateUploadSut(validator)
            .UploadSummernoteImageAsync(file, "Forum", "FreeForum", "uploader@test.com", TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        AssertRefusalLoggedOnce("SVG");
    }

    /// <summary>Bytes that are not a valid JPEG / PNG are logged as refused for not being a valid image.</summary>
    [Fact]
    public async Task UploadSummernoteImageAsync_LogsARefusedInvalidImage()
    {
        Mock<IImageValidatorService> validator = ImageValidatorMock.Create();
        validator.Setup(v => v.IsValidImage(It.IsAny<byte[]>())).Returns(false);
        var file = new AttachedFileDto { Bytes = [1, 2, 3], ContentType = "image/png", FileName = "x.png" };

        SummernoteUploadResult result = await CreateUploadSut(validator)
            .UploadSummernoteImageAsync(file, "Forum", "FreeForum", "uploader@test.com", TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        AssertRefusalLoggedOnce("not a valid image");
    }
}
