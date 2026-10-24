using Maroik.Core.Domain.Calendars;
using Maroik.Core.Repository.Repositories;
using Microsoft.EntityFrameworkCore;
using OrmCalendar = Maroik.Core.PostgreSQL.Models.Calendar;
using OrmCalendarShared = Maroik.Core.PostgreSQL.Models.CalendarShared;
using OrmOtherCalendar = Maroik.Core.PostgreSQL.Models.OtherCalendar;

namespace Maroik.Core.Repository.Tests.Repositories;

/// <summary>
/// Tests for <see cref="CalendarRepository"/> against a real PostgreSQL database (Testcontainers)
/// preloaded with the <c>Init.sql</c> schema and seed data (the demo account's calendar). <c>Calendar</c>
/// has the FK <c>Calendar_fk_0</c> to <c>Account.Email</c> and the checks
/// <c>Calendar_HtmlColorCode_check</c> (<c>^#[0-9A-Fa-f]{6}$</c>) and <c>Calendar_Name_check</c>.
/// The sharing queries also see the seed rows, so their assertions are scoped to the calendars this test created.
/// </summary>
public sealed class CalendarRepositoryTests(DatabaseFixture database) : RepositoryTestBase(database)
{
    private CalendarRepository Sut => new(Context);

    /// <summary>An unsaved calendar row owned by <paramref name="email"/>.</summary>
    private static OrmCalendar MakeCalendar(string email, string name, string color = "#FF5733") => new()
    {
        AccountEmail = email,
        Name = name,
        Description = "Test calendar",
        TimeZoneIanaId = "UTC",
        HtmlColorCode = color,
        Created = DateTime.UtcNow,
        Updated = DateTime.UtcNow
    };

    /// <summary>Ensures each owner account exists, then inserts <paramref name="calendars"/>, saves, and clears the change tracker.</summary>
    private async Task SeedAsync(params OrmCalendar[] calendars)
    {
        await EnsureAccountsAsync([.. calendars.Select(c => c.AccountEmail!)]);
        await Context.Calendars.AddRangeAsync(calendars);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();
    }

    // -- Shared and subscribed calendars ------------------------------------------

    /// <summary>Inserts a calendar owned by a fresh account with the given sharing row (none when both flags are null) and returns its id.</summary>
    private async Task<long> SharedCalendarAsync(bool? user, bool? anonymous)
    {
        long id = await InsertCalendarAsync();
        if (user is { } u && anonymous is { } a)
        {
            await Context.CalendarShareds.AddAsync(new OrmCalendarShared { CalendarId = id, User = u, Anonymous = a });
            await Context.SaveChangesAsync();
            Context.ChangeTracker.Clear();
        }
        return id;
    }

    /// <summary>Subscribes <paramref name="email"/> to each of <paramref name="calendarIds"/>.</summary>
    private async Task SubscribeAsync(string email, params long[] calendarIds)
    {
        await EnsureAccountsAsync(email);
        await Context.OtherCalendars.AddRangeAsync(calendarIds.Select(id => new OrmOtherCalendar { AccountEmail = email, CalendarId = id }));
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();
    }

    /// <summary>Only the calendars shared with guests (CalendarShared.Anonymous) come back — not the user-only, private or unconfigured ones.</summary>
    [Fact]
    public async Task GetSharedWithGuestsAsync_ReturnsOnlyCalendarsSharedWithGuests()
    {
        long guests = await SharedCalendarAsync(user: false, anonymous: true);
        long both = await SharedCalendarAsync(user: true, anonymous: true);
        long usersOnly = await SharedCalendarAsync(user: true, anonymous: false);
        long privateOne = await SharedCalendarAsync(user: false, anonymous: false);
        long unconfigured = await SharedCalendarAsync(user: null, anonymous: null);

        HashSet<long> ids = [.. (await Sut.GetSharedWithGuestsAsync(TestContext.Current.CancellationToken)).Select(c => c.Id)];

        Assert.Contains(guests, ids);
        Assert.Contains(both, ids);
        Assert.DoesNotContain(usersOnly, ids);
        Assert.DoesNotContain(privateOne, ids);
        Assert.DoesNotContain(unconfigured, ids);
    }

