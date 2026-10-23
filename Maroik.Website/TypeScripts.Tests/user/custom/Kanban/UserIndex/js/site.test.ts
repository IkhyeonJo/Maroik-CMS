import { describe, it, expect } from "vitest";
import { loadSite, hidden, antiForgery, stubPlugin, type SiteHandle } from "@tests/_common/harness";

// wwwroot/user/custom/Kanban/UserIndex/js/site.js — the personal Kanban board: one modal adds a card to the lane whose
// "+ Add card" was clicked, or edits / deletes the clicked card; after every write the board partial is re-fetched.

/** One board card as _KanbanBoard renders it (a null description / due date leaves the attribute out, as Razor does). */
function card(id: number, title: string, description: string | null, dueDate: string | null): string {
    const attrs = [`data-id="${id}"`, `data-title="${title}"`];
    if (description !== null) attrs.push(`data-description="${description}"`);
    if (dueDate !== null) attrs.push(`data-due-date="${dueDate}"`);
    return `<div class="card kanban-card-item" role="button" tabindex="0" ${attrs.join(" ")}><h5>${title}</h5></div>`;
}

/** The page: the board (two lanes are enough), the card modal, the delete confirmation and the localized modal titles. */
function fixture(): string {
    return (
        antiForgery +
        hidden("localizerAddCard", "Add card") +
        hidden("localizerEditCard", "Edit card") +
        `<div id="kanbanBoard">
            <div class="kanban-lane" data-lane="Backlog">
                ${card(7, "Write", "line 1\nline 2", "2026-05-28")}
                <button type="button" class="kanban-add-card" data-lane="Backlog">+ Add card</button>
            </div>
            <div class="kanban-lane" data-lane="Done">
                ${card(8, "Plain", null, null)}
                <button type="button" class="kanban-add-card" data-lane="Done">+ Add card</button>
            </div>
         </div>
         <div id="kanbanCardModal">
            <form id="formKanbanCard">
                <h5 id="kanbanCardModalTitle">Add card</h5>
                <input type="hidden" id="kanbanCardId" value="" />
                <input type="hidden" id="kanbanCardLane" value="" />
                <input type="text" id="kanbanCardTitle" value="" />
                <textarea id="kanbanCardDescription"></textarea>
                <input type="date" id="kanbanCardDueDate" value="" />
                <button type="button" id="btnDeleteKanbanCard" class="d-none">Delete</button>
                <input type="submit" value="Save" />
            </form>
         </div>
         <div id="confirmDeleteKanbanCardModal"><button id="btnConfirmDeleteKanbanCard" type="button">Delete</button></div>`
    );
}

/** Loads the page and records every `.modal(...)` call as `[element id, argument]`. */
function load(): { h: SiteHandle; modals: [string, unknown][] } {
    const h = loadSite("user", "Kanban", "UserIndex", fixture());
    const modals: [string, unknown][] = [];
    stubPlugin(h, "modal", function(this: JQuery, arg: never) {
        modals.push([String(this.attr("id")), arg]);
        return this;
    });
    return { h, modals };
}

/** The JSON body of the last ajax call. */
function lastBody(h: SiteHandle): unknown {
    return JSON.parse(String(h.lastAjax().data));
}

