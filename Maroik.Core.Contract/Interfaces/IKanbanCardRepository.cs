using Maroik.Core.Domain.Kanbans;
namespace Maroik.Core.Contract.Interfaces;

/// <summary>
/// Repository abstraction over <see cref="KanbanCard"/> persistence. Every lookup and delete is scoped to the owning account's email,
/// so another user's card id behaves as "not found" (K2).
/// </summary>
public interface IKanbanCardRepository : IGenericRepository<KanbanCard>
{
    /// <summary>Returns every card of the given account (unordered; <c>KanbanBoardPolicy</c> lays them out).</summary>
    Task<List<KanbanCard>> GetByAccountEmailAsync(string accountEmail, CancellationToken ct = default);

    /// <summary>Returns the card with the given ID when it belongs to the given account, or null otherwise.</summary>
    Task<KanbanCard?> FindByEmailAndIdAsync(string accountEmail, long id, CancellationToken ct = default);

    /// <summary>Deletes the card with the given ID when it belongs to the given account (another account's card is left alone).</summary>
    Task DeleteByEmailAndIdAsync(string accountEmail, long id, CancellationToken ct = default);
}
