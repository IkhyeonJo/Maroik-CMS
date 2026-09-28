using Maroik.Core.PostgreSQL.Data;
using Microsoft.EntityFrameworkCore;
using OrmAccount = Maroik.Core.PostgreSQL.Models.Account;
using OrmAsset = Maroik.Core.PostgreSQL.Models.Asset;
using OrmCalendar = Maroik.Core.PostgreSQL.Models.Calendar;
using OrmCalendarEvent = Maroik.Core.PostgreSQL.Models.CalendarEvent;

namespace Maroik.Core.Repository.Tests.Infrastructure;

/// <summary>
/// Base class for every repository test. Supplies an <see cref="ApplicationDbContext"/> bound to
/// this class's throwaway PostgreSQL database, which already contains the real Init.sql schema and
/// its seed data (the menu/category rows plus the admin and demo accounts).
/// <para>
/// Because the whole class shares one database, a test must not assume an empty table. Insert with
/// <see cref="UniqueEmail"/> / <see cref="Unique"/> so keys never collide with the seed or with a
/// sibling test, and scope "list/count" assertions to the account or ids the test itself created.
/// </para>
/// </summary>
public abstract class RepositoryTestBase : IClassFixture<DatabaseFixture>, IAsyncDisposable
{
    private readonly DatabaseFixture _database;
    private readonly List<ApplicationDbContext> _contexts = [];

    /// <summary>Short token unique to this test instance (xUnit constructs one instance per test method).</summary>
    protected string Token { get; } = Guid.NewGuid().ToString("N")[..12];

    /// <param name="database">Per-class seeded database, injected by xUnit.</param>
    protected RepositoryTestBase(DatabaseFixture database)
    {
        _database = database;
        Context = NewDbContext();
    }

    /// <summary>Primary context for the test. Use <see cref="NewDbContext"/> for an independent
    /// second connection (e.g. to simulate a concurrent writer).</summary>
    protected ApplicationDbContext Context { get; }

    /// <summary>An e-mail address that cannot exist in the seed data or in another test.</summary>
    protected string UniqueEmail(string label = "user") =>
        $"{label}-{Token}@example.com".ToLowerInvariant();

    /// <summary>A string (asset product name, nickname, board title, …) unique to this test instance.</summary>
    protected string Unique(string label) => $"{label}-{Token}";

    /// <summary>
    /// Inserts an <c>Account</c> row for each e-mail that does not already exist (seed or a prior
    /// call), so tests can satisfy the many <c>*_fk_*</c> foreign keys that point at
    /// <c>Account.Email</c> (Asset, Calendar, Income, Expenditure, …) without every file repeating
    /// the boilerplate. Uses a random nickname to stay clear of <c>Account_Nickname_unique</c>.
    /// </summary>
    protected async Task EnsureAccountsAsync(params string[] emails)
    {
        List<string> existing = await Context.Accounts.AsNoTracking()
            .Where(a => ((IEnumerable<string>)emails).Contains(a.Email))
            .Select(a => a.Email!)
            .ToListAsync();

        List<OrmAccount> toAdd = [.. emails.Distinct()
            .Where(e => !existing.Contains(e))
            .Select(e => new OrmAccount
            {
                Email = e,
                Nickname = "nick-" + Guid.NewGuid().ToString("N")[..16],
                HashedPassword = "$2a$13$placeholder",
                AvatarImagePath = "/upload/Management/Profile/default-avatar.jpg",
                Role = "User",
                TimeZoneIanaId = "UTC",
                Deleted = false,
                Locked = false,
                EmailConfirmed = true,
                AgreedServiceTerms = true,
                LoginAttempt = 0,
                SecurityStamp = "stamp",
                Created = DateTime.UtcNow,
                Updated = DateTime.UtcNow,
            })];

        if (toAdd.Count == 0) return;

        await Context.Accounts.AddRangeAsync(toAdd);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();
    }