describe("Kanban/UserIndex — opening the modal", () => {
    it("a lane's \"+ Add card\" opens an empty add form for that lane, without the delete button", () => {
        const { h, modals } = load();
        h.$("#kanbanCardTitle").val("left over");
        h.$("#kanbanCardId").val("3");

        h.$(".kanban-add-card[data-lane='Done']").trigger("click");

        expect(h.$("#kanbanCardLane").val()).toBe("Done");
        expect(h.$("#kanbanCardId").val()).toBe("");
        expect(h.$("#kanbanCardTitle").val()).toBe("");
        expect(h.$("#kanbanCardDescription").val()).toBe("");
        expect(h.$("#kanbanCardDueDate").val()).toBe("");
        expect(h.$("#kanbanCardModalTitle").text()).toBe("Add card");
        expect(h.$("#btnDeleteKanbanCard").hasClass("d-none")).toBe(true);
        expect(modals).toContainEqual(["kanbanCardModal", "show"]);
    });

    it("clicking a card opens the edit form filled from the card, with the delete button", () => {
        const { h, modals } = load();

        h.$(".kanban-card-item[data-id='7']").trigger("click");

        expect(h.$("#kanbanCardId").val()).toBe("7");
        expect(h.$("#kanbanCardLane").val()).toBe("");
        expect(h.$("#kanbanCardTitle").val()).toBe("Write");
        expect(h.$("#kanbanCardDescription").val()).toBe("line 1\nline 2");
        expect(h.$("#kanbanCardDueDate").val()).toBe("2026-05-28");
        expect(h.$("#kanbanCardModalTitle").text()).toBe("Edit card");
        expect(h.$("#btnDeleteKanbanCard").hasClass("d-none")).toBe(false);
        expect(modals).toContainEqual(["kanbanCardModal", "show"]);
    });

    it("a card without a description or due date fills those fields empty", () => {
        const { h } = load();
        h.$("#kanbanCardDescription").val("stale");
        h.$("#kanbanCardDueDate").val("2020-01-01");

        h.$(".kanban-card-item[data-id='8']").trigger("click");

        expect(h.$("#kanbanCardDescription").val()).toBe("");
        expect(h.$("#kanbanCardDueDate").val()).toBe("");
    });

    it("Enter on a focused card opens it too; other keys do not", () => {
        const { h, modals } = load();

        h.$(".kanban-card-item[data-id='8']").trigger(h.$.Event("keydown", { key: "a" }));
        expect(modals).toEqual([]);

        h.$(".kanban-card-item[data-id='8']").trigger(h.$.Event("keydown", { key: "Enter" }));
        expect(h.$("#kanbanCardId").val()).toBe("8");
        expect(modals).toContainEqual(["kanbanCardModal", "show"]);
    });

    it("cards and buttons swapped in by a board refresh still open the modal (delegated handlers)", () => {
        const { h } = load();
        h.$("#kanbanBoard").html(card(42, "Fresh", null, null));

        h.$(".kanban-card-item[data-id='42']").trigger("click");

        expect(h.$("#kanbanCardTitle").val()).toBe("Fresh");
    });
});

describe("Kanban/UserIndex — saving", () => {
    it("the add form posts the lane, title, description and due date to CreateKanbanCard", () => {
        const { h } = load();
        h.$(".kanban-add-card[data-lane='Backlog']").trigger("click");
        h.$("#kanbanCardTitle").val("New");
        h.$("#kanbanCardDescription").val("a\nb");
        h.$("#kanbanCardDueDate").val("2026-06-01");

        h.$("#formKanbanCard").trigger("submit");

        const call = h.lastAjax();
        expect(call.url).toBe("/Kanban/CreateKanbanCard");
        expect(call.type).toBe("POST");
        expect(call.headers).toEqual({ RequestVerificationToken: "tok" });
        expect(lastBody(h)).toEqual({ Lane: "Backlog", Title: "New", Description: "a\nb", DueDate: "2026-06-01" });
    });

    it("an empty due date is sent as null, not as an empty string", () => {
        const { h } = load();
        h.$(".kanban-add-card[data-lane='Backlog']").trigger("click");
        h.$("#kanbanCardTitle").val("New");

        h.$("#formKanbanCard").trigger("submit");

        expect(lastBody(h)).toEqual({ Lane: "Backlog", Title: "New", Description: "", DueDate: null });
    });

    it("the edit form posts the id (as a number) and the content to UpdateKanbanCard — never a lane", () => {
        const { h } = load();
        h.$(".kanban-card-item[data-id='7']").trigger("click");
        h.$("#kanbanCardTitle").val("Renamed");

        h.$("#formKanbanCard").trigger("submit");

        expect(h.lastAjax().url).toBe("/Kanban/UpdateKanbanCard");
        expect(lastBody(h)).toEqual({ Id: 7, Title: "Renamed", Description: "line 1\nline 2", DueDate: "2026-05-28" });
    });

    it("an invalid form (blank title) posts nothing", () => {
        const { h } = load();
        stubPlugin(h, "valid", () => false);
        h.$(".kanban-add-card[data-lane='Backlog']").trigger("click");

        h.$("#formKanbanCard").trigger("submit");

        expect(h.ajaxCalls).toEqual([]);
    });

    it("on success: closes the modal, shows the message and swaps in the refreshed board", () => {
        const { h, modals } = load();
        h.$(".kanban-add-card[data-lane='Backlog']").trigger("click");
        h.$("#kanbanCardTitle").val("New");
        h.$("#formKanbanCard").trigger("submit");

        h.respond(0, { result: true, message: "Created" });

        expect(modals).toContainEqual(["kanbanCardModal", "hide"]);
        expect(h.toastr.success).toHaveBeenCalledWith("Created");
        const refresh = h.lastAjax();
        expect(refresh.url).toBe("/Kanban/UserIndex");
        expect(refresh.type).toBe("GET");
        expect(refresh.settings.dataType).toBe("html");

        h.respond(0, "<p id=\"refreshed\">board</p>");
        expect(h.$("#kanbanBoard #refreshed").text()).toBe("board");
    });

    it("on a refusal: shows the server's reason and keeps the modal open", () => {
        const { h, modals } = load();
        h.$(".kanban-card-item[data-id='7']").trigger("click");
        h.$("#formKanbanCard").trigger("submit");

        h.respond(0, { result: false, error: "The card could not be found." });

        expect(h.toastr.error).toHaveBeenCalledWith("The card could not be found.");
        expect(modals).not.toContainEqual(["kanbanCardModal", "hide"]);
        expect(h.ajaxCalls).toHaveLength(1);
    });
});

