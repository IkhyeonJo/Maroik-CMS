/*
 * Shared behavior suite for the "grid CRUD" page scripts that follow one pattern:
 * AccountBook/{Asset,Income,Expenditure} and Notice/{FixedIncome,FixedExpenditure}
 * (a NonFactors MVC.Grid with create / edit / delete modals, row highlight, double-click to edit,
 * whole-row search and a create/update/delete AJAX round trip against `/{Controller}/{Verb}{Entity}`).
 * Each site.test.ts calls `describeGridCrudScript(...)` with its own names and a sample record; the
 * script under test is the compiled one at the matching wwwroot path.
 */
import { describe, it, expect, vi } from "vitest";
import { loadSite, fireNative, type SiteHandle } from "@tests/_common/harness";

/** What one grid page supplies to {@link describeGridCrudScript}. */
export interface GridCrudConfig {
    /** area folder of the compiled script */
    area: "admin" | "user";
    /** feature (controller) folder, e.g. "Notice" */
    feature: string;
    /** page folder, e.g. "FixedIncome" */
    page: string;
    /** entity name used in element ids and endpoints, e.g. "FixedIncome" */
    entity: string;
    /** controller segment of the endpoints, e.g. "Notice" */
    controller: string;
    /** property of the IsXExists reply that carries the record, e.g. "fixedIncome" */
    existsKey: string;
    /** what identifies a grid row: a numeric `Id` (`data-id`), the asset's `ProductName` (`data-productName`) or an account `Email` (`data-email`) */
    rowKey: "Id" | "ProductName" | "Email";
    /** the page's own fixture (element ids the script caches); the suite appends grid rows + modal shells */
    fixture: () => string;
    /** a complete record as the IsXExists endpoint returns it (every field the edit-fill code reads) */
    record: Record<string, unknown>;
    /** ids of the maturity-date inputs that get the "No maturity date" / "Today" button panel (Fixed* pages only) */
    maturityPickers?: string[];
    /** the value of the page's `#noMaturityDate` hidden field, e.g. "9999-12-31" */
    noMaturityDate?: string;
}

/** Keys of the two grid rows the suite appends. */
const KEYS = ["suite-row-a", "suite-row-b"] as const; // no characters that percent-encoding would change

