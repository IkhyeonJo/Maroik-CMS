using System.Data;
using Maroik.Core.Contract.Dtos;
using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Contract.Misc.Settings;
using Maroik.Core.Domain.Account;
using Maroik.Core.Domain.Board;
using Maroik.Core.Service.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
// ReSharper disable InvalidXmlDocComment

namespace Maroik.Core.Service.Tests.Services;

/// <summary>
/// Unit tests for <see cref="BoardService"/>.
/// All external dependencies (repositories, ClamAV client, file client, logger)
/// are replaced with Moq mocks. Covers board post CRUD, comment management,
/// attachment upload/delete, and view-count incrementing.
/// </summary>
public class BoardServiceTests
{
    /// <summary>Mock <c>IBoardRepository</c> injected into the system under test.</summary>
    private readonly Mock<IBoardRepository> _boardRepo = new();
    /// <summary>Mock <c>IBoardAttachedFileRepository</c> injected into the system under test.</summary>
    private readonly Mock<IBoardAttachedFileRepository> _attachedFileRepo = new();
    /// <summary>Mock <c>IBoardCommentRepository</c> injected into the system under test.</summary>
    private readonly Mock<IBoardCommentRepository> _commentRepo = new();
    /// <summary>Mock <c>IFileClient</c> injected into the system under test.</summary>
    private readonly Mock<IFileClient> _fileClient = new();
    /// <summary>Mock <c>IUnitOfWork</c> injected into the system under test.</summary>
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    /// <summary>Mock <c>IAttachmentContentService</c> injected into the system under test.</summary>
    private readonly Mock<IAttachmentContentService> _attachmentContent = new();
    /// <summary>Settings with a fake file-storage URL.</summary>
    private readonly IOptions<ServerSetting> _settings =
        Options.Create(new ServerSetting { FileStorageBaseUrl = "https://files.example.com" });

