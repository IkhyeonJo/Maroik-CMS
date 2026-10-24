using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Domain.Budgets;
using Maroik.Core.PostgreSQL.Contexts;
using Microsoft.EntityFrameworkCore;
using OrmBudgetSheetCell = Maroik.Core.PostgreSQL.Models.BudgetSheetCell;

namespace Maroik.Core.Repository.Repositories;

/// <summary>EF Core repository for <see cref="BudgetSheetCell"/> domain objects.</summary>
public class BudgetSheetCellRepository(ApplicationDbContext context)
    : GenericRepository<BudgetSheetCell, OrmBudgetSheetCell>(context), IBudgetSheetCellRepository
{
    /// <summary>The underlying <see cref="DbSet{TEntity}"/> for <see cref="OrmBudgetSheetCell"/> rows.</summary>
    protected override DbSet<OrmBudgetSheetCell> Set => Context.BudgetSheetCells;

    /// <summary>Maps a persisted row to the domain object.</summary>
    protected override BudgetSheetCell ToDomain(OrmBudgetSheetCell e) =>
        BudgetSheetCell.Reconstitute(e.Id, e.SheetId, e.RowKey, e.Column, e.Input, e.Updated);

    /// <summary>Maps the domain object to its row.</summary>
    protected override OrmBudgetSheetCell ToEntity(BudgetSheetCell cell) => new()
    {
        Id = cell.Id,
        SheetId = cell.SheetId,
        RowKey = cell.RowKey,
        Column = cell.Column,
        Input = cell.Input,
        Updated = cell.Updated
    };

    /// <inheritdoc />
    public Task<List<BudgetSheetCell>> GetBySheetIdsAsync(IReadOnlyCollection<long> sheetIds, CancellationToken ct = default)
        => QueryAsync(e => sheetIds.Contains(e.SheetId), noTracking: true, ct: ct);

    /// <inheritdoc />
    public Task<BudgetSheetCell?> FindAsync(long sheetId, string rowKey, string column, CancellationToken ct = default)
        => QueryFirstAsync(e => e.SheetId == sheetId && e.RowKey == rowKey && e.Column == column, ct: ct);

    /// <inheritdoc />
    public Task DeleteAsync(long sheetId, string rowKey, string column, CancellationToken ct = default)
        => DeleteWhereAsync(e => e.SheetId == sheetId && e.RowKey == rowKey && e.Column == column, ct);

    /// <inheritdoc />
    public Task DeleteRowAsync(long sheetId, string rowKey, CancellationToken ct = default)
        => DeleteWhereAsync(e => e.SheetId == sheetId && e.RowKey == rowKey, ct);
}
