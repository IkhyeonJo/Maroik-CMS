using Maroik.Core.Contract.Dtos;
using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Domain.Accounts;
using Maroik.Website.Attributes;
using Maroik.Website.Extensions;
using Maroik.Website.Mappers;
using Maroik.Website.Models.ViewModels.Kanbans;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;

namespace Maroik.Website.Controllers;

/// <summary>
/// The personal Kanban board (menu entry <c>/Kanban/UserIndex</c>, User role only): one board per user, shown as the four fixed
/// lanes. Every change is a POST: create a card in a lane, edit a card (it never changes lane), delete a card. The rules are the
/// domain's; the service scopes every card to the signed-in account.
/// </summary>
/// <param name="localizer">Localizer for this controller's user-facing messages.</param>
/// <param name="logger">Logger for unexpected failures in the Kanban actions.</param>
/// <param name="kanbanService">Kanban board use cases.</param>
public class KanbanController(
    IHtmlLocalizer<KanbanController> localizer,
    ILogger<KanbanController> logger,
    IKanbanService kanbanService) : Controller
{
    #region Read
    /// <summary>
    /// Returns the board page, or — to the page script's AJAX request after a write — only the board partial to swap in.
    /// Overdue due dates are decided in the signed-in user's time zone.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> UserIndex(CancellationToken ct)
    {
        AccountResponse account = HttpContext.GetLoggedInAccount();
        List<KanbanLaneResponse> board = await kanbanService.GetBoardAsync(account.Email!, account.TimeZoneIanaId ?? "UTC", ct);
        KanbanBoardViewModel vm = board.ToViewModel();

        return HttpContext.Request.Headers.XRequestedWith == "XMLHttpRequest"
            ? PartialView("_KanbanBoard", vm)
            : View(vm);
    }
    #endregion

    #region Create
    /// <summary>Creates a card in the lane whose "+ Add card" button opened the modal.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.User)]
    public async Task<IActionResult> CreateKanbanCard([FromBody] KanbanCardInputViewModel kanbanCardInputViewModel, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return Json(new { result = false, error = localizer["Input is invalid"].Value });

        string email = HttpContext.GetLoggedInAccount().Email!;
        try
        {
            var result = await kanbanService.CreateAsync(email, new KanbanCardCreateRequest
            {
                Lane = kanbanCardInputViewModel.Lane,
                Title = kanbanCardInputViewModel.Title,
                Description = kanbanCardInputViewModel.Description,
                DueDate = kanbanCardInputViewModel.DueDate
            }, ct);
            return result.Success
                ? Json(new { result = true, message = localizer["The card has been successfully created."].Value })
                : Json(new { result = false, error = localizer[result.ErrorKey, result.ErrorArgs].ToPlainString() });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create Kanban card for {Email}", email);
            return Json(new { result = false, error = localizer[ServiceResult.TemporaryErrorKey].Value });
        }
    }
    #endregion

    #region Update
    /// <summary>Edits a card's title, description and due date; the card stays in its lane.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.User)]
    public async Task<IActionResult> UpdateKanbanCard([FromBody] KanbanCardEditInputViewModel kanbanCardEditInputViewModel, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return Json(new { result = false, error = localizer["Input is invalid"].Value });

        string email = HttpContext.GetLoggedInAccount().Email!;
        try
        {
            var result = await kanbanService.UpdateAsync(email, new KanbanCardUpdateRequest
            {
                Id = kanbanCardEditInputViewModel.Id,
                Title = kanbanCardEditInputViewModel.Title,
                Description = kanbanCardEditInputViewModel.Description,
                DueDate = kanbanCardEditInputViewModel.DueDate
            }, ct);
            return result.Success
                ? Json(new { result = true, message = localizer["The card has been successfully updated."].Value })
                : Json(new { result = false, error = localizer[result.ErrorKey, result.ErrorArgs].ToPlainString() });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update Kanban card {CardId} for {Email}", kanbanCardEditInputViewModel.Id, email);
            return Json(new { result = false, error = localizer[ServiceResult.TemporaryErrorKey].Value });
        }
    }
    #endregion

    #region Delete
    /// <summary>Deletes a card.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequiredHttpPostAccess(Role = Role.User)]
    public async Task<IActionResult> DeleteKanbanCard(long id, CancellationToken ct)
    {
        string email = HttpContext.GetLoggedInAccount().Email!;
        try
        {
            var result = await kanbanService.DeleteAsync(email, id, ct);
            return result.Success
                ? Json(new { result = true, message = localizer["The card has been successfully deleted."].Value })
                : Json(new { result = false, error = localizer[result.ErrorKey, result.ErrorArgs].ToPlainString() });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete Kanban card {CardId} for {Email}", id, email);
            return Json(new { result = false, error = localizer[ServiceResult.TemporaryErrorKey].Value });
        }
    }
    #endregion
}
