using Maroik.Core.Domain.Calendar;
using Maroik.Core.Repository.Repositories;
using Microsoft.EntityFrameworkCore;
using OrmCalendarEvent = Maroik.Core.PostgreSQL.Models.CalendarEvent;

namespace Maroik.Core.Repository.Tests.Repositories;

/// <summary>
/// Tests for <see cref="CalendarEventRepository"/> against a real PostgreSQL database
/// (Testcontainers) preloaded with the <c>Init.sql</c> schema and seed data
/// (~1,880 events). <c>CalendarEvent</c> has the FK <c>CalendarEvent_fk_0</c> to <c>Calendar.ID</c>
/// (unenforced by InMemory) plus the checks <c>CalendarEvent_Status_check</c> (Busy/Free),
/// <c>CalendarEvent_EndDate_check</c> (<c>EndDate &gt;= StartDate</c>) and
/// <c>CalendarEvent_Title_check</c>. Every test creates a real parent calendar and scopes to its id.
/// </summary>
public sealed class CalendarEventRepositoryTests(DatabaseFixture database) : RepositoryTestBase(database)
{
    private CalendarEventRepository Sut => new(Context);

    private OrmCalendarEvent MakeEvent(long calendarId, string? title = null) => new()
    {
        CalendarId = calendarId,
        Title = title ?? Unique("Meeting"),
        AllDay = false,
        StartDate = DateTime.UtcNow,
        EndDate = DateTime.UtcNow.AddHours(1),
        Status = "Busy",
        Created = DateTime.UtcNow,
        Updated = DateTime.UtcNow
    };

    private async Task SeedAsync(params OrmCalendarEvent[] events)
    {
        await Context.CalendarEvents.AddRangeAsync(events);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();
    }

    // -- GetByCalendarIdsAsync --------------------------------------------------

    /// <summary>Verifies that <c>GetByCalendarIdsAsync</c> returns events for every requested calendar in one query.</summary>
    [Fact]
    public async Task GetByCalendarIdsAsync_ReturnsEventsForAllRequestedCalendars()
    {
        long cal1 = await InsertCalendarAsync();
        long cal2 = await InsertCalendarAsync();
        long cal3 = await InsertCalendarAsync();
        await SeedAsync(MakeEvent(cal1, "Event A"), MakeEvent(cal2, "Event B"), MakeEvent(cal3, "Not Requested"));

        List<CalendarEvent> result = await Sut.GetByCalendarIdsAsync([cal1, cal2], TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, e => e.CalendarId == cal1);
        Assert.Contains(result, e => e.CalendarId == cal2);
        Assert.DoesNotContain(result, e => e.CalendarId == cal3);
    }

    /// <summary>Verifies that <c>GetByCalendarIdsAsync</c> returns empty when no calendar ids match.</summary>
    [Fact]
    public async Task GetByCalendarIdsAsync_ReturnsEmpty_WhenNoCalendarIdsMatch()
    {
        List<CalendarEvent> result = await Sut.GetByCalendarIdsAsync([long.MaxValue - 1, long.MaxValue], TestContext.Current.CancellationToken);

        Assert.Empty(result);
    }

    /// <summary>Verifies that <c>GetByCalendarIdsAsync</c> returns empty for an empty id collection.</summary>
    [Fact]
    public async Task GetByCalendarIdsAsync_ReturnsEmpty_WhenCalendarIdsEmpty()
    {
        List<CalendarEvent> result = await Sut.GetByCalendarIdsAsync([], TestContext.Current.CancellationToken);

        Assert.Empty(result);
    }

    // -- FindByIdAsync ------------------------------------------------------

