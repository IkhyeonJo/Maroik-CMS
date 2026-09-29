using Maroik.Core.Domain.Calendar;
using Maroik.Core.PostgreSQL.Data;
using Maroik.Core.Repository.Repositories;
using Microsoft.EntityFrameworkCore;
using OrmCalendarShared = Maroik.Core.PostgreSQL.Models.CalendarShared;

namespace Maroik.Core.Repository.Tests.Repositories;

/// <summary>
/// Tests for <see cref="CalendarSharedRepository"/> against a real PostgreSQL database
/// (Testcontainers) preloaded with the <c>Init.sql</c> schema and seed data. <c>CalendarShared.CalendarId</c> is both the primary key and the FK
/// <c>CalendarShared_fk_0</c> to <c>Calendar.ID</c> (unenforced by InMemory), so every test creates
/// a real parent calendar; the global <c>GetAllAsync</c> assertions are scoped to the ids this test
/// created.
/// </summary>
public sealed class CalendarSharedRepositoryTests(DatabaseFixture database) : RepositoryTestBase(database)
{
    private CalendarSharedRepository Sut => new(Context);

    /// <summary>An unsaved sharing row for calendar <paramref name="calendarId"/>.</summary>
    private static OrmCalendarShared MakeShared(long calendarId, bool user = false, bool anonymous = false) => new()
    {
        CalendarId = calendarId,
        User = user,
        Anonymous = anonymous
    };

    /// <summary>Inserts <paramref name="entries"/>, saves, and clears the change tracker so later reads hit the database.</summary>
    private async Task SeedAsync(params OrmCalendarShared[] entries)
    {
        await Context.CalendarShareds.AddRangeAsync(entries);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();
    }

    // -- GetAllAsync ------------------------------------------------------

    /// <summary>Verifies that <c>GetAllAsync</c> includes every entry this test inserted.</summary>
    [Fact]
    public async Task GetAllAsync_ReturnsInsertedEntries()
    {
        long c1 = await InsertCalendarAsync();
        long c2 = await InsertCalendarAsync();
        long c3 = await InsertCalendarAsync();
        await SeedAsync(MakeShared(c1, user: true), MakeShared(c2, anonymous: true), MakeShared(c3));

        List<CalendarShared> result = await Sut.GetAllAsync(TestContext.Current.CancellationToken);

        long[] mine = [c1, c2, c3];
        Assert.Equal(3, result.Count(s => mine.Contains(s.Id)));
    }

    // -- CreateAsync --------------------------------------------------

    /// <summary>Verifies that <c>CreateAsync</c> inserts a shared-calendar entry.</summary>
    [Fact]
    public async Task CreateAsync_PersistsEntry()
    {
        long calendarId = await InsertCalendarAsync();

        var shared = CalendarShared.Reconstitute(calendarId: calendarId, user: true, anonymous: false);

        await Sut.CreateAsync(shared, TestContext.Current.CancellationToken);
        Context.ChangeTracker.Clear();

        OrmCalendarShared? saved = await Context.CalendarShareds
            .FirstOrDefaultAsync(s => s.CalendarId == calendarId, TestContext.Current.CancellationToken);
        Assert.NotNull(saved);
        Assert.True(saved.User);
        Assert.False(saved.Anonymous);
    }

    /// <summary>PostgreSQL enforces <c>CalendarShared_fk_0</c> — a share for an unknown calendar throws (InMemory did not).</summary>
    [Fact]
    public async Task CreateAsync_Throws_WhenParentCalendarDoesNotExist()
    {
        var shared = CalendarShared.Reconstitute(calendarId: long.MaxValue, user: true, anonymous: false);

        await Assert.ThrowsAsync<DbUpdateException>(
            () => Sut.CreateAsync(shared, TestContext.Current.CancellationToken));
    }

    // -- UpdateEntityAsync --------------------------------------------

