import { describe, it, expect, vi } from "vitest";
import { loadSite, antiForgery, fireNative, stubPlugin } from "@tests/_common/harness";

// wwwroot/admin/custom/Management/Menu/js/site.js
function fixture(): string {
    return (
        antiForgery +
        `<div id="createMenuTabs"></div><div id="editCategoryTabs"></div><div id="editSubCategoryTabs"></div>
     <input id="gridSearch" /><div class="mvc-grid"></div>
     <table><tbody>
       <tr class="clsGridRow" data-id="1" data-categoryid=""></tr>
       <tr class="clsGridRow" data-id="2" data-categoryid="1"></tr>
     </tbody></table>
     <form id="formCreateCategory"></form><form id="formCreateSubCategory"></form>
     <form id="formEditCategory"></form><form id="formEditSubCategory"></form>
     <button id="btnExportExcelMenu"></button>
     <button id="btnEditMenuGridRow" data-errorMessageSelectGridRow="select a row"></button>
     <button id="btnConfirmDeleteMenu" data-errorMessageSelectGridRow="select a row"></button>
     <button id="btnDeleteMenu" data-errorMessageSelectGridRow="select a row"></button>`
    );
}

describe("Management/Menu (admin)", () => {
    it("highlights the sub-category row matching id + categoryId", () => {
        const h = loadSite("admin", "Management", "Menu", fixture());
        fireNative(h.win.document, "rowclick", { data: { Id: 2, CategoryId: 1 } });
        const rows = [...h.win.document.querySelectorAll(".clsGridRow")];
        expect(rows.map((r) => r.classList.contains("table-primary"))).toEqual([false, true]);
    });

    it("errors when Edit is clicked with nothing selected", () => {
        const h = loadSite("admin", "Management", "Menu", fixture());
        h.$("#btnEditMenuGridRow").trigger("click");
        expect(h.toastr.error).toHaveBeenCalledWith("select a row");
        expect(h.ajaxCalls).toHaveLength(0);
    });

    it("Excel export targets /Management/ExportExcelMenu", () => {
        const h = loadSite("admin", "Management", "Menu", fixture());
        h.$("#btnExportExcelMenu").trigger("click");
        expect(h.submittedForms.at(-1)!.action).toContain("/Management/ExportExcelMenu");
    });
});

// Real markup (Views/Management/_MenuGrid.cshtml): a category row carries data-categoryid="-1",
// a sub-category row the id of its parent category.
const menuFixture = () =>
    fixture() +
    `<div id="createMenuDialogModal"></div><div id="editCategoryDialogModal"></div>
   <div id="editSubCategoryDialogModal"></div><div id="confirmDeleteMenuDialogModal"></div>
   <table><tbody>
     <tr class="clsGridRow" data-id="10" data-categoryid="-1"></tr>
     <tr class="clsGridRow" data-id="10" data-categoryid="4"></tr>
   </tbody></table>`;
/** Selector of the category row with id 10 (no parent). */
const categoryRow = `.clsGridRow[data-id="10"][data-categoryid="-1"]`;
/** Selector of the sub-category row with the same id 10, under category 4. */
const subRow = `.clsGridRow[data-id="10"][data-categoryid="4"]`;
/** A category record as the IsCategoryExists endpoint returns it. */
const category = { id: 10, name: "n", displayName: "d", iconPath: "i", controller: "c", action: "a", role: "Admin", order: 3 };
/** A sub-category record as the IsSubCategoryExists endpoint returns it. */
const subCategory = { id: 10, categoryId: 4, name: "n", displayName: "d", iconPath: "i", action: "a", role: "User", order: 1 };

