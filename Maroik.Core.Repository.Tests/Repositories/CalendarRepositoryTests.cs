using Maroik.Core.Domain.Calendar;
using Maroik.Core.Repository.Repositories;
using Microsoft.EntityFrameworkCore;
using OrmCalendar = Maroik.Core.PostgreSQL.Models.Calendar;

namespace Maroik.Core.Repository.Tests.Repositories;

/// <summary>
/// Tests for <see cref="CalendarRepository"/> against a real PostgreSQL database (Testcontainers)
/// preloaded with the <c>Init.sql</c> schema and seed data (the demo account's calendar). <c>Calendar</c>
/// has the FK <c>Calendar_fk_0</c> to <c>Account.Email</c> and the checks
/// <c>Calendar_HtmlColorCode_check</c> (<c>^#[0-9A-Fa-f]{6}$</c>) and <c>Calendar_Name_check</c>.
/// The global <c>GetAllOrderedByNameAsync</c> also returns the seed rows, so its assertions are
/// scoped to this test's own account.
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

    // -- GetAllOrderedByNameAsync -----------------------------------------------

    /// <summary>Verifies that <c>GetAllOrderedByNameAsync</c> includes every calendar this test inserted.</summary>
    [Fact]
    public async Task GetAllOrderedByNameAsync_ReturnsInsertedCalendars()
    {
        string alice = UniqueEmail("alice");
        await SeedAsync(MakeCalendar(alice, Unique("Work")), MakeCalendar(alice, Unique("Personal")));

        List<Calendar> result = await Sut.GetAllOrderedByNameAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Count(c => c.AccountEmail.Value == alice));
    }

    /// <summary>Verifies that <c>GetAllOrderedByNameAsync</c> returns rows in <c>Name</c> order.</summary>
    [Fact]
    public async Task GetAllOrderedByNameAsync_ReturnsOrderedByName()
    {
        string alice = UniqueEmail("alice");
        // Prefix with the token so the pair sorts adjacently regardless of seed names.
        await SeedAsync(MakeCalendar(alice, Token + "-Work"), MakeCalendar(alice, Token + "-Personal"));

        List<string> mine =
        [
            .. (await Sut.GetAllOrderedByNameAsync(TestContext.Current.CancellationToken))
            .Where(c => c.AccountEmail.Value == alice)
            .Select(c => c.Name)
        ];

        Assert.Equal([Token + "-Personal", Token + "-Work"], mine);
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

    // -- FindByIdForUpdateAsync (SELECT ... FOR UPDATE) --------------------------

    /// <summary>Verifies that the row-locking lookup returns the calendar when it exists.</summary>
    [Fact]
    public async Task FindByIdForUpdateAsync_ReturnsCalendar_WhenExists()
    {
        string alice = UniqueEmail("alice");
        var seed = MakeCalendar(alice, Unique("Work"));
        await SeedAsync(seed);

        await using var unitOfWork = new UnitOfWork(Context);
        await unitOfWork.BeginAsync(TestContext.Current.CancellationToken);
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
        await unitOfWork.BeginAsync(TestContext.Current.CancellationToken);
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