    /// <summary>Only the calendars shared with registered users (CalendarShared.User) come back.</summary>
    [Fact]
    public async Task GetSharedWithUsersAsync_ReturnsOnlyCalendarsSharedWithUsers()
    {
        long usersOnly = await SharedCalendarAsync(user: true, anonymous: false);
        long both = await SharedCalendarAsync(user: true, anonymous: true);
        long guestsOnly = await SharedCalendarAsync(user: false, anonymous: true);
        long unconfigured = await SharedCalendarAsync(user: null, anonymous: null);

        HashSet<long> ids = [.. (await Sut.GetSharedWithUsersAsync(TestContext.Current.CancellationToken)).Select(c => c.Id)];

        Assert.Contains(usersOnly, ids);
        Assert.Contains(both, ids);
        Assert.DoesNotContain(guestsOnly, ids);
        Assert.DoesNotContain(unconfigured, ids);
    }

    /// <summary>
    /// The calendars an account subscribes to that are still shared with users: another account's subscriptions, and a
    /// subscription to a calendar since turned private, are left out.
    /// </summary>
    [Fact]
    public async Task GetSubscribedByAsync_ReturnsTheAccountsStillSharedSubscriptions()
    {
        string alice = UniqueEmail("alice");
        string bob = UniqueEmail("bob");
        long shared = await SharedCalendarAsync(user: true, anonymous: false);
        long turnedPrivate = await SharedCalendarAsync(user: false, anonymous: false);
        long bobs = await SharedCalendarAsync(user: true, anonymous: false);
        await SubscribeAsync(alice, shared, turnedPrivate);
        await SubscribeAsync(bob, bobs);

        List<Calendar> result = await Sut.GetSubscribedByAsync(alice, TestContext.Current.CancellationToken);

        Assert.Equal([shared], result.Select(c => c.Id));
    }

    // -- GetByAccountEmailAsync ------------------------------------------------

    /// <summary>Verifies that <c>GetByAccountEmailAsync</c> returns only calendars for the given e-mail.</summary>
    [Fact]
    public async Task GetByAccountEmailAsync_ReturnsOnlyCalendarsForGivenEmail()
    {
        string alice = UniqueEmail("alice");
        string bob = UniqueEmail("bob");
        await SeedAsync(
            MakeCalendar(alice, Unique("Work")),
            MakeCalendar(alice, Unique("Personal")),
            MakeCalendar(bob, Unique("Bob")));

        List<Calendar> result = await Sut.GetByAccountEmailAsync(alice, TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Count);
        Assert.All(result, c => Assert.Equal(alice, c.AccountEmail.Value));
    }

    /// <summary>Verifies that <c>GetByAccountEmailAsync</c> returns empty when the e-mail has no calendars.</summary>
    [Fact]
    public async Task GetByAccountEmailAsync_ReturnsEmpty_WhenNoCalendarsForEmail()
    {
        List<Calendar> result = await Sut.GetByAccountEmailAsync(UniqueEmail("nobody"), TestContext.Current.CancellationToken);

        Assert.Empty(result);
    }

    // -- CreateAsync --------------------------------------------------------------

    /// <summary>Verifies that <c>CreateAsync</c> inserts a calendar row.</summary>
    [Fact]
    public async Task CreateAsync_AddsCalendarToDatabase()
    {
        string alice = UniqueEmail("alice");
        await EnsureAccountsAsync(alice);

        var calendar = Calendar.Reconstitute(
            id: 0, accountEmail: alice, name: Unique("New Calendar"), description: "My new calendar",
            timeZoneIanaId: "Asia/Seoul", htmlColorCode: "#3498DB",
            created: DateTime.UtcNow, updated: DateTime.UtcNow);

        await Sut.CreateAsync(calendar, TestContext.Current.CancellationToken);
        Context.ChangeTracker.Clear();

        OrmCalendar? saved = await Context.Calendars.FirstOrDefaultAsync(
            c => c.AccountEmail == alice, TestContext.Current.CancellationToken);
        Assert.NotNull(saved);
        Assert.Equal("#3498DB", saved.HtmlColorCode);
        Assert.Equal("Asia/Seoul", saved.TimeZoneIanaId);
    }

    /// <summary>PostgreSQL enforces <c>Calendar_HtmlColorCode_check</c> — a malformed color throws (InMemory did not).</summary>
    [Fact]
    public async Task CreateAsync_Throws_WhenColourCodeIsMalformed()
    {
        string alice = UniqueEmail("alice");
        await EnsureAccountsAsync(alice);

        var calendar = Calendar.Reconstitute(
            id: 0, accountEmail: alice, name: Unique("Bad"), description: null,
            timeZoneIanaId: "UTC", htmlColorCode: "#GGGGGG",
            created: DateTime.UtcNow, updated: DateTime.UtcNow);

        await Assert.ThrowsAsync<DbUpdateException>(
            () => Sut.CreateAsync(calendar, TestContext.Current.CancellationToken));
    }

