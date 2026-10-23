using Maroik.Core.Contract.Dtos;

namespace Maroik.Core.Contract.Interfaces;

/// <summary>
/// Service interface for the personal Kanban board: one board per user, laid out as the four fixed lanes, and the card writes.
/// Every operation is scoped to the given account; another account's card id is answered as "not found" (K2).
/// </summary>
public interface IKanbanService
{
    /// <summary>
    /// Returns the account's board: the four lanes in order, each with its cards, and each card's overdue flag decided in
    /// <paramref name="timeZoneIanaId"/> (the owner's time zone).
    /// </summary>
    Task<List<KanbanLaneResponse>> GetBoardAsync(string accountEmail, string timeZoneIanaId, CancellationToken ct = default);

    /// <summary>Validates and creates a card in the requested lane.</summary>
    Task<ServiceResult> CreateAsync(string accountEmail, KanbanCardCreateRequest request, CancellationToken ct = default);

    /// <summary>Validates and edits a card owned by the account; its lane is kept.</summary>
    Task<ServiceResult> UpdateAsync(string accountEmail, KanbanCardUpdateRequest request, CancellationToken ct = default);

    /// <summary>Deletes a card owned by the account.</summary>
    Task<ServiceResult> DeleteAsync(string accountEmail, long id, CancellationToken ct = default);
}
