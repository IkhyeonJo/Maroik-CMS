using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Domain.Finance;
using Maroik.Core.PostgreSQL.Data;
using Microsoft.EntityFrameworkCore;
using OrmAsset = Maroik.Core.PostgreSQL.Models.Asset;

namespace Maroik.Core.Repository.Repositories;

/// <summary>
/// EF Core repository for <see cref="Asset"/> domain objects.
/// Includes a raw SQL update method for cases where the composite primary key (ProductName, AccountEmail) changes.
/// </summary>
public class AssetRepository(ApplicationDbContext context)
    : GenericRepository<Asset, OrmAsset>(context), IAssetRepository
{
    /// <summary>The underlying <see cref="DbSet{TEntity}"/> for <see cref="OrmAsset"/> rows.</summary>
    protected override DbSet<OrmAsset> Set => Context.Assets;

    /// <inheritdoc />
    public async Task<int> UpdateAssetWithProductNameAsync(Asset asset, string originalProductName, CancellationToken ct = default)
    {
        // Raw SQL is required because the composite PK (ProductName, AccountEmail) changes.
        return await Context.Database.ExecuteSqlInterpolatedAsync(
            $"""
             UPDATE "Asset"
             SET
                 "ProductName" = {asset.ProductName},
                 "Item" = {asset.Item},
                 "Amount" = {asset.Balance.Amount},
                 "MonetaryUnit" = {asset.Balance.Currency},
                 "Note" = {asset.Note ?? ""},
                 "Deleted" = {asset.Deleted},
                 "Updated" = {asset.Updated}
             WHERE
                 "ProductName" = {originalProductName}
                 AND "AccountEmail" = {asset.AccountEmail.Value}
             """, ct);
    }

    /// <summary>Maps a persisted <see cref="OrmAsset"/> row to the <see cref="Asset"/> domain object.</summary>
    protected override Asset ToDomain(OrmAsset e) => Asset.Reconstitute(
        e.ProductName, e.AccountEmail, e.Item,
        e.Amount, e.MonetaryUnit, e.Note, e.Deleted, e.Created, e.Updated);

    /// <summary>Maps an <see cref="Asset"/> domain object to its <see cref="OrmAsset"/> persistence representation.</summary>
    protected override OrmAsset ToEntity(Asset a) => new()
    {
        ProductName = a.ProductName,
        AccountEmail = a.AccountEmail.Value,
        Item = a.Item,
        Amount = a.Balance.Amount,
        MonetaryUnit = a.Balance.Currency,
        Note = a.Note ?? "",
        Deleted = a.Deleted,
        Created = a.Created,
        Updated = a.Updated
    };

    /// <inheritdoc />
    public Task<List<Asset>> GetByAccountEmailAsync(string accountEmail, CancellationToken ct = default)
        => QueryAsync(e => e.AccountEmail == accountEmail, ct: ct);

    /// <inheritdoc />
    // See ExpenditureRepository.SearchByAccountEmailAsync for why this is raw SQL rather than LINQ
    // (ILIKE is case-insensitive natively, so it also covers the Deleted/bool matching).
    // Asset's key is (AccountEmail, ProductName), not a single ID, so the two-step match collects
    // ProductName instead.
    public async Task<List<Asset>> SearchByAccountEmailAsync(string accountEmail, string search, CancellationToken ct = default)
    {
        string pattern = ToLikePattern(search);
        List<string> matchingProductNames = await Context.Database.SqlQuery<string>($"""
            SELECT "ProductName" AS "Value" FROM "Asset"
            WHERE "AccountEmail" = {accountEmail}
              AND (
                  "ProductName" ILIKE {pattern} OR
                  "Item" ILIKE {pattern} OR
                  "MonetaryUnit" ILIKE {pattern} OR
                  "Note" ILIKE {pattern} OR
                  CAST("Amount" AS TEXT) ILIKE {pattern} OR
                  CAST("Created" AS TEXT) ILIKE {pattern} OR
                  CAST("Updated" AS TEXT) ILIKE {pattern} OR
                  CAST("Deleted" AS TEXT) ILIKE {pattern}
              )
            """).ToListAsync(ct);

        if (matchingProductNames.Count == 0) return [];
        return await QueryAsync(e => e.AccountEmail == accountEmail && matchingProductNames.Contains(e.ProductName), ct: ct);
    }

    /// <inheritdoc />
    public Task<Asset?> FindByEmailAndProductNameAsync(string accountEmail, string productName, CancellationToken ct = default)
        => QueryFirstAsync(e => e.AccountEmail == accountEmail && e.ProductName == productName, ct: ct);

    /// <inheritdoc />
    public async Task<Asset?> FindByEmailAndProductNameForUpdateAsync(string accountEmail, string productName, CancellationToken ct = default)
    {
        // Raw SQL is required because EF Core LINQ cannot emit FOR UPDATE.
        // ToListAsync on the raw root executes the SQL verbatim (no composition),
        // keeping FOR UPDATE at the top level of the statement.
        // AsNoTracking is required too: if this Asset's row was already tracked earlier in the
        // same DbContext scope (e.g. loaded via an Include on an unrelated query), a tracking
        // query performs identity resolution and hands back that already-tracked (pre-lock)
        // instance instead of the row this FOR UPDATE just (re-)read — silently defeating the
        // lock's whole purpose. AsNoTracking always materializes a fresh instance from what was
        // actually just selected, bypassing that snap-back.
        var entities = await Set.FromSqlInterpolated(
            $"""
             SELECT *
             FROM "Asset"
             WHERE
                 "AccountEmail" = {accountEmail}
                 AND "ProductName" = {productName}
             LIMIT 1
             FOR UPDATE
             """).AsNoTracking().ToListAsync(ct);

        var entity = entities.FirstOrDefault();
        return entity == null ? null : ToDomain(entity);
    }

    /// <inheritdoc />
    public async Task<List<Asset>> FindByEmailAndProductNamesForUpdateAsync(string accountEmail, IReadOnlyCollection<string> productNames, CancellationToken ct = default)
    {
        if (productNames.Count == 0) return [];

        // Same rationale as FindByEmailAndProductNameForUpdateAsync (raw SQL for "FOR UPDATE",
        // AsNoTracking to bypass identity-resolution snap-back to a pre-lock tracked instance).
        // ORDER BY "ProductName" locks every matched row in the same order a caller that already
        // sorts its requested names (see AssetBalanceOrdinalLockExtensions) would have locked them
        // one at a time, so batching here doesn't change the deadlock-avoidance guarantee -- the
        // same ORDER BY + FOR UPDATE technique is what main's GetAssetsForUpdateAsync has used in
        // production for exactly this reason since it was added to fix a lost-update bug.
        var entities = await Set.FromSqlInterpolated(
            $"""
             SELECT *
             FROM "Asset"
             WHERE
                 "AccountEmail" = {accountEmail}
                 AND "ProductName" = ANY({productNames.ToArray()})
             ORDER BY "ProductName"
             FOR UPDATE
             """).AsNoTracking().ToListAsync(ct);

        return [.. entities.Select(ToDomain)];
    }
}
