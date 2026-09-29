using Maroik.Core.Domain.Account;
using Maroik.Core.Domain.Calendar;
using Maroik.Core.Domain.Menu;
using Maroik.Core.PostgreSQL.Data;
using Maroik.Core.Repository.Repositories;
using Microsoft.EntityFrameworkCore;
using OrmCategory = Maroik.Core.PostgreSQL.Models.Category;

namespace Maroik.Core.Repository.Tests.Repositories;

/// <summary>
/// Tests the flush contract shared by every <see cref="GenericRepository{TDomain,TEntity}"/>:
/// write is persisted immediately when no <see cref="UnitOfWork"/> transaction is open, but is
/// left pending — and flushed by <see cref="UnitOfWork.CommitAsync"/> as a single batch — while a
/// transaction is open. <see cref="CategoryRepository"/> is used as a representative concrete
/// repository (root table, no FKs).
/// </summary>
public sealed class GenericRepositoryTransactionTests(DatabaseFixture database) : RepositoryTestBase(database)
{
    /// <summary>A new (id 0) category with a name unique to this test.</summary>
    private Category NewCategory() =>
        Category.Reconstitute(0, Unique("TxCat"), "Tx Category", "/icons/i.svg", "Tx", "Index", Role.User, 1);

    /// <summary>True when a category named <paramref name="name"/> is visible from a fresh context, i.e. has been committed.</summary>
    private async Task<bool> ExistsInSeparateConnectionAsync(string name)
    {
        await using var probe = NewDbContext();
        return await probe.Categories.AsNoTracking().AnyAsync(c => c.Name == name, TestContext.Current.CancellationToken);
    }

    /// <summary>Without a transaction, <c>CreateAsync</c> flushes right away (unchanged legacy behavior).</summary>
    [Fact]
    public async Task CreateAsync_WithoutTransaction_FlushesImmediately()
    {
        var category = NewCategory();

        await new CategoryRepository(Context).CreateAsync(category, TestContext.Current.CancellationToken);

        Assert.DoesNotContain(
            Context.ChangeTracker.Entries<OrmCategory>(),
            e => e.State == EntityState.Added);
        Assert.True(await ExistsInSeparateConnectionAsync(category.Name!));
    }

    /// <summary>Inside an open transaction the INSERT is deferred until <c>CommitAsync</c> flushes it.</summary>
    [Fact]
    public async Task CreateAsync_InsideTransaction_IsDeferredThenFlushedOnCommit()
    {
        await using var uow = new UnitOfWork(Context);
        var repo = new CategoryRepository(Context);
        var category = NewCategory();

        await uow.BeginAsync(TestContext.Current.CancellationToken);
        await repo.CreateAsync(category, TestContext.Current.CancellationToken);

        // Still pending — no SaveChanges was issued by the repository call.
        Assert.Contains(
            Context.ChangeTracker.Entries<OrmCategory>(),
            e => e.State == EntityState.Added && e.Entity.Name == category.Name);

        await uow.CommitAsync(TestContext.Current.CancellationToken);

        Assert.DoesNotContain(
            Context.ChangeTracker.Entries<OrmCategory>(),
            e => e.State == EntityState.Added);
        Assert.True(await ExistsInSeparateConnectionAsync(category.Name!));
    }

    /// <summary>A deferred write is discarded when the transaction is rolled back.</summary>
    [Fact]
    public async Task CreateAsync_InsideTransaction_RolledBack_IsNotPersisted()
    {
        await using var uow = new UnitOfWork(Context);
        var repo = new CategoryRepository(Context);
        var category = NewCategory();

        await uow.BeginAsync(TestContext.Current.CancellationToken);
        await repo.CreateAsync(category, TestContext.Current.CancellationToken);
        await uow.RollbackAsync(TestContext.Current.CancellationToken);

        Assert.False(await ExistsInSeparateConnectionAsync(category.Name!));
    }

    /// <summary>
    /// Regression: <c>QueryAsync</c> (used here via <c>OtherCalendarRepository.GetByAccountEmailAsync</c>)
    /// used to translate its predicate straight into SQL and run it against the database only, so a row
    /// created earlier in the same still-open transaction -- whose INSERT is deferred -- was invisible
    /// to it even though it already exists for every practical purpose within this unit of work.
    /// </summary>
    [Fact]
    public async Task QueryAsync_InsideTransaction_SeesRowCreatedEarlierInSameTransaction()
    {
        long calendarId = await InsertCalendarAsync();
        string email = UniqueEmail("subscriber-query");
        await EnsureAccountsAsync(email);
        var link = OtherCalendar.Reconstitute(email, calendarId);

        await using var uow = new UnitOfWork(Context);
        var repo = new OtherCalendarRepository(Context);

        await uow.BeginAsync(TestContext.Current.CancellationToken);
        await repo.CreateAsync(link, TestContext.Current.CancellationToken);
        List<OtherCalendar> subscriptions = await repo.GetByAccountEmailAsync(email, TestContext.Current.CancellationToken);

        Assert.Contains(subscriptions, s => s.CalendarId == calendarId);
    }

