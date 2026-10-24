using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Domain.Budgets;

namespace Maroik.Core.Service.Tests.Fixtures;

/// <summary>An in-memory <see cref="IBudgetSheetLineRepository"/>: ids are assigned on create, rows are stored as copies.</summary>
internal sealed class InMemoryBudgetSheetLineRepository : IBudgetSheetLineRepository
{
    /// <summary>The stored lines.</summary>
    private readonly List<BudgetSheetLine> _lines = [];

    /// <summary>A copy of every stored line.</summary>
    public IReadOnlyList<BudgetSheetLine> All => [.. _lines.Select(Copy)];

    /// <summary>A detached copy.</summary>
    private static BudgetSheetLine Copy(BudgetSheetLine l) => BudgetSheetLine.Reconstitute(l.Id, l.SheetId, l.Line.MainClass, l.Line.SubClass, l.Line.Currency, l.Created);

    /// <inheritdoc />
    public Task CreateAsync(BudgetSheetLine domain, CancellationToken ct = default)
    {
        long id = _lines.Count == 0 ? 1 : _lines.Max(l => l.Id) + 1;
        _lines.Add(BudgetSheetLine.Reconstitute(id, domain.SheetId, domain.Line.MainClass, domain.Line.SubClass, domain.Line.Currency, domain.Created));
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task UpdateEntityAsync(BudgetSheetLine domain, CancellationToken ct = default) => throw new NotSupportedException("Lines are never updated.");

    /// <inheritdoc />
    public Task<List<BudgetSheetLine>> GetBySheetIdsAsync(IReadOnlyCollection<long> sheetIds, CancellationToken ct = default) =>
        Task.FromResult<List<BudgetSheetLine>>([.. _lines.Where(l => sheetIds.Contains(l.SheetId)).Select(Copy)]);

    /// <inheritdoc />
    public Task DeleteAsync(long sheetId, BudgetLine line, CancellationToken ct = default)
    {
        _lines.RemoveAll(l => l.SheetId == sheetId && l.Line == line);
        return Task.CompletedTask;
    }
}
