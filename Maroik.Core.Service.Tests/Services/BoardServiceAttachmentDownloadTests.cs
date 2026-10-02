using Maroik.Core.Contract.Dtos;
using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Contract.Misc.Settings;
using Maroik.Core.Domain.Account;
using Maroik.Core.Domain.Board;
using Maroik.Core.Service.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace Maroik.Core.Service.Tests.Services;

/// <summary>
/// <see cref="BoardService.OpenAttachedFileAsync"/>: a post's attachment is handed out only after the
/// post's own visibility rule is checked again for the requesting viewer, and it is streamed from
/// file storage rather than loaded into memory. A refused download is logged as a permission denial.
/// </summary>
public class BoardServiceAttachmentDownloadTests
{
    /// <summary>Mock <c>IBoardRepository</c> injected into the system under test.</summary>
    private readonly Mock<IBoardRepository> _boardRepo = new();
    /// <summary>Mock <c>IBoardAttachedFileRepository</c> injected into the system under test.</summary>
    private readonly Mock<IBoardAttachedFileRepository> _attachedFileRepo = new();
    /// <summary>Mock <c>IAttachmentContentService</c> that opens the stored file.</summary>
    private readonly Mock<IAttachmentContentService> _attachmentContent = new();
    /// <summary>Captures the log entries the system under test writes.</summary>
    private readonly FakeLogger<BoardService> _logger = new();

    /// <summary>The service under test over the mocked dependencies.</summary>
    private BoardService CreateSut() => new(_boardRepo.Object, _attachedFileRepo.Object, Mock.Of<IBoardCommentRepository>(),
        Mock.Of<IFileClient>(), Mock.Of<IUnitOfWork>(), _attachmentContent.Object,
        Options.Create(new ServerSetting()), _logger, new FakeTimeProvider());

    /// <summary>Arranges post 7 of <paramref name="type"/> by Alice (optionally locked) with a stored attachment "report.zip".</summary>
    private void GivenAPostWithAnAttachment(string type = BoardTypes.FreeForum, bool locked = false)
    {
        _boardRepo.Setup(r => r.FindActiveByIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Board.Reconstitute(7, type, "t", "c", "Alice", DateTime.UtcNow, DateTime.UtcNow, 0, false, locked, false));
        _attachedFileRepo.Setup(r => r.FindByBoardIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BoardAttachedFile.Reconstitute(1, 7, 4, "report", ".zip", "upload/Forum/FreeForum/7/report.zip"));
    }

    /// <summary>A visible post's attachment is returned as the opened stream, named after the stored file.</summary>
    [Fact]
    public async Task OpenAttachedFileAsync_ReturnsTheStream_ForAViewerWhoMaySeeThePost()
    {
        GivenAPostWithAnAttachment();
        var stream = new MemoryStream([1, 2, 3, 4]);
        _attachmentContent.Setup(a => a.OpenFileAsync("upload/Forum/FreeForum/7/report.zip", It.IsAny<CancellationToken>())).ReturnsAsync(stream);

        var (result, file) = await CreateSut().OpenAttachedFileAsync(7, BoardTypes.FreeForum, null, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.NotNull(file);
        Assert.Same(stream, file.Content);
        Assert.Equal("report.zip", file.FileName);
    }

    /// <summary>
    /// A locked post's attachment is refused to anyone who may not view the post (here an anonymous
    /// visitor) exactly like the post itself; nothing is opened and the denial is logged.
    /// </summary>
    [Fact]
    public async Task OpenAttachedFileAsync_RefusesAndLogs_WhenTheViewerMayNotSeeThePost()
    {
        GivenAPostWithAnAttachment(locked: true);

        var (result, file) = await CreateSut().OpenAttachedFileAsync(7, BoardTypes.FreeForum, null, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal("Board.NotFound", result.ErrorCode);
        Assert.Null(file);
        _attachmentContent.Verify(a => a.OpenFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        FakeLogRecord denial = Assert.Single(_logger.Collector.GetSnapshot(), r => r.Level == LogLevel.Warning);
        Assert.Contains("Attachment download refused", denial.Message);
        Assert.Contains("7", denial.Message);
    }

    /// <summary>A private note's attachment is its owner's only: another account is refused.</summary>
    [Fact]
    public async Task OpenAttachedFileAsync_RefusesAnotherAccount_ForAPrivateNote()
    {
        GivenAPostWithAnAttachment(type: BoardTypes.PrivateNote);

        var (result, _) = await CreateSut().OpenAttachedFileAsync(7, BoardTypes.PrivateNote,
            new AccountResponse { Nickname = "Mallory", Role = Role.User }, TestContext.Current.CancellationToken);

        Assert.Equal("Board.NotFound", result.ErrorCode);
    }

    /// <summary>The requested board type must match: a private note is not served through the forum endpoint.</summary>
    [Fact]
    public async Task OpenAttachedFileAsync_Refuses_WhenThePostIsOfAnotherType()
    {
        GivenAPostWithAnAttachment(type: BoardTypes.PrivateNote);

        var (result, _) = await CreateSut().OpenAttachedFileAsync(7, BoardTypes.FreeForum,
            new AccountResponse { Nickname = "Alice", Role = Role.User }, TestContext.Current.CancellationToken);

        Assert.Equal("Board.NotFound", result.ErrorCode);
    }

    /// <summary>A missing post is reported as not found.</summary>
    [Fact]
    public async Task OpenAttachedFileAsync_ReportsNotFound_ForAMissingPost()
    {
        var (result, _) = await CreateSut().OpenAttachedFileAsync(99, BoardTypes.FreeForum, null, TestContext.Current.CancellationToken);

        Assert.Equal("Board.NotFound", result.ErrorCode);
    }

    /// <summary>A post without an attachment is reported as such.</summary>
    [Fact]
    public async Task OpenAttachedFileAsync_ReportsNotFound_WhenThePostHasNoAttachment()
    {
        GivenAPostWithAnAttachment();
        _attachedFileRepo.Setup(r => r.FindByBoardIdAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync((BoardAttachedFile?)null);

        var (result, _) = await CreateSut().OpenAttachedFileAsync(7, BoardTypes.FreeForum, null, TestContext.Current.CancellationToken);

        Assert.Equal("Board.AttachedFileNotFound", result.ErrorCode);
        Assert.Equal("The attached file could not be found.", result.ErrorKey);
    }

    /// <summary>A file the storage service cannot hand out is a failure (logged where it happens), not a missing file.</summary>
    [Fact]
    public async Task OpenAttachedFileAsync_ReportsAFailure_WhenTheFileCannotBeOpened()
    {
        GivenAPostWithAnAttachment();
        _attachmentContent.Setup(a => a.OpenFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Stream?)null);

        var (result, file) = await CreateSut().OpenAttachedFileAsync(7, BoardTypes.FreeForum, null, TestContext.Current.CancellationToken);

        Assert.Equal("Board.AttachedFileUnavailable", result.ErrorCode);
        Assert.Equal(ServiceResult.TemporaryErrorKey, result.ErrorKey);
        Assert.Equal(ServiceErrorType.Failure, result.ErrorType);
        Assert.Null(file);
    }
}
