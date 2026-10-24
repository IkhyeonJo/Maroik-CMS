using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Domain.Budgets;

namespace Maroik.Core.Service.Tests.Fixtures;

/// <summary>An in-memory <see cref="IBudgetSheetCellRepository"/>: ids are assigned on create, rows are stored as copies.</summary>
internal sealed class InMemoryBudgetSheetCellRepository : IBudgetSheetCellRepository
{
    /// <summary>The stored cells.</summary>
    private readonly List<BudgetSheetCell> _cells = [];

    /// <summary>When set, every write throws it (an infrastructure failure).</summary>
    public Exception? FailWrites { get; set; }

    /// <summary>A copy of every stored cell.</summary>
    public IReadOnlyList<BudgetSheetCell> All => [.. _cells.Select(Copy)];

    /// <summary>A detached copy.</summary>
    private static BudgetSheetCell Copy(BudgetSheetCell c) => BudgetSheetCell.Reconstitute(c.Id, c.SheetId, c.RowKey, c.Column, c.Input, c.Updated);

    /// <summary>Throws <see cref="FailWrites"/> when set.</summary>
    private void Write()
    {
        if (FailWrites is not null) throw FailWrites;
    }

    /// <inheritdoc />
    public Task CreateAsync(BudgetSheetCell domain, CancellationToken ct = default)
    {
        Write();
        long id = _cells.Count == 0 ? 1 : _cells.Max(c => c.Id) + 1;
        _cells.Add(BudgetSheetCell.Reconstitute(id, domain.SheetId, domain.RowKey, domain.Column, domain.Input, domain.Updated));
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task UpdateEntityAsync(BudgetSheetCell domain, CancellationToken ct = default)
    {
        Write();
        _cells.RemoveAll(c => c.Id == domain.Id);
        _cells.Add(Copy(domain));
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<List<BudgetSheetCell>> GetBySheetIdsAsync(IReadOnlyCollection<long> sheetIds, CancellationToken ct = default) =>
        Task.FromResult<List<BudgetSheetCell>>([.. _cells.Where(c => sheetIds.Contains(c.SheetId)).Select(Copy)]);

    /// <inheritdoc />
    public Task<BudgetSheetCell?> FindAsync(long sheetId, string rowKey, string column, CancellationToken ct = default) =>
        Task.FromResult(_cells.Where(c => c.SheetId == sheetId && c.RowKey == rowKey && c.Column == column).Select(Copy).SingleOrDefault());

    /// <inheritdoc />
    public Task DeleteAsync(long sheetId, string rowKey, string column, CancellationToken ct = default)
    {
        Write();
        _cells.RemoveAll(c => c.SheetId == sheetId && c.RowKey == rowKey && c.Column == column);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DeleteRowAsync(long sheetId, string rowKey, CancellationToken ct = default)
    {
        Write();
        _cells.RemoveAll(c => c.SheetId == sheetId && c.RowKey == rowKey);
        return Task.CompletedTask;
    }
}
