using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Domain.Budgets;
using Maroik.Core.PostgreSQL.Contexts;
using Microsoft.EntityFrameworkCore;
using OrmBudgetSheetLine = Maroik.Core.PostgreSQL.Models.BudgetSheetLine;

namespace Maroik.Core.Repository.Repositories;

/// <summary>EF Core repository for <see cref="BudgetSheetLine"/> domain objects.</summary>
public class BudgetSheetLineRepository(ApplicationDbContext context)
    : GenericRepository<BudgetSheetLine, OrmBudgetSheetLine>(context), IBudgetSheetLineRepository
{
    /// <summary>The underlying <see cref="DbSet{TEntity}"/> for <see cref="OrmBudgetSheetLine"/> rows.</summary>
    protected override DbSet<OrmBudgetSheetLine> Set => Context.BudgetSheetLines;

    /// <summary>Maps a persisted row to the domain object.</summary>
    protected override BudgetSheetLine ToDomain(OrmBudgetSheetLine e) =>
        BudgetSheetLine.Reconstitute(e.Id, e.SheetId, e.MainClass, e.SubClass, e.MonetaryUnit, e.Created);

    /// <summary>Maps the domain object to its row.</summary>
    protected override OrmBudgetSheetLine ToEntity(BudgetSheetLine line) => new()
    {
        Id = line.Id,
        SheetId = line.SheetId,
        MainClass = line.Line.MainClass,
        SubClass = line.Line.SubClass,
        MonetaryUnit = line.Line.Currency,
        Created = line.Created
    };

    /// <inheritdoc />
    public Task<List<BudgetSheetLine>> GetBySheetIdsAsync(IReadOnlyCollection<long> sheetIds, CancellationToken ct = default)
        => QueryAsync(e => sheetIds.Contains(e.SheetId), noTracking: true, ct: ct);

    /// <inheritdoc />
    public Task DeleteAsync(long sheetId, BudgetLine line, CancellationToken ct = default)
        => DeleteWhereAsync(e => e.SheetId == sheetId && e.MainClass == line.MainClass && e.SubClass == line.SubClass && e.MonetaryUnit == line.Currency, ct);
}