    /// <summary>Initializes the test fixture, setting up all required test doubles and the system under test.</summary>
    public BoardServiceTests()
    {
        _unitOfWork.Setup(u => u.BeginAsync(It.IsAny<CancellationToken>(), It.IsAny<IsolationLevel?>())).Returns(Task.CompletedTask);
        _unitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _unitOfWork.Setup(u => u.RollbackAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _unitOfWork.Setup(u => u.DisposeAsync()).Returns(ValueTask.CompletedTask);
        _attachmentContent.Setup(s => s.SanitizeAndDecryptContent(It.IsAny<string>())).Returns<string>(html => html);
        _attachmentContent.Setup(s => s.SanitizeContent(It.IsAny<string>())).Returns<string>(html => html);
    }

    /// <summary>The service under test over the mocked dependencies.</summary>
    private BoardService CreateSut() => new(
        _boardRepo.Object,
        _attachedFileRepo.Object,
        _commentRepo.Object,
        _fileClient.Object,
        _unitOfWork.Object,
        _attachmentContent.Object,
        _settings,
        NullLogger<BoardService>.Instance);

    // -- Helpers --------------------------------------------------------------

    /// <summary>A persisted free-forum post whose id, writer and flags are set by the arguments.</summary>
    private static Board MakeBoard(long id = 1, string writer = "Alice", bool deleted = false, bool locked = false) =>
        Board.Reconstitute(id, BoardTypes.FreeForum, "Test Title", "Test Content", writer,
            DateTime.UtcNow, DateTime.UtcNow, 0L, deleted, locked, false);

    /// <summary>A persisted top-level comment on board 1.</summary>
    private static BoardComment MakeComment(long id = 1, string writer = "Alice", long order = 0L) =>
        BoardComment.Reconstitute(id, 1L, order, null, writer, "Comment", DateTime.UtcNow, false);

    // -- GetBoardByIdAsync ----------------------------------------------------

    /// <summary>Verifies that <c>GetBoardByIdAsync</c> delegates to repository.</summary>
    [Fact]
    public async Task GetBoardByIdAsync_DelegatesToRepository()
    {
        _boardRepo.Setup(r => r.FindActiveByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync(MakeBoard());
        var sut = CreateSut();

        BoardResponse? result = await sut.GetBoardByIdAsync(1, ct: TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(1, result.Id);
    }

    /// <summary>
    /// Verifies that <c>GetBoardByIdAsync</c> returns null when the post's type doesn't match
    /// the required board type — e.g. a caller scoped to PrivateNote must not be able to
    /// read a FreeForum post (or vice versa) just by supplying its ID.
    /// </summary>
    [Fact]
    public async Task GetBoardByIdAsync_ReturnsNull_WhenTypeDoesNotMatchRequiredType()
    {
        _boardRepo.Setup(r => r.FindActiveByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeBoard()); // Type = BoardTypes.FreeForum
        var sut = CreateSut();

        BoardResponse? result = await sut.GetBoardByIdAsync(1, BoardTypes.PrivateNote, TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    // -- GetBoardPageAsync ------------------------------------------------------

    /// <summary>An unfiltered first-page free-forum query.</summary>
    private static BoardPageQuery MakeQuery() => new(
        BoardTypes.FreeForum, OwnerNickname: null, SearchType: null, SearchText: null,
        IsLoggedIn: false, ViewerRole: null, ViewerNickname: null, Page: 1, PageSize: 10);

    /// <summary>
    /// The count/page read this delegates to is only snapshot-consistent inside a REPEATABLE READ
    /// transaction — asserts the Service (not the repository) opens exactly that isolation level,
    /// and commits it once the repository call returns.
    /// </summary>
    [Fact]
    public async Task GetBoardPageAsync_WrapsRepositoryCallInRepeatableReadTransaction()
    {
        _boardRepo.Setup(r => r.QueryPageAsync(It.IsAny<BoardPageQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(([MakeBoard()], 1));
        var sut = CreateSut();

        var (items, totalCount) = await sut.GetBoardPageAsync(MakeQuery(), TestContext.Current.CancellationToken);

        Assert.Single(items);
        Assert.Equal(1, totalCount);
        _unitOfWork.Verify(u => u.BeginAsync(It.IsAny<CancellationToken>(), IsolationLevel.RepeatableRead), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>A failure inside the transaction rolls back and propagates rather than swallowing the error.</summary>
    [Fact]
    public async Task GetBoardPageAsync_RollsBackAndRethrows_WhenRepositoryThrows()
    {
        _boardRepo.Setup(r => r.QueryPageAsync(It.IsAny<BoardPageQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));
        var sut = CreateSut();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.GetBoardPageAsync(MakeQuery(), TestContext.Current.CancellationToken));

        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // -- CanView ----------------------------------------------------------------

    /// <summary>A board response carrying only the fields the view-permission check reads.</summary>
    private static BoardResponse MakeBoardResponse(string type, string writer = "Alice", bool locked = false) =>
        new() { Type = type, Writer = writer, Locked = locked };

    /// <summary>A viewer with the given nickname and role.</summary>
    private static AccountResponse MakeAccount(string nickname, string role = Role.User) =>
        new() { Nickname = nickname, Role = role };

    /// <summary>Verifies that an anonymous visitor can view an unlocked forum post.</summary>
    [Fact]
    public void CanView_ReturnsTrue_ForUnlockedFreeForumBoard_WhenViewerIsAnonymous()
    {
        var sut = CreateSut();
        var board = MakeBoardResponse(BoardTypes.FreeForum, locked: false);

        Assert.True(sut.CanView(board, null));
    }

    /// <summary>Verifies that an anonymous visitor cannot view a locked forum post.</summary>
    [Fact]
    public void CanView_ReturnsFalse_ForLockedFreeForumBoard_WhenViewerIsAnonymous()
    {
        var sut = CreateSut();
        var board = MakeBoardResponse(BoardTypes.FreeForum, locked: true);

        Assert.False(sut.CanView(board, null));
    }

    /// <summary>Verifies that the writer can view their own locked forum post.</summary>
    [Fact]
    public void CanView_ReturnsTrue_ForLockedFreeForumBoard_WhenViewerIsWriter()
    {
        var sut = CreateSut();
        var board = MakeBoardResponse(BoardTypes.FreeForum, writer: "Alice", locked: true);

        Assert.True(sut.CanView(board, MakeAccount("Alice")));
    }

    /// <summary>Verifies that an admin can view someone else's locked forum post.</summary>
    [Fact]
    public void CanView_ReturnsTrue_ForLockedFreeForumBoard_WhenViewerIsAdmin()
    {
        var sut = CreateSut();
        var board = MakeBoardResponse(BoardTypes.FreeForum, writer: "Alice", locked: true);

        Assert.True(sut.CanView(board, MakeAccount("Bob", Role.Admin)));
    }

    /// <summary>Verifies that another signed-in user cannot view a locked forum post.</summary>
    [Fact]
    public void CanView_ReturnsFalse_ForLockedFreeForumBoard_WhenViewerIsUnrelatedUser()
    {
        var sut = CreateSut();
        var board = MakeBoardResponse(BoardTypes.FreeForum, writer: "Alice", locked: true);

        Assert.False(sut.CanView(board, MakeAccount("Bob")));
    }

    /// <summary>Verifies that the writer can view their own private note.</summary>
    [Fact]
    public void CanView_ReturnsTrue_ForPrivateNoteBoard_WhenViewerIsWriter()
    {
        var sut = CreateSut();
        var board = MakeBoardResponse(BoardTypes.PrivateNote, writer: "Alice", locked: false);

        Assert.True(sut.CanView(board, MakeAccount("Alice")));
    }

    /// <summary>Verifies that an anonymous visitor cannot view a private note.</summary>
    [Fact]
    public void CanView_ReturnsFalse_ForPrivateNoteBoard_WhenViewerIsAnonymous()
    {
        var sut = CreateSut();
        var board = MakeBoardResponse(BoardTypes.PrivateNote, writer: "Alice", locked: false);

        Assert.False(sut.CanView(board, null));
    }

    /// <summary>Private notes are visible only to their author — unlike FreeForum, Admin does not bypass this.</summary>
    [Fact]
    public void CanView_ReturnsFalse_ForPrivateNoteBoard_WhenViewerIsAdminButNotWriter()
    {
        var sut = CreateSut();
        var board = MakeBoardResponse(BoardTypes.PrivateNote, writer: "Alice", locked: false);

        Assert.False(sut.CanView(board, MakeAccount("Bob", Role.Admin)));
    }

    // -- WriteBoardAsync ------------------------------------------------------

    /// <summary>Verifies that <c>WriteBoardAsync</c> returns success with no attachment.</summary>
    [Fact]
    public async Task WriteBoardAsync_ReturnsSuccess_WithNoAttachment()
    {
        _boardRepo.Setup(r => r.WriteBoardAsync(It.IsAny<Board>(), It.IsAny<CancellationToken>())).ReturnsAsync(1L);
        var sut = CreateSut();

        ServiceResult result = await sut.WriteBoardAsync(
            new BoardRequest { Type = BoardTypes.FreeForum, Title = "Test Title", Writer = "Alice" }, false, null, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Verifies that <c>WriteBoardAsync</c> returns success with attachment.</summary>
    [Fact]
    public async Task WriteBoardAsync_ReturnsSuccess_WithAttachment()
    {
        _boardRepo.Setup(r => r.WriteBoardAsync(It.IsAny<Board>(), It.IsAny<CancellationToken>())).ReturnsAsync(1L);
        _fileClient.Setup(f => f.UploadAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _attachedFileRepo.Setup(r => r.CreateAsync(It.IsAny<BoardAttachedFile>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var sut = CreateSut();

        var file = new AttachedFileDto { FileName = "doc.zip", ContentType = "application/zip", Bytes = [1, 2], Size = 2 };
        ServiceResult result = await sut.WriteBoardAsync(
            new BoardRequest { Type = BoardTypes.FreeForum, Title = "Test Title", Writer = "Alice" }, false, file, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        _attachedFileRepo.Verify(r => r.CreateAsync(It.IsAny<BoardAttachedFile>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Verifies that <c>WriteBoardAsync</c> rolls back and fails when the file upload itself fails, instead of committing an attachment record for a file that was never saved.</summary>
    [Fact]
    public async Task WriteBoardAsync_RollsBackAndFails_WhenUploadFails()
    {
        _boardRepo.Setup(r => r.WriteBoardAsync(It.IsAny<Board>(), It.IsAny<CancellationToken>())).ReturnsAsync(1L);
        _fileClient.Setup(f => f.UploadAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var sut = CreateSut();

        var file = new AttachedFileDto { FileName = "doc.zip", ContentType = "application/zip", Bytes = [1, 2], Size = 2 };
        ServiceResult result = await sut.WriteBoardAsync(
            new BoardRequest { Type = BoardTypes.FreeForum, Title = "Test Title", Writer = "Alice" }, false, file, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        _attachedFileRepo.Verify(r => r.CreateAsync(It.IsAny<BoardAttachedFile>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Regression: an attachment record the domain rejects (here a file name over the 255-character
    /// column) is caught BEFORE the file is uploaded, so nothing is left orphaned in storage.
    /// </summary>
    [Fact]
    public async Task WriteBoardAsync_RejectsAnOverlongFileName_BeforeUploadingAnything()
    {
        _boardRepo.Setup(r => r.WriteBoardAsync(It.IsAny<Board>(), It.IsAny<CancellationToken>())).ReturnsAsync(1L);
        var sut = CreateSut();

        var file = new AttachedFileDto { FileName = new string('n', 256) + ".zip", ContentType = "application/zip", Bytes = [1, 2], Size = 2 };
        ServiceResult result = await sut.WriteBoardAsync(
            new BoardRequest { Type = BoardTypes.FreeForum, Title = "Test Title", Writer = "Alice" }, false, file, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal("BoardAttachedFile.NameTooLong", result.ErrorCode);
        _fileClient.Verify(f => f.UploadAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Regression test: a file whose name is only an extension (e.g. ".zip") produces an empty
    /// Path.GetFileNameWithoutExtension result. The attachment used to be persisted via
    /// BoardAttachedFile.Reconstitute (a validation-bypass method meant only for trusted
    /// repository data), so this empty name slipped through uncaught; it must now go through
    /// BoardAttachedFile.Create and be rejected.
    /// </summary>
    [Fact]
    public async Task WriteBoardAsync_RollsBackAndFails_WhenAttachmentNameIsEmpty()
    {
        _boardRepo.Setup(r => r.WriteBoardAsync(It.IsAny<Board>(), It.IsAny<CancellationToken>())).ReturnsAsync(1L);
        _fileClient.Setup(f => f.UploadAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var sut = CreateSut();

        var file = new AttachedFileDto { FileName = ".zip", ContentType = "application/zip", Bytes = [1, 2], Size = 2 };
        ServiceResult result = await sut.WriteBoardAsync(
            new BoardRequest { Type = BoardTypes.FreeForum, Title = "Test Title", Writer = "Alice" }, false, file, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        _attachedFileRepo.Verify(r => r.CreateAsync(It.IsAny<BoardAttachedFile>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Verifies that <c>WriteBoardAsync</c> returns fail when repository throws.</summary>
    [Fact]
    public async Task WriteBoardAsync_ReturnsFail_WhenRepositoryThrows()
    {
        _boardRepo.Setup(r => r.WriteBoardAsync(It.IsAny<Board>(), It.IsAny<CancellationToken>())).ThrowsAsync(new Exception("DB error"));
        var sut = CreateSut();

        ServiceResult result = await sut.WriteBoardAsync(
            new BoardRequest { Type = BoardTypes.FreeForum, Title = "Test Title", Writer = "Alice" }, false, null, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Verifies that <c>Noticed=true</c> from a non-admin caller does not pin the post —
    /// enforced by the service itself, not just by the controller that builds the request.</summary>
    [Fact]
    public async Task WriteBoardAsync_DoesNotPin_WhenNoticedTrueButNotAdmin()
    {
        Board? written = null;
        _boardRepo.Setup(r => r.WriteBoardAsync(It.IsAny<Board>(), It.IsAny<CancellationToken>()))
            .Callback<Board, CancellationToken>((b, _) => written = b)
            .ReturnsAsync(1L);
        var sut = CreateSut();

        ServiceResult result = await sut.WriteBoardAsync(
            new BoardRequest { Type = BoardTypes.FreeForum, Title = "Test Title", Writer = "Alice", Noticed = true },
            false, null, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.NotNull(written);
        Assert.False(written!.Noticed);
    }

    /// <summary>Verifies that <c>Noticed=true</c> from an admin caller does pin the post.</summary>
    [Fact]
    public async Task WriteBoardAsync_Pins_WhenNoticedTrueAndAdmin()
    {
        Board? written = null;
        _boardRepo.Setup(r => r.WriteBoardAsync(It.IsAny<Board>(), It.IsAny<CancellationToken>()))
            .Callback<Board, CancellationToken>((b, _) => written = b)
            .ReturnsAsync(1L);
        var sut = CreateSut();

        ServiceResult result = await sut.WriteBoardAsync(
            new BoardRequest { Type = BoardTypes.FreeForum, Title = "Test Title", Writer = "Alice", Noticed = true },
            true, null, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.NotNull(written);
        Assert.True(written!.Noticed);
    }

    // -- EditBoardAsync -------------------------------------------------------

    /// <summary>Verifies that <c>EditBoardAsync</c> returns fail when board not found.</summary>
    [Fact]
    public async Task EditBoardAsync_ReturnsFail_WhenBoardNotFound()
    {
        _boardRepo.Setup(r => r.FindActiveByIdForUpdateAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync((Board?)null);
        var sut = CreateSut();

        ServiceResult result = await sut.EditBoardAsync(new BoardRequest { Id = 99, Type = BoardTypes.FreeForum }, "Alice", isAdmin: false, null, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Verifies that <c>EditBoardAsync</c> returns fail when board is deleted.</summary>
    [Fact]
    public async Task EditBoardAsync_ReturnsFail_WhenBoardIsDeleted()
    {
        // FindActiveByIdForUpdateAsync already filters for !Deleted, so deleted board returns null
        _boardRepo.Setup(r => r.FindActiveByIdForUpdateAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync((Board?)null);
        var sut = CreateSut();

        ServiceResult result = await sut.EditBoardAsync(new BoardRequest { Id = 1, Type = BoardTypes.FreeForum }, "Alice", isAdmin: false, null, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Verifies that <c>EditBoardAsync</c> returns fail when requester is not writer.</summary>
    [Fact]
    public async Task EditBoardAsync_ReturnsFail_WhenRequesterIsNotWriter()
    {
        _boardRepo.Setup(r => r.FindActiveByIdForUpdateAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync(MakeBoard(writer: "Alice"));
        var sut = CreateSut();

        ServiceResult result = await sut.EditBoardAsync(new BoardRequest { Id = 1, Type = BoardTypes.FreeForum }, "Bob", isAdmin: false, null, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Verifies that <c>EditBoardAsync</c> returns success when writer matches and no new file.</summary>
    [Fact]
    public async Task EditBoardAsync_ReturnsSuccess_WhenWriterMatchesAndNoNewFile()
    {
        _boardRepo.Setup(r => r.FindActiveByIdForUpdateAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync(MakeBoard(writer: "Alice"));
        _boardRepo.Setup(r => r.UpdateEntityAsync(It.IsAny<Board>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _attachedFileRepo.Setup(r => r.FindByBoardIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync((BoardAttachedFile?)null);
        var sut = CreateSut();

        ServiceResult result = await sut.EditBoardAsync(
            new BoardRequest { Id = 1, Type = BoardTypes.FreeForum, Title = "New Title", Content = "New Content" }, "Alice", isAdmin: false, null, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Verifies that <c>EditBoardAsync</c> rolls back and fails when the new attachment's upload fails, instead of committing an attachment record for a file that was never saved.</summary>
    [Fact]
    public async Task EditBoardAsync_RollsBackAndFails_WhenUploadFails()
    {
        _boardRepo.Setup(r => r.FindActiveByIdForUpdateAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync(MakeBoard(writer: "Alice"));
        _boardRepo.Setup(r => r.UpdateEntityAsync(It.IsAny<Board>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _attachedFileRepo.Setup(r => r.FindByBoardIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync((BoardAttachedFile?)null);
        _fileClient.Setup(f => f.UploadAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var sut = CreateSut();

        var file = new AttachedFileDto { FileName = "doc.zip", ContentType = "application/zip", Bytes = [1, 2], Size = 2 };
        ServiceResult result = await sut.EditBoardAsync(
            new BoardRequest { Id = 1, Type = BoardTypes.FreeForum, Title = "New Title", Content = "New Content" }, "Alice", isAdmin: false, file, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        _attachedFileRepo.Verify(r => r.CreateAsync(It.IsAny<BoardAttachedFile>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Regression test: no edit form exposes a "Noticed" control, so <c>BoardRequest.Noticed</c>
    /// is always false on an edit request. Editing must not un-pin an already-pinned post.
    /// </summary>
    [Fact]
    public async Task EditBoardAsync_PreservesNoticed_WhenRequestDoesNotSpecifyIt()
    {
        Board pinnedBoard = Board.Reconstitute(1, BoardTypes.FreeForum, "Test Title", "Test Content", "Alice",
            DateTime.UtcNow, DateTime.UtcNow, 0L, deleted: false, locked: false, noticed: true);
        _boardRepo.Setup(r => r.FindActiveByIdForUpdateAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync(pinnedBoard);
        _attachedFileRepo.Setup(r => r.FindByBoardIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync((BoardAttachedFile?)null);
        Board? savedBoard = null;
        _boardRepo.Setup(r => r.UpdateEntityAsync(It.IsAny<Board>(), It.IsAny<CancellationToken>()))
            .Callback<Board, CancellationToken>((b, _) => savedBoard = b)
            .Returns(Task.CompletedTask);
        var sut = CreateSut();

        ServiceResult result = await sut.EditBoardAsync(
            new BoardRequest { Id = 1, Type = BoardTypes.FreeForum, Title = "New Title", Content = "New Content", Noticed = false },
            "Alice", isAdmin: false, null, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.NotNull(savedBoard);
        Assert.True(savedBoard.Noticed);
    }

    /// <summary>
    /// A non-admin author may lower the lock on their own post as well as raise it. A lock is only
    /// ever raised by the post's own author (an admin who is not the author can only clear one), so
    /// there is no "admin-imposed" lock for an author edit to clobber.
    /// </summary>
    [Fact]
    public async Task EditBoardAsync_NonAdminAuthor_CanClearTheirOwnLock()
    {
        Board lockedBoard = Board.Reconstitute(1, BoardTypes.FreeForum, "Test Title", "Test Content", "Alice",
            DateTime.UtcNow, DateTime.UtcNow, 0L, deleted: false, locked: true, noticed: false);
        _boardRepo.Setup(r => r.FindActiveByIdForUpdateAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync(lockedBoard);
        _attachedFileRepo.Setup(r => r.FindByBoardIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync((BoardAttachedFile?)null);
        Board? savedBoard = null;
        _boardRepo.Setup(r => r.UpdateEntityAsync(It.IsAny<Board>(), It.IsAny<CancellationToken>()))
            .Callback<Board, CancellationToken>((b, _) => savedBoard = b)
            .Returns(Task.CompletedTask);
        var sut = CreateSut();

        ServiceResult result = await sut.EditBoardAsync(
            new BoardRequest { Id = 1, Type = BoardTypes.FreeForum, Title = "New Title", Content = "New Content", Locked = false },
            "Alice", isAdmin: false, null, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.NotNull(savedBoard);
        Assert.False(savedBoard.Locked);
        Assert.Equal("New Title", savedBoard.Title);
    }

    /// <summary>A non-admin author's edit with <c>Locked = true</c> still raises the lock.</summary>
    [Fact]
    public async Task EditBoardAsync_NonAdminAuthor_CanRaiseTheirOwnLock()
    {
        Board unlockedBoard = MakeBoard(writer: "Alice");
        _boardRepo.Setup(r => r.FindActiveByIdForUpdateAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync(unlockedBoard);
        _attachedFileRepo.Setup(r => r.FindByBoardIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync((BoardAttachedFile?)null);
        Board? savedBoard = null;
        _boardRepo.Setup(r => r.UpdateEntityAsync(It.IsAny<Board>(), It.IsAny<CancellationToken>()))
            .Callback<Board, CancellationToken>((b, _) => savedBoard = b)
            .Returns(Task.CompletedTask);
        var sut = CreateSut();

        ServiceResult result = await sut.EditBoardAsync(
            new BoardRequest { Id = 1, Type = BoardTypes.FreeForum, Title = "New Title", Content = "New Content", Locked = true },
            "Alice", isAdmin: false, null, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.NotNull(savedBoard);
        Assert.True(savedBoard.Locked);
    }

    /// <summary>An admin author's edit can clear their own lock too.</summary>
    [Fact]
    public async Task EditBoardAsync_AdminAuthor_CanClearAnExistingLock()
    {
        Board lockedBoard = Board.Reconstitute(1, BoardTypes.FreeForum, "Test Title", "Test Content", "Alice",
            DateTime.UtcNow, DateTime.UtcNow, 0L, deleted: false, locked: true, noticed: false);
        _boardRepo.Setup(r => r.FindActiveByIdForUpdateAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync(lockedBoard);
        _attachedFileRepo.Setup(r => r.FindByBoardIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync((BoardAttachedFile?)null);
        Board? savedBoard = null;
        _boardRepo.Setup(r => r.UpdateEntityAsync(It.IsAny<Board>(), It.IsAny<CancellationToken>()))
            .Callback<Board, CancellationToken>((b, _) => savedBoard = b)
            .Returns(Task.CompletedTask);
        var sut = CreateSut();

        ServiceResult result = await sut.EditBoardAsync(
            new BoardRequest { Id = 1, Type = BoardTypes.FreeForum, Title = "New Title", Content = "New Content", Locked = false },
            "Alice", isAdmin: true, null, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.NotNull(savedBoard);
        Assert.False(savedBoard.Locked);
    }

    /// <summary>
    /// Regression test: before this fix, a non-admin author's locked post could never be
    /// unlocked by anyone -- the author isn't admin, and an admin who isn't the author was
    /// rejected by the ownership check before the lock logic ever ran. An admin acting on
    /// someone else's post must be able to clear the lock, without being able to change the
    /// post's title/content.
    /// </summary>
    [Fact]
    public async Task EditBoardAsync_AdminNonOwner_CanClearAnExistingLock_ButNotContent()
    {
        Board lockedBoard = Board.Reconstitute(1, BoardTypes.FreeForum, "Test Title", "Test Content", "Alice",
            DateTime.UtcNow, DateTime.UtcNow, 0L, deleted: false, locked: true, noticed: false);
        _boardRepo.Setup(r => r.FindActiveByIdForUpdateAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync(lockedBoard);
        _attachedFileRepo.Setup(r => r.FindByBoardIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync((BoardAttachedFile?)null);
        Board? savedBoard = null;
        _boardRepo.Setup(r => r.UpdateEntityAsync(It.IsAny<Board>(), It.IsAny<CancellationToken>()))
            .Callback<Board, CancellationToken>((b, _) => savedBoard = b)
            .Returns(Task.CompletedTask);
        var sut = CreateSut();

        ServiceResult result = await sut.EditBoardAsync(
            new BoardRequest { Id = 1, Type = BoardTypes.FreeForum, Title = "Attempted New Title", Content = "Attempted New Content", Locked = false },
            "Bob", isAdmin: true, null, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.NotNull(savedBoard);
        Assert.False(savedBoard.Locked);
        Assert.Equal("Test Title", savedBoard.Title);
        Assert.Equal("Test Content", savedBoard.Content);
    }

    /// <summary>An admin who is not the writer and does not clear an existing lock is still rejected.</summary>
    [Fact]
    public async Task EditBoardAsync_AdminNonOwner_ReturnsFail_WhenNotClearingALock()
    {
        Board unlockedBoard = MakeBoard(writer: "Alice");
        _boardRepo.Setup(r => r.FindActiveByIdForUpdateAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync(unlockedBoard);
        var sut = CreateSut();

        ServiceResult result = await sut.EditBoardAsync(
            new BoardRequest { Id = 1, Type = BoardTypes.FreeForum, Title = "New Title", Content = "New Content", Locked = false },
            "Bob", isAdmin: true, null, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// A private note is single-owner (no admin bypass, like view and delete): an admin who is not
    /// the writer must not be able to clear its lock, unlike on a free-forum post. The row must be
    /// left untouched and the transaction rolled back.
    /// </summary>
    [Fact]
    public async Task EditBoardAsync_AdminNonOwner_CannotClearLock_OnPrivateNote()
    {
        Board lockedNote = Board.Reconstitute(1, BoardTypes.PrivateNote, "Test Title", "Test Content", "Alice",
            DateTime.UtcNow, DateTime.UtcNow, 0L, deleted: false, locked: true, noticed: false);
        _boardRepo.Setup(r => r.FindActiveByIdForUpdateAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync(lockedNote);
        var sut = CreateSut();

        ServiceResult result = await sut.EditBoardAsync(
            new BoardRequest { Id = 1, Type = BoardTypes.PrivateNote, Title = "New Title", Content = "New Content", Locked = false },
            "Bob", isAdmin: true, null, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.True(lockedNote.Locked);
        _boardRepo.Verify(r => r.UpdateEntityAsync(It.IsAny<Board>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // -- DeleteBoardAsync -----------------------------------------------------

    /// <summary>Verifies that <c>DeleteBoardAsync</c> returns fail when board not found.</summary>
    [Fact]
    public async Task DeleteBoardAsync_ReturnsFail_WhenBoardNotFound()
    {
        _boardRepo.Setup(r => r.FindActiveByIdForUpdateAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync((Board?)null);
        var sut = CreateSut();

        ServiceResult result = await sut.DeleteBoardAsync(99, BoardTypes.FreeForum, "Alice", false, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
    }

    /// <summary>Verifies that <c>DeleteBoardAsync</c> returns fail when not owner and not admin.</summary>
    [Fact]
    public async Task DeleteBoardAsync_ReturnsFail_WhenNotOwnerAndNotAdmin()
    {
        _boardRepo.Setup(r => r.FindActiveByIdForUpdateAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync(MakeBoard(writer: "Alice"));
        var sut = CreateSut();

        ServiceResult result = await sut.DeleteBoardAsync(1, BoardTypes.FreeForum, "Bob", false, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains("permission", result.ErrorKey, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Verifies that <c>DeleteBoardAsync</c> returns success when owner.</summary>
    [Fact]
    public async Task DeleteBoardAsync_ReturnsSuccess_WhenOwner()
    {
        _boardRepo.Setup(r => r.FindActiveByIdForUpdateAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync(MakeBoard(writer: "Alice"));
        _boardRepo.Setup(r => r.UpdateEntityAsync(It.IsAny<Board>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var sut = CreateSut();

        ServiceResult result = await sut.DeleteBoardAsync(1, BoardTypes.FreeForum, "Alice", false, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
    }

    /// <summary>Verifies that <c>DeleteBoardAsync</c> returns success when admin.</summary>
    [Fact]
    public async Task DeleteBoardAsync_ReturnsSuccess_WhenAdmin()
    {
        _boardRepo.Setup(r => r.FindActiveByIdForUpdateAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync(MakeBoard(writer: "Alice"));
        _boardRepo.Setup(r => r.UpdateEntityAsync(It.IsAny<Board>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var sut = CreateSut();

        ServiceResult result = await sut.DeleteBoardAsync(1, BoardTypes.FreeForum, "Admin", true, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
    }

    // -- WriteCommentAsync ----------------------------------------------------

    /// <summary>Verifies that <c>WriteCommentAsync</c> returns fail when board not found.</summary>
    [Fact]
    public async Task WriteCommentAsync_ReturnsFail_WhenBoardNotFound()
    {
        _boardRepo.Setup(r => r.FindActiveByIdForUpdateAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync((Board?)null);
        var sut = CreateSut();

        ServiceResult result = await sut.WriteCommentAsync(new BoardCommentRequest { BoardId = 99 }, ct: TestContext.Current.CancellationToken);

        Assert.False(result.Success);
    }

    /// <summary>Verifies that <c>WriteCommentAsync</c> returns fail when required owner does not match.</summary>
    [Fact]
    public async Task WriteCommentAsync_ReturnsFail_WhenRequiredOwnerDoesNotMatch()
    {
        _boardRepo.Setup(r => r.FindActiveByIdForUpdateAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync(MakeBoard(writer: "Alice"));
        var sut = CreateSut();

        ServiceResult result = await sut.WriteCommentAsync(new BoardCommentRequest { BoardId = 1 }, "Bob", ct: TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains("permission", result.ErrorKey, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Verifies that <c>WriteCommentAsync</c> returns success when owner matches and no owner restriction.</summary>
    [Fact]
    public async Task WriteCommentAsync_ReturnsSuccess_WhenOwnerMatchesAndNoOwnerRestriction()
    {
        _boardRepo.Setup(r => r.FindActiveByIdForUpdateAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync(MakeBoard(writer: "Alice"));
        _commentRepo.Setup(r => r.GetByBoardIdOrderedAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _commentRepo.Setup(r => r.CreateAsync(It.IsAny<BoardComment>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var sut = CreateSut();

        ServiceResult result = await sut.WriteCommentAsync(new BoardCommentRequest { BoardId = 1, Writer = "Bob", Content = "Hello" }, ct: TestContext.Current.CancellationToken);

        Assert.True(result.Success);
    }

    /// <summary>Verifies that <c>WriteCommentAsync</c> fails on a locked board when the commenter is neither owner nor admin.</summary>
    [Fact]
    public async Task WriteCommentAsync_ReturnsFail_WhenBoardLockedAndCommenterIsNotOwnerOrAdmin()
    {
        _boardRepo.Setup(r => r.FindActiveByIdForUpdateAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync(MakeBoard(writer: "Alice", locked: true));
        var sut = CreateSut();

        ServiceResult result = await sut.WriteCommentAsync(new BoardCommentRequest { BoardId = 1, Writer = "Bob", Content = "Hi" }, ct: TestContext.Current.CancellationToken);

        Assert.False(result.Success);
    }

    /// <summary>Verifies that <c>WriteCommentAsync</c> still succeeds on a locked board when the commenter is the post's owner.</summary>
    [Fact]
    public async Task WriteCommentAsync_ReturnsSuccess_WhenBoardLockedAndCommenterIsOwner()
    {
        _boardRepo.Setup(r => r.FindActiveByIdForUpdateAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync(MakeBoard(writer: "Alice", locked: true));
        _commentRepo.Setup(r => r.GetByBoardIdOrderedAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _commentRepo.Setup(r => r.CreateAsync(It.IsAny<BoardComment>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var sut = CreateSut();

        ServiceResult result = await sut.WriteCommentAsync(new BoardCommentRequest { BoardId = 1, Writer = "Alice", Content = "Hi" }, ct: TestContext.Current.CancellationToken);

        Assert.True(result.Success);
    }

    /// <summary>Verifies that <c>WriteCommentAsync</c> still succeeds on a locked board when the commenter is an admin.</summary>
    [Fact]
    public async Task WriteCommentAsync_ReturnsSuccess_WhenBoardLockedAndCommenterIsAdmin()
    {
        _boardRepo.Setup(r => r.FindActiveByIdForUpdateAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync(MakeBoard(writer: "Alice", locked: true));
        _commentRepo.Setup(r => r.GetByBoardIdOrderedAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _commentRepo.Setup(r => r.CreateAsync(It.IsAny<BoardComment>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var sut = CreateSut();

        ServiceResult result = await sut.WriteCommentAsync(new BoardCommentRequest { BoardId = 1, Writer = "Bob", Content = "Hi" }, isAdmin: true, ct: TestContext.Current.CancellationToken);

        Assert.True(result.Success);
    }

    /// <summary>Verifies that <c>WriteCommentAsync</c> sets order based on existing comment count.</summary>
    [Fact]
    public async Task WriteCommentAsync_SetsOrderBasedOnHighestExistingCommentOrder()
    {
        _boardRepo.Setup(r => r.FindActiveByIdForUpdateAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync(MakeBoard());
        _commentRepo.Setup(r => r.GetByBoardIdOrderedAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                MakeComment(), MakeComment(2, order: 1L)
            ]);

        BoardComment? captured = null;
        _commentRepo.Setup(r => r.CreateAsync(It.IsAny<BoardComment>(), It.IsAny<CancellationToken>()))
            .Callback<BoardComment, CancellationToken>((c, _) => captured = c)
            .Returns(Task.CompletedTask);
        var sut = CreateSut();

        await sut.WriteCommentAsync(new BoardCommentRequest { BoardId = 1, Writer = "Bob", Content = "Hi" }, ct: TestContext.Current.CancellationToken);

        Assert.NotNull(captured);
        Assert.Equal(2L, captured.Order);
    }

    /// <summary>
    /// Regression test: after a comment is soft-deleted, <c>GetByBoardIdOrderedAsync</c> excludes it,
    /// so the remaining comments' count is no longer <c>max(Order) + 1</c>. The new comment's Order
    /// must still be derived from the highest surviving Order, not the count, or it collides with an
    /// existing comment's Order.
    /// </summary>
    [Fact]
    public async Task WriteCommentAsync_DoesNotCollideWithExistingOrder_AfterAPriorCommentWasDeleted()
    {
        _boardRepo.Setup(r => r.FindActiveByIdForUpdateAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync(MakeBoard());
        // Order 1 is "missing" here, simulating a soft-deleted comment excluded by the repository query.
        _commentRepo.Setup(r => r.GetByBoardIdOrderedAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                MakeComment(), MakeComment(3, order: 2L)
            ]);

        BoardComment? captured = null;
        _commentRepo.Setup(r => r.CreateAsync(It.IsAny<BoardComment>(), It.IsAny<CancellationToken>()))
            .Callback<BoardComment, CancellationToken>((c, _) => captured = c)
            .Returns(Task.CompletedTask);
        var sut = CreateSut();

        await sut.WriteCommentAsync(new BoardCommentRequest { BoardId = 1, Writer = "Bob", Content = "Hi" }, ct: TestContext.Current.CancellationToken);

        Assert.NotNull(captured);
        Assert.Equal(3L, captured.Order);
    }

    /// <summary>
    /// Regression test: comment writes must lock the parent post row (FOR UPDATE) inside a
    /// transaction, so concurrent commenters on the same post serialize instead of racing to
    /// read the same <c>Count()</c> and insert duplicate <c>Order</c> values.
    /// </summary>
    [Fact]
    public async Task WriteCommentAsync_LocksBoardRowInTransaction()
    {
        _boardRepo.Setup(r => r.FindActiveByIdForUpdateAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync(MakeBoard());
        _commentRepo.Setup(r => r.GetByBoardIdOrderedAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _commentRepo.Setup(r => r.CreateAsync(It.IsAny<BoardComment>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var sut = CreateSut();

        ServiceResult result = await sut.WriteCommentAsync(new BoardCommentRequest { BoardId = 1, Writer = "Bob", Content = "Hi" }, ct: TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        _boardRepo.Verify(r => r.FindActiveByIdForUpdateAsync(1, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.BeginAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // -- DeleteCommentAsync ---------------------------------------------------

    /// <summary>Verifies that <c>DeleteCommentAsync</c> returns fail when comment not found.</summary>
    [Fact]
    public async Task DeleteCommentAsync_ReturnsFail_WhenCommentNotFound()
    {
        _commentRepo.Setup(r => r.FindByIdForUpdateAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync((BoardComment?)null);
        var sut = CreateSut();

        ServiceResult result = await sut.DeleteCommentAsync(99, "Alice", false, ct: TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Verifies that <c>DeleteCommentAsync</c> returns fail when not owner and not admin.</summary>
    [Fact]
    public async Task DeleteCommentAsync_ReturnsFail_WhenNotOwnerAndNotAdmin()
    {
        _commentRepo.Setup(r => r.FindByIdForUpdateAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync(MakeComment(writer: "Alice"));
        var sut = CreateSut();

        ServiceResult result = await sut.DeleteCommentAsync(1, "Bob", false, ct: TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains("permission", result.ErrorKey, StringComparison.OrdinalIgnoreCase);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Verifies that <c>DeleteCommentAsync</c> returns success when owner.</summary>
    [Fact]
    public async Task DeleteCommentAsync_ReturnsSuccess_WhenOwner()
    {
        _commentRepo.Setup(r => r.FindByIdForUpdateAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync(MakeComment(writer: "Alice"));
        _commentRepo.Setup(r => r.UpdateEntityAsync(It.IsAny<BoardComment>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var sut = CreateSut();

        ServiceResult result = await sut.DeleteCommentAsync(1, "Alice", false, ct: TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        _unitOfWork.Verify(u => u.BeginAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Regression test: the comment lookup must be the locked (FOR UPDATE) variant, matching this
    /// file's other writes (WriteCommentAsync/EditBoardAsync/DeleteBoardAsync), not the unlocked
    /// FindByIdAsync — otherwise two concurrent deletes of the same comment could both succeed.
    /// </summary>
    [Fact]
    public async Task DeleteCommentAsync_UsesLockedLookup_NotUnlockedFindById()
    {
        _commentRepo.Setup(r => r.FindByIdForUpdateAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync(MakeComment(writer: "Alice"));
        _commentRepo.Setup(r => r.UpdateEntityAsync(It.IsAny<BoardComment>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var sut = CreateSut();

        await sut.DeleteCommentAsync(1, "Alice", false, ct: TestContext.Current.CancellationToken);

        _commentRepo.Verify(r => r.FindByIdForUpdateAsync(1, It.IsAny<CancellationToken>()), Times.Once);
        _commentRepo.Verify(r => r.FindByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // -- IncrementBoardViewAsync ----------------------------------------------

    /// <summary>Verifies that <c>IncrementBoardViewAsync</c> returns success when no exception.</summary>
    [Fact]
    public async Task IncrementBoardViewAsync_ReturnsSuccess_WhenNoException()
    {
        _boardRepo.Setup(r => r.IncrementViewAsync(1, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var sut = CreateSut();

        ServiceResult result = await sut.IncrementBoardViewAsync(1, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        _boardRepo.Verify(r => r.IncrementViewAsync(1, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Verifies that <c>IncrementBoardViewAsync</c> returns fail when repository throws.</summary>
    [Fact]
    public async Task IncrementBoardViewAsync_ReturnsFail_WhenRepositoryThrows()
    {
        _boardRepo.Setup(r => r.IncrementViewAsync(1, It.IsAny<CancellationToken>())).ThrowsAsync(new Exception("error"));
        var sut = CreateSut();

        ServiceResult result = await sut.IncrementBoardViewAsync(1, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
    }

    // -- EditBoardAsync: the four attachment states ------------------------------

    /// <summary>A tiny zip attachment named <paramref name="name"/>.</summary>
    private static AttachedFileDto Zip(string name = "new.zip") =>
        new() { FileName = name, ContentType = "application/zip", Bytes = [1, 2], Size = 2 };

    /// <summary>Arranges an editable post by Alice whose current attachment is <paramref name="previous"/>, with uploads succeeding.</summary>
    private void SetupEditableBoardWithAttachment(BoardAttachedFile? previous)
    {
        _boardRepo.Setup(r => r.FindActiveByIdForUpdateAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync(MakeBoard(writer: "Alice"));
        _boardRepo.Setup(r => r.UpdateEntityAsync(It.IsAny<Board>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _attachedFileRepo.Setup(r => r.FindByBoardIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync(previous);
        _fileClient.Setup(f => f.UploadAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    /// <summary>An edit request for post 1 with a new title and content.</summary>
    private static BoardRequest EditRequest() =>
        new() { Id = 1, Type = BoardTypes.FreeForum, Title = "New Title", Content = "New Content" };

    /// <summary>Replace: uploading a new file overwrites the SAME attachment row (same id) with the new file's metadata.</summary>
    [Fact]
    public async Task EditBoardAsync_ReplacesTheExistingAttachmentRecord_WhenANewFileIsUploaded()
    {
        SetupEditableBoardWithAttachment(BoardAttachedFile.Reconstitute(77, 1, 10, "old", ".txt", "/upload/old.txt"));

        ServiceResult result = await CreateSut().EditBoardAsync(EditRequest(), "Alice", false, Zip(), TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        _fileClient.Verify(f => f.UploadAsync(It.IsAny<byte[]>(), "application/zip", It.Is<string>(p => p.StartsWith("upload/Forum/FreeForum/boardAttachedFiles/1/") && p.EndsWith(".zip")), "https://files.example.com", It.IsAny<CancellationToken>()), Times.Once);
        _attachedFileRepo.Verify(r => r.UpdateEntityAsync(
            It.Is<BoardAttachedFile>(f => f.Id == 77 && f.BoardId == 1 && f.Name == "new" && f.Extension == ".zip" && f.Size == 2
                && f.Path.StartsWith("upload/Forum/FreeForum/boardAttachedFiles/1/") && f.Path.EndsWith(".zip")),
            It.IsAny<CancellationToken>()), Times.Once);
        _attachedFileRepo.Verify(r => r.CreateAsync(It.IsAny<BoardAttachedFile>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Clear: submitting the edit without a file empties the existing attachment record; nothing is uploaded (this is how an attachment is removed).</summary>
    [Fact]
    public async Task EditBoardAsync_ClearsTheExistingAttachmentRecord_WhenNoFileIsSubmitted()
    {
        SetupEditableBoardWithAttachment(BoardAttachedFile.Reconstitute(77, 1, 10, "old", ".txt", "/upload/old.txt"));

        ServiceResult result = await CreateSut().EditBoardAsync(EditRequest(), "Alice", false, null, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        _attachedFileRepo.Verify(r => r.UpdateEntityAsync(
            It.Is<BoardAttachedFile>(f => f.Id == 77 && f.BoardId == 1 && f.Size == 0 && f.Name == "" && f.Path == ""),
            It.IsAny<CancellationToken>()), Times.Once);
        _fileClient.Verify(f => f.UploadAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Add: a post that had no attachment gets a new record for the uploaded file.</summary>
    [Fact]
    public async Task EditBoardAsync_CreatesAnAttachmentRecord_WhenThePostHadNone()
    {
        SetupEditableBoardWithAttachment(previous: null);

        ServiceResult result = await CreateSut().EditBoardAsync(EditRequest(), "Alice", false, Zip(), TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        _attachedFileRepo.Verify(r => r.CreateAsync(It.Is<BoardAttachedFile>(f => f.BoardId == 1 && f.Name == "new"), It.IsAny<CancellationToken>()), Times.Once);
        _attachedFileRepo.Verify(r => r.UpdateEntityAsync(It.IsAny<BoardAttachedFile>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>No previous attachment and no new file: the attachment table is not touched at all.</summary>
    [Fact]
    public async Task EditBoardAsync_LeavesAttachmentsAlone_WhenThereWasNoneAndNoneIsSubmitted()
    {
        SetupEditableBoardWithAttachment(previous: null);

        ServiceResult result = await CreateSut().EditBoardAsync(EditRequest(), "Alice", false, null, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        _attachedFileRepo.Verify(r => r.CreateAsync(It.IsAny<BoardAttachedFile>(), It.IsAny<CancellationToken>()), Times.Never);
        _attachedFileRepo.Verify(r => r.UpdateEntityAsync(It.IsAny<BoardAttachedFile>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>A failed upload while REPLACING rolls everything back: the old record must not be overwritten to point at a missing file.</summary>
    [Fact]
    public async Task EditBoardAsync_RollsBackWithoutTouchingTheRecord_WhenReplacingAndTheUploadFails()
    {
        SetupEditableBoardWithAttachment(BoardAttachedFile.Reconstitute(77, 1, 10, "old", ".txt", "/upload/old.txt"));
        _fileClient.Setup(f => f.UploadAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        ServiceResult result = await CreateSut().EditBoardAsync(EditRequest(), "Alice", false, Zip(), TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal("Board.AttachmentUploadFailed", result.ErrorCode);
        _attachedFileRepo.Verify(r => r.UpdateEntityAsync(It.IsAny<BoardAttachedFile>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>A record the domain rejects (over-long name) is refused BEFORE uploading, for replace and add alike, so no orphan file is left.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task EditBoardAsync_RejectsAnOverlongFileName_BeforeUploading(bool hadPreviousAttachment)
    {
        SetupEditableBoardWithAttachment(hadPreviousAttachment ? BoardAttachedFile.Reconstitute(77, 1, 10, "old", ".txt", "/upload/old.txt") : null);

        ServiceResult result = await CreateSut().EditBoardAsync(
            EditRequest(), "Alice", false, Zip(new string('n', 256) + ".zip"), TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal("BoardAttachedFile.NameTooLong", result.ErrorCode);
        _fileClient.Verify(f => f.UploadAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>A post edited through another board type's endpoint is "not found" (no cross-type edit).</summary>
    [Fact]
    public async Task EditBoardAsync_ReturnsNotFound_WhenTheCallerExpectsADifferentBoardType()
    {
        _boardRepo.Setup(r => r.FindActiveByIdForUpdateAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync(MakeBoard(writer: "Alice"));

        ServiceResult result = await CreateSut().EditBoardAsync(
            new BoardRequest { Id = 1, Type = BoardTypes.PrivateNote, Title = "t", Content = "c" }, "Alice", false, null, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal("Board.NotFound", result.ErrorCode);
        _boardRepo.Verify(r => r.UpdateEntityAsync(It.IsAny<Board>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>A blank title on edit is returned as the domain validation error and nothing is written.</summary>
    [Fact]
    public async Task EditBoardAsync_ReturnsTheDomainValidationError_WhenTheTitleIsBlank()
    {
        SetupEditableBoardWithAttachment(previous: null);

        ServiceResult result = await CreateSut().EditBoardAsync(
            new BoardRequest { Id = 1, Type = BoardTypes.FreeForum, Title = "  ", Content = "c" }, "Alice", false, null, TestContext.Current.CancellationToken);

        Assert.Equal("Board.TitleEmpty", result.ErrorCode);
        _boardRepo.Verify(r => r.UpdateEntityAsync(It.IsAny<Board>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>An unexpected failure on edit is rolled back and reported without leaking internals.</summary>
    [Fact]
    public async Task EditBoardAsync_RollsBackAndReportsEditFailed_WhenTheRepositoryThrows()
    {
        _boardRepo.Setup(r => r.FindActiveByIdForUpdateAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("secret detail"));

        ServiceResult result = await CreateSut().EditBoardAsync(EditRequest(), "Alice", false, null, TestContext.Current.CancellationToken);

        Assert.Equal("Board.EditFailed", result.ErrorCode);
        Assert.DoesNotContain("secret detail", result.ErrorKey);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // -- WriteBoardAsync / DeleteBoardAsync: validation and failures ---------------

    /// <summary>A blank title is returned as the domain validation error; nothing is stored.</summary>
    [Fact]
    public async Task WriteBoardAsync_ReturnsTheDomainValidationError_WhenTheTitleIsBlank()
    {
        ServiceResult result = await CreateSut().WriteBoardAsync(
            new BoardRequest { Type = BoardTypes.FreeForum, Title = "", Writer = "Alice" }, false, null, TestContext.Current.CancellationToken);

        Assert.Equal("Board.TitleEmpty", result.ErrorCode);
        _boardRepo.Verify(r => r.WriteBoardAsync(It.IsAny<Board>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>A post can only be deleted through its own board type's endpoint.</summary>
    [Fact]
    public async Task DeleteBoardAsync_ReturnsNotFound_WhenTheBoardTypeDoesNotMatch()
    {
        _boardRepo.Setup(r => r.FindActiveByIdForUpdateAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync(MakeBoard(writer: "Alice"));

        ServiceResult result = await CreateSut().DeleteBoardAsync(1, BoardTypes.PrivateNote, "Alice", isAdmin: true, TestContext.Current.CancellationToken);

        Assert.Equal("Board.NotFound", result.ErrorCode);
        _boardRepo.Verify(r => r.UpdateEntityAsync(It.IsAny<Board>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>An unexpected failure on delete is rolled back and reported without leaking internals.</summary>
    [Fact]
    public async Task DeleteBoardAsync_RollsBackAndReportsDeleteFailed_WhenTheRepositoryThrows()
    {
        _boardRepo.Setup(r => r.FindActiveByIdForUpdateAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync(MakeBoard(writer: "Alice"));
        _boardRepo.Setup(r => r.UpdateEntityAsync(It.IsAny<Board>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("secret detail"));

        ServiceResult result = await CreateSut().DeleteBoardAsync(1, BoardTypes.FreeForum, "Alice", false, TestContext.Current.CancellationToken);

        Assert.Equal("Board.DeleteFailed", result.ErrorCode);
        Assert.DoesNotContain("secret detail", result.ErrorKey);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // -- Comments: validation, scoping and failures -------------------------------

    /// <summary>An empty comment is returned as the domain validation error; nothing is stored.</summary>
    [Fact]
    public async Task WriteCommentAsync_ReturnsTheDomainValidationError_WhenTheCommentIsEmpty()
    {
        _boardRepo.Setup(r => r.FindActiveByIdForUpdateAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync(MakeBoard(writer: "Alice"));
        _commentRepo.Setup(r => r.GetByBoardIdOrderedAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);

        ServiceResult result = await CreateSut().WriteCommentAsync(
            new BoardCommentRequest { BoardId = 1, Writer = "Bob", Content = "" }, ct: TestContext.Current.CancellationToken);

        Assert.Equal("BoardComment.ContentEmpty", result.ErrorCode);
        _commentRepo.Verify(r => r.CreateAsync(It.IsAny<BoardComment>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>A comment can only be written through the endpoint of the post's own board type.</summary>
    [Fact]
    public async Task WriteCommentAsync_ReturnsNotFound_WhenTheBoardTypeDoesNotMatchTheRequiredType()
    {
        _boardRepo.Setup(r => r.FindActiveByIdForUpdateAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync(MakeBoard(writer: "Alice"));

        ServiceResult result = await CreateSut().WriteCommentAsync(
            new BoardCommentRequest { BoardId = 1, Writer = "Bob", Content = "Hi" }, requiredType: BoardTypes.PrivateNote, ct: TestContext.Current.CancellationToken);

        Assert.Equal("Board.NotFound", result.ErrorCode);
        _commentRepo.Verify(r => r.CreateAsync(It.IsAny<BoardComment>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>An unexpected failure while writing a comment is rolled back and reported without leaking internals.</summary>
    [Fact]
    public async Task WriteCommentAsync_RollsBackAndReportsWriteCommentFailed_WhenTheRepositoryThrows()
    {
        _boardRepo.Setup(r => r.FindActiveByIdForUpdateAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("secret detail"));

        ServiceResult result = await CreateSut().WriteCommentAsync(
            new BoardCommentRequest { BoardId = 1, Writer = "Bob", Content = "Hi" }, ct: TestContext.Current.CancellationToken);

        Assert.Equal("Board.WriteCommentFailed", result.ErrorCode);
        Assert.DoesNotContain("secret detail", result.ErrorKey);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>When the caller is scoped to a board type, a comment whose parent post is gone (or of another type) is "not found".</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeleteCommentAsync_ReturnsNotFound_WhenTheParentPostIsMissingOrOfAnotherType(bool parentExists)
    {
        _commentRepo.Setup(r => r.FindByIdForUpdateAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync(MakeComment(writer: "Alice"));
        _boardRepo.Setup(r => r.FindActiveByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(parentExists ? MakeBoard(writer: "Alice") : null);

        ServiceResult result = await CreateSut().DeleteCommentAsync(1, "Alice", false, BoardTypes.PrivateNote, TestContext.Current.CancellationToken);

        Assert.Equal("BoardComment.NotFound", result.ErrorCode);
        _commentRepo.Verify(r => r.UpdateEntityAsync(It.IsAny<BoardComment>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>A private note is owner-only: an admin who did not write the comment cannot delete it, unlike on the free forum.</summary>
    [Fact]
    public async Task DeleteCommentAsync_DeniesAnAdmin_OnAPrivateNoteComment_ButAllowsThemOnAForumComment()
    {
        var privateNote = Board.Reconstitute(1, BoardTypes.PrivateNote, "t", "c", "Owner", DateTime.UtcNow, DateTime.UtcNow, 0L, false, false, false);
        _commentRepo.Setup(r => r.FindByIdForUpdateAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync(MakeComment(writer: "Owner"));
        _commentRepo.Setup(r => r.UpdateEntityAsync(It.IsAny<BoardComment>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _boardRepo.Setup(r => r.FindActiveByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync(privateNote);

        ServiceResult onPrivateNote = await CreateSut().DeleteCommentAsync(1, "AdminNick", isAdmin: true, BoardTypes.PrivateNote, TestContext.Current.CancellationToken);

        Assert.False(onPrivateNote.Success);
        Assert.Contains("permission", onPrivateNote.ErrorKey, StringComparison.OrdinalIgnoreCase);

        _boardRepo.Setup(r => r.FindActiveByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync(MakeBoard(writer: "Owner"));
        ServiceResult onForum = await CreateSut().DeleteCommentAsync(1, "AdminNick", isAdmin: true, BoardTypes.FreeForum, TestContext.Current.CancellationToken);

        Assert.True(onForum.Success);
    }

    /// <summary>An unexpected failure while deleting a comment is rolled back and reported without leaking internals.</summary>
    [Fact]
    public async Task DeleteCommentAsync_RollsBackAndReportsDeleteCommentFailed_WhenTheRepositoryThrows()
    {
        _commentRepo.Setup(r => r.FindByIdForUpdateAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("secret detail"));

        ServiceResult result = await CreateSut().DeleteCommentAsync(1, "Alice", false, ct: TestContext.Current.CancellationToken);

        Assert.Equal("Board.DeleteCommentFailed", result.ErrorCode);
        Assert.DoesNotContain("secret detail", result.ErrorKey);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // -- Attachment-content delegation ---------------------------------------------

    /// <summary>The Summernote image upload is delegated to the shared attachment service with the caller's area and board type.</summary>
    [Fact]
    public async Task UploadSummernoteImageAsync_DelegatesToTheAttachmentService()
    {
        var file = new AttachedFileDto { Bytes = [1], ContentType = "image/png", FileName = "a.png", Size = 1 };
        var expected = SummernoteUploadResult.Fail("nope");
        _attachmentContent.Setup(a => a.UploadSummernoteImageAsync(file, "user", BoardTypes.FreeForum, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        SummernoteUploadResult actual = await CreateSut().UploadSummernoteImageAsync(file, "user", BoardTypes.FreeForum, TestContext.Current.CancellationToken);

        Assert.Same(expected, actual);
    }

    /// <summary>The file download is delegated to the shared attachment service.</summary>
    [Fact]
    public async Task DownloadFileAsync_DelegatesToTheAttachmentService()
    {
        _attachmentContent.Setup(a => a.DownloadFileAsync("/upload/x.png", It.IsAny<CancellationToken>())).ReturnsAsync([
            .. "\t\t"u8
        ]);

        Assert.Equal("\t\t"u8.ToArray(), await CreateSut().DownloadFileAsync("/upload/x.png", TestContext.Current.CancellationToken));
    }

    // -- Domain refusals after the service's own checks ----------------------------------------------

    /// <summary>A comment on a locked post by someone who is neither its author nor an admin is refused by the post itself, and the transaction rolls back.</summary>
    [Fact]
    public async Task WriteCommentAsync_ReturnsTheDomainRefusal_ForALockedPostAndAStranger()
    {
        _boardRepo.Setup(r => r.FindActiveByIdForUpdateAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync(MakeBoard(writer: "Alice", locked: true));
        _commentRepo.Setup(r => r.GetByBoardIdOrderedAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var sut = CreateSut();

        ServiceResult result = await sut.WriteCommentAsync(new BoardCommentRequest { BoardId = 1, Writer = "Bob", Content = "Hello" }, ct: TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal("Board.Locked", result.ErrorCode);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        _commentRepo.Verify(r => r.CreateAsync(It.IsAny<BoardComment>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Deleting a comment that is already deleted is refused by the comment itself (the lookup does not filter deleted rows), and the transaction rolls back.</summary>
    [Fact]
    public async Task DeleteCommentAsync_ReturnsTheDomainRefusal_ForAnAlreadyDeletedComment()
    {
        _commentRepo.Setup(r => r.FindByIdForUpdateAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BoardComment.Reconstitute(1, 1L, 0L, null, "Alice", "Comment", DateTime.UtcNow, true));
        var sut = CreateSut();

        ServiceResult result = await sut.DeleteCommentAsync(1, "Alice", false, ct: TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal("BoardComment.AlreadyDeleted", result.ErrorCode);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
