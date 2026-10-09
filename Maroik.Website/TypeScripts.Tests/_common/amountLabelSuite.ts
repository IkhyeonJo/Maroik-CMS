/*
 * Shared behavior for the finance pages' "amount label" lookup: changing the payment method / deposit asset asks the
 * server for the currency label shown next to the amount, and draws it (or a localized error) — success and failure,
 * on both the creation and the edit form.
 */
import { describe, it, expect } from "vitest";
import type { SiteHandle } from "@tests/_common/harness";

/** What one page supplies to {@link describeAmountLabel}. */
export interface AmountLabelConfig {
    /** suite-name prefix, e.g. "user/Notice/FixedExpenditure" */
    label: string;
    /** loads the page script with its fixture */
    load: () => SiteHandle;
    /** loads it with nothing to choose in the creation form's select (an account without assets) */
    loadWithoutCreateChoices: () => SiteHandle;
    /** substring of the lookup URL, e.g. "GetFixedExpenditureAmountLabel" */
    url: string;
    /** creation form: the select whose change triggers the lookup, and the span the label is drawn into */
    create: { trigger: string; span: string };
    /** edit form: the select whose change triggers the lookup, and the span the label is drawn into */
    edit: { trigger: string; span: string };
}

/** Registers the amount-label lookup tests (create and edit form, accepted and refused reply) for one page. */
export function describeAmountLabel(c: AmountLabelConfig): void {
    describe(`${c.label} — amount label lookup`, () => {
        it.each([["create", c.create], ["edit", c.edit]] as const)("%s form: the reply's label is drawn, whether the lookup was accepted or refused", (_side, side) => {
            const h = c.load();
            h.$(side.trigger).trigger("change");
            const call = h.lastAjax();
            expect(call.url).toContain(c.url);
            call.success!({ result: true, label: "KRW" });
            expect(h.$(side.span).text()).toBe("KRW");
            call.success!({ result: false, label: "—" });
            expect(h.$(side.span).text()).toBe("—");
        });

        it("on load, the creation form asks for the label of the preselected choice — and asks nothing when there is none", () => {
            expect(c.load().ajaxCalls.filter((call) => call.url.includes(c.url))).toHaveLength(1);
            expect(c.loadWithoutCreateChoices().ajaxCalls.filter((call) => call.url.includes(c.url))).toEqual([]);
        });

        it.each([["create", c.create], ["edit", c.edit]] as const)("%s form: the lookup's URL carries the URL-encoded selection", (_side, side) => {
            const h = c.load();
            h.$(side.trigger).append("<option value=\"A&B Bank\">x</option>").val("A&B Bank").trigger("change");
            expect(h.lastAjax().url).toContain("productName=A%26B%20Bank");
        });
    });
}
