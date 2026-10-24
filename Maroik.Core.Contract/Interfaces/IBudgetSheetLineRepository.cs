using Maroik.Core.Domain.Budgets;

namespace Maroik.Core.Contract.Interfaces;

/// <summary>Repository abstraction over <see cref="BudgetSheetLine"/> persistence (the caller has checked the sheet's owner).</summary>
public interface IBudgetSheetLineRepository : IGenericRepository<BudgetSheetLine>
{
    /// <summary>The lines of the given sheets.</summary>
    Task<List<BudgetSheetLine>> GetBySheetIdsAsync(IReadOnlyCollection<long> sheetIds, CancellationToken ct = default);

    /// <summary>Deletes a sheet's line.</summary>
    Task DeleteAsync(long sheetId, BudgetLine line, CancellationToken ct = default);
}
