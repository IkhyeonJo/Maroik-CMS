using Maroik.Core.Contract.Dtos;
using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Contract.Misc.Settings;
using Maroik.Core.Domain.Calendar;
using Maroik.Core.Service.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
// ReSharper disable InvalidXmlDocComment

namespace Maroik.Core.Service.Tests.Services;

/// <summary>
/// Unit tests for <see cref="CalendarService"/>.
/// All external dependencies (repositories, ClamAV client, file client, logger)
/// are replaced with Moq mocks. Covers calendar and calendar-event CRUD,
/// calendar sharing / subscription, and reminder management.
/// </summary>
public class CalendarServiceTests
{
    private readonly Mock<ICalendarRepository> _calendarRepo = new();
    private readonly Mock<ICalendarEventRepository> _eventRepo = new();
    private readonly Mock<ICalendarEventAttachedFileRepository> _eventFileRepo = new();
    private readonly Mock<ICalendarEventReminderRepository> _reminderRepo = new();
    private readonly Mock<ICalendarSharedRepository> _sharedRepo = new();
    private readonly Mock<IOtherCalendarRepository> _otherCalendarRepo = new();
    private readonly Mock<IFileClient> _fileClient = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IAttachmentContentService> _attachmentContent = new();
    private readonly IOptions<ServerSetting> _settings =
        Options.Create(new ServerSetting { FileStorageBaseUrl = "https://files.example.com" });

