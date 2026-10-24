using Maroik.Core.Domain.Calendars;
using Maroik.Core.Repository.Repositories;
using Microsoft.EntityFrameworkCore;
using OrmCalendarEventReminder = Maroik.Core.PostgreSQL.Models.CalendarEventReminder;

namespace Maroik.Core.Repository.Tests.Repositories;

/// <summary>
/// Tests for <see cref="CalendarEventReminderRepository"/> against a real PostgreSQL database
/// (Testcontainers) preloaded with the <c>Init.sql</c> schema. <c>CalendarEventReminder</c>
/// carries the FK <c>CalendarEventReminder_fk_0</c> to <c>CalendarEvent.ID</c> and several checks the
/// EF Core InMemory provider ignored: <c>Method</c> must be <c>Email</c>, and
/// <c>CalendarEventReminder_BeforeEvent_check</c> requires exactly one of
/// Minutes/Hours/Days/Weeks-before to be set. Every test creates a real parent event.
/// </summary>
public sealed class CalendarEventReminderRepositoryTests(DatabaseFixture database) : RepositoryTestBase(database)
{
    private CalendarEventReminderRepository Sut => new(Context);

    /// <summary>An unsaved reminder row for event <paramref name="calendarEventId"/>.</summary>
    private static OrmCalendarEventReminder MakeReminder(long calendarEventId, long minutesBefore = 30, DateTime? fireAtUtc = null,
        DateTime? handledAt = null) => new()
    {
        CalendarEventId = calendarEventId,
        Method = "Email",
        MinutesBeforeEvent = minutesBefore,
        FireAtUtc = fireAtUtc ?? new DateTime(2030, 1, 1),
        HandledAt = handledAt
    };

    /// <summary>Inserts <paramref name="reminders"/>, saves, and clears the change tracker so later reads hit the database.</summary>
    private async Task SeedAsync(params OrmCalendarEventReminder[] reminders)
    {
        await Context.CalendarEventReminders.AddRangeAsync(reminders);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();
    }

    // -- GetByCalendarEventIdAsync ---------------------------------------------

    /// <summary>Verifies that <c>GetByCalendarEventIdAsync</c> returns only reminders for the given event.</summary>
    [Fact]
    public async Task GetByCalendarEventIdAsync_ReturnsOnlyRemindersForGivenEvent()
    {
        long event1 = await InsertCalendarEventAsync();
        long event2 = await InsertCalendarEventAsync();
        await SeedAsync(
            MakeReminder(event1, 15),
            MakeReminder(event1, 60),
            MakeReminder(event2));

        List<CalendarEventReminder> result = await Sut.GetByCalendarEventIdAsync(event1, TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Count);
        Assert.All(result, r => Assert.Equal(event1, r.CalendarEventId));
    }

    /// <summary>Verifies that <c>GetByCalendarEventIdAsync</c> returns empty when the event has no reminders.</summary>
    [Fact]
    public async Task GetByCalendarEventIdAsync_ReturnsEmpty_WhenNoRemindersForEvent()
    {
        List<CalendarEventReminder> result = await Sut.GetByCalendarEventIdAsync(long.MaxValue, TestContext.Current.CancellationToken);

        Assert.Empty(result);
    }

    // -- CreateAsync --------------------------------------------------------------

    /// <summary>Verifies that <c>CreateAsync</c> inserts a reminder row with its fire time and handled time.</summary>
    [Fact]
    public async Task CreateAsync_PersistsReminder()
    {
        long eventId = await InsertCalendarEventAsync();
        DateTime fire = new(2030, 3, 4, 5, 6, 0, DateTimeKind.Utc), handled = new(2030, 3, 4, 5, 7, 0, DateTimeKind.Utc);

        var reminder = CalendarEventReminder.Reconstitute(
            id: 0, calendarEventId: eventId, method: "Email",
            minutesBefore: null, hoursBefore: 2, daysBefore: null, weeksBefore: null, timesBefore: null, fireAtUtc: fire, handledAt: handled);

        await Sut.CreateAsync(reminder, TestContext.Current.CancellationToken);
        Context.ChangeTracker.Clear();

        OrmCalendarEventReminder? saved = await Context.CalendarEventReminders
            .FirstOrDefaultAsync(r => r.CalendarEventId == eventId, TestContext.Current.CancellationToken);
        Assert.NotNull(saved);
        Assert.Equal(("Email", (long?)2, fire, (DateTime?)handled), (saved.Method, saved.HoursBeforeEvent, saved.FireAtUtc, saved.HandledAt));
    }

    /// <summary>PostgreSQL enforces <c>CalendarEventReminder_Method_check</c> — a reminder is an e-mail; anything else throws.</summary>
    [Theory]
    [InlineData("Notification")]
    [InlineData("Push")]
    public async Task CreateAsync_Throws_WhenMethodIsNotAllowed(string method)
    {
        long eventId = await InsertCalendarEventAsync();

        var reminder = CalendarEventReminder.Reconstitute(
            id: 0, calendarEventId: eventId, method: method,
            minutesBefore: 10, hoursBefore: null, daysBefore: null, weeksBefore: null, timesBefore: null,
            fireAtUtc: new DateTime(2030, 1, 1), handledAt: null);

        await Assert.ThrowsAsync<DbUpdateException>(
            () => Sut.CreateAsync(reminder, TestContext.Current.CancellationToken));
    }

