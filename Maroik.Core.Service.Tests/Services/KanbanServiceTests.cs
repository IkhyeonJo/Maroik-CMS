using Maroik.Core.Contract.Dtos;
using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Domain.Kanbans;
using Maroik.Core.Service.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace Maroik.Core.Service.Tests.Services;

/// <summary>
/// Unit tests for <see cref="KanbanService"/>. The repository is a Moq mock; the clock is stopped at <see cref="Now"/>.
/// Covers the board layout (four lanes, creation order, the overdue flag in the owner's time zone), the card writes (each a single
/// non-transactional repository call), the owner scoping (another account's card is "not found", K2), the fixed lane (K11), and
/// the log entries: a delete at Information, a card that was not found at Warning — never the card's text.
/// </summary>
public class KanbanServiceTests
{
    /// <summary>The owner of the cards these tests use.</summary>
    private const string Email = "user@example.com";

    /// <summary>A title that must never appear in a log entry.</summary>
    private const string SecretTitle = "SECRET TITLE";

    /// <summary>The fixed "current time" of these tests: 2026-05-24 20:00 UTC (already the 25th in Seoul).</summary>
    private static readonly DateTime Now = new(2026, 5, 24, 20, 0, 0, DateTimeKind.Utc);

    /// <summary>Mock <c>IKanbanCardRepository</c> injected into the system under test.</summary>
    private readonly Mock<IKanbanCardRepository> _repository = new(MockBehavior.Strict);

    /// <summary>Captures the log entries the system under test writes.</summary>
    private readonly FakeLogger<KanbanService> _logger = new();

