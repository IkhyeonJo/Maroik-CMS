using Maroik.Core.Contract.Dtos;
using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Domain.Kanbans;
using Maroik.Core.Service.Mappers;
using Microsoft.Extensions.Logging;

namespace Maroik.Core.Service.Services;

/// <summary>
/// Implementation of <see cref="IKanbanService"/> for the personal Kanban board. The board is the account's cards laid out by
/// <see cref="KanbanBoardPolicy"/>; the content rules and the fixed lane live in <see cref="KanbanCard"/>. Create, edit and delete
/// each touch one card row, so they are single non-transactional repository writes (like the fixed income / expenditure CRUD).
/// Every lookup is scoped to the signed-in account, so another account's card id is answered as "not found" (K2).
/// </summary>
public class KanbanService(IKanbanCardRepository kanbanCardRepository, ILogger<KanbanService> logger, TimeProvider timeProvider) : IKanbanService
{
    /// <inheritdoc />
    public async Task<List<KanbanLaneResponse>> GetBoardAsync(string accountEmail, string timeZoneIanaId, CancellationToken ct = default)
    {
        DateTime utcNow = timeProvider.GetUtcNow().UtcDateTime;
        List<KanbanCard> cards = await kanbanCardRepository.GetByAccountEmailAsync(accountEmail, ct);
        return
        [
            .. KanbanBoardPolicy.Arrange(cards).Select(lane => new KanbanLaneResponse
            {
                Lane = lane.Lane,
                Cards = [.. lane.Cards.Select(card => KanbanCardMapper.ToResponse(card, utcNow, timeZoneIanaId))]
            })
        ];
    }

    /// <inheritdoc />
    public async Task<ServiceResult> CreateAsync(string accountEmail, KanbanCardCreateRequest request, CancellationToken ct = default)
    {
        var createResult = KanbanCard.Create(accountEmail, request.Lane, request.Title, request.Description, request.DueDate,
            timeProvider.GetUtcNow().UtcDateTime);
        if (createResult.IsError) return ServiceResult.FromError(createResult.FirstError);

        await kanbanCardRepository.CreateAsync(createResult.Value, ct);
        return ServiceResult.Ok();
    }

    /// <inheritdoc />
    public async Task<ServiceResult> UpdateAsync(string accountEmail, KanbanCardUpdateRequest request, CancellationToken ct = default)
    {
        KanbanCard? card = await kanbanCardRepository.FindByEmailAndIdAsync(accountEmail, request.Id, ct);
        if (card == null)
        {
            logger.LogWarning("Kanban card {CardId} not found for {Email} (update)", request.Id, accountEmail);
            return ServiceResult.NotFound("KanbanCard.NotFound", ServiceErrorKeys.KanbanCardNotFound);
        }

        var updateResult = card.Update(request.Title, request.Description, request.DueDate, timeProvider.GetUtcNow().UtcDateTime);
        if (updateResult.IsError) return ServiceResult.FromError(updateResult.FirstError);

        await kanbanCardRepository.UpdateEntityAsync(card, ct);
        return ServiceResult.Ok();
    }

    /// <inheritdoc />
    public async Task<ServiceResult> DeleteAsync(string accountEmail, long id, CancellationToken ct = default)
    {
        KanbanCard? card = await kanbanCardRepository.FindByEmailAndIdAsync(accountEmail, id, ct);
        if (card == null)
        {
            logger.LogWarning("Kanban card {CardId} not found for {Email} (delete)", id, accountEmail);
            return ServiceResult.NotFound("KanbanCard.NotFound", ServiceErrorKeys.KanbanCardNotFound);
        }

        await kanbanCardRepository.DeleteByEmailAndIdAsync(accountEmail, id, ct);
        logger.LogInformation("Kanban card {CardId} ({Lane}) deleted by {Email}", id, card.Lane, accountEmail);
        return ServiceResult.Ok();
    }
}
