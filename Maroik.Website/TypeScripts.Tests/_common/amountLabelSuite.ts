/*
 * Shared behavior for the finance pages' "amount label" lookup: changing the payment method / deposit asset asks the
 * server for the currency label shown next to the amount, and draws it (or a localized error) — success and failure,
 * on both the creation and the edit form.
 */
import { describe, it, expect } from "vitest";
import type { SiteHandle } from "@tests/_common/harness";

export interface AmountLabelConfig {
    label: string;
    load: () => SiteHandle;
    /** substring of the lookup URL, e.g. "GetFixedExpenditureAmountLabel" */
    url: string;
    create: { trigger: string; span: string };
    edit: { trigger: string; span: string };
}

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

        it.each([["create", c.create], ["edit", c.edit]] as const)("%s form: the lookup's URL carries the URL-encoded selection", (_side, side) => {
            const h = c.load();
            h.$(side.trigger).append("<option value=\"A&B Bank\">x</option>").val("A&B Bank").trigger("change");
            expect(h.lastAjax().url).toContain("productName=A%26B%20Bank");
        });
    });
}
