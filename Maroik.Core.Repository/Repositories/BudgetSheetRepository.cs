using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Domain.Budgets;
using Maroik.Core.PostgreSQL.Contexts;
using Microsoft.EntityFrameworkCore;
using OrmBudgetSheet = Maroik.Core.PostgreSQL.Models.BudgetSheet;
using OrmBudgetSheetLine = Maroik.Core.PostgreSQL.Models.BudgetSheetLine;

namespace Maroik.Core.Repository.Repositories;

/// <summary>
/// EF Core repository for <see cref="BudgetSheet"/> domain objects. Every lookup is filtered by the owning account's email; the row lock
/// serializes the edits of one sheet.
/// </summary>
public class BudgetSheetRepository(ApplicationDbContext context)
    : GenericRepository<BudgetSheet, OrmBudgetSheet>(context), IBudgetSheetRepository
{
    /// <summary>The underlying <see cref="DbSet{TEntity}"/> for <see cref="OrmBudgetSheet"/> rows.</summary>
    protected override DbSet<OrmBudgetSheet> Set => Context.BudgetSheets;

    /// <summary>Maps a persisted row to the domain object.</summary>
    protected override BudgetSheet ToDomain(OrmBudgetSheet e) =>
        BudgetSheet.Reconstitute(e.Id, e.AccountEmail, e.Kind, e.Year, e.Created, e.Updated);

    /// <summary>Maps the domain object to its row.</summary>
    protected override OrmBudgetSheet ToEntity(BudgetSheet sheet) => new()
    {
        Id = sheet.Id,
        AccountEmail = sheet.AccountEmail.Value,
        Kind = sheet.Kind.ToString(),
        Year = sheet.Year,
        Created = sheet.Created,
        Updated = sheet.Updated
    };

    /// <inheritdoc />
    public Task<List<BudgetSheet>> GetByAccountEmailAsync(string accountEmail, CancellationToken ct = default)
        => QueryAsync(e => e.AccountEmail == accountEmail, noTracking: true, ct: ct);

    /// <inheritdoc />
    public async Task<BudgetSheet?> FindForUpdateAsync(string accountEmail, BudgetKind kind, int year, CancellationToken ct = default)
    {
        // Raw SQL because EF Core LINQ cannot emit FOR UPDATE; AsNoTracking so a row this context already tracks is not handed back
        // instead of the row the lock just read (as AccountRepository.FindByEmailForUpdateAsync).
        string kindName = kind.ToString();
        var entities = await Set.FromSqlInterpolated(
            $"""
             SELECT *
             FROM "BudgetSheet"
             WHERE "AccountEmail" = {accountEmail} AND "Kind" = {kindName} AND "Year" = {year}
             FOR UPDATE
             """).AsNoTracking().ToListAsync(ct);
        return entities.Count == 0 ? null : ToDomain(entities[0]);
    }

    /// <inheritdoc />
    public async Task CreateWithLinesAsync(BudgetSheet sheet, IReadOnlyList<BudgetLine> lines, CancellationToken ct = default)
    {
        // The lines hang off the new sheet row, so EF Core fills in their SheetId when it inserts the sheet.
        OrmBudgetSheet entity = ToEntity(sheet);
        foreach (BudgetLine line in lines)
            entity.BudgetSheetLines.Add(new OrmBudgetSheetLine { MainClass = line.MainClass, SubClass = line.SubClass, MonetaryUnit = line.Currency, Created = sheet.Created });
        _ = await Set.AddAsync(entity, ct);
        if (Context.Database.CurrentTransaction is null)
            _ = await Context.SaveChangesAsync(ct);
    }
}
