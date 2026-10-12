/*
 * Shared suite for the amount fields of the finance pages (AccountBook/{Asset,Income,Expenditure} and
 * Notice/{FixedIncome,FixedExpenditure}): each page hands
 * its create / edit amount inputs to the user _Layout's `bindAmountInput`, so a comma appears between every
 * three integer digits as the user types, and it shows a stored amount the same way when it fills the edit
 * form. The text is posted as shown ("1,234"); the server parses and re-validates it
 * (FinanceAmountPolicy.TryParseInput).
 */
import { describe, it, expect } from "vitest";
import { loadSite, fireNative, type SiteHandle, stubPlugin, present, successOf, instanceOfType } from "@tests/_common/harness";

/** What one finance page supplies to {@link describeAmountInput}. */
export interface AmountInputConfig {
    /** feature (controller) folder under user/custom, also the endpoints' controller segment */
    feature: "AccountBook" | "Notice";
    /** page folder, also the entity name in element ids and endpoints, e.g. "Asset" */
    page: "Asset" | "Income" | "Expenditure" | "FixedIncome" | "FixedExpenditure";
    /** the page's own fixture (without amount inputs); the suite appends the amount inputs, a grid row and the modal shells */
    fixture: () => string;
    /** property of the IsXExists reply that carries the record, e.g. "asset" */
    existsKey: string;
    /** what identifies a grid row: a numeric `Id` (`data-id`) or the asset's `ProductName` (`data-productName`) */
    rowKey: "Id" | "ProductName";
    /** a complete record as the IsXExists endpoint returns it (its `amount` is replaced by the suite) */
    record: Record<string, unknown>;
    /** whether the field keeps a leading minus (an asset balance may be negative; an income / expenditure, one-off or fixed, may not) */
    allowNegative: boolean;
}

/** Registers the amount-field tests for one finance page. */
export function describeAmountInput(c: AmountInputConfig): void {
    const N = c.page;
    const attr = c.rowKey === "Id" ? "data-id" : "data-productName";
    const load = (): SiteHandle => loadSite("user", c.feature, N, c.fixture() +
        `<input id="create${N}Amount" value="0" /><input id="edit${N}Amount" value="0" />` +
        `<div class="mvc-grid"></div><table><tbody><tr class="clsGridRow" ${attr}="row-1"></tr></tbody></table>` +
        `<div id="create${N}DialogModal"></div><div id="edit${N}DialogModal"></div><div id="confirmDelete${N}DialogModal"></div>`);
    /** The amount input `#id`. */
    const input = (h: SiteHandle, id: string): HTMLInputElement => instanceOfType(h.win.document.getElementById(id), HTMLInputElement);
    /** Types `text` into `#id` (sets the value with the caret at its end, then fires `input`). */
    const type = (h: SiteHandle, id: string, text: string): HTMLInputElement => {
        const field = input(h, id);
        field.value = text;
        field.setSelectionRange(text.length, text.length);
        h.$(field).trigger("input");
        return field;
    };

    describe(`user/${c.feature}/${N} — amount fields`, () => {
        it.each(["create", "edit"])("the %s amount gets a comma between every three integer digits as it is typed", (prefix) => {
            const h = load();
            expect(type(h, `${prefix}${N}Amount`, "1234567.891").value).toBe("1,234,567.891");
        });

        it.each(["create", "edit"])("the %s amount drops anything that is not a digit or the decimal point", (prefix) => {
            const h = load();
            expect(type(h, `${prefix}${N}Amount`, "12a3,45").value).toBe("12,345");
        });

        it.each(["create", "edit"])(`the %s amount ${c.allowNegative ? "keeps" : "drops"} a leading minus`, (prefix) => {
            const h = load();
            expect(type(h, `${prefix}${N}Amount`, "-1234").value).toBe(c.allowNegative ? "-1,234" : "1,234");
        });

        it("posts the create amount as shown", () => {
            const h = load();
            type(h, `create${N}Amount`, "1234");

            h.$(`#formCreate${N}`).trigger("submit");

            const call = present(h.ajaxCalls.find((a) => a.url === `/${c.feature}/Create${N}`));
            expect(JSON.parse(String(call.data))).toMatchObject({ Amount: "1,234" });
        });

        it("fills the edit form with the stored amount comma-grouped, and posts it as shown", () => {
            const h = load();
            stubPlugin(h, "modal", function(this: JQuery) {
                return this;
            });
            fireNative(h.win.document, "rowclick", { data: { [c.rowKey]: "row-1" } });
            h.$(`#btnEdit${N}GridRow`).trigger("click");

            successOf(present(h.ajaxCalls.find((a) => String(a.url).includes(`Is${N}Exists`))))({
                result: true,
                [c.existsKey]: { ...c.record, amount: 1234567.5 },
            });

            expect(input(h, `edit${N}Amount`).value).toBe("1,234,567.5");

            h.$(`#formEdit${N}`).trigger("submit");
            const call = present(h.ajaxCalls.find((a) => a.url === `/${c.feature}/Update${N}`));
            expect(JSON.parse(String(call.data))).toMatchObject({ Amount: "1,234,567.5" });
        });
    });
}
