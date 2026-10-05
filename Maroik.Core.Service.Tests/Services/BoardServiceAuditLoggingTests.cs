using System.Data;
using Maroik.Core.Contract.Dtos;
using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Contract.Misc.Settings;
using Maroik.Core.Domain.Board;
using Maroik.Core.Service.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace Maroik.Core.Service.Tests.Services;

/// <summary>
/// The moderation trail of <see cref="BoardService"/>: who deleted whose post or comment, who was refused,
/// and when an admin cleared someone else's lock. Post and comment text is never logged.
/// </summary>
public class BoardServiceAuditLoggingTests
{
    /// <summary>Mock <c>IBoardRepository</c> injected into the system under test.</summary>
    private readonly Mock<IBoardRepository> _boardRepo = new();
    /// <summary>Mock <c>IBoardAttachedFileRepository</c> injected into the system under test.</summary>
    private readonly Mock<IBoardAttachedFileRepository> _attachedFileRepo = new();
    /// <summary>Mock <c>IBoardCommentRepository</c> injected into the system under test.</summary>
    private readonly Mock<IBoardCommentRepository> _commentRepo = new();
    /// <summary>Mock <c>IUnitOfWork</c> injected into the system under test.</summary>
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    /// <summary>Mock <c>IAttachmentContentService</c> injected into the system under test.</summary>
    private readonly Mock<IAttachmentContentService> _attachmentContent = new();
    /// <summary>Captures the log entries the system under test writes.</summary>
    private readonly FakeLogger<BoardService> _logger = new();