    /// <summary>Initializes the test fixture, setting up all required test doubles and the system under test.</summary>
    public CalendarServiceTests()
    {
        _unitOfWork.Setup(u => u.BeginAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _unitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _unitOfWork.Setup(u => u.RollbackAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _unitOfWork.Setup(u => u.DisposeAsync()).Returns(ValueTask.CompletedTask);
    }

    private CalendarService CreateSut() => new(
        _calendarRepo.Object,
        _eventRepo.Object,
        _eventFileRepo.Object,
        _reminderRepo.Object,
        _sharedRepo.Object,
        _otherCalendarRepo.Object,
        _fileClient.Object,
        _unitOfWork.Object,
        _attachmentContent.Object,
        _settings,
        NullLogger<CalendarService>.Instance);

    // -- Helpers --------------------------------------------------------------

    private static Calendar MakeCalendar(long id = 1, string name = "MyCalendar", string email = "user@example.com") =>
        Calendar.Reconstitute(
            id: id,
            accountEmail: email,
            name: name,
            description: null,
            timeZoneIanaId: "UTC",
            htmlColorCode: "#3788d8",
            created: DateTime.UtcNow,
            updated: DateTime.UtcNow);

    private static CalendarEvent MakeCalendarEvent(long id = 1, long calendarId = 1, string title = "Event") =>
        CalendarEvent.Reconstitute(
            id: id,
            calendarId: calendarId,
            title: title,
            description: null,
            allDay: false,
            startDate: DateTime.UtcNow,
            endDate: DateTime.UtcNow.AddHours(1),
            startTz: null,
            endTz: null,
            location: null,
            status: null,
            recurrenceId: null,
            created: DateTime.UtcNow,
            updated: DateTime.UtcNow);

    // -- CreateCalendarAsync --------------------------------------------------

    /// <summary>Verifies that <c>CreateCalendarAsync</c> returns fail when calendar with same name exists.</summary>
    [Fact]
    public async Task CreateCalendarAsync_ReturnsFail_WhenCalendarWithSameNameExists()
    {
        _calendarRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([MakeCalendar(name: "MyCalendar")]);
        var sut = CreateSut();

        ServiceResult result = await sut.CreateCalendarAsync("user@example.com", new CalendarRequest
        {
            AccountEmail = "user@example.com",
            Name = "MyCalendar"
        }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains("already exists", result.ErrorKey, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Verifies that <c>CreateCalendarAsync</c> returns success when name is unique.</summary>
    [Fact]
    public async Task CreateCalendarAsync_ReturnsSuccess_WhenNameIsUnique()
    {
        _calendarRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _calendarRepo.Setup(r => r.CreateAsync(It.IsAny<Calendar>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var sut = CreateSut();

        ServiceResult result = await sut.CreateCalendarAsync("user@example.com", new CalendarRequest
        {
            AccountEmail = "user@example.com",
            Name = "NewCalendar",
            TimeZoneIanaId = "UTC",
            HtmlColorCode = "#3788d8"
        }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        _calendarRepo.Verify(r => r.CreateAsync(It.IsAny<Calendar>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Regression: the new calendar's database-generated id is handed back on the request (the aggregate
    /// itself is created with ID 0), because the client builds the new calendar's checkbox / edit / delete
    /// controls from the id the controller echoes — with id 0 they addressed no calendar until a reload.
    /// </summary>
    [Fact]
    public async Task CreateCalendarAsync_HandsTheGeneratedIdBackOnTheRequest()
    {
        _calendarRepo.SetupSequence(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([MakeCalendar(id: 7, name: "Other")])                                      // duplicate check (before)
            .ReturnsAsync([MakeCalendar(id: 7, name: "Other"), MakeCalendar(id: 42, name: "NewCalendar")]); // after the insert
        _calendarRepo.Setup(r => r.CreateAsync(It.IsAny<Calendar>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var sut = CreateSut();
        var request = new CalendarRequest { Name = "NewCalendar", TimeZoneIanaId = "UTC", HtmlColorCode = "#3788d8" };

        ServiceResult result = await sut.CreateCalendarAsync("user@example.com", request, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(42, request.Id);
    }

    /// <summary>
    /// Regression test: the prior name-existence check is a read-then-write race (two concurrent
    /// creates of the same name can both pass it). The "Calendar_AccountEmail_Name_unique"
    /// constraint is what actually closes the race -- verify the resulting unique-violation
    /// exception is turned into the same friendly conflict instead of an unhandled failure.
    /// </summary>
    [Fact]
    public async Task CreateCalendarAsync_ReturnsConflict_WhenConcurrentCreateWinsTheRace()
    {
        _calendarRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var pgException = new Exception("23505: duplicate key value violates unique constraint \"Calendar_AccountEmail_Name_unique\"");
        _calendarRepo.Setup(r => r.CreateAsync(It.IsAny<Calendar>(), It.IsAny<CancellationToken>())).ThrowsAsync(pgException);
        var sut = CreateSut();

        ServiceResult result = await sut.CreateCalendarAsync("user@example.com", new CalendarRequest
        {
            AccountEmail = "user@example.com",
            Name = "NewCalendar",
            TimeZoneIanaId = "UTC",
            HtmlColorCode = "#3788d8"
        }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains("already exists", result.ErrorKey, StringComparison.OrdinalIgnoreCase);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// A unique-violation (23505) on a different constraint than the calendar-name uniqueness one
    /// (e.g. the "Calendar_pk" surrogate-key PK, which can collide if its sequence ever falls behind
    /// the table's existing rows) must not be misreported as "name already exists" -- there is no
    /// name conflict here, and swallowing it that way would hide a real database-integrity problem.
    /// </summary>
    [Fact]
    public async Task CreateCalendarAsync_ReturnsUnexpectedFailure_WhenUniqueViolationIsOnADifferentConstraint()
    {
        _calendarRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var pgException = new Exception("23505: duplicate key value violates unique constraint \"Calendar_pk\"");
        _calendarRepo.Setup(r => r.CreateAsync(It.IsAny<Calendar>(), It.IsAny<CancellationToken>())).ThrowsAsync(pgException);
        var sut = CreateSut();

        ServiceResult result = await sut.CreateCalendarAsync("user@example.com", new CalendarRequest
        {
            AccountEmail = "user@example.com",
            Name = "NewCalendar",
            TimeZoneIanaId = "UTC",
            HtmlColorCode = "#3788d8"
        }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal("Calendar.Unexpected", result.ErrorCode);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // -- UpdateCalendarAsync --------------------------------------------------

    /// <summary>Verifies that <c>UpdateCalendarAsync</c> returns fail when calendar not found.</summary>
    [Fact]
    public async Task UpdateCalendarAsync_ReturnsFail_WhenCalendarNotFound()
    {
        _calendarRepo.Setup(r => r.FindByIdForUpdateAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync((Calendar?)null);
        var sut = CreateSut();

        ServiceResult result = await sut.UpdateCalendarAsync("user@example.com", new CalendarRequest
        {
            AccountEmail = "user@example.com",
            Id = 99,
            Name = "Updated"
        }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
    }

    /// <summary>Verifies that <c>UpdateCalendarAsync</c> returns success when calendar found.</summary>
    [Fact]
    public async Task UpdateCalendarAsync_ReturnsSuccess_WhenCalendarFound()
    {
        _calendarRepo.Setup(r => r.FindByIdForUpdateAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeCalendar(id: 1));
        _calendarRepo.Setup(r => r.UpdateEntityAsync(It.IsAny<Calendar>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var sut = CreateSut();

        ServiceResult result = await sut.UpdateCalendarAsync("user@example.com", new CalendarRequest
        {
            AccountEmail = "user@example.com",
            Id = 1,
            Name = "Updated",
            TimeZoneIanaId = "UTC",
            HtmlColorCode = "#3788d8"
        }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        _calendarRepo.Verify(r => r.UpdateEntityAsync(It.IsAny<Calendar>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Verifies that a calendar owned by a different account is treated as not found, not
    /// updated — <c>FindByIdForUpdateAsync</c> looks up by ID alone, so the service itself must
    /// still enforce ownership before applying the update.</summary>
    [Fact]
    public async Task UpdateCalendarAsync_ReturnsFail_WhenCalendarOwnedByDifferentAccount()
    {
        _calendarRepo.Setup(r => r.FindByIdForUpdateAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeCalendar(id: 1, email: "someone-else@example.com"));
        var sut = CreateSut();

        ServiceResult result = await sut.UpdateCalendarAsync("user@example.com", new CalendarRequest
        {
            AccountEmail = "user@example.com",
            Id = 1,
            Name = "Updated",
            TimeZoneIanaId = "UTC",
            HtmlColorCode = "#3788d8"
        }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        _calendarRepo.Verify(r => r.UpdateEntityAsync(It.IsAny<Calendar>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Verifies that renaming to a name already used by another of the account's calendars is reported as a conflict, not a generic failure.</summary>
    [Fact]
    public async Task UpdateCalendarAsync_ReturnsConflict_WhenRenamedToAnExistingName()
    {
        _calendarRepo.Setup(r => r.FindByIdForUpdateAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeCalendar(id: 1));
        var pgException = new Exception("23505: duplicate key value violates unique constraint \"Calendar_AccountEmail_Name_unique\"");
        _calendarRepo.Setup(r => r.UpdateEntityAsync(It.IsAny<Calendar>(), It.IsAny<CancellationToken>())).ThrowsAsync(pgException);
        var sut = CreateSut();

        ServiceResult result = await sut.UpdateCalendarAsync("user@example.com", new CalendarRequest
        {
            AccountEmail = "user@example.com",
            Id = 1,
            Name = "AlreadyTaken",
            TimeZoneIanaId = "UTC",
            HtmlColorCode = "#3788d8"
        }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains("already exists", result.ErrorKey, StringComparison.OrdinalIgnoreCase);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // -- DeleteCalendarAsync --------------------------------------------------

    /// <summary>Verifies that <c>DeleteCalendarAsync</c> returns fail when calendar not found.</summary>
    [Fact]
    public async Task DeleteCalendarAsync_ReturnsFail_WhenCalendarNotFound()
    {
        _calendarRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var sut = CreateSut();

        ServiceResult result = await sut.DeleteCalendarAsync("user@example.com", new CalendarRequest
        {
            AccountEmail = "user@example.com",
            Id = 99
        }, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
    }

    /// <summary>Verifies that <c>DeleteCalendarAsync</c> returns success when calendar found.</summary>
    [Fact]
    public async Task DeleteCalendarAsync_ReturnsSuccess_WhenCalendarFound()
    {
        _calendarRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([MakeCalendar(id: 1)]);
        _calendarRepo.Setup(r => r.DeleteByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var sut = CreateSut();

        ServiceResult result = await sut.DeleteCalendarAsync("user@example.com", new CalendarRequest
        {
            AccountEmail = "user@example.com",
            Id = 1
        }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
    }

    // -- EnsureDefaultCalendarAsync -------------------------------------------

    /// <summary>Verifies that <c>EnsureDefaultCalendarAsync</c> creates calendar when none exist.</summary>
    [Fact]
    public async Task EnsureDefaultCalendarAsync_CreatesCalendar_WhenNoneExist()
    {
        _calendarRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _calendarRepo.Setup(r => r.CreateAsync(It.IsAny<Calendar>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var sut = CreateSut();

        ServiceResult result = await sut.EnsureDefaultCalendarAsync("user@example.com", new CalendarRequest
        {
            AccountEmail = "user@example.com",
            Name = "Default",
            TimeZoneIanaId = "UTC",
            HtmlColorCode = "#3788d8"
        }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        _calendarRepo.Verify(r => r.CreateAsync(It.IsAny<Calendar>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Verifies that <c>EnsureDefaultCalendarAsync</c> does not create when calendar exists.</summary>
    [Fact]
    public async Task EnsureDefaultCalendarAsync_DoesNotCreate_WhenCalendarExists()
    {
        _calendarRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([MakeCalendar()]);
        var sut = CreateSut();

        ServiceResult result = await sut.EnsureDefaultCalendarAsync("user@example.com", new CalendarRequest
        {
            AccountEmail = "user@example.com",
            Name = "Default"
        }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        _calendarRepo.Verify(r => r.CreateAsync(It.IsAny<Calendar>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Regression test: the prior "any calendars exist" check is a read-then-write race (two
    /// concurrent first-logins can both pass it). This method's contract is "some default calendar
    /// now exists" -- already satisfied once a concurrent request wins the unique-constraint race
    /// -- so the resulting exception must be treated as success, not surfaced as a failure.
    /// </summary>
    [Fact]
    public async Task EnsureDefaultCalendarAsync_ReturnsSuccess_WhenConcurrentRequestWinsTheRace()
    {
        _calendarRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var pgException = new Exception("23505: duplicate key value violates unique constraint \"Calendar_AccountEmail_Name_unique\"");
        _calendarRepo.Setup(r => r.CreateAsync(It.IsAny<Calendar>(), It.IsAny<CancellationToken>())).ThrowsAsync(pgException);
        var sut = CreateSut();

        ServiceResult result = await sut.EnsureDefaultCalendarAsync("user@example.com", new CalendarRequest
        {
            AccountEmail = "user@example.com",
            Name = "Default",
            TimeZoneIanaId = "UTC",
            HtmlColorCode = "#3788d8"
        }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // -- EnsureCalendarSharedAsync --------------------------------------------

    /// <summary>Verifies that <c>EnsureCalendarSharedAsync</c> creates shared when not already shared.</summary>
    [Fact]
    public async Task EnsureCalendarSharedAsync_CreatesShared_WhenNotAlreadyShared()
    {
        _sharedRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _sharedRepo.Setup(r => r.CreateAsync(It.IsAny<CalendarShared>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var sut = CreateSut();

        ServiceResult result = await sut.EnsureCalendarSharedAsync([1L], TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        _sharedRepo.Verify(r => r.CreateAsync(It.Is<CalendarShared>(s => s.Id == 1), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Verifies that <c>EnsureCalendarSharedAsync</c> does not create when already shared.</summary>
    [Fact]
    public async Task EnsureCalendarSharedAsync_DoesNotCreate_WhenAlreadyShared()
    {
        _sharedRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([CalendarShared.Reconstitute(1, false, false)]);
        var sut = CreateSut();

        ServiceResult result = await sut.EnsureCalendarSharedAsync([1L], TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        _sharedRepo.Verify(r => r.CreateAsync(It.IsAny<CalendarShared>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Verifies that <c>EnsureCalendarSharedAsync</c> fetches the shared table once and only
    /// creates rows for the IDs that don't already have one, even across multiple calendars.</summary>
    [Fact]
    public async Task EnsureCalendarSharedAsync_Batch_OnlyCreatesMissingIds_WithSingleFetch()
    {
        _sharedRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([CalendarShared.Reconstitute(2, false, false)]);
        _sharedRepo.Setup(r => r.CreateAsync(It.IsAny<CalendarShared>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var sut = CreateSut();

        ServiceResult result = await sut.EnsureCalendarSharedAsync([1L, 2L, 3L], TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        _sharedRepo.Verify(r => r.GetAllAsync(It.IsAny<CancellationToken>()), Times.Once);
        _sharedRepo.Verify(r => r.CreateAsync(It.Is<CalendarShared>(s => s.Id == 1), It.IsAny<CancellationToken>()), Times.Once);
        _sharedRepo.Verify(r => r.CreateAsync(It.Is<CalendarShared>(s => s.Id == 2), It.IsAny<CancellationToken>()), Times.Never);
        _sharedRepo.Verify(r => r.CreateAsync(It.Is<CalendarShared>(s => s.Id == 3), It.IsAny<CancellationToken>()), Times.Once);
    }

    // -- GetCalendarSharedSummariesAsync ---------------------------------------

    /// <summary>
    /// Verifies that <c>GetCalendarSharedSummariesAsync</c> joins the account's own calendars with
    /// their sharing status and backfills any missing CalendarShared row first.
    /// </summary>
    [Fact]
    public async Task GetCalendarSharedSummariesAsync_ReturnsOwnedCalendars_JoinedWithSharingStatus()
    {
        const string email = "user@example.com";
        _calendarRepo.Setup(r => r.GetByAccountEmailAsync(email, It.IsAny<CancellationToken>()))
            .ReturnsAsync([MakeCalendar(id: 1, name: "Personal", email: email)]);
        _sharedRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([CalendarShared.Reconstitute(1, true, false)]);
        var sut = CreateSut();

        List<CalendarSharedSummaryResponse> result =
            await sut.GetCalendarSharedSummariesAsync(email, TestContext.Current.CancellationToken);

        CalendarSharedSummaryResponse only = Assert.Single(result);
        Assert.Equal(1, only.Id);
        Assert.Equal("Personal", only.Name);
        Assert.True(only.User);
        Assert.False(only.Guest);
    }

    /// <summary>
    /// Verifies that <c>GetCalendarSharedSummariesAsync</c> excludes another account's calendars
    /// even when that calendar has a sharing row — the join is keyed off the caller's own
    /// calendars, not off every CalendarShared row.
    /// </summary>
    [Fact]
    public async Task GetCalendarSharedSummariesAsync_ExcludesCalendarsNotOwnedByAccount()
    {
        const string email = "user@example.com";
        _calendarRepo.Setup(r => r.GetByAccountEmailAsync(email, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _sharedRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([CalendarShared.Reconstitute(99, true, true)]);
        var sut = CreateSut();

        List<CalendarSharedSummaryResponse> result =
            await sut.GetCalendarSharedSummariesAsync(email, TestContext.Current.CancellationToken);

        Assert.Empty(result);
    }

    // -- GetBrowseCalendarsOfInterestAsync -------------------------------------

    /// <summary>
    /// Verifies that <c>GetBrowseCalendarsOfInterestAsync</c> only returns calendars shared with
    /// registered users, and correctly flags one the account is already subscribed to.
    /// </summary>
    [Fact]
    public async Task GetBrowseCalendarsOfInterestAsync_FlagsAlreadySubscribedCalendar()
    {
        const string email = "user@example.com";
        _calendarRepo.Setup(r => r.GetAllOrderedByNameAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([MakeCalendar(id: 1, name: "Shared A"), MakeCalendar(id: 2, name: "Shared B")]);
        _sharedRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([CalendarShared.Reconstitute(1, true, false), CalendarShared.Reconstitute(2, true, false)]);
        _otherCalendarRepo.Setup(r => r.GetByAccountEmailAsync(email, It.IsAny<CancellationToken>()))
            .ReturnsAsync([OtherCalendar.Reconstitute(email, 1)]);
        var sut = CreateSut();

        List<CalendarBrowseSummaryResponse> result =
            await sut.GetBrowseCalendarsOfInterestAsync(email, TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Count);
        Assert.True(result.Single(x => x.Id == 1).Checked);
        Assert.False(result.Single(x => x.Id == 2).Checked);
    }

    /// <summary>
    /// Verifies that <c>GetBrowseCalendarsOfInterestAsync</c> excludes a calendar shared only with
    /// anonymous visitors (User = false), matching the controller's prior <c>.Where(x =&gt; x.User)</c>
    /// pre-filter.
    /// </summary>
    [Fact]
    public async Task GetBrowseCalendarsOfInterestAsync_ExcludesCalendarSharedOnlyWithAnonymousVisitors()
    {
        const string email = "user@example.com";
        _calendarRepo.Setup(r => r.GetAllOrderedByNameAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([MakeCalendar(id: 1, name: "GuestOnly")]);
        _sharedRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([CalendarShared.Reconstitute(1, false, true)]);
        _otherCalendarRepo.Setup(r => r.GetByAccountEmailAsync(email, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var sut = CreateSut();

        List<CalendarBrowseSummaryResponse> result =
            await sut.GetBrowseCalendarsOfInterestAsync(email, TestContext.Current.CancellationToken);

        Assert.Empty(result);
    }

    // -- CreateCalendarEventAsync ---------------------------------------------

    /// <summary>Verifies that <c>CreateCalendarEventAsync</c> returns fail when calendar not found.</summary>
    [Fact]
    public async Task CreateCalendarEventAsync_ReturnsFail_WhenCalendarNotFound()
    {
        _calendarRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var sut = CreateSut();

        ServiceResult result = await sut.CreateCalendarEventAsync(
            new CalendarEventRequest { CalendarId = 1 }, "user@example.com", "1", [], null, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains("does not exist", result.ErrorKey, StringComparison.OrdinalIgnoreCase);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Verifies that <c>CreateCalendarEventAsync</c> returns success with no attachment and no reminders.</summary>
    [Fact]
    public async Task CreateCalendarEventAsync_ReturnsSuccess_WithNoAttachmentAndNoReminders()
    {
        _calendarRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([MakeCalendar(id: 1)]);
        _eventRepo.Setup(r => r.CreateCalendarEventAsync(It.IsAny<CalendarEvent>(), It.IsAny<CancellationToken>())).ReturnsAsync(1L);
        var sut = CreateSut();

        ServiceResult result = await sut.CreateCalendarEventAsync(
            new CalendarEventRequest { CalendarId = 1, Title = "Meeting" }, "user@example.com", "1", [], null, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Verifies that <c>CreateCalendarEventAsync</c> creates the attachment record when the file upload succeeds.</summary>
    [Fact]
    public async Task CreateCalendarEventAsync_CreatesAttachment_WhenFileUploadSucceeds()
    {
        _calendarRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([MakeCalendar(id: 1)]);
        _eventRepo.Setup(r => r.CreateCalendarEventAsync(It.IsAny<CalendarEvent>(), It.IsAny<CancellationToken>())).ReturnsAsync(1L);
        _fileClient.Setup(f => f.UploadAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var sut = CreateSut();
        var attachedFile = new AttachedFileDto { Bytes = [1, 2, 3], ContentType = "application/zip", FileName = "a.zip", Size = 3 };

        ServiceResult result = await sut.CreateCalendarEventAsync(
            new CalendarEventRequest { CalendarId = 1, Title = "Meeting" }, "user@example.com", "1", [], attachedFile, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        _eventFileRepo.Verify(r => r.CreateAsync(It.IsAny<CalendarEventAttachedFile>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Verifies that <c>CreateCalendarEventAsync</c> rolls back and fails, rather than creating a dangling record, when the file upload fails.</summary>
    [Fact]
    public async Task CreateCalendarEventAsync_RollsBackAndFails_WhenFileUploadFails()
    {
        _calendarRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([MakeCalendar(id: 1)]);
        _eventRepo.Setup(r => r.CreateCalendarEventAsync(It.IsAny<CalendarEvent>(), It.IsAny<CancellationToken>())).ReturnsAsync(1L);
        _fileClient.Setup(f => f.UploadAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var sut = CreateSut();
        var attachedFile = new AttachedFileDto { Bytes = [1, 2, 3], ContentType = "application/zip", FileName = "a.zip", Size = 3 };

        ServiceResult result = await sut.CreateCalendarEventAsync(
            new CalendarEventRequest { CalendarId = 1, Title = "Meeting" }, "user@example.com", "1", [], attachedFile, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        _eventFileRepo.Verify(r => r.CreateAsync(It.IsAny<CalendarEventAttachedFile>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Regression: an attachment record the domain rejects (a file name over 255 characters) is caught
    /// BEFORE the file is uploaded, so no orphaned file is left in storage.
    /// </summary>
    [Fact]
    public async Task CreateCalendarEventAsync_RejectsAnOverlongFileName_BeforeUploadingAnything()
    {
        _calendarRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([MakeCalendar(id: 1)]);
        _eventRepo.Setup(r => r.CreateCalendarEventAsync(It.IsAny<CalendarEvent>(), It.IsAny<CancellationToken>())).ReturnsAsync(1L);
        var sut = CreateSut();
        var attachedFile = new AttachedFileDto { Bytes = [1, 2, 3], ContentType = "application/zip", FileName = new string('n', 256) + ".zip", Size = 3 };

        ServiceResult result = await sut.CreateCalendarEventAsync(
            new CalendarEventRequest { CalendarId = 1, Title = "Meeting" }, "user@example.com", "1", [], attachedFile, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal("CalendarEventAttachedFile.NameTooLong", result.ErrorCode);
        _fileClient.Verify(f => f.UploadAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Verifies that <c>CreateCalendarEventAsync</c> creates reminders when provided.</summary>
    [Fact]
    public async Task CreateCalendarEventAsync_CreatesReminders_WhenProvided()
    {
        _calendarRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([MakeCalendar(id: 1)]);
        _eventRepo.Setup(r => r.CreateCalendarEventAsync(It.IsAny<CalendarEvent>(), It.IsAny<CancellationToken>())).ReturnsAsync(1L);
        _reminderRepo.Setup(r => r.CreateAsync(It.IsAny<CalendarEventReminder>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var sut = CreateSut();

        var reminders = new List<CalendarReminderDto>
        {
            new() { Method = "Email", MinutesBeforeEvent = 30 },
            new() { Method = "Notification", MinutesBeforeEvent = 10 }
        };

        ServiceResult result = await sut.CreateCalendarEventAsync(
            new CalendarEventRequest { CalendarId = 1, Title = "Meeting" }, "user@example.com", "1", reminders, null, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        _reminderRepo.Verify(r => r.CreateAsync(It.IsAny<CalendarEventReminder>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    /// <summary>Verifies that <c>CreateCalendarEventAsync</c> rolls back and fails, rather than silently
    /// dropping the reminder, when a reminder's lead time is outside the range allowed by CalendarReminderPolicy.</summary>
    [Fact]
    public async Task CreateCalendarEventAsync_RollsBackAndFails_WhenReminderLeadTimeOutOfRange()
    {
        _calendarRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([MakeCalendar(id: 1)]);
        _eventRepo.Setup(r => r.CreateCalendarEventAsync(It.IsAny<CalendarEvent>(), It.IsAny<CancellationToken>())).ReturnsAsync(1L);
        _reminderRepo.Setup(r => r.CreateAsync(It.IsAny<CalendarEventReminder>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var sut = CreateSut();

        var reminders = new List<CalendarReminderDto>
        {
            new() { Method = "Email", MinutesBeforeEvent = 30 },
            new() { Method = "Email", DaysBeforeEvent = 29 } // exceeds the 28-day ceiling
        };

        ServiceResult result = await sut.CreateCalendarEventAsync(
            new CalendarEventRequest { CalendarId = 1, Title = "Meeting" }, "user@example.com", "1", reminders, null, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains("range", result.ErrorKey, StringComparison.OrdinalIgnoreCase);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // -- GetCalendarEventsAsync (batch) ----------------------------------------

    /// <summary>Verifies that the batch overload fetches events in a single repository call and groups them by calendar ID.</summary>
    [Fact]
    public async Task GetCalendarEventsAsync_Batch_GroupsEventsByCalendarId()
    {
        _eventRepo.Setup(r => r.GetByCalendarIdsAsync(It.IsAny<IEnumerable<long>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                MakeCalendarEvent(id: 1, calendarId: 1, title: "A"),
                MakeCalendarEvent(id: 2, calendarId: 1, title: "B"),
                MakeCalendarEvent(id: 3, calendarId: 2, title: "C")
            ]);
        var sut = CreateSut();

        IReadOnlyDictionary<long, List<CalendarEventResponse>> result = await sut.GetCalendarEventsAsync([1L, 2L], TestContext.Current.CancellationToken);

        Assert.Equal(2, result[1L].Count);
        Assert.Single(result[2L]);
        _eventRepo.Verify(r => r.GetByCalendarIdsAsync(It.IsAny<IEnumerable<long>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Verifies that the batch overload returns no entry for a calendar ID the repository finds no events for.</summary>
    [Fact]
    public async Task GetCalendarEventsAsync_Batch_ReturnsEmptyLookup_WhenNoEventsFound()
    {
        _eventRepo.Setup(r => r.GetByCalendarIdsAsync(It.IsAny<IEnumerable<long>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var sut = CreateSut();

        IReadOnlyDictionary<long, List<CalendarEventResponse>> result = await sut.GetCalendarEventsAsync([1L], TestContext.Current.CancellationToken);

        Assert.Empty(result.GetValueOrDefault(1L, []));
    }

    // -- UpdateCalendarEventAsync ---------------------------------------------

    /// <summary>Verifies that <c>UpdateCalendarEventAsync</c> returns fail when calendar not found.</summary>
    [Fact]
    public async Task UpdateCalendarEventAsync_ReturnsFail_WhenCalendarNotFound()
    {
        _calendarRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var sut = CreateSut();

        ServiceResult result = await sut.UpdateCalendarEventAsync(
            new CalendarEventRequest { Id = 1, CalendarId = 1 }, "user@example.com", "1", [], null, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Verifies that <c>UpdateCalendarEventAsync</c> returns fail when event not found.</summary>
    [Fact]
    public async Task UpdateCalendarEventAsync_ReturnsFail_WhenEventNotFound()
    {
        _calendarRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([MakeCalendar(id: 1)]);
        _eventRepo.Setup(r => r.FindByIdForUpdateAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync((CalendarEvent?)null);
        var sut = CreateSut();

        ServiceResult result = await sut.UpdateCalendarEventAsync(
            new CalendarEventRequest { Id = 1, CalendarId = 1 }, "user@example.com", "1", [], null, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Verifies that <c>UpdateCalendarEventAsync</c> returns success when valid and no attachment.</summary>
    [Fact]
    public async Task UpdateCalendarEventAsync_ReturnsSuccess_WhenValidAndNoAttachment()
    {
        _calendarRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([MakeCalendar(id: 1)]);
        _eventRepo.Setup(r => r.FindByIdForUpdateAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeCalendarEvent(id: 1, calendarId: 1, title: "Old"));
        _eventRepo.Setup(r => r.UpdateEntityAsync(It.IsAny<CalendarEvent>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _eventFileRepo.Setup(r => r.DeleteByCalendarEventIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _reminderRepo.Setup(r => r.DeleteByCalendarEventIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var sut = CreateSut();

        ServiceResult result = await sut.UpdateCalendarEventAsync(
            new CalendarEventRequest { Id = 1, CalendarId = 1, Title = "New Title" }, "user@example.com", "1", [], null, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Verifies that <c>UpdateCalendarEventAsync</c> creates the attachment record when the file upload succeeds.</summary>
    [Fact]
    public async Task UpdateCalendarEventAsync_CreatesAttachment_WhenFileUploadSucceeds()
    {
        _calendarRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([MakeCalendar(id: 1)]);
        _eventRepo.Setup(r => r.FindByIdForUpdateAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeCalendarEvent(id: 1, calendarId: 1, title: "Old"));
        _eventRepo.Setup(r => r.UpdateEntityAsync(It.IsAny<CalendarEvent>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _eventFileRepo.Setup(r => r.DeleteByCalendarEventIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _reminderRepo.Setup(r => r.DeleteByCalendarEventIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _fileClient.Setup(f => f.UploadAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var sut = CreateSut();
        var attachedFile = new AttachedFileDto { Bytes = [1, 2, 3], ContentType = "application/zip", FileName = "a.zip", Size = 3 };

        ServiceResult result = await sut.UpdateCalendarEventAsync(
            new CalendarEventRequest { Id = 1, CalendarId = 1, Title = "New Title" }, "user@example.com", "1", [], attachedFile, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        _eventFileRepo.Verify(r => r.CreateAsync(It.IsAny<CalendarEventAttachedFile>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Verifies that <c>UpdateCalendarEventAsync</c> rolls back and fails, rather than creating a dangling record, when the file upload fails.</summary>
    [Fact]
    public async Task UpdateCalendarEventAsync_RollsBackAndFails_WhenFileUploadFails()
    {
        _calendarRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([MakeCalendar(id: 1)]);
        _eventRepo.Setup(r => r.FindByIdForUpdateAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeCalendarEvent(id: 1, calendarId: 1, title: "Old"));
        _eventRepo.Setup(r => r.UpdateEntityAsync(It.IsAny<CalendarEvent>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _eventFileRepo.Setup(r => r.DeleteByCalendarEventIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _reminderRepo.Setup(r => r.DeleteByCalendarEventIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _fileClient.Setup(f => f.UploadAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var sut = CreateSut();
        var attachedFile = new AttachedFileDto { Bytes = [1, 2, 3], ContentType = "application/zip", FileName = "a.zip", Size = 3 };

        ServiceResult result = await sut.UpdateCalendarEventAsync(
            new CalendarEventRequest { Id = 1, CalendarId = 1, Title = "New Title" }, "user@example.com", "1", [], attachedFile, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        _eventFileRepo.Verify(r => r.CreateAsync(It.IsAny<CalendarEventAttachedFile>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Verifies that <c>UpdateCalendarEventAsync</c> rolls back and fails, rather than silently
    /// dropping the reminder, when a reminder's lead time is outside the range allowed by CalendarReminderPolicy.</summary>
    [Fact]
    public async Task UpdateCalendarEventAsync_RollsBackAndFails_WhenReminderLeadTimeOutOfRange()
    {
        _calendarRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([MakeCalendar(id: 1)]);
        _eventRepo.Setup(r => r.FindByIdForUpdateAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeCalendarEvent(id: 1, calendarId: 1, title: "Old"));
        _eventRepo.Setup(r => r.UpdateEntityAsync(It.IsAny<CalendarEvent>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _eventFileRepo.Setup(r => r.DeleteByCalendarEventIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _reminderRepo.Setup(r => r.DeleteByCalendarEventIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var sut = CreateSut();

        var reminders = new List<CalendarReminderDto>
        {
            new() { Method = "Email", WeeksBeforeEvent = 5 } // exceeds the 4-week ceiling
        };

        ServiceResult result = await sut.UpdateCalendarEventAsync(
            new CalendarEventRequest { Id = 1, CalendarId = 1, Title = "New Title" }, "user@example.com", "1", reminders, null, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains("range", result.ErrorKey, StringComparison.OrdinalIgnoreCase);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Verifies that <c>UpdateCalendarEventAsync</c> returns fail and does not mutate when the
    /// existing event belongs to a calendar the caller does not own (IDOR guard).</summary>
    [Fact]
    public async Task UpdateCalendarEventAsync_ReturnsFail_WhenExistingEventBelongsToDifferentCalendar()
    {
        _calendarRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([MakeCalendar(id: 1)]);
        // Caller owns calendar 1 and passes CalendarId = 1, but the target event currently lives in
        // calendar 999 (another user's). The update must be rejected.
        _eventRepo.Setup(r => r.FindByIdForUpdateAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeCalendarEvent(id: 1, calendarId: 999, title: "Victim"));
        var sut = CreateSut();

        ServiceResult result = await sut.UpdateCalendarEventAsync(
            new CalendarEventRequest { Id = 1, CalendarId = 1, Title = "Hijacked" }, "user@example.com", "1", [], null, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        _eventRepo.Verify(r => r.UpdateEntityAsync(It.IsAny<CalendarEvent>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // -- DeleteCalendarEventAsync ---------------------------------------------

    /// <summary>Verifies that <c>DeleteCalendarEventAsync</c> returns fail when event not found.</summary>
    [Fact]
    public async Task DeleteCalendarEventAsync_ReturnsFail_WhenEventNotFound()
    {
        _calendarRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([MakeCalendar(id: 1)]);
        _eventRepo.Setup(r => r.FindByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync((CalendarEvent?)null);
        var sut = CreateSut();

        ServiceResult result = await sut.DeleteCalendarEventAsync(99, "user@example.com", TestContext.Current.CancellationToken);

        Assert.False(result.Success);
    }

    /// <summary>Verifies that <c>DeleteCalendarEventAsync</c> returns fail when event belongs to different calendar.</summary>
    [Fact]
    public async Task DeleteCalendarEventAsync_ReturnsFail_WhenEventBelongsToDifferentCalendar()
    {
        _calendarRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([MakeCalendar(id: 1)]);
        _eventRepo.Setup(r => r.FindByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeCalendarEvent(id: 1, calendarId: 999)); // different calendar
        var sut = CreateSut();

        ServiceResult result = await sut.DeleteCalendarEventAsync(1, "user@example.com", TestContext.Current.CancellationToken);

        Assert.False(result.Success);
    }

    /// <summary>Verifies that <c>DeleteCalendarEventAsync</c> returns success when ownership verified.</summary>
    [Fact]
    public async Task DeleteCalendarEventAsync_ReturnsSuccess_WhenOwnershipVerified()
    {
        _calendarRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([MakeCalendar(id: 1)]);
        _eventRepo.Setup(r => r.FindByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeCalendarEvent(id: 1, calendarId: 1));
        _eventRepo.Setup(r => r.DeleteByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var sut = CreateSut();

        ServiceResult result = await sut.DeleteCalendarEventAsync(1, "user@example.com", TestContext.Current.CancellationToken);

        Assert.True(result.Success);
    }

    // -- UpdateOtherCalendarAsync ---------------------------------------------

    /// <summary>Verifies that <c>UpdateOtherCalendarAsync</c> clears existing and creates new subscriptions for calendars shared to Users.</summary>
    [Fact]
    public async Task UpdateOtherCalendarAsync_ClearsExistingAndCreatesNew()
    {
        const string email = "user@example.com";
        _sharedRepo.Setup(r => r.GetByIdsForUpdateAsync(It.IsAny<IEnumerable<long>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([CalendarShared.Reconstitute(20, user: true, anonymous: false)]);
        _otherCalendarRepo.Setup(r => r.DeleteByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _otherCalendarRepo.Setup(r => r.CreateAsync(It.IsAny<OtherCalendar>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var sut = CreateSut();

        var newEntries = new List<OtherCalendarRequest> { new() { CalendarId = 20 } };
        ServiceResult result = await sut.UpdateOtherCalendarAsync(email, newEntries, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        _otherCalendarRepo.Verify(r => r.DeleteByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        _otherCalendarRepo.Verify(r => r.CreateAsync(It.IsAny<OtherCalendar>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Verifies the IDOR fix: subscribing to a calendar that was never marked shared to Users
    /// (<see cref="CalendarShared.User"/>) is rejected instead of silently creating the subscription,
    /// which previously let any account grant itself read access to any calendar's events by
    /// supplying an arbitrary <c>CalendarId</c>.
    /// </summary>
    [Fact]
    public async Task UpdateOtherCalendarAsync_RejectsSubscriptionToCalendarNotSharedToUsers()
    {
        const string email = "user@example.com";
        _sharedRepo.Setup(r => r.GetByIdsForUpdateAsync(It.IsAny<IEnumerable<long>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([CalendarShared.Reconstitute(20, user: false, anonymous: false)]);
        _otherCalendarRepo.Setup(r => r.DeleteByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var sut = CreateSut();

        var newEntries = new List<OtherCalendarRequest> { new() { CalendarId = 20 } };
        ServiceResult result = await sut.UpdateOtherCalendarAsync(email, newEntries, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        _otherCalendarRepo.Verify(r => r.CreateAsync(It.IsAny<OtherCalendar>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // -- UpdateCalendarSharedAsync ---------------------------------------------

    /// <summary>
    /// Verifies the ownership fix: submitting a <see cref="CalendarSharedRequest"/> for a calendar
    /// not owned by <paramref name="email"/> is rejected instead of silently updating it, which
    /// previously let one admin change another admin's calendar's sharing (public/anonymous
    /// visibility) by supplying an arbitrary <c>CalendarId</c>.
    /// </summary>
    [Fact]
    public async Task UpdateCalendarSharedAsync_RejectsCalendarNotOwnedByCaller()
    {
        const string email = "admin-a@example.com";
        _calendarRepo.Setup(r => r.GetByAccountEmailAsync(email, It.IsAny<CancellationToken>()))
            .ReturnsAsync([MakeCalendar(id: 1, email: email)]);
        var sut = CreateSut();

        // CalendarId 99 belongs to a different account (not returned by GetByAccountEmailAsync above).
        var requests = new List<CalendarSharedRequest> { new() { CalendarId = 99, User = true, Anonymous = true } };
        ServiceResult result = await sut.UpdateCalendarSharedAsync(email, requests, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        _sharedRepo.Verify(r => r.UpdateEntityAsync(It.IsAny<CalendarShared>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.BeginAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Verifies that updating the sharing settings of a calendar the caller owns succeeds and, when a
    /// <c>CalendarShared</c> row already exists for it, goes through the update path.
    /// </summary>
    [Fact]
    public async Task UpdateCalendarSharedAsync_Succeeds_WhenCallerOwnsCalendar()
    {
        const string email = "admin-a@example.com";
        _calendarRepo.Setup(r => r.GetByAccountEmailAsync(email, It.IsAny<CancellationToken>()))
            .ReturnsAsync([MakeCalendar(id: 1, email: email)]);
        _sharedRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([CalendarShared.Reconstitute(1, false, false)]);
        _sharedRepo.Setup(r => r.UpdateEntityAsync(It.IsAny<CalendarShared>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _sharedRepo.Setup(r => r.CreateAsync(It.IsAny<CalendarShared>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _otherCalendarRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var sut = CreateSut();

        var requests = new List<CalendarSharedRequest> { new() { CalendarId = 1, User = true, Anonymous = false } };
        ServiceResult result = await sut.UpdateCalendarSharedAsync(email, requests, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        _sharedRepo.Verify(r => r.UpdateEntityAsync(It.IsAny<CalendarShared>(), It.IsAny<CancellationToken>()), Times.Once);
        _sharedRepo.Verify(r => r.CreateAsync(It.IsAny<CalendarShared>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Verifies that sharing a calendar the caller owns for which no <c>CalendarShared</c> row exists
    /// yet inserts one instead of failing (UpdateEntityAsync throws on a missing row).
    /// </summary>
    [Fact]
    public async Task UpdateCalendarSharedAsync_InsertsSharedRow_WhenNoneExistsYet()
    {
        const string email = "admin-a@example.com";
        _calendarRepo.Setup(r => r.GetByAccountEmailAsync(email, It.IsAny<CancellationToken>()))
            .ReturnsAsync([MakeCalendar(id: 1, email: email)]);
        _sharedRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _sharedRepo.Setup(r => r.UpdateEntityAsync(It.IsAny<CalendarShared>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _sharedRepo.Setup(r => r.CreateAsync(It.IsAny<CalendarShared>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _otherCalendarRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var sut = CreateSut();

        var requests = new List<CalendarSharedRequest> { new() { CalendarId = 1, User = true, Anonymous = false } };
        ServiceResult result = await sut.UpdateCalendarSharedAsync(email, requests, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        _sharedRepo.Verify(r => r.CreateAsync(It.Is<CalendarShared>(s => s.Id == 1 && s.User), It.IsAny<CancellationToken>()), Times.Once);
        _sharedRepo.Verify(r => r.UpdateEntityAsync(It.IsAny<CalendarShared>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // -- Read-time sharing filters ---------------------------------------------

    /// <summary>
    /// A subscription to a calendar that has since been turned private must stop showing up at read time,
    /// even if the cleanup that should have deleted the stale row never ran.
    /// </summary>
    [Fact]
    public async Task GetOtherCalendarsAsync_DropsSubscriptionsToCalendarsThatAreNoLongerSharedToUsers()
    {
        _otherCalendarRepo.Setup(r => r.GetByAccountEmailAsync("user@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync([OtherCalendar.Reconstitute("user@example.com", 1), OtherCalendar.Reconstitute("user@example.com", 2)]);
        _sharedRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([CalendarShared.Reconstitute(1, user: true, anonymous: false), CalendarShared.Reconstitute(2, user: false, anonymous: true)]);

        List<OtherCalendarResponse> result = await CreateSut().GetOtherCalendarsAsync("user@example.com", TestContext.Current.CancellationToken);

        Assert.Equal([1L], result.Select(r => r.CalendarId));
    }

    /// <summary>A signed-in visitor sees exactly the calendars they subscribed to (and are still shared), sorted by name.</summary>
    [Fact]
    public async Task GetVisibleOtherCalendarsAsync_ForASignedInAccount_ReturnsSubscribedCalendarsSortedByName()
    {
        _calendarRepo.Setup(r => r.GetAllOrderedByNameAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([MakeCalendar(1, "Zeta"), MakeCalendar(2, "Alpha"), MakeCalendar(3, "Unsubscribed")]);
        _otherCalendarRepo.Setup(r => r.GetByAccountEmailAsync("user@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync([OtherCalendar.Reconstitute("user@example.com", 1), OtherCalendar.Reconstitute("user@example.com", 2)]);
        _sharedRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([CalendarShared.Reconstitute(1, true, false), CalendarShared.Reconstitute(2, true, false), CalendarShared.Reconstitute(3, true, false)]);

        List<CalendarResponse> result = await CreateSut().GetVisibleOtherCalendarsAsync(
            new AccountResponse { Email = "user@example.com" }, TestContext.Current.CancellationToken);

        Assert.Equal(["Alpha", "Zeta"], result.Select(c => c.Name));
    }

    /// <summary>An anonymous visitor sees only calendars an admin shared with guests, sorted by name.</summary>
    [Fact]
    public async Task GetVisibleOtherCalendarsAsync_ForAnAnonymousVisitor_ReturnsOnlyGuestSharedCalendars()
    {
        _calendarRepo.Setup(r => r.GetAllOrderedByNameAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([MakeCalendar(1, "Zeta"), MakeCalendar(2, "Alpha"), MakeCalendar(3, "UsersOnly")]);
        _sharedRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([CalendarShared.Reconstitute(1, false, true), CalendarShared.Reconstitute(2, false, true), CalendarShared.Reconstitute(3, true, false)]);

        List<CalendarResponse> result = await CreateSut().GetVisibleOtherCalendarsAsync(null, TestContext.Current.CancellationToken);

        Assert.Equal(["Alpha", "Zeta"], result.Select(c => c.Name));
        _otherCalendarRepo.Verify(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // -- EnsureCalendarSharedAsync: races and failures ---------------------------

    private static Exception SharedPkViolation() =>
        new("23505: duplicate key value violates unique constraint \"CalendarShared_pk\"");

    /// <summary>The first attempt loses the CalendarShared PK race; the retry (in a fresh transaction) succeeds.</summary>
    [Fact]
    public async Task EnsureCalendarSharedAsync_RetriesInAFreshTransaction_AfterLosingThePrimaryKeyRace()
    {
        _sharedRepo.SetupSequence(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([])
            .ReturnsAsync([CalendarShared.Reconstitute(5, false, false)]);
        _sharedRepo.SetupSequence(r => r.CreateAsync(It.IsAny<CalendarShared>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(SharedPkViolation());

        ServiceResult result = await CreateSut().EnsureCalendarSharedAsync([5], TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        _unitOfWork.Verify(u => u.BeginAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>A persistent PK collision gives up after the retry limit instead of looping forever.</summary>
    [Fact]
    public async Task EnsureCalendarSharedAsync_GivesUp_WhenThePrimaryKeyRaceNeverResolves()
    {
        _sharedRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _sharedRepo.Setup(r => r.CreateAsync(It.IsAny<CalendarShared>(), It.IsAny<CancellationToken>())).ThrowsAsync(SharedPkViolation());

        ServiceResult result = await CreateSut().EnsureCalendarSharedAsync([5], TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal("Calendar.Unexpected", result.ErrorCode);
        _unitOfWork.Verify(u => u.BeginAsync(It.IsAny<CancellationToken>()), Times.Exactly(3));
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Exactly(3));
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Any other failure is not retried: it rolls back once and reports the generic failure.</summary>
    [Fact]
    public async Task EnsureCalendarSharedAsync_DoesNotRetry_WhenTheFailureIsNotThePrimaryKeyRace()
    {
        _sharedRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("db down"));

        ServiceResult result = await CreateSut().EnsureCalendarSharedAsync([5], TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal("Calendar.Unexpected", result.ErrorCode);
        _unitOfWork.Verify(u => u.BeginAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // -- Calendar CRUD: validation and unexpected failures -----------------------

    /// <summary>A domain validation error (bad color) is returned as-is and nothing is persisted.</summary>
    [Fact]
    public async Task CreateCalendarAsync_ReturnsTheDomainValidationError_AndPersistsNothing()
    {
        _calendarRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);

        ServiceResult result = await CreateSut().CreateCalendarAsync("user@example.com",
            new CalendarRequest { Name = "Work", TimeZoneIanaId = "UTC", HtmlColorCode = "not-a-colour" }, TestContext.Current.CancellationToken);

        Assert.Equal("HtmlColorCode.Invalid", result.ErrorCode);
        _calendarRepo.Verify(r => r.CreateAsync(It.IsAny<Calendar>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>A domain validation error on update (blank name) is returned and the row is not written.</summary>
    [Fact]
    public async Task UpdateCalendarAsync_ReturnsTheDomainValidationError_AndDoesNotWrite()
    {
        _calendarRepo.Setup(r => r.FindByIdForUpdateAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(MakeCalendar());

        ServiceResult result = await CreateSut().UpdateCalendarAsync("user@example.com",
            new CalendarRequest { Id = 1, Name = "   ", TimeZoneIanaId = "UTC", HtmlColorCode = "#3788d8" }, TestContext.Current.CancellationToken);

        Assert.Equal("Calendar.NameEmpty", result.ErrorCode);
        _calendarRepo.Verify(r => r.UpdateEntityAsync(It.IsAny<Calendar>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>An unexpected repository failure on update is rolled back and reported generically (no internals leaked).</summary>
    [Fact]
    public async Task UpdateCalendarAsync_RollsBackAndReportsUnexpectedFailure_WhenTheRepositoryThrows()
    {
        _calendarRepo.Setup(r => r.FindByIdForUpdateAsync(1, It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("secret detail"));

        ServiceResult result = await CreateSut().UpdateCalendarAsync("user@example.com",
            new CalendarRequest { Id = 1, Name = "Work", TimeZoneIanaId = "UTC", HtmlColorCode = "#3788d8" }, TestContext.Current.CancellationToken);

        Assert.Equal("Calendar.Unexpected", result.ErrorCode);
        Assert.DoesNotContain("secret detail", result.ErrorKey);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>An unexpected repository failure on delete is rolled back and reported generically.</summary>
    [Fact]
    public async Task DeleteCalendarAsync_RollsBackAndReportsUnexpectedFailure_WhenTheRepositoryThrows()
    {
        _calendarRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([MakeCalendar()]);
        _calendarRepo.Setup(r => r.DeleteByIdAsync(1, It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("db down"));

        ServiceResult result = await CreateSut().DeleteCalendarAsync("user@example.com", new CalendarRequest { Id = 1 }, TestContext.Current.CancellationToken);

        Assert.Equal("Calendar.Unexpected", result.ErrorCode);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// First-visit provisioning must never leave the grid empty: an unknown / missing time zone on the
    /// account falls back to UTC instead of failing the auto-created default calendar.
    /// </summary>
    [Theory]
    [InlineData("Not/AZone")]
    [InlineData(null)]
    public async Task EnsureDefaultCalendarAsync_FallsBackToUtc_WhenTheAccountTimeZoneIsUnusable(string? timeZone)
    {
        _calendarRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        Calendar? created = null;
        _calendarRepo.Setup(r => r.CreateAsync(It.IsAny<Calendar>(), It.IsAny<CancellationToken>()))
            .Callback<Calendar, CancellationToken>((c, _) => created = c).Returns(Task.CompletedTask);

        ServiceResult result = await CreateSut().EnsureDefaultCalendarAsync("user@example.com",
            new CalendarRequest { Name = "Nick", TimeZoneIanaId = timeZone, HtmlColorCode = "#3788d8" }, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal("UTC", created?.TimeZone.Value);
    }

    /// <summary>A failure other than the name race while provisioning the default calendar is rolled back and reported.</summary>
    [Fact]
    public async Task EnsureDefaultCalendarAsync_ReportsUnexpectedFailure_WhenTheRepositoryThrows()
    {
        _calendarRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db down"));

        ServiceResult result = await CreateSut().EnsureDefaultCalendarAsync("user@example.com",
            new CalendarRequest { Name = "Nick", TimeZoneIanaId = "UTC", HtmlColorCode = "#3788d8" }, TestContext.Current.CancellationToken);

        Assert.Equal("Calendar.Unexpected", result.ErrorCode);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>A default-calendar name the domain rejects (e.g. a nickname with angle brackets) returns the validation error, not an exception.</summary>
    [Fact]
    public async Task EnsureDefaultCalendarAsync_ReturnsTheDomainValidationError_WhenTheNameIsRejected()
    {
        _calendarRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);

        ServiceResult result = await CreateSut().EnsureDefaultCalendarAsync("user@example.com",
            new CalendarRequest { Name = "a<b", TimeZoneIanaId = "UTC", HtmlColorCode = "#3788d8" }, TestContext.Current.CancellationToken);

        Assert.Equal("Calendar.NameInvalid", result.ErrorCode);
        _calendarRepo.Verify(r => r.CreateAsync(It.IsAny<Calendar>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // -- UpdateOtherCalendarAsync / UpdateCalendarSharedAsync: failures ----------

    /// <summary>A failure while re-creating subscriptions is rolled back (the delete-all must not survive) and reported.</summary>
    [Fact]
    public async Task UpdateOtherCalendarAsync_RollsBackAndFails_WhenTheRepositoryThrows()
    {
        _sharedRepo.Setup(r => r.GetByIdsForUpdateAsync(It.IsAny<IEnumerable<long>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([CalendarShared.Reconstitute(20, true, false)]);
        _otherCalendarRepo.Setup(r => r.CreateAsync(It.IsAny<OtherCalendar>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db down"));

        ServiceResult result = await CreateSut().UpdateOtherCalendarAsync("user@example.com",
            [new OtherCalendarRequest { CalendarId = 20 }], TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal("Calendar.UpdateOtherCalendarsFailed", result.ErrorCode);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Turning a calendar private must remove EVERY account's subscription to it (one batched delete per
    /// unshared calendar), otherwise the leftovers silently reactivate when it is shared again.
    /// </summary>
    [Fact]
    public async Task UpdateCalendarSharedAsync_RemovesAllSubscriptions_OnlyForCalendarsNoLongerSharedToUsers()
    {
        const string email = "admin@example.com";
        _calendarRepo.Setup(r => r.GetByAccountEmailAsync(email, It.IsAny<CancellationToken>()))
            .ReturnsAsync([MakeCalendar(1, "A", email), MakeCalendar(2, "B", email)]);
        _sharedRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([CalendarShared.Reconstitute(1, true, false), CalendarShared.Reconstitute(2, true, false)]);

        ServiceResult result = await CreateSut().UpdateCalendarSharedAsync(email,
        [
            new CalendarSharedRequest { CalendarId = 1, User = true, Anonymous = false },
            new CalendarSharedRequest { CalendarId = 2, User = false, Anonymous = true },
        ], TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        _otherCalendarRepo.Verify(r => r.DeleteByCalendarIdAsync(2, It.IsAny<CancellationToken>()), Times.Once);
        _otherCalendarRepo.Verify(r => r.DeleteByCalendarIdAsync(1, It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>A lost CalendarShared PK race on the upsert is retried and the second attempt applies the same values.</summary>
    [Fact]
    public async Task UpdateCalendarSharedAsync_RetriesAfterLosingThePrimaryKeyRace()
    {
        const string email = "admin@example.com";
        _calendarRepo.Setup(r => r.GetByAccountEmailAsync(email, It.IsAny<CancellationToken>())).ReturnsAsync([MakeCalendar(1, "A", email)]);
        _sharedRepo.SetupSequence(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([])
            .ReturnsAsync([CalendarShared.Reconstitute(1, false, false)]);
        _sharedRepo.Setup(r => r.CreateAsync(It.IsAny<CalendarShared>(), It.IsAny<CancellationToken>())).ThrowsAsync(SharedPkViolation());

        ServiceResult result = await CreateSut().UpdateCalendarSharedAsync(email,
            [new CalendarSharedRequest { CalendarId = 1, User = true, Anonymous = false }], TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        _sharedRepo.Verify(r => r.UpdateEntityAsync(It.Is<CalendarShared>(s => s.Id == 1 && s.User), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.BeginAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    /// <summary>A persistent PK race gives up after the retry limit with the shared-settings failure code.</summary>
    [Fact]
    public async Task UpdateCalendarSharedAsync_GivesUp_WhenThePrimaryKeyRaceNeverResolves()
    {
        const string email = "admin@example.com";
        _calendarRepo.Setup(r => r.GetByAccountEmailAsync(email, It.IsAny<CancellationToken>())).ReturnsAsync([MakeCalendar(1, "A", email)]);
        _sharedRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _sharedRepo.Setup(r => r.CreateAsync(It.IsAny<CalendarShared>(), It.IsAny<CancellationToken>())).ThrowsAsync(SharedPkViolation());

        ServiceResult result = await CreateSut().UpdateCalendarSharedAsync(email,
            [new CalendarSharedRequest { CalendarId = 1, User = true, Anonymous = false }], TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal("Calendar.UpdateSharedFailed", result.ErrorCode);
        _unitOfWork.Verify(u => u.BeginAsync(It.IsAny<CancellationToken>()), Times.Exactly(3));
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Any other failure is not retried: one rollback, then the shared-settings failure code.</summary>
    [Fact]
    public async Task UpdateCalendarSharedAsync_DoesNotRetry_WhenTheFailureIsNotThePrimaryKeyRace()
    {
        const string email = "admin@example.com";
        _calendarRepo.Setup(r => r.GetByAccountEmailAsync(email, It.IsAny<CancellationToken>())).ReturnsAsync([MakeCalendar(1, "A", email)]);
        _sharedRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("db down"));

        ServiceResult result = await CreateSut().UpdateCalendarSharedAsync(email,
            [new CalendarSharedRequest { CalendarId = 1, User = true, Anonymous = false }], TestContext.Current.CancellationToken);

        Assert.Equal("Calendar.UpdateSharedFailed", result.ErrorCode);
        _unitOfWork.Verify(u => u.BeginAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // -- Calendar events: validation and unexpected failures ----------------------

    /// <summary>A blank event title is returned as the domain validation error; nothing is stored.</summary>
    [Fact]
    public async Task CreateCalendarEventAsync_ReturnsTheDomainValidationError_WhenTheTitleIsBlank()
    {
        _calendarRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([MakeCalendar()]);

        ServiceResult result = await CreateSut().CreateCalendarEventAsync(
            new CalendarEventRequest { CalendarId = 1, Title = "" }, "user@example.com", "1", [], null, TestContext.Current.CancellationToken);

        Assert.Equal("CalendarEvent.TitleEmpty", result.ErrorCode);
        _eventRepo.Verify(r => r.CreateCalendarEventAsync(It.IsAny<CalendarEvent>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>An unexpected repository failure while creating an event is rolled back and reported without leaking internals.</summary>
    [Fact]
    public async Task CreateCalendarEventAsync_RollsBackAndReportsCreateFailed_WhenTheRepositoryThrows()
    {
        _calendarRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([MakeCalendar()]);
        _eventRepo.Setup(r => r.CreateCalendarEventAsync(It.IsAny<CalendarEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("secret detail"));

        ServiceResult result = await CreateSut().CreateCalendarEventAsync(
            new CalendarEventRequest { CalendarId = 1, Title = "Meeting" }, "user@example.com", "1", [], null, TestContext.Current.CancellationToken);

        Assert.Equal("CalendarEvent.CreateFailed", result.ErrorCode);
        Assert.DoesNotContain("secret detail", result.ErrorKey);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>A blank title on update is returned as the domain validation error and the event row is not written.</summary>
    [Fact]
    public async Task UpdateCalendarEventAsync_ReturnsTheDomainValidationError_WhenTheTitleIsBlank()
    {
        _calendarRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([MakeCalendar()]);
        _eventRepo.Setup(r => r.FindByIdForUpdateAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(MakeCalendarEvent(1, 1, "Old"));

        ServiceResult result = await CreateSut().UpdateCalendarEventAsync(
            new CalendarEventRequest { Id = 1, CalendarId = 1, Title = "" }, "user@example.com", "1", [], null, TestContext.Current.CancellationToken);

        Assert.Equal("CalendarEvent.TitleEmpty", result.ErrorCode);
        _eventRepo.Verify(r => r.UpdateEntityAsync(It.IsAny<CalendarEvent>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>An unexpected repository failure while updating an event is rolled back and reported without leaking internals.</summary>
    [Fact]
    public async Task UpdateCalendarEventAsync_RollsBackAndReportsUpdateFailed_WhenTheRepositoryThrows()
    {
        _calendarRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([MakeCalendar()]);
        _eventRepo.Setup(r => r.FindByIdForUpdateAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(MakeCalendarEvent(1, 1, "Old"));
        _eventRepo.Setup(r => r.UpdateEntityAsync(It.IsAny<CalendarEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("secret detail"));

        ServiceResult result = await CreateSut().UpdateCalendarEventAsync(
            new CalendarEventRequest { Id = 1, CalendarId = 1, Title = "New" }, "user@example.com", "1", [], null, TestContext.Current.CancellationToken);

        Assert.Equal("CalendarEvent.UpdateFailed", result.ErrorCode);
        Assert.DoesNotContain("secret detail", result.ErrorKey);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>An account with no calendar at all cannot delete an event (and the lookup never runs).</summary>
    [Fact]
    public async Task DeleteCalendarEventAsync_ReturnsNotFound_WhenTheAccountHasNoCalendar()
    {
        _calendarRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);

        ServiceResult result = await CreateSut().DeleteCalendarEventAsync(1, "user@example.com", TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        _eventRepo.Verify(r => r.FindByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>An unexpected repository failure while deleting an event is rolled back and reported generically.</summary>
    [Fact]
    public async Task DeleteCalendarEventAsync_RollsBackAndReportsUnexpectedFailure_WhenTheRepositoryThrows()
    {
        _calendarRepo.Setup(r => r.GetByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([MakeCalendar()]);
        _eventRepo.Setup(r => r.FindByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(MakeCalendarEvent());
        _eventRepo.Setup(r => r.DeleteByIdAsync(1, It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("db down"));

        ServiceResult result = await CreateSut().DeleteCalendarEventAsync(1, "user@example.com", TestContext.Current.CancellationToken);

        Assert.Equal("Calendar.Unexpected", result.ErrorCode);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // -- Attachment-content delegation --------------------------------------------

    /// <summary>The Summernote image upload is delegated to the shared attachment service under the "Calendar" area.</summary>
    [Fact]
    public async Task UploadSummernoteImageAsync_DelegatesToTheAttachmentServiceUnderTheCalendarArea()
    {
        var file = new AttachedFileDto { Bytes = [1], ContentType = "image/png", FileName = "a.png", Size = 1 };
        var expected = SummernoteUploadResult.Fail("nope");
        _attachmentContent.Setup(a => a.UploadSummernoteImageAsync(file, "Calendar", "7", It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        SummernoteUploadResult actual = await CreateSut().UploadSummernoteImageAsync(file, "7", TestContext.Current.CancellationToken);

        Assert.Same(expected, actual);
    }

    /// <summary>The file download is delegated to the shared attachment service.</summary>
    [Fact]
    public async Task DownloadFileAsync_DelegatesToTheAttachmentService()
    {
        _attachmentContent.Setup(a => a.DownloadFileAsync("/upload/x.png", It.IsAny<CancellationToken>())).ReturnsAsync([
            .. "\t\t"u8
        ]);

        byte[]? bytes = await CreateSut().DownloadFileAsync("/upload/x.png", TestContext.Current.CancellationToken);

        Assert.Equal("\t\t"u8.ToArray(), bytes);
    }

    /// <summary>A shared-calendar row that cannot become a subscription (a non-positive calendar id) is refused, and the transaction rolls back.</summary>
    [Fact]
    public async Task UpdateOtherCalendarAsync_ReturnsTheDomainRefusal_ForAnInvalidCalendarId()
    {
        _sharedRepo.Setup(r => r.GetByIdsForUpdateAsync(It.IsAny<IEnumerable<long>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([CalendarShared.Reconstitute(0, user: true, anonymous: false)]);
        _otherCalendarRepo.Setup(r => r.DeleteByAccountEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var sut = CreateSut();

        ServiceResult result = await sut.UpdateOtherCalendarAsync("user@example.com", [new OtherCalendarRequest { CalendarId = 0 }], TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal("OtherCalendar.InvalidCalendarId", result.ErrorCode);
        _unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        _otherCalendarRepo.Verify(r => r.CreateAsync(It.IsAny<OtherCalendar>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
