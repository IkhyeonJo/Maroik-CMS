using Maroik.Core.Contract.Dtos;
using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Contract.Misc.Settings;
using Maroik.Core.Domain.Account;
using Maroik.Core.Domain.Calendar;
using Maroik.Core.Service.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace Maroik.Core.Service.Tests.Services;

/// <summary>
/// <see cref="CalendarService.OpenCalendarEventAttachedFileAsync"/>: an event's attachment is handed out
/// only after it is checked again that the event's calendar is one the requesting viewer may see (their
/// own, one they subscribe to that is still shared, or — for an anonymous visitor — one shared with
/// anonymous visitors), and it is streamed from file storage rather than embedded in the event detail.
/// A refused download is logged as a permission denial.
/// </summary>
public class CalendarServiceAttachmentDownloadTests
{
    /// <summary>Mock <c>ICalendarRepository</c> injected into the system under test.</summary>
    private readonly Mock<ICalendarRepository> _calendarRepo = new();
    /// <summary>Mock <c>ICalendarEventRepository</c> injected into the system under test.</summary>
    private readonly Mock<ICalendarEventRepository> _eventRepo = new();
    /// <summary>Mock <c>ICalendarEventAttachedFileRepository</c> injected into the system under test.</summary>
    private readonly Mock<ICalendarEventAttachedFileRepository> _attachedFileRepo = new();
    /// <summary>Mock <c>ICalendarSharedRepository</c> injected into the system under test.</summary>
    private readonly Mock<ICalendarSharedRepository> _sharedRepo = new();
    /// <summary>Mock <c>IOtherCalendarRepository</c> injected into the system under test.</summary>
    private readonly Mock<IOtherCalendarRepository> _otherCalendarRepo = new();
    /// <summary>Mock <c>IAttachmentContentService</c> that opens the stored file.</summary>
    private readonly Mock<IAttachmentContentService> _attachmentContent = new();
    /// <summary>Captures the log entries the system under test writes.</summary>
    private readonly FakeLogger<CalendarService> _logger = new();

    /// <summary>The storage path of the attachment arranged by <see cref="GivenAnEventWithAnAttachment"/>.</summary>
    private const string StoredPath = "upload/Calendar/UserIndex/calendarEventAttachedFiles/7/A.zip";

    /// <summary>The service under test over the mocked dependencies.</summary>
    private CalendarService CreateSut() => new(_calendarRepo.Object, _eventRepo.Object, _attachedFileRepo.Object,
        Mock.Of<ICalendarEventReminderRepository>(), _sharedRepo.Object, _otherCalendarRepo.Object,
        Mock.Of<IFileClient>(), Mock.Of<IUnitOfWork>(), _attachmentContent.Object,
        Options.Create(new ServerSetting()), _logger, new FakeTimeProvider());

    /// <summary>A signed-in viewer with <paramref name="email"/>.</summary>
    private static AccountResponse Viewer(string email = "alice@example.com") =>
        new() { Email = email, Nickname = "Alice", Role = Role.User };

    /// <summary>
    /// Arranges calendar 3 owned by alice@example.com (also the only calendar in the site) holding event 7,
    /// which has a stored attachment "report.zip"; nothing is shared or subscribed.
    /// </summary>
    private void GivenAnEventWithAnAttachment()
    {
        Calendar calendar = Calendar.Reconstitute(3, "alice@example.com", "Mine", null, "UTC", "#3788d8", DateTime.UtcNow, DateTime.UtcNow);
        _calendarRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _calendarRepo.Setup(r => r.GetByAccountEmailAsync("alice@example.com", It.IsAny<CancellationToken>())).ReturnsAsync([calendar]);
        _calendarRepo.Setup(r => r.GetAllOrderedByNameAsync(It.IsAny<CancellationToken>())).ReturnsAsync([calendar]);
        _sharedRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _otherCalendarRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _eventRepo.Setup(r => r.FindByIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CalendarEvent.Reconstitute(7, 3, "t", null, false, DateTime.UtcNow, DateTime.UtcNow.AddHours(1),
                null, null, null, null, null, DateTime.UtcNow, DateTime.UtcNow));
        _attachedFileRepo.Setup(r => r.FindByCalendarEventIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CalendarEventAttachedFile.Reconstitute(1, 7, 4, "report", ".zip", StoredPath));
    }