    // -- FindByIdAsync ------------------------------------------------------------

    /// <summary>Verifies that the plain lookup returns the calendar by id, and null for an unknown id.</summary>
    [Fact]
    public async Task FindByIdAsync_ReturnsTheCalendar_OrNull()
    {
        var seed = MakeCalendar(UniqueEmail("carol"), Unique("Home"));
        await SeedAsync(seed);

        Calendar? found = await Sut.FindByIdAsync(seed.Id, TestContext.Current.CancellationToken);
        Calendar? missing = await Sut.FindByIdAsync(long.MaxValue, TestContext.Current.CancellationToken);

        Assert.Equal(seed.Name, found?.Name);
        Assert.Null(missing);
    }

    // -- FindByIdForUpdateAsync (SELECT ... FOR UPDATE) --------------------------

    /// <summary>Verifies that the row-locking lookup returns the calendar when it exists.</summary>
    [Fact]
    public async Task FindByIdForUpdateAsync_ReturnsCalendar_WhenExists()
    {
        string alice = UniqueEmail("alice");
        var seed = MakeCalendar(alice, Unique("Work"));
        await SeedAsync(seed);

        await using var unitOfWork = new UnitOfWork(Context);
        await unitOfWork.BeginAsync(ct: TestContext.Current.CancellationToken);
        Calendar? result = await Sut.FindByIdForUpdateAsync(seed.Id, TestContext.Current.CancellationToken);
        await unitOfWork.CommitAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(seed.Name, result.Name);
    }

    /// <summary>Verifies that the row-locking lookup returns null when the calendar does not exist.</summary>
    [Fact]
    public async Task FindByIdForUpdateAsync_ReturnsNull_WhenNotFound()
    {
        await using var unitOfWork = new UnitOfWork(Context);
        await unitOfWork.BeginAsync(ct: TestContext.Current.CancellationToken);
        Calendar? result = await Sut.FindByIdForUpdateAsync(long.MaxValue, TestContext.Current.CancellationToken);
        await unitOfWork.CommitAsync(TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    // -- UpdateEntityAsync --------------------------------------------------

    /// <summary>Verifies that <c>UpdateEntityAsync</c> persists changes to an existing calendar.</summary>
    [Fact]
    public async Task UpdateEntityAsync_UpdatesExistingCalendar()
    {
        string alice = UniqueEmail("alice");
        var seed = MakeCalendar(alice, Unique("Work"), color: "#FF5733");
        await SeedAsync(seed);

        var calendar = Calendar.Reconstitute(
            id: seed.Id, accountEmail: alice, name: Unique("Work Updated"), description: "Updated",
            timeZoneIanaId: "Asia/Seoul", htmlColorCode: "#2ECC71",
            created: seed.Created, updated: DateTime.UtcNow);

        await Sut.UpdateEntityAsync(calendar, TestContext.Current.CancellationToken);
        Context.ChangeTracker.Clear();

        OrmCalendar? updated = await Context.Calendars.FirstOrDefaultAsync(c => c.Id == seed.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(updated);
        Assert.Equal("#2ECC71", updated.HtmlColorCode);
    }

    // -- DeleteByIdAsync --------------------------------------------------

    /// <summary>Verifies that <c>DeleteByIdAsync</c> removes the calendar row.</summary>
    [Fact]
    public async Task DeleteByIdAsync_RemovesCalendarFromDatabase()
    {
        string alice = UniqueEmail("alice");
        var seed = MakeCalendar(alice, Unique("ToDelete"));
        await SeedAsync(seed);

        await Sut.DeleteByIdAsync(seed.Id, TestContext.Current.CancellationToken);
        Context.ChangeTracker.Clear();

        Assert.Null(await Context.Calendars.FirstOrDefaultAsync(c => c.Id == seed.Id, TestContext.Current.CancellationToken));
    }

    /// <summary>Verifies that <c>DeleteByIdAsync</c> does not affect other calendars.</summary>
    [Fact]
    public async Task DeleteByIdAsync_DoesNotAffectOtherCalendars()
    {
        string alice = UniqueEmail("alice");
        var keep = MakeCalendar(alice, Unique("Keep"));
        var remove = MakeCalendar(alice, Unique("Remove"));
        await SeedAsync(keep, remove);

        await Sut.DeleteByIdAsync(remove.Id, TestContext.Current.CancellationToken);
        Context.ChangeTracker.Clear();

        List<OrmCalendar> remaining = await Context.Calendars
            .Where(c => c.AccountEmail == alice).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(keep.Id, Assert.Single(remaining).Id);
    }
}