    /// <summary>The clock the service under test reads, stopped at <see cref="Now"/>.</summary>
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(Now));

    /// <summary>The service under test over the mocked repository.</summary>
    private KanbanService CreateSut() => new(_repository.Object, _logger, _time);

    /// <summary>A stored card of <see cref="Email"/>.</summary>
    private static KanbanCard Stored(long id, string lane = "ToDo", DateOnly? dueDate = null, DateTime? created = null, string title = SecretTitle) =>
        KanbanCard.Reconstitute(id, Email, lane, title, "SECRET BODY", dueDate, created ?? Now.AddDays(-1), created ?? Now.AddDays(-1));

    /// <summary>Makes the repository answer the owner-scoped lookup of <paramref name="id"/> with <paramref name="card"/>.</summary>
    private void Find(long id, KanbanCard? card) =>
        _repository.Setup(r => r.FindByEmailAndIdAsync(Email, id, It.IsAny<CancellationToken>())).ReturnsAsync(card);

    // -- GetBoardAsync ------------------------------------------------------------

    /// <summary>A user with no cards gets the four lanes, in order, each empty.</summary>
    [Fact]
    public async Task GetBoardAsync_ReturnsTheFourEmptyLanes_ForANewUser()
    {
        _repository.Setup(r => r.GetByAccountEmailAsync(Email, It.IsAny<CancellationToken>())).ReturnsAsync([]);

        List<KanbanLaneResponse> board = await CreateSut().GetBoardAsync(Email, "UTC", TestContext.Current.CancellationToken);

        Assert.Equal(["Backlog", "ToDo", "InProgress", "Done"], board.Select(l => l.Lane));
        Assert.All(board, lane => Assert.Empty(lane.Cards));
    }

    /// <summary>Each card is mapped into its lane, oldest first, with every field.</summary>
    [Fact]
    public async Task GetBoardAsync_MapsEachCard_IntoItsLane_OldestFirst()
    {
        _repository.Setup(r => r.GetByAccountEmailAsync(Email, It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            Stored(2, "Done", created: Now.AddHours(-1)),
            Stored(1, "Done", new DateOnly(2026, 6, 1), Now.AddHours(-2)),
        ]);

        List<KanbanLaneResponse> board = await CreateSut().GetBoardAsync(Email, "UTC", TestContext.Current.CancellationToken);

        KanbanLaneResponse done = board[3];
        Assert.Equal("Done", done.Lane);
        Assert.Equal([1L, 2L], done.Cards.Select(c => c.Id));
        KanbanCardResponse first = done.Cards[0];
        Assert.Equal("Done", first.Lane);
        Assert.Equal(SecretTitle, first.Title);
        Assert.Equal("SECRET BODY", first.Description);
        Assert.Equal(new DateOnly(2026, 6, 1), first.DueDate);
        Assert.False(first.Overdue);
        Assert.Equal(Now.AddHours(-2), first.Created);
        Assert.Equal(Now.AddHours(-2), first.Updated);
    }

    /// <summary>
    /// The overdue flag is decided in the time zone the caller passes (the owner's): at 2026-05-24 20:00 UTC a card due on the 24th
    /// is overdue in Seoul (already the 25th) but not in New York (still the 24th).
    /// </summary>
    [Fact]
    public async Task GetBoardAsync_DecidesOverdue_InTheGivenTimeZone()
    {
        _repository.Setup(r => r.GetByAccountEmailAsync(Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Stored(1, "ToDo", new DateOnly(2026, 5, 24))]);

        var seoul = await CreateSut().GetBoardAsync(Email, "Asia/Seoul", TestContext.Current.CancellationToken);
        var newYork = await CreateSut().GetBoardAsync(Email, "America/New_York", TestContext.Current.CancellationToken);

        Assert.True(seoul[1].Cards[0].Overdue);
        Assert.False(newYork[1].Cards[0].Overdue);
    }

    // -- CreateAsync --------------------------------------------------------------

    /// <summary>A valid request creates the card in the requested lane, stamped with the clock's time.</summary>
    [Fact]
    public async Task CreateAsync_CreatesTheCard_InTheRequestedLane()
    {
        KanbanCard? created = null;
        _repository.Setup(r => r.CreateAsync(It.IsAny<KanbanCard>(), It.IsAny<CancellationToken>()))
            .Callback((KanbanCard c, CancellationToken _) => created = c).Returns(Task.CompletedTask);

        ServiceResult result = await CreateSut().CreateAsync(Email, new KanbanCardCreateRequest
        {
            Lane = "InProgress", Title = "Write", Description = "Body", DueDate = new DateOnly(2026, 6, 1),
        }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.NotNull(created);
        Assert.Equal(Email, created.AccountEmail.Value);
        Assert.Equal("InProgress", created.Lane);
        Assert.Equal("Write", created.Title);
        Assert.Equal("Body", created.Description);
        Assert.Equal(new DateOnly(2026, 6, 1), created.DueDate);
        Assert.Equal(Now, created.Created);
    }

    /// <summary>A request the domain refuses returns its error (code, template, arguments) and writes nothing.</summary>
    [Fact]
    public async Task CreateAsync_ReturnsTheDomainError_AndWritesNothing()
    {
        ServiceResult result = await CreateSut().CreateAsync(Email, new KanbanCardCreateRequest { Lane = "Archive", Title = "Write" },
            TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(ServiceErrorType.Validation, result.ErrorType);
        Assert.Equal("KanbanCard.LaneInvalid", result.ErrorCode);
        Assert.Equal("'{0}' is not a Kanban lane.", result.ErrorKey);
        Assert.Equal(["Archive"], result.ErrorArgs);
        _repository.Verify(r => r.CreateAsync(It.IsAny<KanbanCard>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // -- UpdateAsync --------------------------------------------------------------

    /// <summary>An edit replaces the content of the owner's card, keeps its lane, and saves it.</summary>
    [Fact]
    public async Task UpdateAsync_SavesTheNewContent_AndKeepsTheLane()
    {
        KanbanCard card = Stored(5, "Backlog", new DateOnly(2026, 5, 1));
        Find(5, card);
        _repository.Setup(r => r.UpdateEntityAsync(card, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        ServiceResult result = await CreateSut().UpdateAsync(Email, new KanbanCardUpdateRequest { Id = 5, Title = "New", Description = null, DueDate = null },
            TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal("New", card.Title);
        Assert.Null(card.Description);
        Assert.Null(card.DueDate);
        Assert.Equal("Backlog", card.Lane);
        Assert.Equal(Now, card.Updated);
        _repository.Verify(r => r.UpdateEntityAsync(card, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>The edit request has no lane at all, so no edit can move a card to another lane (K11).</summary>
    [Fact]
    public void TheUpdateRequest_CarriesNoLane()
    {
        Assert.Null(typeof(KanbanCardUpdateRequest).GetProperty("Lane"));
    }

    /// <summary>An edit the domain refuses returns its error and saves nothing.</summary>
    [Fact]
    public async Task UpdateAsync_ReturnsTheDomainError_AndSavesNothing()
    {
        Find(5, Stored(5));

        ServiceResult result = await CreateSut().UpdateAsync(Email, new KanbanCardUpdateRequest { Id = 5, Title = " " }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal("KanbanCard.TitleEmpty", result.ErrorCode);
        Assert.Equal("Card title cannot be empty.", result.ErrorKey);
        _repository.Verify(r => r.UpdateEntityAsync(It.IsAny<KanbanCard>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>A card the account does not own (or that is gone) is "not found", logged at Warning without its text.</summary>
    [Fact]
    public async Task UpdateAsync_ReturnsNotFound_AndLogsAWarning_WhenTheCardIsNotTheAccounts()
    {
        Find(9, null);

        ServiceResult result = await CreateSut().UpdateAsync(Email, new KanbanCardUpdateRequest { Id = 9, Title = SecretTitle }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(ServiceErrorType.NotFound, result.ErrorType);
        Assert.Equal("KanbanCard.NotFound", result.ErrorCode);
        Assert.Equal(ServiceErrorKeys.KanbanCardNotFound, result.ErrorKey);
        FakeLogRecord record = Assert.Single(_logger.Collector.GetSnapshot());
        Assert.Equal(LogLevel.Warning, record.Level);
        Assert.Equal($"Kanban card 9 not found for {Email} (update)", record.Message);
        Assert.Null(record.Exception);
    }

    // -- DeleteAsync --------------------------------------------------------------

    /// <summary>The owner's delete removes the card (owner-scoped) and is logged at Information: who, which card — not its text.</summary>
    [Fact]
    public async Task DeleteAsync_DeletesTheCard_AndLogsWhoDeletedWhichCard()
    {
        Find(7, Stored(7, "InProgress"));
        _repository.Setup(r => r.DeleteByEmailAndIdAsync(Email, 7, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        ServiceResult result = await CreateSut().DeleteAsync(Email, 7, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        _repository.Verify(r => r.DeleteByEmailAndIdAsync(Email, 7, It.IsAny<CancellationToken>()), Times.Once);
        FakeLogRecord record = Assert.Single(_logger.Collector.GetSnapshot());
        Assert.Equal(LogLevel.Information, record.Level);
        Assert.Equal($"Kanban card 7 (InProgress) deleted by {Email}", record.Message);
        Assert.DoesNotContain("SECRET", record.Message, StringComparison.Ordinal);
    }

    /// <summary>A delete of a card the account does not own is "not found", deletes nothing, and is logged at Warning.</summary>
    [Fact]
    public async Task DeleteAsync_ReturnsNotFound_AndLogsAWarning_WhenTheCardIsNotTheAccounts()
    {
        Find(9, null);

        ServiceResult result = await CreateSut().DeleteAsync(Email, 9, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(ServiceErrorType.NotFound, result.ErrorType);
        Assert.Equal("KanbanCard.NotFound", result.ErrorCode);
        Assert.Equal(ServiceErrorKeys.KanbanCardNotFound, result.ErrorKey);
        _repository.Verify(r => r.DeleteByEmailAndIdAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
        FakeLogRecord record = Assert.Single(_logger.Collector.GetSnapshot());
        Assert.Equal(LogLevel.Warning, record.Level);
        Assert.Equal($"Kanban card 9 not found for {Email} (delete)", record.Message);
    }

    /// <summary>A repository failure propagates to the caller (the controller logs it and answers the generic failure).</summary>
    [Fact]
    public async Task DeleteAsync_PropagatesARepositoryFailure_WithoutLoggingItTwice()
    {
        Find(7, Stored(7));
        _repository.Setup(r => r.DeleteByEmailAndIdAsync(Email, 7, It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("db down"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateSut().DeleteAsync(Email, 7, TestContext.Current.CancellationToken));

        Assert.Empty(_logger.Collector.GetSnapshot());
    }
}
