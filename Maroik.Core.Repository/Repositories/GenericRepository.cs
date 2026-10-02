using System.Linq.Expressions;
using Maroik.Core.Contract.Interfaces;
using Maroik.Core.PostgreSQL.Data;
using Microsoft.EntityFrameworkCore;

namespace Maroik.Core.Repository.Repositories;

/// <summary>
/// Base class for EF Core repositories, implementing the create/update operations of
/// <see cref="IGenericRepository{TDomain}"/> and providing protected query/delete helpers shared
/// by every concrete repository.
/// </summary>
/// <typeparam name="TDomain">The domain aggregate/entity type exposed to the rest of the application.</typeparam>
/// <typeparam name="TEntity">The EF Core persistence model (table mapping) type in <see cref="Maroik.Core.PostgreSQL.Models"/>.</typeparam>
/// <remarks>
/// Subclasses must implement <see cref="Set"/>, <see cref="ToDomain"/>, and <see cref="ToEntity"/> to
/// wire up the concrete <see cref="DbSet{TEntity}"/> and the domain/entity mapping in both directions,
/// and may override <see cref="ApplyIncludes"/> to eager-load navigation properties needed by <see cref="ToDomain"/>.
/// <see cref="ApplicationDbContext"/> uses EF Core's default (tracking) query behavior — it does NOT
/// disable change tracking globally — so <see cref="UpdateEntityAsync"/> goes through explicit
/// <see cref="Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry"/> state assignment (rather than
/// relying on auto-detect-changes against an entity mutated in place) simply because the freshly-mapped
/// entity built from the domain object is always a new, Detached instance, not because tracking itself
/// is off.
/// <para>
/// Flush behavior: when no <see cref="IUnitOfWork"/> transaction is open on the context, each
/// write is persisted immediately (a single-write service method, or a repository integration
/// test, behaves exactly as before). When a transaction <em>is</em> open, the write is left
/// pending so that <see cref="IUnitOfWork.CommitAsync"/> flushes the whole unit of work as one
/// round trip instead of one per repository call.
/// </para>
/// </remarks>
public abstract class GenericRepository<TDomain, TEntity>(ApplicationDbContext context) : IGenericRepository<TDomain>
    where TEntity : class
{
    /// <summary>The shared EF Core context (and so the unit of work's transaction) this repository reads and writes through.</summary>
    protected readonly ApplicationDbContext Context = context;

    /// <summary>The concrete <see cref="DbSet{TEntity}"/> on <see cref="ApplicationDbContext"/> this repository operates on.</summary>
    protected abstract DbSet<TEntity> Set { get; }

    /// <summary>Maps a persisted <typeparamref name="TEntity"/> row to the <typeparamref name="TDomain"/> domain object.</summary>
    protected abstract TDomain ToDomain(TEntity entity);

    /// <summary>Maps a <typeparamref name="TDomain"/> domain object to its <typeparamref name="TEntity"/> persistence representation.</summary>
    protected abstract TEntity ToEntity(TDomain domain);

    /// <summary>
    /// Hook for subclasses to eager-load navigation properties (e.g. via <c>.Include(...)</c>) that
    /// <see cref="ToDomain"/> needs. No-op by default.
    /// </summary>
    protected virtual IQueryable<TEntity> ApplyIncludes(IQueryable<TEntity> query) => query;

    /// <summary>
    /// Compiled getters for <typeparamref name="TEntity"/>'s primary-key properties, built once per
    /// closed generic type and reused for every <see cref="UpdateEntityAsync"/> /
    /// <see cref="DeleteWhereAsync"/> / <see cref="MergeWithPendingChanges"/> call. This replaces a per-call
    /// reflection walk; the key-property list still comes from the EF model (itself cached by EF Core),
    /// only the value extraction is compiled. Assigned via <c>??=</c> — a benign race just rebuilds an
    /// identical delegate array.
    /// </summary>
    private static Func<TEntity, object?>[]? _primaryKeyGetters;

    /// <summary>
    /// Compiles one <c>e =&gt; (object?)e.KeyProperty</c> getter per primary-key property of
    /// <typeparamref name="TEntity"/>, in the EF model's key order.
    /// </summary>
    private Func<TEntity, object?>[] BuildPrimaryKeyGetters()
    {
        var keyProperties = Context.Model.FindEntityType(typeof(TEntity))!.FindPrimaryKey()!.Properties;

        return [.. keyProperties.Select(property =>
        {
            var entity = Expression.Parameter(typeof(TEntity), "e");
            Expression access = Expression.Property(entity, property.PropertyInfo!);
            return Expression.Lambda<Func<TEntity, object?>>(
                Expression.Convert(access, typeof(object)), entity).Compile();
        })];
    }

    /// <summary>Reads <paramref name="entity"/>'s primary-key values (simple or composite) in key order.</summary>
    private object?[] GetPrimaryKeyValues(TEntity entity) =>
        [.. (_primaryKeyGetters ??= BuildPrimaryKeyGetters()).Select(getter => getter(entity))];

    /// <summary>
    /// Persists the context's pending changes unless a <see cref="IUnitOfWork"/> transaction is
    /// currently open — in that case the caller's <see cref="IUnitOfWork.CommitAsync"/> owns the
    /// flush, so every repository call in the unit of work reaches the database as a single batch.
    /// </summary>
    private async Task FlushUnlessInTransactionAsync(CancellationToken ct)
    {
        if (Context.Database.CurrentTransaction is null)
            _ = await Context.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task CreateAsync(TDomain domain, CancellationToken ct = default)
    {
        _ = await Set.AddAsync(ToEntity(domain), ct);
        await FlushUnlessInTransactionAsync(ct);
    }

    /// <inheritdoc />
    public virtual async Task UpdateEntityAsync(TDomain domain, CancellationToken ct = default)
    {
        var newEntity = ToEntity(domain);
        // ToEntity always builds a brand-new instance that this context has never seen, so it starts
        // out Detached regardless of the context's tracking mode. Read the primary key (works for
        // both simple and composite keys) via the compiled per-type getters, then re-find the row
        // via Set.FindAsync, which returns a tracked instance we can then overwrite.
        var keyValues = GetPrimaryKeyValues(newEntity);
        var existing = await Set.FindAsync(keyValues, ct);
        if (existing is not null)
        {
            var entry = Context.Entry(existing);
            if (entry.State == EntityState.Detached) entry.State = EntityState.Unchanged;
            // Copy all scalar values from the mapped domain object onto the tracked entity, then mark
            // the whole entry Modified so SaveChanges issues an UPDATE for every column regardless of
            // which individual values actually changed.
            entry.CurrentValues.SetValues(newEntity);
            entry.State = EntityState.Modified;
            await FlushUnlessInTransactionAsync(ct);
        }
        else
        {
            throw new InvalidOperationException(
                $"Cannot update {typeof(TEntity).Name}: no row found for key ({string.Join(", ", keyValues)}). " +
                "It may have been deleted concurrently.");
        }
    }

    /// <summary>
    /// Runs <paramref name="predicate"/> (with optional <paramref name="orderBy"/>) and maps every matching row to
    /// <typeparamref name="TDomain"/>, including rows this context has added — and excluding rows it has deleted —
    /// but not yet flushed (see <see cref="MergeWithPendingChanges"/>).
    /// </summary>
    /// <param name="predicate">Row filter, translated to SQL (and re-evaluated in memory against pending rows).</param>
    /// <param name="orderBy">Optional ordering, applied in SQL and re-applied after pending rows are merged in.</param>
    /// <param name="noTracking">
    /// Pass <see langword="true"/> to run as <c>AsNoTracking()</c> for a pure-display read that is
    /// never saved back (e.g. a list rendered on a dashboard/grid). Leaves the pending-changes merge
    /// below unaffected -- that reconciles against the context's change tracker, not against this
    /// query's own tracking mode. Defaults to <see langword="false"/> so existing callers keep EF
    /// Core's normal identity-map behavior.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    protected async Task<List<TDomain>> QueryAsync(
        Expression<Func<TEntity, bool>> predicate,
        Func<IQueryable<TEntity>, IQueryable<TEntity>>? orderBy = null,
        bool noTracking = false,
        CancellationToken ct = default)
    {
        var query = ApplyIncludes(Set.Where(predicate));
        if (noTracking) query = query.AsNoTracking();
        if (orderBy != null) query = orderBy(query);
        var dbResults = await query.ToListAsync(ct);
        var merged = MergeWithPendingChanges(dbResults, predicate);
        if (orderBy != null && !ReferenceEquals(merged, dbResults))
            merged = [.. orderBy(merged.AsQueryable())];
        return [.. merged.Select(ToDomain)];
    }

    /// <summary>
    /// Returns every row in <see cref="Set"/> (with optional <paramref name="orderBy"/>), mapped to
    /// <typeparamref name="TDomain"/>. Unlike <see cref="QueryAsync"/> this does not merge in pending,
    /// not-yet-flushed changes — only what the database returns.
    /// </summary>
    /// <param name="orderBy">Optional ordering, applied in SQL.</param>
    /// <param name="noTracking">See the identically-named parameter on <see cref="QueryAsync"/>.</param>
    /// <param name="ct">Cancellation token.</param>
    protected async Task<List<TDomain>> QueryAllAsync(
        Func<IQueryable<TEntity>, IQueryable<TEntity>>? orderBy = null,
        bool noTracking = false,
        CancellationToken ct = default)
    {
        var query = ApplyIncludes(Set.AsQueryable());
        if (noTracking) query = query.AsNoTracking();
        if (orderBy != null) query = orderBy(query);
        return [.. (await query.ToListAsync(ct)).Select(ToDomain)];
    }

    /// <summary>
    /// Returns the first row matching <paramref name="predicate"/> (with optional <paramref name="orderBy"/>)
    /// mapped to <typeparamref name="TDomain"/>, or <see langword="default"/> if none exists. Pending,
    /// not-yet-flushed adds/deletes are merged in the same way as <see cref="QueryAsync"/>.
    /// </summary>
    /// <param name="orderBy">Optional ordering that decides which match is "first".</param>
    /// <param name="noTracking">
    /// Pass <see langword="true"/> to run as <c>AsNoTracking()</c>. Required for a read that must
    /// reflect the row's true current column values — e.g. re-reading a row right after taking a
    /// <c>FOR UPDATE</c> lock on it: a normal tracking query performs identity resolution and hands
    /// back an already-tracked instance from an earlier (pre-lock) read instead of the freshly
    /// queried values, silently defeating the lock's isolation guarantee.
    /// </param>
    /// <param name="predicate">Row filter, translated to SQL (and re-evaluated in memory against pending rows).</param>
    /// <param name="ct">Cancellation token.</param>
    protected async Task<TDomain?> QueryFirstAsync(
        Expression<Func<TEntity, bool>> predicate,
        Func<IQueryable<TEntity>, IQueryable<TEntity>>? orderBy = null,
        bool noTracking = false,
        CancellationToken ct = default)
    {
        var query = ApplyIncludes(Set.Where(predicate));
        if (noTracking) query = query.AsNoTracking();
        if (orderBy != null) query = orderBy(query);
        // Fetched as a list (not FirstOrDefaultAsync's single-row LIMIT 1 SQL) so a pending,
        // not-yet-flushed row from earlier in the same open transaction -- invisible to the DB
        // query below -- can still be merged in and considered for "first" below. Predicates used
        // with this helper are expected to be selective (unique key / small FK-scoped set), so the
        // extra rows fetched are typically zero or few.
        var dbResults = await query.ToListAsync(ct);
        var merged = MergeWithPendingChanges(dbResults, predicate);
        if (orderBy != null && !ReferenceEquals(merged, dbResults))
            merged = [.. orderBy(merged.AsQueryable())];
        var entity = merged.Count > 0 ? merged[0] : null;
        return entity == null ? default : ToDomain(entity);
    }

    /// <summary>
    /// Wraps <paramref name="search"/> as a <c>%...%</c> ILIKE pattern, escaping the LIKE wildcard
    /// characters (<c>%</c>, <c>_</c>) and the escape character itself (<c>\</c>, Postgres's default
    /// LIKE escape) so a search term containing them is matched literally instead of as a wildcard.
    /// Used to build the raw-SQL search predicates that <c>.ToString()</c> can't reach via LINQ
    /// (see <c>ExpenditureRepository.SearchByAccountEmailAsync</c>).
    /// </summary>
    protected static string ToLikePattern(string search) =>
        $"%{search.Replace("\\", @"\\").Replace("%", "\\%").Replace("_", "\\_")}%";

    /// <summary>
    /// Deletes every row matching <paramref name="predicate"/>, including matching rows this context has
    /// added or modified but not yet flushed. Flushed immediately unless a transaction is open.
    /// </summary>
    protected async Task DeleteWhereAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken ct = default)
    {
        // Fetch the matching rows once, explicitly AsNoTracking so this lookup never disturbs
        // entities already tracked in the current unit of work. Set.Remove requires a tracked
        // entity. If this context already has a tracked instance for a given row (e.g. it was
        // just added/queried earlier in the same unit of work), that instance is removed directly
        // — attaching the freshly-fetched no-tracking copy instead would conflict with it (same
        // key, two instances). Otherwise, the fetched row is attached in memory (no DB round trip)
        // before being removed. Both paths avoid the DB round trip that a per-row Set.FindAsync
        // would incur.
        var toRemove = await Set.AsNoTracking().Where(predicate).ToListAsync(ct);
        
        Dictionary<string, TEntity> trackedByKey = Context.ChangeTracker.Entries<TEntity>()
            .GroupBy(e => KeyOf(GetPrimaryKeyValues(e.Entity)))
            .ToDictionary(g => g.Key, g => g.First().Entity);

        foreach (var entity in toRemove)
        {
            if (trackedByKey.TryGetValue(KeyOf(GetPrimaryKeyValues(entity)), out var tracked))
            {
                Set.Remove(tracked);
            }
            else
            {
                Set.Attach(entity);
                Set.Remove(entity);
            }
        }
        
        // Pending rows the database query above cannot see: an Added (never-flushed) match simply
        // stops being inserted, and a Modified match is turned into a delete.
        var predicateFunc = predicate.Compile();
        foreach (var entry in Context.ChangeTracker.Entries<TEntity>().Where(e => e.State == EntityState.Added).ToList().Where(entry => predicateFunc(entry.Entity)))
        {
            entry.State = EntityState.Detached;
        }
        foreach (var entry in Context.ChangeTracker.Entries<TEntity>().Where(e => e.State == EntityState.Modified).ToList().Where(entry => predicateFunc(entry.Entity)))
        {
            entry.State = EntityState.Deleted;
        }

        await FlushUnlessInTransactionAsync(ct);
    }

    /// <summary>
    /// Reconciles <paramref name="dbResults"/> (rows already fetched from the database via a tracking
    /// query) with any pending, not-yet-flushed <see cref="EntityState.Added"/> or
    /// <see cref="EntityState.Deleted"/> changes this context holds for <typeparamref name="TEntity"/>,
    /// so a caller inside an open <see cref="IUnitOfWork"/> transaction sees its own uncommitted
    /// writes. A row Added earlier in the same transaction has its INSERT deferred (see
    /// <see cref="FlushUnlessInTransactionAsync"/>), so <paramref name="dbResults"/> cannot contain it
    /// even when it matches <paramref name="predicate"/> -- it is added here. A row Deleted earlier in
    /// the same transaction has its DELETE likewise deferred, so <paramref name="dbResults"/> can still
    /// contain it -- it is removed here. (A row Modified earlier in the same transaction needs no such
    /// reconciliation for a *tracking* query: EF Core's identity resolution returns the already-tracked,
    /// current-value instance for any row a tracking query's SQL happens to return, which is why this
    /// gap only bites <see cref="DeleteWhereAsync"/>'s explicitly <c>AsNoTracking</c> lookup.)
    /// Returns <paramref name="dbResults"/> itself, unchanged, when nothing of <typeparamref name="TEntity"/>
    /// is pending, so the common case (no open transaction, or no writes yet in this one) pays no
    /// extra cost and callers can tell nothing changed via reference equality.
    /// </summary>
    private List<TEntity> MergeWithPendingChanges(List<TEntity> dbResults, Expression<Func<TEntity, bool>> predicate)
    {
        var pending = Context.ChangeTracker.Entries<TEntity>()
            .Where(e => e.State is EntityState.Added or EntityState.Deleted)
            .ToList();
        if (pending.Count == 0)
            return dbResults;

        var deletedKeys = new HashSet<string>(pending
            .Where(e => e.State == EntityState.Deleted)
            .Select(e => KeyOf(GetPrimaryKeyValues(e.Entity))));

        List<TEntity> merged = [.. dbResults.Where(e => !deletedKeys.Contains(KeyOf(GetPrimaryKeyValues(e))))];

        var predicateFunc = predicate.Compile();
        merged.AddRange(pending
            .Where(e => e.State == EntityState.Added && predicateFunc(e.Entity))
            .Select(e => e.Entity));

        return merged;
    }

    /// <summary>
    /// Collapses primary-key values into one comparable string (each value URI-escaped, joined by
    /// <c>|</c>, so a value containing <c>|</c> cannot make two different composite keys collide).
    /// </summary>
    private static string KeyOf(object?[] keyValues) =>
        string.Join("|", keyValues.Select(v => Uri.EscapeDataString(v?.ToString() ?? "")));
}