    /// <summary>PostgreSQL enforces <c>CalendarEventReminder_BeforeEvent_check</c> — more than one lead-time throws (InMemory did not).</summary>
    [Fact]
    public async Task CreateAsync_Throws_WhenMoreThanOneLeadTimeSet()
    {
        long eventId = await InsertCalendarEventAsync();

        var reminder = CalendarEventReminder.Reconstitute(
            id: 0, calendarEventId: eventId, method: "Email",
            minutesBefore: 10, hoursBefore: 1, daysBefore: null, weeksBefore: null, timesBefore: null,
            fireAtUtc: new DateTime(2030, 1, 1), handledAt: null);

        await Assert.ThrowsAsync<DbUpdateException>(
            () => Sut.CreateAsync(reminder, TestContext.Current.CancellationToken));
    }

    // -- DeleteByCalendarEventIdAsync -------------------------------------------

    /// <summary>Verifies that <c>DeleteByCalendarEventIdAsync</c> removes only the target event's reminders.</summary>
    [Fact]
    public async Task DeleteByCalendarEventIdAsync_RemovesRemindersByEventId()
    {
        long keepEvent = await InsertCalendarEventAsync();
        long removeEvent = await InsertCalendarEventAsync();
        await SeedAsync(
            MakeReminder(removeEvent, 10),
            MakeReminder(removeEvent, 20),
            MakeReminder(keepEvent));

        await Sut.DeleteByCalendarEventIdAsync(removeEvent, TestContext.Current.CancellationToken);
        Context.ChangeTracker.Clear();

        Assert.False(await Context.CalendarEventReminders.AnyAsync(r => r.CalendarEventId == removeEvent, TestContext.Current.CancellationToken));
        Assert.True(await Context.CalendarEventReminders.AnyAsync(r => r.CalendarEventId == keepEvent, TestContext.Current.CancellationToken));
    }

    // -- LockDueAsync / handling ----------------------------------------------------

    /// <summary>
    /// Marks every waiting reminder of this test class's database handled, so a queue test sees only its own rows (the class's
    /// tests share one database, and the queue is global).
    /// </summary>
    private async Task EmptyTheQueueAsync() =>
        await Context.CalendarEventReminders.Where(r => r.HandledAt == null)
            .ExecuteUpdateAsync(u => u.SetProperty(r => r.HandledAt, new DateTime(1970, 1, 1)), TestContext.Current.CancellationToken);

    /// <summary>
    /// <c>LockDueAsync</c> returns the unhandled reminders due by <c>now</c>, oldest first, up to the limit; one due later or already
    /// handled is not returned.
    /// </summary>
    [Fact]
    public async Task LockDueAsync_ReturnsTheUnhandledDueReminders_OldestFirst()
    {
        long eventId = await InsertCalendarEventAsync();
        DateTime now = new(2001, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        await EmptyTheQueueAsync();
        await SeedAsync(
            MakeReminder(eventId, 1, now),
            MakeReminder(eventId, 2, now.AddMinutes(-5)),
            MakeReminder(eventId, 3, now.AddSeconds(1)),
            MakeReminder(eventId, 4, now.AddMinutes(-9), handledAt: now),
            MakeReminder(eventId, 5, now.AddMinutes(-1)));

        await using var tx = await Context.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);
        List<CalendarEventReminder> due = await Sut.LockDueAsync(now, 100, TestContext.Current.CancellationToken);
        List<CalendarEventReminder> firstTwo = await Sut.LockDueAsync(now, 2, TestContext.Current.CancellationToken);

        Assert.Equal([2L, 5L, 1L], due.Select(r => r.MinutesBeforeEvent!.Value));
        Assert.Equal([2L, 5L], firstTwo.Select(r => r.MinutesBeforeEvent!.Value));
    }

    /// <summary>A due reminder another transaction holds is skipped (<c>SKIP LOCKED</c>), so two workers never take the same one.</summary>
    [Fact]
    public async Task LockDueAsync_SkipsTheRemindersAnotherTransactionHolds()
    {
        long eventId = await InsertCalendarEventAsync();
        DateTime now = new(2001, 7, 1, 12, 0, 0, DateTimeKind.Utc);
        await EmptyTheQueueAsync();
        await SeedAsync(MakeReminder(eventId, 11, now.AddMinutes(-2)), MakeReminder(eventId, 12, now.AddMinutes(-1)));
        var otherContext = NewDbContext();
        var other = new CalendarEventReminderRepository(otherContext);

        await using var otherTx = await otherContext.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);
        List<CalendarEventReminder> heldByOther = await other.LockDueAsync(now, 1, TestContext.Current.CancellationToken);
        await using var tx = await Context.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);
        List<CalendarEventReminder> mine = await Sut.LockDueAsync(now, 100, TestContext.Current.CancellationToken);

        Assert.Equal([11L], heldByOther.Select(r => r.MinutesBeforeEvent!.Value));
        Assert.Equal([12L], mine.Select(r => r.MinutesBeforeEvent!.Value));
    }

    /// <summary>A reminder marked handled is saved with its handled time and is no longer due.</summary>
    [Fact]
    public async Task MarkingAReminderHandled_IsSaved_AndTakesItOutOfTheDueList()
    {
        long eventId = await InsertCalendarEventAsync();
        DateTime now = new(2001, 8, 1, 12, 0, 0, DateTimeKind.Utc);
        await SeedAsync(MakeReminder(eventId, 21, now.AddMinutes(-1)));
        CalendarEventReminder reminder = (await Sut.LockDueAsync(now, 100, TestContext.Current.CancellationToken)).Single(r => r.CalendarEventId == eventId);

        reminder.MarkHandled(now);
        await Sut.UpdateEntityAsync(reminder, TestContext.Current.CancellationToken);
        Context.ChangeTracker.Clear();

        Assert.Equal(now, (await Context.CalendarEventReminders.SingleAsync(r => r.Id == reminder.Id, TestContext.Current.CancellationToken)).HandledAt);
        Assert.DoesNotContain(await Sut.LockDueAsync(now, 100, TestContext.Current.CancellationToken), r => r.Id == reminder.Id);
    }
}
