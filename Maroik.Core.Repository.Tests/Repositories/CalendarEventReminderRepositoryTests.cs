using Maroik.Core.Domain.Calendar;
using Maroik.Core.Repository.Repositories;
using Microsoft.EntityFrameworkCore;
using OrmCalendarEventReminder = Maroik.Core.PostgreSQL.Models.CalendarEventReminder;

namespace Maroik.Core.Repository.Tests.Repositories;

/// <summary>
/// Tests for <see cref="CalendarEventReminderRepository"/> against a real PostgreSQL database
/// (Testcontainers) preloaded with the <c>Init.sql</c> schema. <c>CalendarEventReminder</c>
/// carries the FK <c>CalendarEventReminder_fk_0</c> to <c>CalendarEvent.ID</c> and several checks the
/// EF Core InMemory provider ignored: <c>Method</c> must be <c>Email</c>/<c>Notification</c>, and
/// <c>CalendarEventReminder_BeforeEvent_check</c> requires exactly one of
/// Minutes/Hours/Days/Weeks-before to be set. Every test creates a real parent event.
/// </summary>
public sealed class CalendarEventReminderRepositoryTests(DatabaseFixture database) : RepositoryTestBase(database)
{
    private CalendarEventReminderRepository Sut => new(Context);

    /// <summary>An unsaved reminder row for event <paramref name="calendarEventId"/>.</summary>
    private static OrmCalendarEventReminder MakeReminder(long calendarEventId, string method = "Email", long minutesBefore = 30) => new()
    {
        CalendarEventId = calendarEventId,
        Method = method,
        MinutesBeforeEvent = minutesBefore
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
            MakeReminder(event1, "Email", 15),
            MakeReminder(event1, "Notification", 60),
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

    /// <summary>Verifies that <c>CreateAsync</c> inserts a reminder row.</summary>
    [Fact]
    public async Task CreateAsync_PersistsReminder()
    {
        long eventId = await InsertCalendarEventAsync();

        var reminder = CalendarEventReminder.Reconstitute(
            id: 0, calendarEventId: eventId, method: "Notification",
            minutesBefore: null, hoursBefore: 2, daysBefore: null, weeksBefore: null, timesBefore: null);

        await Sut.CreateAsync(reminder, TestContext.Current.CancellationToken);
        Context.ChangeTracker.Clear();

        OrmCalendarEventReminder? saved = await Context.CalendarEventReminders
            .FirstOrDefaultAsync(r => r.CalendarEventId == eventId, TestContext.Current.CancellationToken);
        Assert.NotNull(saved);
        Assert.Equal("Notification", saved.Method);
        Assert.Equal(2, saved.HoursBeforeEvent);
    }

    /// <summary>PostgreSQL enforces <c>CalendarEventReminder_Method_check</c> — an unknown method throws (InMemory did not).</summary>
    [Fact]
    public async Task CreateAsync_Throws_WhenMethodIsNotAllowed()
    {
        long eventId = await InsertCalendarEventAsync();

        var reminder = CalendarEventReminder.Reconstitute(
            id: 0, calendarEventId: eventId, method: "Push",
            minutesBefore: 10, hoursBefore: null, daysBefore: null, weeksBefore: null, timesBefore: null);

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
            minutesBefore: 10, hoursBefore: 1, daysBefore: null, weeksBefore: null, timesBefore: null);

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
            MakeReminder(removeEvent, "Email", 10),
            MakeReminder(removeEvent, "Notification", 20),
            MakeReminder(keepEvent));

        await Sut.DeleteByCalendarEventIdAsync(removeEvent, TestContext.Current.CancellationToken);
        Context.ChangeTracker.Clear();

        Assert.False(await Context.CalendarEventReminders.AnyAsync(r => r.CalendarEventId == removeEvent, TestContext.Current.CancellationToken));
        Assert.True(await Context.CalendarEventReminders.AnyAsync(r => r.CalendarEventId == keepEvent, TestContext.Current.CancellationToken));
    }
}