    /// <summary>
    /// Regression: <c>QueryFirstAsync</c> (used here via <c>AccountRepository.FindByEmailAsync</c>) had
    /// the same DB-only gap as <c>QueryAsync</c> above -- a row created earlier in the same still-open
    /// transaction was invisible to it because its INSERT is deferred until <c>CommitAsync</c>.
    /// </summary>
    [Fact]
    public async Task QueryFirstAsync_InsideTransaction_SeesRowCreatedEarlierInSameTransaction()
    {
        string email = UniqueEmail("first-query");
        var account = Account.Reconstitute(
            email: email, hashedPassword: "$2a$13$placeholder", nickname: "nick-" + Token,
            avatarImagePath: null, role: Role.User, timeZoneIanaId: "UTC", defaultMonetaryUnit: null,
            locked: false, loginAttempt: 0, emailConfirmed: true, agreedServiceTerms: true,
            registrationToken: null, resetPasswordToken: null, created: DateTime.UtcNow, updated: DateTime.UtcNow,
            message: null, deleted: false, securityStamp: "stamp", mustChangePassword: false);

        await using var uow = new UnitOfWork(Context);
        var repo = new AccountRepository(Context);

        await uow.BeginAsync(TestContext.Current.CancellationToken);
        await repo.CreateAsync(account, TestContext.Current.CancellationToken);
        Account? found = await repo.FindByEmailAsync(email, TestContext.Current.CancellationToken);

        Assert.NotNull(found);
        Assert.Equal(email, found.Email.Value);
    }

    /// <summary>Test-only subclass exposing a <c>DeleteWhereAsync</c> filtered on <c>Name</c> (a mutable,
    /// non-key column) so the Modified-row extension below can be exercised through it.</summary>
    private sealed class NameFilterableCategoryRepository(ApplicationDbContext context) : CategoryRepository(context)
    {
        /// <summary>Deletes every category with the given name through the protected <c>DeleteWhereAsync</c>.</summary>
        public Task DeleteByNameAsync(string name, CancellationToken ct) => DeleteWhereAsync(e => e.Name == name, ct);
    }

    /// <summary>
    /// Regression: <c>DeleteWhereAsync</c>'s delete-set query is deliberately <c>AsNoTracking</c> (see
    /// its own comment), so it cannot see a row renamed -- but not yet flushed -- earlier in the same
    /// transaction; without the Modified-row reconciliation, a predicate matching only the row's new,
    /// unflushed name would silently delete nothing and the pending rename would still commit.
    /// </summary>
    [Fact]
    public async Task DeleteWhereAsync_InsideTransaction_RemovesRowRenamedEarlierInSameTransactionToMatchPredicate()
    {
        string originalName = Unique("OrigCat");
        string newName = Unique("RenamedCat");
        var repo = new NameFilterableCategoryRepository(Context);
        var category = Category.Reconstitute(0, originalName, "Display", "/icons/i.svg", "Ctrl", "Index", Role.User, 1);
        await repo.CreateAsync(category, TestContext.Current.CancellationToken);
        long id = await Context.Categories.AsNoTracking()
            .Where(c => c.Name == originalName)
            .Select(c => c.Id)
            .SingleAsync(TestContext.Current.CancellationToken);

        await using var uow = new UnitOfWork(Context);
        await uow.BeginAsync(TestContext.Current.CancellationToken);
        var renamed = Category.Reconstitute(id, newName, "Display", "/icons/i.svg", "Ctrl", "Index", Role.User, 1);
        await repo.UpdateEntityAsync(renamed, TestContext.Current.CancellationToken);
        await repo.DeleteByNameAsync(newName, TestContext.Current.CancellationToken);
        await uow.CommitAsync(TestContext.Current.CancellationToken);

        Assert.False(await ExistsInSeparateConnectionAsync(originalName));
        Assert.False(await ExistsInSeparateConnectionAsync(newName));
    }

    /// <summary>Test-only subclass exposing <c>QueryAsync</c> / <c>QueryFirstAsync</c> with an <c>orderBy</c>
    /// and a <c>DeleteWhereAsync</c>, all filtered on a name prefix, so the pending-change merge can be
    /// exercised through the protected helpers.</summary>
    private sealed class PrefixCategoryRepository(ApplicationDbContext context) : CategoryRepository(context)
    {
        /// <summary>Categories whose name starts with <paramref name="prefix"/>, ordered by name.</summary>
        public Task<List<Category>> ListOrderedAsync(string prefix, CancellationToken ct) =>
            QueryAsync(e => e.Name.StartsWith(prefix), q => q.OrderBy(e => e.Name), ct: ct);

        /// <summary>The first category (by name) whose name starts with <paramref name="prefix"/>.</summary>
        public Task<Category?> FirstOrderedAsync(string prefix, CancellationToken ct) =>
            QueryFirstAsync(e => e.Name.StartsWith(prefix), q => q.OrderBy(e => e.Name), ct: ct);