    /// <summary>
    /// Ensures an <c>Account</c> and an <c>Asset</c> row exist for each given product name, so
    /// tests can satisfy the composite <c>(ProductName, AccountEmail)</c> foreign keys the finance
    /// tables carry (<c>Income_fk_0</c>, <c>Expenditure_fk_0/1</c>, <c>FixedIncome_fk_0</c>,
    /// <c>FixedExpenditure_fk_0/1</c>). Null/empty names are skipped — those columns are nullable
    /// and a NULL is not FK-checked.
    /// </summary>
    protected async Task EnsureAssetsAsync(string accountEmail, params string?[] productNames)
    {
        await EnsureAccountsAsync(accountEmail);

        string[] names = [.. productNames.Where(n => !string.IsNullOrEmpty(n)).Select(n => n!).Distinct()];
        if (names.Length == 0) return;

        List<string> existing = await Context.Assets.AsNoTracking()
            .Where(a => a.AccountEmail == accountEmail && ((IEnumerable<string>)names).Contains(a.ProductName))
            .Select(a => a.ProductName!)
            .ToListAsync();

        List<OrmAsset> toAdd = [.. names.Where(n => !existing.Contains(n)).Select(n => new OrmAsset
        {
            ProductName = n,
            AccountEmail = accountEmail,
            Item = "FreeDepositAndWithdrawal",
            MonetaryUnit = "KRW",
            Amount = 0m,
            Note = "",
            Deleted = false,
            Created = DateTime.UtcNow,
            Updated = DateTime.UtcNow,
        })];

        if (toAdd.Count == 0) return;

        await Context.Assets.AddRangeAsync(toAdd);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();
    }

    /// <summary>
    /// Inserts a <c>Calendar</c> (creating its owning <c>Account</c> if needed) and returns its
    /// DB-generated id, so tests can satisfy the FKs on <c>CalendarEvent</c>, <c>CalendarShared</c>
    /// and <c>OtherCalendar</c>. The color matches <c>Calendar_HtmlColorCode_check</c>.
    /// </summary>
    protected async Task<long> InsertCalendarAsync(string? accountEmail = null, string? name = null)
    {
        string email = accountEmail ?? UniqueEmail("cal-owner-" + Guid.NewGuid().ToString("N")[..8]);
        await EnsureAccountsAsync(email);

        var calendar = new OrmCalendar
        {
            AccountEmail = email,
            Name = name ?? Unique("Calendar"),
            Description = null,
            TimeZoneIanaId = "UTC",
            HtmlColorCode = "#fc330e",
            Created = DateTime.UtcNow,
            Updated = DateTime.UtcNow,
        };
        await Context.Calendars.AddAsync(calendar);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();
        return calendar.Id;
    }

    /// <summary>
    /// Inserts a <c>CalendarEvent</c> (creating a parent <c>Calendar</c> if <paramref name="calendarId"/>
    /// is null) and returns its DB-generated id, for the FKs on <c>CalendarEventReminder</c> and
    /// <c>CalendarEventAttachedFile</c>. Values satisfy the event's <c>Status</c>/<c>Title</c>/<c>EndDate</c> checks.
    /// </summary>
    protected async Task<long> InsertCalendarEventAsync(long? calendarId = null, string? title = null)
    {
        long calId = calendarId ?? await InsertCalendarAsync();

        var calendarEvent = new OrmCalendarEvent
        {
            CalendarId = calId,
            Title = title ?? Unique("Event"),
            Description = null,
            AllDay = false,
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddHours(1),
            Status = "Busy",
            Created = DateTime.UtcNow,
            Updated = DateTime.UtcNow,
        };
        await Context.CalendarEvents.AddAsync(calendarEvent);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();
        return calendarEvent.Id;
    }

    /// <summary>Creates an additional context on the same database; it is disposed with the test.</summary>
    protected ApplicationDbContext NewDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(_database.ConnectionString)
            .Options;
        var context = new ApplicationDbContext(options);
        _contexts.Add(context);
        return context;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        foreach (var context in _contexts)
            await context.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