    /// <summary>Arranges a unit of work that always succeeds, pass-through content decryption, successful updates, and no existing attachment.</summary>
    public BoardServiceAuditLoggingTests()
    {
        _unitOfWork.Setup(u => u.BeginAsync(It.IsAny<IsolationLevel?>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _unitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _unitOfWork.Setup(u => u.RollbackAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _attachmentContent.Setup(s => s.SanitizeAndDecryptContent(It.IsAny<string>())).Returns<string>(html => html);
        _boardRepo.Setup(r => r.UpdateEntityAsync(It.IsAny<Board>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _commentRepo.Setup(r => r.UpdateEntityAsync(It.IsAny<BoardComment>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _attachedFileRepo.Setup(r => r.FindByBoardIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync((BoardAttachedFile?)null);
    }

    /// <summary>The fixed "current time" of these tests.</summary>
    private static readonly DateTime Now = new(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);
    /// <summary>The clock the service under test reads, stopped at <see cref="Now"/>.</summary>
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(Now));

    /// <summary>The service under test over the mocked dependencies and the capturing logger.</summary>
    private BoardService CreateSut() => new(_boardRepo.Object, _attachedFileRepo.Object, _commentRepo.Object,
        Mock.Of<IFileClient>(), _unitOfWork.Object, _attachmentContent.Object,
        Options.Create(new ServerSetting { FileStorageBaseUrl = "https://files.example.com" }), _logger, _time);

    /// <summary>A free-forum post whose title and content are secrets that must never be logged.</summary>
    private static Board MakeBoard(string writer = "Alice", bool locked = false) =>
        Board.Reconstitute(1, BoardTypes.FreeForum, "SECRET TITLE", "SECRET CONTENT", writer,
            DateTime.UtcNow, DateTime.UtcNow, 0L, false, locked, false);

    /// <summary>Asserts exactly one entry at <paramref name="level"/> contains <paramref name="containing"/>, and returns it.</summary>
    private FakeLogRecord Only(LogLevel level, string containing)
    {
        FakeLogRecord record = Assert.Single(_logger.Collector.GetSnapshot(),
            r => r.Level == level && r.Message.Contains(containing, StringComparison.Ordinal));
        Assert.DoesNotContain("SECRET", record.Message);
        return record;
    }

    /// <summary>Verifies that an admin deleting someone else's post logs the post, writer, requester and admin flag at Information.</summary>
    [Fact]
    public async Task DeleteBoard_LogsInformationWithWriterRequesterAndAdminFlag_WhenAnAdminDeletesSomeoneElsesPost()
    {
        _boardRepo.Setup(r => r.FindActiveByIdForUpdateAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync(MakeBoard());

        await CreateSut().DeleteBoardAsync(1, BoardTypes.FreeForum, "Admin", true, TestContext.Current.CancellationToken);

        FakeLogRecord record = Only(LogLevel.Information, "Board post 1 of Alice deleted by Admin");
        Assert.Contains("admin: True", record.Message);
    }

    /// <summary>Verifies that a refused post deletion logs a Warning naming the requester and the post.</summary>
    [Fact]
    public async Task DeleteBoard_LogsWarning_WhenTheRequesterMayNotDeleteThePost()
    {
        _boardRepo.Setup(r => r.FindActiveByIdForUpdateAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync(MakeBoard());

        await CreateSut().DeleteBoardAsync(1, BoardTypes.FreeForum, "Bob", false, TestContext.Current.CancellationToken);

        Only(LogLevel.Warning, "Board delete refused: Bob may not delete post 1");
    }

    /// <summary>Verifies that deleting a comment logs the comment, its writer and the requester at Information.</summary>
    [Fact]
    public async Task DeleteComment_LogsInformation_WhenTheCommentIsDeleted()
    {
        _commentRepo.Setup(r => r.FindByIdForUpdateAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BoardComment.Reconstitute(7, 1L, 0L, null, "Alice", "SECRET COMMENT", DateTime.UtcNow, false));

        await CreateSut().DeleteCommentAsync(7, "Alice", false, ct: TestContext.Current.CancellationToken);

        Only(LogLevel.Information, "Comment 7 of Alice deleted by Alice");
    }

    /// <summary>Verifies that a refused comment deletion logs a Warning naming the requester and the comment.</summary>
    [Fact]
    public async Task DeleteComment_LogsWarning_WhenTheRequesterMayNotDeleteTheComment()
    {
        _commentRepo.Setup(r => r.FindByIdForUpdateAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BoardComment.Reconstitute(7, 1L, 0L, null, "Alice", "SECRET COMMENT", DateTime.UtcNow, false));

        await CreateSut().DeleteCommentAsync(7, "Bob", false, ct: TestContext.Current.CancellationToken);

        Only(LogLevel.Warning, "Comment delete refused: Bob may not delete comment 7");
    }

    /// <summary>Verifies that an edit by someone who is neither the writer nor an admin clearing a lock logs a Warning.</summary>
    [Fact]
    public async Task EditBoard_LogsWarning_WhenTheRequesterIsNeitherTheWriterNorAnAdminClearingALock()
    {
        _boardRepo.Setup(r => r.FindActiveByIdForUpdateAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync(MakeBoard());

        await CreateSut().EditBoardAsync(
            new BoardRequest { Id = 1, Type = BoardTypes.FreeForum, Title = "T", Content = "C", Locked = false },
            "Bob", isAdmin: false, null, TestContext.Current.CancellationToken);

        Only(LogLevel.Warning, "Board edit refused: Bob may not edit post 1");
    }

    /// <summary>Verifies that an admin clearing the lock on someone else's post logs an Information entry.</summary>
    [Fact]
    public async Task EditBoard_LogsInformation_WhenAnAdminClearsSomeoneElsesLock()
    {
        _boardRepo.Setup(r => r.FindActiveByIdForUpdateAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync(MakeBoard(locked: true));

        await CreateSut().EditBoardAsync(
            new BoardRequest { Id = 1, Type = BoardTypes.FreeForum, Title = "T", Content = "C", Locked = false },
            "Admin", isAdmin: true, null, TestContext.Current.CancellationToken);

        Only(LogLevel.Information, "Lock on post 1 of Alice cleared by admin Admin");
    }
}