        /// <summary>Deletes every category whose name starts with <paramref name="prefix"/>.</summary>
        public Task DeleteByPrefixAsync(string prefix, CancellationToken ct) =>
            DeleteWhereAsync(e => e.Name.StartsWith(prefix), ct);
    }

    /// <summary>A new (id 0) category named <paramref name="name"/>.</summary>
    private static Category CategoryNamed(string name) =>
        Category.Reconstitute(0, name, "Display", "/icons/i.svg", "Ctrl", "Index", Role.User, 1);

    /// <summary>
    /// A row created earlier in the open transaction is merged into <c>QueryAsync</c>'s result, and the
    /// merged list must then be re-sorted with the caller's <c>orderBy</c> — the pending row is appended
    /// after the DB rows, so without the re-sort an ordered read would return it last.
    /// </summary>
    [Fact]
    public async Task QueryAsync_InsideTransaction_ReSortsPendingRowsIntoTheCallersOrder()
    {
        string prefix = Unique("Ord");
        var repo = new PrefixCategoryRepository(Context);
        await repo.CreateAsync(CategoryNamed(prefix + "-b"), TestContext.Current.CancellationToken);
        await repo.CreateAsync(CategoryNamed(prefix + "-d"), TestContext.Current.CancellationToken);

        await using var uow = new UnitOfWork(Context);
        await uow.BeginAsync(TestContext.Current.CancellationToken);
        await repo.CreateAsync(CategoryNamed(prefix + "-a"), TestContext.Current.CancellationToken);
        await repo.CreateAsync(CategoryNamed(prefix + "-c"), TestContext.Current.CancellationToken);

        List<Category> ordered = await repo.ListOrderedAsync(prefix, TestContext.Current.CancellationToken);

        Assert.Equal([prefix + "-a", prefix + "-b", prefix + "-c", prefix + "-d"], ordered.Select(c => c.Name));
    }

    /// <summary>
    /// <c>QueryFirstAsync</c> must pick "first" from the merged, re-sorted set: a pending row that sorts
    /// before every persisted row is the answer, not the first row the database happened to return.
    /// </summary>
    [Fact]
    public async Task QueryFirstAsync_InsideTransaction_ConsidersPendingRowsWhenPickingFirst()
    {
        string prefix = Unique("First");
        var repo = new PrefixCategoryRepository(Context);
        await repo.CreateAsync(CategoryNamed(prefix + "-b"), TestContext.Current.CancellationToken);

        await using var uow = new UnitOfWork(Context);
        await uow.BeginAsync(TestContext.Current.CancellationToken);
        await repo.CreateAsync(CategoryNamed(prefix + "-a"), TestContext.Current.CancellationToken);

        Category? first = await repo.FirstOrderedAsync(prefix, TestContext.Current.CancellationToken);

        Assert.Equal(prefix + "-a", first?.Name);
    }

    /// <summary>
    /// A row deleted earlier in the same transaction still exists in the database until commit, so the
    /// DB query returns it; the merge must drop it, otherwise a caller would read a row it just deleted.
    /// </summary>
    [Fact]
    public async Task QueryAsync_InsideTransaction_DoesNotReturnRowDeletedEarlierInSameTransaction()
    {
        string prefix = Unique("Del");
        var repo = new PrefixCategoryRepository(Context);
        await repo.CreateAsync(CategoryNamed(prefix + "-keep"), TestContext.Current.CancellationToken);
        await repo.CreateAsync(CategoryNamed(prefix + "-gone"), TestContext.Current.CancellationToken);

        await using var uow = new UnitOfWork(Context);
        await uow.BeginAsync(TestContext.Current.CancellationToken);
        await repo.DeleteByPrefixAsync(prefix + "-gone", TestContext.Current.CancellationToken);

        List<Category> visible = await repo.ListOrderedAsync(prefix, TestContext.Current.CancellationToken);

        Assert.Equal([prefix + "-keep"], visible.Select(c => c.Name));
    }

    /// <summary>
    /// Deleting a row that was only created earlier in the same transaction (INSERT still deferred) must
    /// cancel the pending insert instead of issuing a DELETE for a row that never reached the database.
    /// </summary>
    [Fact]
    public async Task DeleteWhereAsync_InsideTransaction_CancelsRowCreatedEarlierInSameTransaction()
    {
        string name = Unique("Ephemeral");
        var repo = new PrefixCategoryRepository(Context);

        await using var uow = new UnitOfWork(Context);
        await uow.BeginAsync(TestContext.Current.CancellationToken);
        await repo.CreateAsync(CategoryNamed(name), TestContext.Current.CancellationToken);
        await repo.DeleteByPrefixAsync(name, TestContext.Current.CancellationToken);
        await uow.CommitAsync(TestContext.Current.CancellationToken);

        Assert.False(await ExistsInSeparateConnectionAsync(name));
    }
}
