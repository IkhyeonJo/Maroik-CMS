/**
 * Script for the **Kanban board** page (`Views/Kanban/UserIndex.cshtml`) — the user's personal board of four fixed lanes.
 *
 *   • One modal (`#kanbanCardModal`) both adds and edits a card. A lane's "+ Add card" button opens it empty, for that lane
 *     (the lane goes into a hidden field; the modal has no lane field). Clicking a card — or Enter on a focused card — opens
 *     it filled from the card's `data-*` attributes, with the delete button shown. An edit never sends a lane: a card stays
 *     in the lane it was created in (the server keeps the stored lane whatever is sent).
 *   • Delete asks for confirmation (`#confirmDeleteKanbanCardModal`) first.
 *   • After every successful write the board partial is fetched again (the AJAX form of the page's own GET) and swapped
 *     into `#kanbanBoard`, so lane order, card order and the overdue colour stay the server's decisions.
 *
 * The title / description length limits are `maxlength` attributes the view renders from TitledContentPolicy; the title's
 * "required" check is the form's unobtrusive validation. Both are UX mirrors — authoritative in KanbanCard (server).
 * IIFE-wrapped, no `import` / `export`.
 */
(function() {
    // The runtime-check helpers the _Layout script defines (see TypeScripts/global.d.ts).
    const { check, replies, onReply, fieldValue, attribute } = window;

    // Cached references: the board (its content is replaced after every write, so card / button handlers are delegated
    // to it), the card modal and its fields, the delete confirmation and the anti-forgery input.
    const $kanbanBoard = $("#kanbanBoard");
    const $kanbanCardModal = $("#kanbanCardModal");
    const $kanbanCardModalTitle = $("#kanbanCardModalTitle");
    const $formKanbanCard = $("#formKanbanCard");
    const $kanbanCardId = $("#kanbanCardId");
    const $kanbanCardLane = $("#kanbanCardLane");
    const $kanbanCardTitle = $("#kanbanCardTitle");
    const $kanbanCardDescription = $("#kanbanCardDescription");
    const $kanbanCardDueDate = $("#kanbanCardDueDate");
    const $btnDeleteKanbanCard = $("#btnDeleteKanbanCard");
    const $confirmDeleteKanbanCardModal = $("#confirmDeleteKanbanCardModal");
    const $btnConfirmDeleteKanbanCard = $("#btnConfirmDeleteKanbanCard");
    const $__RequestVerificationToken = $("input[name=\"__RequestVerificationToken\"]");
    // The modal's two localized titles.
    const addCardTitle = fieldValue($("#localizerAddCard"));
    const editCardTitle = fieldValue($("#localizerEditCard"));

    /** Opens the modal empty, to add a card to `lane`. */
    function OpenAddCard(lane: string) {
        $kanbanCardId.val("");
        $kanbanCardLane.val(lane);
        $kanbanCardTitle.val("");
        $kanbanCardDescription.val("");
        $kanbanCardDueDate.val("");
        $kanbanCardModalTitle.text(addCardTitle);
        $btnDeleteKanbanCard.addClass("d-none");
        $kanbanCardModal.modal("show");
    }

    /**
     * Opens the modal filled from `$card`'s attributes, to edit or delete it. The id and title are always rendered; Razor leaves
     * out the description / due-date attribute of a card that has none, which fills the field empty.
     */
    function OpenEditCard($card: JQuery) {
        $kanbanCardId.val(attribute($card, "data-id"));
        $kanbanCardLane.val("");
        $kanbanCardTitle.val(attribute($card, "data-title"));
        $kanbanCardDescription.val($card.attr("data-description") ?? "");
        $kanbanCardDueDate.val($card.attr("data-due-date") ?? "");
        $kanbanCardModalTitle.text(editCardTitle);
        $btnDeleteKanbanCard.removeClass("d-none");
        $kanbanCardModal.modal("show");
    }

    /** Re-fetches the board partial and swaps it in (lane / card order and overdue colours are the server's). */
    function ReloadBoard() {
        $.ajax({
            url: "/Kanban/UserIndex",
            type: "GET",
            dataType: "html",
            success: onReply(check.string, function(html) {
                $kanbanBoard.html(html);
            })
        });
    }

    /** Modal submit: validate, then POST a new card (with its lane) or the edit (id, no lane), then close / toast / refresh. */
    function SaveCard() {
        if (!$formKanbanCard.valid()) {
            return false;
        }
        const id = fieldValue($kanbanCardId);
        const dueDate = fieldValue($kanbanCardDueDate);
        const content = {
            Title: fieldValue($kanbanCardTitle),
            Description: fieldValue($kanbanCardDescription),
            DueDate: dueDate === "" ? null : dueDate,
        };
        const isNew = id === "";
        $.ajax({
            url: isNew ? "/Kanban/CreateKanbanCard" : "/Kanban/UpdateKanbanCard",
            type: "POST",
            headers: { "RequestVerificationToken": fieldValue($__RequestVerificationToken) },
            dataType: "json",
            data: JSON.stringify(isNew ? { Lane: fieldValue($kanbanCardLane), ...content } : { Id: Number(id), ...content }),
            contentType: "application/json; charset=utf-8",
            success: onReply(replies.action, function(data) {
                if (data.result) {
                    $kanbanCardModal.modal("hide");
                    toastr.success(data.message);
                    ReloadBoard();
                } else {
                    toastr.error(data.error);
                }
            })
        });
        return false;
    }

    /** Confirmed delete: POST the open card's id, then close both modals / toast / refresh. */
    function DeleteCard() {
        $.ajax({
            url: "/Kanban/DeleteKanbanCard" + "?id=" + encodeURIComponent(fieldValue($kanbanCardId)),
            type: "POST",
            headers: { "RequestVerificationToken": fieldValue($__RequestVerificationToken) },
            dataType: "json",
            contentType: "application/json; charset=utf-8",
            success: onReply(replies.action, function(data) {
                if (data.result) {
                    $confirmDeleteKanbanCardModal.modal("hide");
                    $kanbanCardModal.modal("hide");
                    toastr.success(data.message);
                    ReloadBoard();
                } else {
                    toastr.error(data.error);
                }
            })
        });
    }

    $kanbanBoard.on("click", ".kanban-add-card", function() {
        OpenAddCard(attribute($(this), "data-lane"));
    });
    $kanbanBoard.on("click", ".kanban-card-item", function() {
        OpenEditCard($(this));
    });
    $kanbanBoard.on("keydown", ".kanban-card-item", function(event) {
        if (event.key === "Enter") {
            OpenEditCard($(this));
        }
    });
    $formKanbanCard.on("submit", SaveCard);
    $btnDeleteKanbanCard.on("click", function() {
        $confirmDeleteKanbanCardModal.modal("show");
    });
    $btnConfirmDeleteKanbanCard.on("click", DeleteCard);
})();