describe("Kanban/UserIndex — deleting", () => {
    it("the delete button asks for confirmation first, posting nothing", () => {
        const { h, modals } = load();
        h.$(".kanban-card-item[data-id='7']").trigger("click");

        h.$("#btnDeleteKanbanCard").trigger("click");

        expect(modals).toContainEqual(["confirmDeleteKanbanCardModal", "show"]);
        expect(h.ajaxCalls).toEqual([]);
    });

    it("confirming posts the card's id to DeleteKanbanCard, then closes both modals and refreshes the board", () => {
        const { h, modals } = load();
        h.$(".kanban-card-item[data-id='7']").trigger("click");
        h.$("#btnDeleteKanbanCard").trigger("click");

        h.$("#btnConfirmDeleteKanbanCard").trigger("click");

        const call = h.lastAjax();
        expect(call.url).toBe("/Kanban/DeleteKanbanCard?id=7");
        expect(call.type).toBe("POST");
        expect(call.headers).toEqual({ RequestVerificationToken: "tok" });

        h.respond(0, { result: true, message: "Deleted" });
        expect(modals).toContainEqual(["confirmDeleteKanbanCardModal", "hide"]);
        expect(modals).toContainEqual(["kanbanCardModal", "hide"]);
        expect(h.toastr.success).toHaveBeenCalledWith("Deleted");
        expect(h.lastAjax().url).toBe("/Kanban/UserIndex");
    });

    it("a refused delete shows the server's reason and refreshes nothing", () => {
        const { h } = load();
        h.$(".kanban-card-item[data-id='7']").trigger("click");
        h.$("#btnDeleteKanbanCard").trigger("click");
        h.$("#btnConfirmDeleteKanbanCard").trigger("click");

        h.respond(0, { result: false, error: "The card could not be found." });

        expect(h.toastr.error).toHaveBeenCalledWith("The card could not be found.");
        expect(h.ajaxCalls).toHaveLength(1);
    });
});

describe("Kanban/UserIndex — reply checks", () => {
    it("a write reply that does not match the controller's shape shows the generic error and refreshes nothing", () => {
        const { h } = load();
        h.$(".kanban-add-card[data-lane='Backlog']").trigger("click");
        h.$("#kanbanCardTitle").val("New");
        h.$("#formKanbanCard").trigger("submit");

        h.respond(0, { result: true });

        expect(h.toastr.success).not.toHaveBeenCalled();
        expect(h.toastr.error).toHaveBeenCalledWith("L_TemporaryError");
        expect(h.ajaxCalls).toHaveLength(1);
    });
});
