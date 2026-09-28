using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Domain.Finance;
using Maroik.Core.PostgreSQL.Data;
using Microsoft.EntityFrameworkCore;
using OrmIncome = Maroik.Core.PostgreSQL.Models.Income;

namespace Maroik.Core.Repository.Repositories;

/// <summary>
/// EF Core repository for <see cref="Income"/> domain objects.
/// Always eager-loads the linked <see cref="OrmIncome.Asset"/> so that the monetary unit
/// is available without a separate query.
/// </summary>
public class IncomeRepository(ApplicationDbContext context)
    : GenericRepository<Income, OrmIncome>(context), IIncomeRepository
{
    /// <summary>The underlying <see cref="DbSet{TEntity}"/> for <see cref="OrmIncome"/> rows.</summary>
    protected override DbSet<OrmIncome> Set => Context.Incomes;

    /// <inheritdoc />
    /// <inheritdoc />
    // Always join the Asset navigation property so query results carry the monetary unit.
    protected override IQueryable<OrmIncome> ApplyIncludes(IQueryable<OrmIncome> query)
        => query.Include(x => x.Asset);

    /// <summary>Maps a persisted <see cref="OrmIncome"/> row to the <see cref="Income"/> domain object.</summary>
    protected override Income ToDomain(OrmIncome e) => Income.Reconstitute(
        e.Id, e.AccountEmail, e.MainClass,
        e.SubClass, e.Content, e.Amount, e.Asset.MonetaryUnit,
        e.DepositMyAssetProductName, e.Note, e.Created, e.Updated);

    /// <summary>Maps an <see cref="Income"/> domain object to its <see cref="OrmIncome"/> persistence representation.</summary>
    protected override OrmIncome ToEntity(Income i) => new()
    {
        Id = i.Id,
        AccountEmail = i.AccountEmail.Value,
        MainClass = i.MainClass,
        SubClass = i.SubClass,
        Content = i.Content ?? "",
        Amount = i.Amount.Amount,
        DepositMyAssetProductName = i.DepositMyAssetProductName,
        Note = i.Note ?? "",
        Created = i.Created,
        Updated = i.Updated
    };

    /// <inheritdoc />
    public Task<List<Income>> GetByAccountEmailAsync(string accountEmail, CancellationToken ct = default)
        => QueryAsync(e => e.AccountEmail == accountEmail, ct: ct);

    /// <inheritdoc />
    // See ExpenditureRepository.SearchByAccountEmailAsync for why this is raw SQL rather than LINQ.
    public async Task<List<Income>> SearchByAccountEmailAsync(string accountEmail, string search, CancellationToken ct = default)
    {
        string pattern = ToLikePattern(search);
        List<long> matchingIds = await Context.Database.SqlQuery<long>($"""
            SELECT "Id" AS "Value" FROM "Income"
            WHERE "AccountEmail" = {accountEmail}
              AND (
                  "MainClass" ILIKE {pattern} OR
                  "SubClass" ILIKE {pattern} OR
                  "Content" ILIKE {pattern} OR
                  "DepositMyAssetProductName" ILIKE {pattern} OR
                  "Note" ILIKE {pattern} OR
                  CAST("Amount" AS TEXT) ILIKE {pattern} OR
                  CAST("Created" AS TEXT) ILIKE {pattern} OR
                  CAST("Updated" AS TEXT) ILIKE {pattern} OR
                  EXISTS (
                      SELECT 1 FROM "Asset"
                      WHERE "Asset"."AccountEmail" = "Income"."AccountEmail"
                        AND "Asset"."ProductName" = "Income"."DepositMyAssetProductName"
                        AND "Asset"."MonetaryUnit" ILIKE {pattern}
                  )
              )
            """).ToListAsync(ct);

        if (matchingIds.Count == 0) return [];
        return await QueryAsync(e => matchingIds.Contains(e.Id), ct: ct);
    }

    /// <inheritdoc />
    public Task<Income?> FindByEmailAndIdAsync(string accountEmail, long id, CancellationToken ct = default)
        => QueryFirstAsync(e => e.AccountEmail == accountEmail && e.Id == id, ct: ct);

    /// <inheritdoc />
    public async Task<Income?> FindByEmailAndIdForUpdateAsync(string accountEmail, long id, CancellationToken ct = default)
    {
        // Raw SQL is required because EF Core LINQ cannot emit FOR UPDATE; ToListAsync on the raw
        // root runs the statement verbatim so FOR UPDATE stays at the top level. AsNoTracking so the
        // lock read is never satisfied from a stale already-tracked instance. This first call only
        // takes the lock — nothing is tracked here.
        var locked = await Set.FromSqlInterpolated(
            $"""
             SELECT *
             FROM "Income"
             WHERE "AccountEmail" = {accountEmail} AND "Id" = {id}
             LIMIT 1
             FOR UPDATE
             """).AsNoTracking().ToListAsync(ct);

        if (locked.Count == 0)
            return null;

        // Re-read through the normal included path so the returned domain object carries the
        // monetary unit from the Asset navigation. The FOR UPDATE lock above is held until the
        // ambient transaction ends, so this second read sees the same committed row. Must stay
        // AsNoTracking (noTracking: true), not delegate to FindByEmailAndIdAsync's tracking query:
        // if this row is already tracked in the context from an earlier read in the same unit of
        // work, a tracking query's identity resolution would hand back that pre-lock instance
        // instead of the just-locked row's values, defeating the lock.
        return await QueryFirstAsync(e => e.AccountEmail == accountEmail && e.Id == id, ct: ct, noTracking: true);
    }

    /// <inheritdoc />
    public Task<List<Income>> GetByAccountEmailAndDateRangeAsync(string accountEmail, DateTime from, DateTime to, CancellationToken ct = default)
        // Pure-display read (dashboard yearly/monthly totals and breakdowns, never saved back):
        // AsNoTracking skips the change-tracking overhead this method has no use for.
        => QueryAsync(e => e.AccountEmail == accountEmail && e.Created >= from && e.Created < to, noTracking: true, ct: ct);

    /// <inheritdoc />
    public Task<Income?> GetFirstByAccountEmailOrderedByCreatedAsync(string accountEmail, CancellationToken ct = default)
        => QueryFirstAsync(e => e.AccountEmail == accountEmail, orderBy: q => q.OrderBy(e => e.Created), ct: ct);

    /// <inheritdoc />
    public Task DeleteByIdAsync(long id, CancellationToken ct = default)
        => DeleteWhereAsync(e => e.Id == id, ct);
}