    /// <summary>Calendar 3 is shared with the given audiences.</summary>
    private void GivenTheCalendarIsShared(bool user, bool anonymous) =>
        _sharedRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([CalendarShared.Reconstitute(3, user, anonymous)]);

    /// <summary>Asserts the call was refused as a missing event, nothing was opened, and the denial was logged.</summary>
    private void AssertRefusedAndLogged(ServiceResult result, AttachmentDownload? file)
    {
        Assert.False(result.Success);
        Assert.Equal("CalendarEvent.NotFound", result.ErrorCode);
        Assert.Equal("The calendar event could not be found.", result.ErrorKey);
        Assert.Null(file);
        _attachmentContent.Verify(a => a.OpenFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        FakeLogRecord denial = Assert.Single(_logger.Collector.GetSnapshot(), r => r.Level == LogLevel.Warning);
        Assert.Contains("Attachment download refused", denial.Message);
        Assert.Contains("7", denial.Message);
    }

    /// <summary>The owner of the event's calendar gets the opened stream, named after the stored file.</summary>
    [Fact]
    public async Task OpenCalendarEventAttachedFileAsync_ReturnsTheStream_ToTheOwnerOfTheCalendar()
    {
        GivenAnEventWithAnAttachment();
        var stream = new MemoryStream([1, 2, 3, 4]);
        _attachmentContent.Setup(a => a.OpenFileAsync(StoredPath, It.IsAny<CancellationToken>())).ReturnsAsync(stream);

        var (result, file) = await CreateSut().OpenCalendarEventAttachedFileAsync(7, Viewer(), TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.NotNull(file);
        Assert.Same(stream, file.Content);
        Assert.Equal("report.zip", file.FileName);
    }

    /// <summary>Another account that neither owns nor subscribes to the calendar is refused.</summary>
    [Fact]
    public async Task OpenCalendarEventAttachedFileAsync_RefusesAndLogs_AnAccountThatMayNotSeeTheCalendar()
    {
        GivenAnEventWithAnAttachment();
        GivenTheCalendarIsShared(user: true, anonymous: false);

        var (result, file) = await CreateSut().OpenCalendarEventAttachedFileAsync(7, Viewer("mallory@example.com"), TestContext.Current.CancellationToken);

        AssertRefusedAndLogged(result, file);
    }

    /// <summary>An account subscribed to a calendar that is still shared with users may download.</summary>
    [Fact]
    public async Task OpenCalendarEventAttachedFileAsync_ReturnsTheStream_ToASubscriberOfASharedCalendar()
    {
        GivenAnEventWithAnAttachment();
        GivenTheCalendarIsShared(user: true, anonymous: false);
        _otherCalendarRepo.Setup(r => r.GetByAccountEmailAsync("bob@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync([OtherCalendar.Reconstitute("bob@example.com", 3)]);
        _attachmentContent.Setup(a => a.OpenFileAsync(StoredPath, It.IsAny<CancellationToken>())).ReturnsAsync(new MemoryStream([1]));

        var (result, file) = await CreateSut().OpenCalendarEventAttachedFileAsync(7, Viewer("bob@example.com"), TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal("report.zip", file!.FileName);
    }

    /// <summary>An anonymous visitor may download from a calendar shared with anonymous visitors.</summary>
    [Fact]
    public async Task OpenCalendarEventAttachedFileAsync_ReturnsTheStream_ToAnAnonymousVisitor_OnAnAnonymouslySharedCalendar()
    {
        GivenAnEventWithAnAttachment();
        GivenTheCalendarIsShared(user: false, anonymous: true);
        _attachmentContent.Setup(a => a.OpenFileAsync(StoredPath, It.IsAny<CancellationToken>())).ReturnsAsync(new MemoryStream([1]));

        var (result, _) = await CreateSut().OpenCalendarEventAttachedFileAsync(7, null, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
    }

    /// <summary>An anonymous visitor is refused a calendar that is shared with users only.</summary>
    [Fact]
    public async Task OpenCalendarEventAttachedFileAsync_RefusesAndLogs_AnAnonymousVisitor_OnACalendarNotSharedWithThem()
    {
        GivenAnEventWithAnAttachment();
        GivenTheCalendarIsShared(user: true, anonymous: false);

        var (result, file) = await CreateSut().OpenCalendarEventAttachedFileAsync(7, null, TestContext.Current.CancellationToken);

        AssertRefusedAndLogged(result, file);
    }

    /// <summary>A missing event is refused the same way as an invisible one.</summary>
    [Fact]
    public async Task OpenCalendarEventAttachedFileAsync_RefusesAndLogs_AMissingEvent()
    {
        GivenAnEventWithAnAttachment();
        _eventRepo.Setup(r => r.FindByIdAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync((CalendarEvent?)null);

        var (result, file) = await CreateSut().OpenCalendarEventAttachedFileAsync(7, Viewer(), TestContext.Current.CancellationToken);

        AssertRefusedAndLogged(result, file);
    }

    /// <summary>An event without an attachment, or with the cleared (empty-path) record, is reported as such.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OpenCalendarEventAttachedFileAsync_ReportsNotFound_WhenTheEventHasNoAttachment(bool clearedRecord)
    {
        GivenAnEventWithAnAttachment();
        _attachedFileRepo.Setup(r => r.FindByCalendarEventIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(clearedRecord ? CalendarEventAttachedFile.Reconstitute(1, 7, 0, "", "", "") : null);

        var (result, file) = await CreateSut().OpenCalendarEventAttachedFileAsync(7, Viewer(), TestContext.Current.CancellationToken);

        Assert.Equal("CalendarEvent.AttachedFileNotFound", result.ErrorCode);
        Assert.Equal("The attached file could not be found.", result.ErrorKey);
        Assert.Null(file);
        _attachmentContent.Verify(a => a.OpenFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>A file the storage service cannot hand out is a failure (logged where it happens), not a missing file.</summary>
    [Fact]
    public async Task OpenCalendarEventAttachedFileAsync_ReportsAFailure_WhenTheFileCannotBeOpened()
    {
        GivenAnEventWithAnAttachment();
        _attachmentContent.Setup(a => a.OpenFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Stream?)null);

        var (result, file) = await CreateSut().OpenCalendarEventAttachedFileAsync(7, Viewer(), TestContext.Current.CancellationToken);

        Assert.Equal("CalendarEvent.AttachedFileUnavailable", result.ErrorCode);
        Assert.Equal(ServiceResult.TemporaryErrorKey, result.ErrorKey);
        Assert.Equal(ServiceErrorType.Failure, result.ErrorType);
        Assert.Null(file);
    }

    /// <summary>A refused anonymous download logs the viewer as "Anonymous"; a signed-in one logs its e-mail.</summary>
    [Theory]
    [InlineData(null, "Anonymous")]
    [InlineData("mallory@example.com", "mallory@example.com")]
    public async Task OpenCalendarEventAttachedFileAsync_LogsWhoWasRefused(string? viewerEmail, string expected)
    {
        GivenAnEventWithAnAttachment();

        await CreateSut().OpenCalendarEventAttachedFileAsync(7, viewerEmail == null ? null : Viewer(viewerEmail), TestContext.Current.CancellationToken);

        FakeLogRecord denial = Assert.Single(_logger.Collector.GetSnapshot(), r => r.Level == LogLevel.Warning);
        Assert.EndsWith($"is missing or not visible to {expected}", denial.Message);
    }
}
