using Maroik.Core.Domain.Budgets;

namespace Maroik.Core.Contract.Interfaces;

/// <summary>Repository abstraction over <see cref="BudgetSheet"/> persistence; every lookup is scoped to the owning account's email.</summary>
public interface IBudgetSheetRepository : IGenericRepository<BudgetSheet>
{
    /// <summary>Every sheet of the account (a few per year at most).</summary>
    Task<List<BudgetSheet>> GetByAccountEmailAsync(string accountEmail, CancellationToken ct = default);

    /// <summary>The account's sheet of a kind and year, its row locked for the current transaction (FOR UPDATE); null when there is none.</summary>
    Task<BudgetSheet?> FindForUpdateAsync(string accountEmail, BudgetKind kind, int year, CancellationToken ct = default);

    /// <summary>Stores a new sheet together with its first lines.</summary>
    Task CreateWithLinesAsync(BudgetSheet sheet, IReadOnlyList<BudgetLine> lines, CancellationToken ct = default);
}