    /// <summary>Verifies that <c>FindByIdAsync</c> returns the event when found.</summary>
    [Fact]
    public async Task FindByIdAsync_ReturnsEvent_WhenFound()
    {
        long cal = await InsertCalendarAsync();
        var orm = MakeEvent(cal, "Team Standup");
        await SeedAsync(orm);

        CalendarEvent? result = await Sut.FindByIdAsync(orm.Id, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal("Team Standup", result.Title);
    }

    /// <summary>Verifies that <c>FindByIdAsync</c> returns null when not found.</summary>
    [Fact]
    public async Task FindByIdAsync_ReturnsNull_WhenNotFound()
    {
        CalendarEvent? result = await Sut.FindByIdAsync(long.MaxValue, TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    // -- CreateCalendarEventAsync ---------------------------------------------

    /// <summary>Verifies that <c>CreateCalendarEventAsync</c> inserts an event and returns its DB-generated id.</summary>
    [Fact]
    public async Task CreateCalendarEventAsync_PersistsEvent()
    {
        long cal = await InsertCalendarAsync();

        var calendarEvent = CalendarEvent.Reconstitute(
            id: 0, calendarId: cal, title: Unique("New Event"), description: null, allDay: true,
            startDate: DateTime.UtcNow.Date, endDate: DateTime.UtcNow.Date.AddDays(1),
            startTz: null, endTz: null, location: null, status: "Busy", recurrenceId: null,
            created: DateTime.UtcNow, updated: DateTime.UtcNow);

        long id = await Sut.CreateCalendarEventAsync(calendarEvent, TestContext.Current.CancellationToken);
        Context.ChangeTracker.Clear();

        Assert.True(id > 0);
        OrmCalendarEvent? saved = await Context.CalendarEvents.FirstOrDefaultAsync(e => e.Id == id, TestContext.Current.CancellationToken);
        Assert.NotNull(saved);
        Assert.True(saved.AllDay);
    }

    /// <summary>
    /// The 255-character location limit the domain enforces is exactly what the real column holds: a
    /// location on the limit persists and round-trips.
    /// </summary>
    [Fact]
    public async Task CreateCalendarEventAsync_PersistsALocationOnTheColumnLimit()
    {
        long cal = await InsertCalendarAsync();
        string location = new('l', 255);
        var calendarEvent = CalendarEvent.Create(
            cal, Unique("Located"), null, false, DateTime.UtcNow, DateTime.UtcNow.AddHours(1), null, null, location, "Busy").Value;

        long id = await Sut.CreateCalendarEventAsync(calendarEvent, TestContext.Current.CancellationToken);
        Context.ChangeTracker.Clear();

        OrmCalendarEvent? saved = await Context.CalendarEvents.FirstOrDefaultAsync(e => e.Id == id, TestContext.Current.CancellationToken);
        Assert.NotNull(saved);
        Assert.Equal(255, saved.Location!.Length);
    }

    /// <summary>
    /// Regression: the domain accepts a null OR whitespace status as "not specified". Both must persist
    /// as the "Busy" default — a whitespace value used to reach <c>CalendarEvent_Status_check</c> and fail.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateCalendarEventAsync_PersistsABlankStatus_AsBusy(string? blankStatus)
    {
        long cal = await InsertCalendarAsync();
        var calendarEvent = CalendarEvent.Create(
            cal, Unique("BlankStatus"), null, false, DateTime.UtcNow, DateTime.UtcNow.AddHours(1), null, null, null, blankStatus).Value;

        long id = await Sut.CreateCalendarEventAsync(calendarEvent, TestContext.Current.CancellationToken);
        Context.ChangeTracker.Clear();

        OrmCalendarEvent? saved = await Context.CalendarEvents.FirstOrDefaultAsync(e => e.Id == id, TestContext.Current.CancellationToken);
        Assert.NotNull(saved);
        Assert.Equal("Busy", saved.Status);
    }

    /// <summary>PostgreSQL enforces <c>CalendarEvent_EndDate_check</c> — an end before the start throws (InMemory did not).</summary>
    [Fact]
    public async Task CreateCalendarEventAsync_Throws_WhenEndBeforeStart()
    {
        long cal = await InsertCalendarAsync();

        var calendarEvent = CalendarEvent.Reconstitute(
            id: 0, calendarId: cal, title: Unique("Backwards"), description: null, allDay: false,
            startDate: DateTime.UtcNow, endDate: DateTime.UtcNow.AddHours(-1),
            startTz: null, endTz: null, location: null, status: "Busy", recurrenceId: null,
            created: DateTime.UtcNow, updated: DateTime.UtcNow);

        await Assert.ThrowsAsync<DbUpdateException>(
            () => Sut.CreateCalendarEventAsync(calendarEvent, TestContext.Current.CancellationToken));
    }

    // -- UpdateEntityAsync ---------------------------------------------

    /// <summary>Verifies that <c>UpdateEntityAsync</c> persists changes to an existing event.</summary>
    [Fact]
    public async Task UpdateEntityAsync_UpdatesExistingEvent()
    {
        long cal = await InsertCalendarAsync();
        var orm = MakeEvent(cal, "Old Title");
        await SeedAsync(orm);

        var calendarEvent = CalendarEvent.Reconstitute(
            id: orm.Id, calendarId: cal, title: "Updated Title", description: null, allDay: false,
            startDate: DateTime.UtcNow, endDate: DateTime.UtcNow.AddHours(2),
            startTz: null, endTz: null, location: null, status: "Free", recurrenceId: null,
            created: orm.Created, updated: DateTime.UtcNow);

        await Sut.UpdateEntityAsync(calendarEvent, TestContext.Current.CancellationToken);
        Context.ChangeTracker.Clear();

        OrmCalendarEvent? updated = await Context.CalendarEvents.FirstOrDefaultAsync(e => e.Id == orm.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(updated);
        Assert.Equal("Updated Title", updated.Title);
        Assert.Equal("Free", updated.Status);
    }

    // -- DeleteByIdAsync ---------------------------------------------

    /// <summary>Verifies that <c>DeleteByIdAsync</c> removes the event row.</summary>
    [Fact]
    public async Task DeleteByIdAsync_RemovesEvent()
    {
        long cal = await InsertCalendarAsync();
        var orm = MakeEvent(cal, "To Be Deleted");
        await SeedAsync(orm);

        await Sut.DeleteByIdAsync(orm.Id, TestContext.Current.CancellationToken);
        Context.ChangeTracker.Clear();

        Assert.False(await Context.CalendarEvents.AnyAsync(e => e.Id == orm.Id, TestContext.Current.CancellationToken));
    }
}
