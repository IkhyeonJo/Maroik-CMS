using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Domain.Finance;
using Maroik.Core.PostgreSQL.Data;
using Microsoft.EntityFrameworkCore;
using OrmExpenditure = Maroik.Core.PostgreSQL.Models.Expenditure;

namespace Maroik.Core.Repository.Repositories;

/// <summary>
/// EF Core repository for <see cref="Expenditure"/> domain objects.
/// Always eager-loads the linked <see cref="OrmExpenditure.AssetNavigation"/> (the PaymentMethod
/// asset, not the MyDepositAsset transfer target) so the amount's monetary unit is available
/// without a separate query.
/// </summary>
public class ExpenditureRepository(ApplicationDbContext context)
    : GenericRepository<Expenditure, OrmExpenditure>(context), IExpenditureRepository
{
    /// <summary>The underlying <see cref="DbSet{TEntity}"/> for <see cref="OrmExpenditure"/> rows.</summary>
    protected override DbSet<OrmExpenditure> Set => Context.Expenditures;

    /// <inheritdoc />
    // AssetNavigation (keyed by PaymentMethod) carries the currency the Amount is denominated in.
    // Asset (keyed by MyDepositAsset) is the transfer target and is null for a non-transfer
    // expenditure, so joining that one instead leaves MonetaryUnit blank for the common case.
    protected override IQueryable<OrmExpenditure> ApplyIncludes(IQueryable<OrmExpenditure> query)
        => query.Include(x => x.AssetNavigation);

    /// <summary>Maps a persisted <see cref="OrmExpenditure"/> row to the <see cref="Expenditure"/> domain object.</summary>
    protected override Expenditure ToDomain(OrmExpenditure e) => Expenditure.Reconstitute(
        e.Id, e.AccountEmail, e.MainClass,
        e.SubClass, e.Content, e.Amount, e.AssetNavigation.MonetaryUnit,
        e.PaymentMethod, e.MyDepositAsset, e.Note,
        e.Created, e.Updated);

    /// <summary>Maps an <see cref="Expenditure"/> domain object to its <see cref="OrmExpenditure"/> persistence representation.</summary>
    protected override OrmExpenditure ToEntity(Expenditure e) => new()
    {
        Id = e.Id,
        AccountEmail = e.AccountEmail.Value,
        MainClass = e.MainClass,
        SubClass = e.SubClass,
        Content = e.Content ?? "",
        Amount = e.Amount.Amount,
        PaymentMethod = e.PaymentMethod,
        MyDepositAsset = e.MyDepositAsset,
        Note = e.Note ?? "",
        Created = e.Created,
        Updated = e.Updated
    };

    /// <inheritdoc />
    public Task<List<Expenditure>> GetByAccountEmailAsync(string accountEmail, CancellationToken ct = default)
        => QueryAsync(e => e.AccountEmail == accountEmail, ct: ct);

    /// <inheritdoc />
    // Raw SQL is required: matching Amount/Created/Updated against search text needs a Postgres-side
    // CAST(... AS TEXT), which plain LINQ `.ToString()` does not reliably translate to SQL (EF Core
    // never shipped a general ToString-to-CAST translator for arbitrary types — see
    // dotnet/efcore#18329, closed as not planned). ILIKE is case-insensitive natively. Amount/Created/
    // Updated match their raw (untrimmed, UTC) text form, not the trimmed/timezone-converted string
    // the grid displays. Two-step (collect matching IDs, then re-fetch via the normal included path)
    // matches this repository's own FindByEmailAndIdForUpdateAsync pattern, since Include() is not
    // reliably composable on top of a SqlQuery root.
    public async Task<List<Expenditure>> SearchByAccountEmailAsync(string accountEmail, string search, CancellationToken ct = default)
    {
        string pattern = ToLikePattern(search);
        List<long> matchingIds = await Context.Database.SqlQuery<long>($"""
            SELECT "Id" AS "Value" FROM "Expenditure"
            WHERE "AccountEmail" = {accountEmail}
              AND (
                  "MainClass" ILIKE {pattern} OR
                  "SubClass" ILIKE {pattern} OR
                  "Content" ILIKE {pattern} OR
                  "PaymentMethod" ILIKE {pattern} OR
                  "MyDepositAsset" ILIKE {pattern} OR
                  "Note" ILIKE {pattern} OR
                  CAST("Amount" AS TEXT) ILIKE {pattern} OR
                  CAST("Created" AS TEXT) ILIKE {pattern} OR
                  CAST("Updated" AS TEXT) ILIKE {pattern} OR
                  EXISTS (
                      SELECT 1 FROM "Asset"
                      WHERE "Asset"."AccountEmail" = "Expenditure"."AccountEmail"
                        AND "Asset"."ProductName" = "Expenditure"."PaymentMethod"
                        AND "Asset"."MonetaryUnit" ILIKE {pattern}
                  )
              )
            """).ToListAsync(ct);

        if (matchingIds.Count == 0) return [];
        return await QueryAsync(e => matchingIds.Contains(e.Id), ct: ct);
    }

    /// <inheritdoc />
    public Task<Expenditure?> FindByEmailAndIdAsync(string accountEmail, long id, CancellationToken ct = default)
        => QueryFirstAsync(e => e.AccountEmail == accountEmail && e.Id == id, ct: ct);

    /// <inheritdoc />
    public async Task<Expenditure?> FindByEmailAndIdForUpdateAsync(string accountEmail, long id, CancellationToken ct = default)
    {
        // Raw SQL is required because EF Core LINQ cannot emit FOR UPDATE; ToListAsync on the raw
        // root runs the statement verbatim so FOR UPDATE stays at the top level. AsNoTracking so the
        // lock read is never satisfied from a stale already-tracked instance. This first call only
        // takes the lock — nothing is tracked here.
        var locked = await Set.FromSqlInterpolated(
            $"""
             SELECT *
             FROM "Expenditure"
             WHERE "AccountEmail" = {accountEmail} AND "Id" = {id}
             LIMIT 1
             FOR UPDATE
             """).AsNoTracking().ToListAsync(ct);

        if (locked.Count == 0)
            return null;

        // Re-read through the normal included path so the returned domain object carries the
        // monetary unit from the AssetNavigation. The FOR UPDATE lock above is held until the
        // ambient transaction ends, so this second read sees the same committed row. Must stay
        // AsNoTracking (noTracking: true), not delegate to FindByEmailAndIdAsync's tracking query:
        // if this row is already tracked in the context from an earlier read in the same unit of
        // work, a tracking query's identity resolution would hand back that pre-lock instance
        // instead of the just-locked row's values, defeating the lock.
        return await QueryFirstAsync(e => e.AccountEmail == accountEmail && e.Id == id, ct: ct, noTracking: true);
    }

    /// <inheritdoc />
    public Task<List<Expenditure>> GetByAccountEmailAndDateRangeAsync(string accountEmail, DateTime from, DateTime to, CancellationToken ct = default)
        // Pure-display read (dashboard yearly/monthly totals and breakdowns, never saved back):
        // AsNoTracking skips the change-tracking overhead this method has no use for.
        => QueryAsync(e => e.AccountEmail == accountEmail && e.Created >= from && e.Created < to, noTracking: true, ct: ct);

    /// <inheritdoc />
    public Task<Expenditure?> GetFirstByAccountEmailOrderedByCreatedAsync(string accountEmail, CancellationToken ct = default)
        => QueryFirstAsync(e => e.AccountEmail == accountEmail, orderBy: q => q.OrderBy(e => e.Created), ct:ct);

    /// <inheritdoc />
    public Task DeleteByIdAsync(long id, CancellationToken ct = default)
        => DeleteWhereAsync(e => e.Id == id, ct);
}
