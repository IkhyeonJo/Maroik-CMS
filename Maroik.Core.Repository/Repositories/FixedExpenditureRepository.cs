using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Domain.Finance;
using Maroik.Core.PostgreSQL.Data;
using Microsoft.EntityFrameworkCore;
using OrmFixedExpenditure = Maroik.Core.PostgreSQL.Models.FixedExpenditure;

namespace Maroik.Core.Repository.Repositories;

/// <summary>
/// EF Core repository for <see cref="FixedExpenditure"/> domain objects,
/// representing recurring monthly expenditures with a scheduled deposit day.
/// Always eager-loads the linked <see cref="OrmFixedExpenditure.AssetNavigation"/> (the
/// PaymentMethod asset, not the MyDepositAsset transfer target) so the amount's monetary unit
/// is available without a separate query.
/// </summary>
public class FixedExpenditureRepository(ApplicationDbContext context)
    : GenericRepository<FixedExpenditure, OrmFixedExpenditure>(context), IFixedExpenditureRepository
{
    /// <summary>The underlying <see cref="DbSet{TEntity}"/> for <see cref="OrmFixedExpenditure"/> rows.</summary>
    protected override DbSet<OrmFixedExpenditure> Set => Context.FixedExpenditures;

    /// <inheritdoc />
    // AssetNavigation (keyed by PaymentMethod) carries the currency the Amount is denominated in.
    // Asset (keyed by MyDepositAsset) is the transfer target and is null for a non-transfer
    // fixed expenditure, so joining that one instead leaves MonetaryUnit blank for the common case.
    protected override IQueryable<OrmFixedExpenditure> ApplyIncludes(IQueryable<OrmFixedExpenditure> query)
        => query.Include(x => x.AssetNavigation);

    /// <summary>Maps a persisted <see cref="OrmFixedExpenditure"/> row to the <see cref="FixedExpenditure"/> domain object.</summary>
    protected override FixedExpenditure ToDomain(OrmFixedExpenditure e) => FixedExpenditure.Reconstitute(
        e.Id, e.AccountEmail, e.MainClass,
        e.SubClass, e.Content, e.Amount, e.AssetNavigation.MonetaryUnit,
        e.PaymentMethod, e.MyDepositAsset, e.DepositMonth, e.DepositDay,
        e.MaturityDate, e.Note, e.Unpunctuality, e.Created, e.Updated);

    /// <summary>Maps a <see cref="FixedExpenditure"/> domain object to its <see cref="OrmFixedExpenditure"/> persistence representation.</summary>
    protected override OrmFixedExpenditure ToEntity(FixedExpenditure fe) => new()
    {
        Id = fe.Id,
        AccountEmail = fe.AccountEmail.Value,
        MainClass = fe.MainClass,
        SubClass = fe.SubClass,
        Content = fe.Content ?? "",
        Amount = fe.Amount.Amount,
        PaymentMethod = fe.PaymentMethod,
        MyDepositAsset = fe.MyDepositAsset,
        DepositMonth = fe.DepositMonth,
        DepositDay = fe.DepositDay,
        MaturityDate = fe.MaturityDate,
        Note = fe.Note ?? "",
        Unpunctuality = fe.Unpunctuality,
        Created = fe.Created,
        Updated = fe.Updated
    };

    /// <inheritdoc />
    public Task<List<FixedExpenditure>> GetByAccountEmailAsync(string accountEmail, CancellationToken ct = default)
        => QueryAsync(e => e.AccountEmail == accountEmail, ct: ct);

    /// <inheritdoc />
    // See ExpenditureRepository.SearchByAccountEmailAsync for why this is raw SQL rather than LINQ.
    public async Task<List<FixedExpenditure>> SearchByAccountEmailAsync(string accountEmail, string search, CancellationToken ct = default)
    {
        string pattern = ToLikePattern(search);
        List<long> matchingIds = await Context.Database.SqlQuery<long>($"""
            SELECT "Id" AS "Value" FROM "FixedExpenditure"
            WHERE "AccountEmail" = {accountEmail}
              AND (
                  "MainClass" ILIKE {pattern} OR
                  "SubClass" ILIKE {pattern} OR
                  "Content" ILIKE {pattern} OR
                  "PaymentMethod" ILIKE {pattern} OR
                  "MyDepositAsset" ILIKE {pattern} OR
                  "Note" ILIKE {pattern} OR
                  CAST("Amount" AS TEXT) ILIKE {pattern} OR
                  CAST("DepositMonth" AS TEXT) ILIKE {pattern} OR
                  CAST("DepositDay" AS TEXT) ILIKE {pattern} OR
                  CAST("MaturityDate" AS TEXT) ILIKE {pattern} OR
                  CAST("Created" AS TEXT) ILIKE {pattern} OR
                  CAST("Updated" AS TEXT) ILIKE {pattern} OR
                  EXISTS (
                      SELECT 1 FROM "Asset"
                      WHERE "Asset"."AccountEmail" = "FixedExpenditure"."AccountEmail"
                        AND "Asset"."ProductName" = "FixedExpenditure"."PaymentMethod"
                        AND "Asset"."MonetaryUnit" ILIKE {pattern}
                  )
              )
            """).ToListAsync(ct);

        if (matchingIds.Count == 0) return [];
        return await QueryAsync(e => matchingIds.Contains(e.Id), ct: ct);
    }

    /// <inheritdoc />
    public Task<FixedExpenditure?> FindByEmailAndIdAsync(string accountEmail, long id, CancellationToken ct = default)
        => QueryFirstAsync(e => e.AccountEmail == accountEmail && e.Id == id, ct: ct);

    /// <inheritdoc />
    public Task DeleteByIdAsync(long id, CancellationToken ct = default)
        => DeleteWhereAsync(e => e.Id == id, ct);
}