describe("Management/Menu (admin) — categories and sub-categories share one grid", () => {
    /** Loads the menu page over its grid fixture. */
    const load = () => loadSite("admin", "Management", "Menu", menuFixture());
    /** Replaces `$.fn.modal` with a chainable spy and returns it. */
    const spyModal = (h: ReturnType<typeof load>) => {
        const modal = vi.fn(function(this: JQuery) {
            return this;
        });
        stubPlugin(h, "modal", modal);
        return modal;
    };
    /** Whether the category row and the sub-category row are highlighted, in that order. */
    const selected = (h: ReturnType<typeof load>) =>
        [categoryRow, subRow].map((s) => h.win.document.querySelector(s)!.classList.contains("table-primary"));

    it("a category row (null CategoryId → \"\") and a same-id sub-category row are told apart by their parent id", () => {
        const h = load();
        fireNative(h.win.document, "rowclick", { data: { Id: 10, CategoryId: "" } });
        expect(selected(h)).toEqual([true, false]);
        fireNative(h.win.document, "rowclick", { data: { Id: 10, CategoryId: 4 } });
        expect(selected(h)).toEqual([false, true]);
    });

    it("the grid lifecycle hooks are inert and the search box reloads the grid with wholeSearch", () => {
        const h = load();
        for (const type of ["reloadstart", "reloadend", "reloadfail", "gridconfigure"])
            expect(() => fireNative(h.win.document, type)).not.toThrow();
        h.$("#gridSearch").val("dash").trigger("input");
        expect(h.MvcGridInstances.at(-1)!.url.searchParams.get("wholeSearch")).toBe("dash");
        expect(h.MvcGridInstances.at(-1)!.reload).toHaveBeenCalled();
    });

    it.each([
        ["CreateCategory", "formCreateCategory", "createMenuDialogModal"],
        ["CreateSubCategory", "formCreateSubCategory", "createMenuDialogModal"],
        ["UpdateCategory", "formEditCategory", "editCategoryDialogModal"],
        ["UpdateSubCategory", "formEditSubCategory", "editSubCategoryDialogModal"],
    ])("%s posts JSON to /Management/%s-style endpoint; success closes the modal, reloads and toasts; failure toasts", (verb, form) => {
        const h = load();
        const modal = spyModal(h);
        const submit = h.$.Event("submit");
        h.$(`#${form}`).trigger(submit);
        const call = h.lastAjax();
        expect(call.url).toBe(`/Management/${verb}`);
        expect(call.headers).toEqual({ RequestVerificationToken: "tok" });
        expect(submit.isDefaultPrevented()).toBe(true);

        call.success!({ result: true, message: "ok" });
        expect(modal).toHaveBeenCalledWith("hide");
        expect(h.MvcGridInstances.at(-1)!.reload).toHaveBeenCalled();
        expect(h.toastr.success).toHaveBeenCalledWith("ok");

        h.$(`#${form}`).trigger("submit");
        h.lastAjax().success!({ result: false, error: "bad" });
        expect(h.toastr.error).toHaveBeenCalledWith("bad");
    });

    it("an invalid form sends nothing", () => {
        const h = load();
        stubPlugin(h, "valid", () => false);
        for (const f of ["formCreateCategory", "formCreateSubCategory", "formEditCategory", "formEditSubCategory"])
            h.$(`#${f}`).trigger("submit");
        expect(h.ajaxCalls).toHaveLength(0);
    });

    it("Edit on a category row asks IsCategoryExists and opens the category modal; on a sub-category row IsSubCategoryExists and the sub-category modal", () => {
        const h = load();
        const modal = spyModal(h);

        fireNative(h.win.document, "rowclick", { data: { Id: 10, CategoryId: "" } });
        h.$("#btnEditMenuGridRow").trigger("click");
        expect(h.lastAjax().url).toBe("/Management/IsCategoryExists?id=10");
        h.lastAjax().success!({ result: true, category });
        expect(modal).toHaveBeenCalledWith("show");

        fireNative(h.win.document, "rowclick", { data: { Id: 10, CategoryId: 4 } });
        h.$("#btnEditMenuGridRow").trigger("click");
        expect(h.lastAjax().url).toBe("/Management/IsSubCategoryExists?id=10");
        h.lastAjax().success!({ result: true, subCategory });
        expect(modal.mock.calls.filter((c) => (c as unknown[])[0] === "show")).toHaveLength(2);
    });

    it("Edit: a record that no longer exists is toasted", () => {
        const h = load();
        fireNative(h.win.document, "rowclick", { data: { Id: 10, CategoryId: "" } });
        h.$("#btnEditMenuGridRow").trigger("click");
        h.lastAjax().success!({ result: false, error: "gone" });
        fireNative(h.win.document, "rowclick", { data: { Id: 10, CategoryId: 4 } });
        h.$("#btnEditMenuGridRow").trigger("click");
        h.lastAjax().success!({ result: false, error: "gone too" });
        expect(h.toastr.error).toHaveBeenCalledWith("gone");
        expect(h.toastr.error).toHaveBeenCalledWith("gone too");
    });

    it("double-clicking opens the edit only for the selected row", () => {
        const h = load();
        h.$(categoryRow).trigger("dblclick");
        expect(h.ajaxCalls).toHaveLength(0);
        fireNative(h.win.document, "rowclick", { data: { Id: 10, CategoryId: "" } });
        h.$(categoryRow).trigger("dblclick");
        expect(h.lastAjax().url).toBe("/Management/IsCategoryExists?id=10");
    });

    it("delete-confirm needs a selection, then opens the confirmation modal", () => {
        const h = load();
        const modal = spyModal(h);
        h.$("#btnConfirmDeleteMenu").trigger("click");
        expect(h.toastr.error).toHaveBeenCalledWith("select a row");
        fireNative(h.win.document, "rowclick", { data: { Id: 10, CategoryId: 4 } });
        h.$("#btnConfirmDeleteMenu").trigger("click");
        expect(modal).toHaveBeenCalledWith({ keyboard: false, backdrop: "static" });
        expect(modal).toHaveBeenCalledWith("show");
    });

    it.each([
        ["category", "", "IsCategoryExists", "DeleteCategory", { result: true, category }],
        ["sub-category", 4, "IsSubCategoryExists", "DeleteSubCategory", { result: true, subCategory }],
    ])("delete of a %s re-checks it exists, then deletes; success closes, reloads and toasts", (_kind, categoryId, existsAction, deleteAction, reply) => {
        const h = load();
        const modal = spyModal(h);
        fireNative(h.win.document, "rowclick", { data: { Id: 10, CategoryId: categoryId } });
        h.$("#btnDeleteMenu").trigger("click");
        expect(h.lastAjax().url).toBe(`/Management/${existsAction}?id=10`);

        h.lastAjax().success!(reply);
        expect(h.lastAjax().url).toBe(`/Management/${deleteAction}`);
        expect((JSON.parse(String(h.lastAjax().data)) as { Id: number }).Id).toBe(10);

        h.lastAjax().success!({ result: true, message: "removed" });
        expect(modal).toHaveBeenCalledWith("hide");
        expect(h.MvcGridInstances.at(-1)!.reload).toHaveBeenCalled();
        expect(h.toastr.success).toHaveBeenCalledWith("removed");
    });

    it.each([
        ["category", ""],
        ["sub-category", 4],
    ])("delete of a %s: a vanished record or a refused delete is toasted, nothing reloads", (_kind, categoryId) => {
        const h = load();
        fireNative(h.win.document, "rowclick", { data: { Id: 10, CategoryId: categoryId } });
        h.$("#btnDeleteMenu").trigger("click");
        h.lastAjax().success!({ result: false, error: "vanished" });
        expect(h.toastr.error).toHaveBeenCalledWith("vanished");
        expect(h.ajaxCalls).toHaveLength(1);

        h.$("#btnDeleteMenu").trigger("click");
        h.lastAjax().success!({ result: true, category, subCategory });
        h.lastAjax().success!({ result: false, error: "in use" });
        expect(h.toastr.error).toHaveBeenCalledWith("in use");
        expect(h.MvcGridInstances).toHaveLength(0);
    });

    it("delete without a selection only shows the message", () => {
        const h = load();
        h.$("#btnDeleteMenu").trigger("click");
        expect(h.toastr.error).toHaveBeenCalledWith("select a row");
        expect(h.ajaxCalls).toHaveLength(0);
    });
});