/** Registers the shared grid-CRUD tests (selection, search, create/edit/delete round trips) for one page. */
export function describeGridCrudScript(c: GridCrudConfig): void {
    const N = c.entity;
    /** The row attribute that carries the key. */
    const attr = { Id: "data-id", ProductName: "data-productName", Email: "data-email" }[c.rowKey];
    /** Two grid rows plus the modal shells and search box the script expects. */
    const rows =
        `<div class="mvc-grid"></div><table><tbody>` +
        KEYS.map((k) => `<tr class="clsGridRow" ${attr}="${k}"></tr>`).join("") +
        `</tbody></table>` +
        `<div id="create${N}DialogModal"></div><div id="edit${N}DialogModal"></div><div id="confirmDelete${N}DialogModal"></div>` +
        `<input id="gridSearch" />`;
    /** Loads the page script over its fixture plus {@link rows}. */
    const load = () => loadSite(c.area, c.feature, c.page, c.fixture() + rows);

    /** Fires the grid's native `rowclick` for the row with `key`. */
    const selectRow = (h: SiteHandle, key: string) =>
        fireNative(h.win.document, "rowclick", { data: { [c.rowKey]: key } });
    /** For each suite row, whether it is highlighted (`table-primary`). */
    const rowClasses = (h: SiteHandle) =>
        [...h.win.document.querySelectorAll<HTMLElement>(`.clsGridRow[${attr}^="suite-row"]`)].map((r) => r.classList.contains("table-primary"));
    /** The most recently constructed MvcGrid stub. */
    const lastGrid = (h: SiteHandle) => h.MvcGridInstances.at(-1)!;
    /** Replaces `$.fn.modal` with a chainable spy and returns it. */
    const spyModal = (h: SiteHandle) => {
        const modal = vi.fn(function(this: any) {
            return this;
        });
        (h.$.fn as any).modal = modal;
        return modal;
    };
    /** The row key as a camelCase query-string name, e.g. "productName". */
    const recordKey = c.rowKey[0].toLowerCase() + c.rowKey.slice(1);
    /** The IsXExists URL the script requests for `key` (non-numeric keys URL-encoded). */
    const existsUrl = (key: string) =>
        `/${c.controller}/Is${N}Exists?${recordKey}=${c.rowKey === "Id" ? key : encodeURIComponent(key)}`;
    /** The body of the delete request for `id`. */
    const deleteBody = (id: unknown) => ({ [c.rowKey]: id });

    describe(`${c.area}/${c.feature}/${c.page} — shared grid CRUD behaviour`, () => {
        // ---- selection -----------------------------------------------------------------------------

        it("rowclick highlights only the row whose key matches, and a later click moves the highlight", () => {
            const h = load();
            selectRow(h, KEYS[1]);
            expect(rowClasses(h)).toEqual([false, true]);
            selectRow(h, KEYS[0]);
            expect(rowClasses(h)).toEqual([true, false]);
        });

        it("the grid's lifecycle events are inert hooks (they must not throw)", () => {
            const h = load();
            for (const type of ["reloadstart", "reloadend", "reloadfail", "gridconfigure"])
                expect(() => fireNative(h.win.document, type)).not.toThrow();
        });

        it("typing in the search box puts the text into the grid query (wholeSearch) and reloads it", () => {
            const h = load();
            h.$("#gridSearch").val("needle & more").trigger("input");
            expect(lastGrid(h).url.searchParams.get("wholeSearch")).toBe("needle & more");
            expect(lastGrid(h).reload).toHaveBeenCalledTimes(1);
        });

        // ---- edit ----------------------------------------------------------------------------------

        it("Edit / Delete-confirm / Delete without a selected row only show the localized message", () => {
            const h = load();
            for (const id of [`#btnEdit${N}GridRow`, `#btnConfirmDelete${N}`, `#btnDelete${N}`]) {
                h.toastr.error.mockClear();
                h.$(id).attr("data-errorMessageSelectGridRow", "pick a row").trigger("click");
                expect(h.toastr.error).toHaveBeenCalledWith("pick a row");
            }
            expect(h.ajaxCalls.filter((a) => String(a.url).includes("Exists"))).toHaveLength(0);
        });

        it("Edit with a selected row asks the server for that record, then opens the (static) edit modal", () => {
            const h = load();
            const modal = spyModal(h);
            selectRow(h, KEYS[0]);
            h.$(`#btnEdit${N}GridRow`).trigger("click");

            const call = h.ajaxCalls.find((a) => String(a.url).includes(`Is${N}Exists`))!;
            expect(call.url).toBe(existsUrl(KEYS[0]));
            expect(call.headers).toEqual({ RequestVerificationToken: "tok" });

            call.success!({ result: true, [c.existsKey]: c.record });

            expect(modal).toHaveBeenCalledWith({ keyboard: false, backdrop: "static" });
            expect(modal).toHaveBeenCalledWith("show");
        });

        if ("mainClass" in c.record) {
            it("a create main class the server's class map does not know leaves no sub-class enabled", () => {
                const h = load();
                h.$(`#create${N}MainClass`).append("<option value=\"NoSuchClass\">?</option>").val("NoSuchClass").trigger("change");
                const enabled = [...h.win.document.querySelectorAll<HTMLOptionElement>(`#create${N}SubClass option`)].filter((o) => !o.disabled);
                expect(enabled).toHaveLength(0);
            });

            it("Edit: a record whose main class the class map does not know still opens the form", () => {
                const h = load();
                const modal = spyModal(h);
                selectRow(h, KEYS[0]);
                h.$(`#btnEdit${N}GridRow`).trigger("click");
                h.ajaxCalls.find((a) => String(a.url).includes(`Is${N}Exists`))!.success!({
                    result: true,
                    [c.existsKey]: { ...c.record, mainClass: "NoSuchClass" }
                });
                expect(modal).toHaveBeenCalledWith("show");
            });
        }

        it("Edit: a record the server can no longer find is toasted and no modal opens", () => {
            const h = load();
            const modal = spyModal(h);
            selectRow(h, KEYS[0]);
            h.$(`#btnEdit${N}GridRow`).trigger("click");
            h.ajaxCalls.find((a) => String(a.url).includes(`Is${N}Exists`))!.success!({ result: false, error: "gone" });
            expect(h.toastr.error).toHaveBeenCalledWith("gone");
            expect(modal).not.toHaveBeenCalledWith("show");
        });

        it("double-clicking a row opens its edit only when that row is the selected one", () => {
            const h = load();
            h.$(`.clsGridRow[${attr}="${KEYS[0]}"]`).trigger("dblclick");
            expect(h.ajaxCalls.filter((a) => String(a.url).includes(`Is${N}Exists`))).toHaveLength(0);

            selectRow(h, KEYS[0]);
            h.$(`.clsGridRow[${attr}="${KEYS[0]}"]`).trigger("dblclick");
            expect(h.ajaxCalls.filter((a) => String(a.url).includes(`Is${N}Exists`))).toHaveLength(1);
        });

        // ---- create / update -------------------------------------------------------------------------

        it.each(["Create", "Update"])("%s: an invalid form sends nothing", (verb) => {
            const h = load();
            (h.$.fn as any).valid = () => false;
            h.$(`#form${verb === "Create" ? "Create" : "Edit"}${N}`).trigger("submit");
            expect(h.ajaxCalls.filter((a) => String(a.url).includes(`/${verb}${N}`))).toHaveLength(0);
        });

        it.each([
            ["Create", "create"],
            ["Update", "edit"],
        ])("%s: posts JSON to its endpoint; success closes the modal, reloads the grid and toasts; the form never navigates", (verb, prefix) => {
            const h = load();
            const modal = spyModal(h);
            const submit = h.$.Event("submit");
            h.$(`#form${verb === "Create" ? "Create" : "Edit"}${N}`).trigger(submit);

            const call = h.ajaxCalls.find((a) => a.url === `/${c.controller}/${verb}${N}`)!;
            expect(call).toBeDefined();
            expect(call.headers).toEqual({ RequestVerificationToken: "tok" });
            expect(() => JSON.parse(String(call.data))).not.toThrow();
            expect(submit.isDefaultPrevented()).toBe(true);

            call.success!({ result: true, message: "done" });

            expect(modal).toHaveBeenCalledWith("hide");
            expect(lastGrid(h).reload).toHaveBeenCalled();
            expect(h.toastr.success).toHaveBeenCalledWith("done");
            void prefix;
        });

        it.each([["Create"], ["Update"]])("%s: a refusal is toasted and the grid is not reloaded", (verb) => {
            const h = load();
            h.$(`#form${verb === "Create" ? "Create" : "Edit"}${N}`).trigger("submit");
            const call = h.ajaxCalls.find((a) => a.url === `/${c.controller}/${verb}${N}`)!;
            call.success!({ result: false, error: "rejected" });
            expect(h.toastr.error).toHaveBeenCalledWith("rejected");
            expect(h.MvcGridInstances).toHaveLength(0);
        });

        // ---- delete ----------------------------------------------------------------------------------

        it("Delete-confirm with a selected row just opens the confirmation modal", () => {
            const h = load();
            const modal = spyModal(h);
            selectRow(h, KEYS[1]);
            h.$(`#btnConfirmDelete${N}`).trigger("click");
            expect(modal).toHaveBeenCalledWith({ keyboard: false, backdrop: "static" });
            expect(modal).toHaveBeenCalledWith("show");
            expect(h.ajaxCalls.filter((a) => String(a.url).includes("Exists"))).toHaveLength(0);
        });

        it("Delete re-checks the record exists and then deletes the key the server returned; success closes, reloads and toasts", () => {
            const h = load();
            const modal = spyModal(h);
            selectRow(h, KEYS[1]);
            h.$(`#btnDelete${N}`).trigger("click");

            const exists = h.ajaxCalls.find((a) => String(a.url).includes(`Is${N}Exists`))!;
            expect(exists.url).toBe(existsUrl(KEYS[1]));
            exists.success!({ result: true, [c.existsKey]: { ...c.record, [recordKey]: 424242 } });

            const del = h.ajaxCalls.find((a) => a.url === `/${c.controller}/Delete${N}`)!;
            expect(JSON.parse(String(del.data))).toEqual(deleteBody(424242));
            del.success!({ result: true, message: "removed" });

            expect(modal).toHaveBeenCalledWith("hide");
            expect(lastGrid(h).reload).toHaveBeenCalled();
            expect(h.toastr.success).toHaveBeenCalledWith("removed");
        });

        it("Delete: a record that no longer exists, or a refused delete, is toasted and nothing is reloaded", () => {
            const h = load();
            selectRow(h, KEYS[1]);
            h.$(`#btnDelete${N}`).trigger("click");
            h.ajaxCalls.find((a) => String(a.url).includes(`Is${N}Exists`))!.success!({ result: false, error: "vanished" });
            expect(h.toastr.error).toHaveBeenCalledWith("vanished");
            expect(h.ajaxCalls.find((a) => a.url === `/${c.controller}/Delete${N}`)).toBeUndefined();

            h.$(`#btnDelete${N}`).trigger("click");
            const exists = h.ajaxCalls.filter((a) => String(a.url).includes(`Is${N}Exists`)).at(-1)!;
            exists.success!({ result: true, [c.existsKey]: c.record });
            h.ajaxCalls.find((a) => a.url === `/${c.controller}/Delete${N}`)!.success!({ result: false, error: "in use" });
            expect(h.toastr.error).toHaveBeenCalledWith("in use");
            expect(h.MvcGridInstances).toHaveLength(0);
        });
    });

    if (c.maturityPickers) {
        describe(`${c.area}/${c.feature}/${c.page} — maturity-date pickers`, () => {
            /** A datepicker fake whose "widget" carries the button panel the script appends its extra buttons to. */
            const fake = (h: SiteHandle) => {
                const pane = h.win.document.createElement("div");
                pane.className = "ui-datepicker-buttonpane";
                const widget = h.win.document.createElement("div");
                widget.appendChild(pane);
                const sets: { id: string; date: Date }[] = [];
                (h.$.fn as any).datepicker = function(this: any, cmd?: string, arg?: unknown) {
                    if (cmd === "widget") return h.$(widget);
                    if (cmd === "setDate") sets.push({ id: this[0]?.id, date: arg as Date });
                    return this;
                };
                return { pane, sets };
            };
            const [nmYear, nmMonth, nmDay] = (c.noMaturityDate ?? "9999-12-31").split("-").map(Number);

            it("localizes the pickers (yy-mm-dd, month after year)", () => {
                const h = load();
                const defaults = (h.$ as any).datepicker.setDefaults.mock.calls[0][0];
                expect(defaults).toMatchObject({ dateFormat: "yy-mm-dd", showMonthAfterYear: true });
                expect(defaults.monthNames).toHaveLength(12);
                expect(defaults.dayNamesMin).toHaveLength(7);
            });

            it.each(c.maturityPickers!)("%s: opening the picker adds 'No maturity date' and 'Today' buttons; each sets its value", (id) => {
                const h = load();
                const { pane, sets } = fake(h);
                vi.useFakeTimers(); // after load: loadSite manages its own fake timers while it evaluates the script
                try {
                    const init = h.datepickerInits.find((i) => i.el?.id === id);
                    expect(init, `a datepicker is attached to #${id}`).toBeDefined();
                    init!.options.beforeShow(init!.el);
                    vi.runAllTimers();
                } finally {
                    vi.useRealTimers();
                }

                const buttons = [...pane.querySelectorAll("button")];
                expect(buttons).toHaveLength(2);

                buttons[0].click(); // "No maturity date" → the far-future sentinel the server published
                expect(sets.at(-1)!.date).toEqual(new Date(nmYear, nmMonth - 1, nmDay));

                const before = Date.now();
                buttons[1].click(); // "Today"
                expect(Math.abs(sets.at(-1)!.date.getTime() - before)).toBeLessThan(5000);
                expect((h.$ as any).datepicker._clearDate).toHaveBeenCalledTimes(2);
            });

            it.each(c.maturityPickers!)("%s: changing the month/year re-adds the same two buttons, and they work", (id) => {
                const h = load();
                const { pane, sets } = fake(h);
                vi.useFakeTimers();
                try {
                    const init = h.datepickerInits.find((i) => i.el?.id === id)!;
                    init.options.onChangeMonthYear(2030, 5, { input: init.el });
                    vi.runAllTimers();
                } finally {
                    vi.useRealTimers();
                }
                const buttons = [...pane.querySelectorAll("button")];
                expect(buttons).toHaveLength(2);
                buttons[0].click();
                expect(sets.at(-1)!.date).toEqual(new Date(nmYear, nmMonth - 1, nmDay));
                const before = Date.now();
                buttons[1].click();
                expect(Math.abs(sets.at(-1)!.date.getTime() - before)).toBeLessThan(5000);
            });
        });
    }
}
