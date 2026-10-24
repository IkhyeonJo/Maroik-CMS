using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Domain.Budgets;

namespace Maroik.Core.Service.Tests.Fixtures;

/// <summary>An in-memory <see cref="IBudgetSheetRepository"/> that stores a new sheet's lines in <paramref name="lines"/> and records the row locks taken.</summary>
/// <param name="lines">Where a new sheet's lines go.</param>
internal sealed class InMemoryBudgetSheetRepository(InMemoryBudgetSheetLineRepository lines) : IBudgetSheetRepository
{
    /// <summary>The stored sheets.</summary>
    private readonly List<BudgetSheet> _sheets = [];

    /// <summary>A copy of every stored sheet.</summary>
    public IReadOnlyList<BudgetSheet> All => [.. _sheets.Select(Copy)];

    /// <summary>The sheets locked with FOR UPDATE, in order.</summary>
    public List<(string Email, BudgetKind Kind, int Year)> Locks { get; } = [];

    /// <summary>A detached copy.</summary>
    private static BudgetSheet Copy(BudgetSheet s) => BudgetSheet.Reconstitute(s.Id, s.AccountEmail.Value, s.Kind.ToString(), s.Year, s.Created, s.Updated);

    /// <inheritdoc />
    public Task CreateAsync(BudgetSheet domain, CancellationToken ct = default) => CreateWithLinesAsync(domain, [], ct);

    /// <inheritdoc />
    public Task UpdateEntityAsync(BudgetSheet domain, CancellationToken ct = default)
    {
        _sheets.RemoveAll(s => s.Id == domain.Id);
        _sheets.Add(Copy(domain));
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<List<BudgetSheet>> GetByAccountEmailAsync(string accountEmail, CancellationToken ct = default) =>
        Task.FromResult<List<BudgetSheet>>([.. _sheets.Where(s => s.AccountEmail.Value == accountEmail).Select(Copy)]);

    /// <inheritdoc />
    public Task<BudgetSheet?> FindForUpdateAsync(string accountEmail, BudgetKind kind, int year, CancellationToken ct = default)
    {
        Locks.Add((accountEmail, kind, year));
        return Task.FromResult(_sheets.Where(s => s.AccountEmail.Value == accountEmail && s.Kind == kind && s.Year == year).Select(Copy).SingleOrDefault());
    }

    /// <inheritdoc />
    public async Task CreateWithLinesAsync(BudgetSheet sheet, IReadOnlyList<BudgetLine> sheetLines, CancellationToken ct = default)
    {
        long id = _sheets.Count == 0 ? 1 : _sheets.Max(s => s.Id) + 1;
        _sheets.Add(BudgetSheet.Reconstitute(id, sheet.AccountEmail.Value, sheet.Kind.ToString(), sheet.Year, sheet.Created, sheet.Updated));
        foreach (BudgetLine line in sheetLines)
            await lines.CreateAsync(BudgetSheetLine.Create(id, line, sheet.Created), ct);
    }
}