    /// <summary>Verifies that <c>UpdateEntityAsync</c> persists changes to an existing entry.</summary>
    [Fact]
    public async Task UpdateEntityAsync_UpdatesExistingEntry()
    {
        long calendarId = await InsertCalendarAsync();
        await SeedAsync(MakeShared(calendarId, user: false, anonymous: false));

        var shared = CalendarShared.Reconstitute(calendarId: calendarId, user: true, anonymous: true);

        await Sut.UpdateEntityAsync(shared, TestContext.Current.CancellationToken);
        Context.ChangeTracker.Clear();

        OrmCalendarShared? updated = await Context.CalendarShareds
            .FirstOrDefaultAsync(s => s.CalendarId == calendarId, TestContext.Current.CancellationToken);
        Assert.NotNull(updated);
        Assert.True(updated.User);
        Assert.True(updated.Anonymous);
    }

    // -- GetByIdsForUpdateAsync (SELECT ... FOR UPDATE) --------------------

    /// <summary>Verifies the batched row-locking lookup returns only the requested, existing entries.</summary>
    [Fact]
    public async Task GetByIdsForUpdateAsync_ReturnsOnlyMatchingEntries()
    {
        long c1 = await InsertCalendarAsync();
        long c2 = await InsertCalendarAsync();
        long c3 = await InsertCalendarAsync();
        await SeedAsync(MakeShared(c1, user: true), MakeShared(c2, anonymous: true), MakeShared(c3));

        List<CalendarShared> result = await Sut.GetByIdsForUpdateAsync([c1, c3, long.MaxValue], TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, s => s.Id == c1 && s.User);
        Assert.Contains(result, s => s.Id == c3);
        Assert.DoesNotContain(result, s => s.Id == c2);
    }

    /// <summary>An empty id set returns an empty list without hitting the database.</summary>
    [Fact]
    public async Task GetByIdsForUpdateAsync_ReturnsEmptyList_ForEmptyIdSet()
    {
        List<CalendarShared> result = await Sut.GetByIdsForUpdateAsync([], TestContext.Current.CancellationToken);

        Assert.Empty(result);
    }

    /// <summary>
    /// Reproduces the deadlock hazard the query's <c>ORDER BY "CalendarId"</c> guards against: two
    /// concurrent transactions locking the same two rows in opposite request order must still both
    /// complete (serialized, not deadlocked) because the query itself normalizes the lock-acquisition
    /// order regardless of the order ids were passed in.
    /// </summary>
    [Fact]
    public async Task GetByIdsForUpdateAsync_DoesNotDeadlock_WhenConcurrentCallersRequestOverlappingIdsInOppositeOrder()
    {
        long low = await InsertCalendarAsync();
        long high = await InsertCalendarAsync();
        // Ensure "low" really does sort before "high" regardless of insertion order.
        if (low > high) (low, high) = (high, low);
        await SeedAsync(MakeShared(low), MakeShared(high));

        ApplicationDbContext[] contexts = [NewDbContext(), NewDbContext()];

        // One caller requests [low, high]; the other requests the same pair reversed, [high, low].
        // Without the ORDER BY, this is exactly the pattern that can lock rows in different physical
        // orders across the two transactions and deadlock in PostgreSQL.
        await Task.WhenAll(
            LockAndUpdateAsync(contexts[0], [low, high], user: true, anonymous: false),
            LockAndUpdateAsync(contexts[1], [high, low], user: false, anonymous: true));
        return;

        // One contender: locks both rows (ids in the order given), waits so the other contender overlaps, then updates and commits.
 #pragma warning disable IDE0062
        async Task LockAndUpdateAsync(ApplicationDbContext context, long[] ids, bool user, bool anonymous)
 #pragma warning restore IDE0062
        {
            var repo = new CalendarSharedRepository(context);
            await using var tx = await context.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);

            List<CalendarShared> locked = await repo.GetByIdsForUpdateAsync(ids, TestContext.Current.CancellationToken);
            Assert.Equal(2, locked.Count);

            await Task.Delay(200, TestContext.Current.CancellationToken);

            foreach (CalendarShared shared in locked)
                await repo.UpdateEntityAsync(shared.Update(user, anonymous), TestContext.Current.CancellationToken);

            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
            await tx.CommitAsync(TestContext.Current.CancellationToken);
        }
    }
}
