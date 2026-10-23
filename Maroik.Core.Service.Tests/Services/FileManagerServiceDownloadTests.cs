using System.Text;
using Maroik.Core.Contract.Dtos;
using Maroik.Core.Service.Services;
using Microsoft.Extensions.Logging;
using Moq;
using H = Maroik.Core.Service.Tests.Fixtures.FileManagerServiceHarness;

namespace Maroik.Core.Service.Tests.Services;

/// <summary>
/// Download (F3) and preview (F9) in <see cref="FileManagerService"/>: a file of the account is streamed from file storage under its
/// name; a preview is answered only for a png / jpg / gif / webp image of at most 100 MB whose magic bytes prove the format, with the
/// Content-Type the server sets (S6). Another account's file, a folder and an unfinished upload are "not found".
/// </summary>
public class FileManagerServiceDownloadTests
{
    /// <summary>The service, its collaborators and the seeding helpers.</summary>
    private readonly H _h = new();

    /// <summary>The leading bytes of a PNG.</summary>
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0, 1, 2];

    /// <summary>A file opens as a stream from file storage, under its name.</summary>
    [Fact]
    public async Task OpenFileAsync_StreamsTheFile_UnderItsName()
    {
        long id = _h.File("report.pdf");
        string path = _h.Node(id).StoragePath!;
        _h.Files.Setup(f => f.OpenReadAsync(path, H.BaseUrl, It.IsAny<CancellationToken>())).ReturnsAsync(new MemoryStream([1, 2]));

        var (result, file) = await _h.Sut.OpenFileAsync(H.Email, id, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal("report.pdf", file!.FileName);
        Assert.Equal(2, file.Content.Length);
    }

    /// <summary>Another account's file, a folder, an upload and a missing id are "not found"; file storage is not asked.</summary>
    [Fact]
    public async Task OpenFileAsync_AnswersNotFound_ForAnythingButTheAccountsFinishedFile()
    {
        long theirs = _h.File("a.txt", email: H.OtherEmail);
        long folder = _h.Folder("F");
        long upload = _h.File("b.txt", uploading: true);

        foreach (long id in new[] { theirs, folder, upload, 999L })
            Assert.Equal(ServiceErrorKeys.FileManagerItemNotFound, (await _h.Sut.OpenFileAsync(H.Email, id, TestContext.Current.CancellationToken)).Result.ErrorKey);
        _h.Files.Verify(f => f.OpenReadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>A file storage cannot open is a temporary error, logged at Error with the exception and the path.</summary>
    [Fact]
    public async Task OpenFileAsync_AnswersATemporaryError_WhenStorageCannotOpenIt()
    {
        long id = _h.File("a.txt");
        string path = _h.Node(id).StoragePath!;
        _h.Files.Setup(f => f.OpenReadAsync(path, H.BaseUrl, It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException("404"));

        var (result, file) = await _h.Sut.OpenFileAsync(H.Email, id, TestContext.Current.CancellationToken);

        Assert.Equal(ServiceResult.TemporaryErrorKey, result.ErrorKey);
        Assert.Null(file);
        Assert.Contains(_h.Logger.Collector.GetSnapshot(), r => r.Level == LogLevel.Error && r.Message == $"File Manager: stored file {path} could not be opened" && r.Exception != null);
    }

    /// <summary>A previewable image is answered with its bytes and the Content-Type its magic bytes prove (not the extension's).</summary>
    [Fact]
    public async Task OpenPreviewAsync_AnswersTheImage_WithTheProvenContentType()
    {
        long id = _h.File("photo.jpg", size: Png.Length);
        _h.Files.Setup(f => f.DownloadAsync(_h.Node(id).StoragePath!, H.BaseUrl, It.IsAny<CancellationToken>())).ReturnsAsync(Png);

        var (result, preview) = await _h.Sut.OpenPreviewAsync(H.Email, id, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal("image/png", preview!.ContentType);
        Assert.Equal(Png, preview.Bytes);
    }

    /// <summary>A type with no preview (SVG, PDF) or an image over 100 MB is refused without fetching it.</summary>
    [Theory]
    [InlineData("a.svg", 10L)]
    [InlineData("a.pdf", 10L)]
    [InlineData("a.png", 100L * 1024 * 1024 + 1)]
    public async Task OpenPreviewAsync_RefusesAFileWithNoPreview(string name, long size)
    {
        long id = _h.File(name, size: size);

        var (result, preview) = await _h.Sut.OpenPreviewAsync(H.Email, id, TestContext.Current.CancellationToken);

        Assert.Equal(ServiceErrorKeys.FileManagerNoPreview, result.ErrorKey);
        Assert.Null(preview);
        _h.Files.Verify(f => f.DownloadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>A ".png" whose bytes are an HTML page (or anything not an image) is refused: the extension alone is not trusted.</summary>
    [Fact]
    public async Task OpenPreviewAsync_RefusesAFileWhoseBytesAreNotAnImage()
    {
        long id = _h.File("trick.png");
        _h.Files.Setup(f => f.DownloadAsync(_h.Node(id).StoragePath!, H.BaseUrl, It.IsAny<CancellationToken>())).ReturnsAsync(Encoding.ASCII.GetBytes("<html><script>alert(1)</script>"));

        Assert.Equal(ServiceErrorKeys.FileManagerNoPreview, (await _h.Sut.OpenPreviewAsync(H.Email, id, TestContext.Current.CancellationToken)).Result.ErrorKey);
    }

    /// <summary>Another account's image is "not found".</summary>
    [Fact]
    public async Task OpenPreviewAsync_AnswersNotFound_ForAnotherAccountsImage()
    {
        long theirs = _h.File("a.png", email: H.OtherEmail);

        Assert.Equal(ServiceErrorKeys.FileManagerItemNotFound, (await _h.Sut.OpenPreviewAsync(H.Email, theirs, TestContext.Current.CancellationToken)).Result.ErrorKey);
    }
}
