using Maroik.Core.Domain.Budgets;

namespace Maroik.Core.Contract.Interfaces;

/// <summary>Repository abstraction over <see cref="BudgetSheetCell"/> persistence (the caller has checked the sheet's owner).</summary>
public interface IBudgetSheetCellRepository : IGenericRepository<BudgetSheetCell>
{
    /// <summary>The cell inputs of the given sheets.</summary>
    Task<List<BudgetSheetCell>> GetBySheetIdsAsync(IReadOnlyCollection<long> sheetIds, CancellationToken ct = default);

    /// <summary>The input of one cell, or null.</summary>
    Task<BudgetSheetCell?> FindAsync(long sheetId, string rowKey, string column, CancellationToken ct = default);

    /// <summary>Deletes one cell's input.</summary>
    Task DeleteAsync(long sheetId, string rowKey, string column, CancellationToken ct = default);

    /// <summary>Deletes every input of a row (its line was removed).</summary>
    Task DeleteRowAsync(long sheetId, string rowKey, CancellationToken ct = default);
}
