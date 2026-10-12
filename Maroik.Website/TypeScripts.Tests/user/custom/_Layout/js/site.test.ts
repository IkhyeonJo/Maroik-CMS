import { describe, it, expect } from "vitest";
import { describeLayoutScript } from "@tests/_common/layoutSuite";
import { describeToolkit } from "@tests/_common/toolkitSuite";
import { loadSite, antiForgery, hidden, instanceOfType } from "@tests/_common/harness";

// wwwroot/user/custom/_Layout/js/site.js
function fixture(): string {
    return (
        antiForgery +
        hidden("returnUri", "/back") +
        `<a id="aChangeCultureEnUS"></a><a id="aChangeCultureKoKR"></a>
     <div id="loading"></div>
     <form><button type="submit" id="sbmt">go</button></form>`
    );
}

describe("user/_Layout", () => {
    it("posts the chosen culture to /Dashboard/CultureManagement", () => {
        const h = loadSite("user", "_Layout", "", fixture());
        h.$("#aChangeCultureEnUS").trigger("click");
        const call = h.lastAjax();
        expect(call.url).toBe("/Dashboard/CultureManagement");
        expect(JSON.parse(String(call.data))).toEqual({ Culture: "en-US" });
    });

    it("re-enables submit buttons on pageshow", () => {
        const h = loadSite("user", "_Layout", "", fixture());
        const btn = instanceOfType(h.win.document.getElementById("sbmt"), HTMLButtonElement);
        h.$(h.win.document).trigger("ajaxSend", [{}, { type: "POST" }]);
        expect(btn.disabled).toBe(true);
        h.$(h.win).trigger("pageshow");
        expect(btn.disabled).toBe(false);
    });
});

describe("user/_Layout — amount fields (window.formatAmountInput / bindAmountInput)", () => {
    it.each([
        ["", true, ""],
        ["0", true, "0"],
        ["7", true, "7"],
        ["999", true, "999"],
        ["1000", true, "1,000"],
        ["1234567", true, "1,234,567"],
        ["1234567.891", true, "1,234,567.891"],
        ["1,2345", true, "12,345"],
        ["0012", true, "12"],
        ["0.5", true, "0.5"],
        ["00.5", true, "0.5"],
        [".", true, "0."],
        [".5", true, "0.5"],
        ["1.", true, "1."],
        ["1.2.3", true, "1.23"],
        ["1.00010", true, "1.00010"],
        ["abc12x3", true, "123"],
        ["-1234.5", true, "-1,234.5"],
        ["-1234.5", false, "1,234.5"],
        ["-", true, "-"],
        ["-", false, ""],
        ["12-34", true, "1,234"],
        ["--5", true, "-5"],
        ["99999999999999999", true, "99,999,999,999,999,999"],
    ] as const)("formats %j (negative allowed: %s) as %j", (text, allowNegative, expected) => {
        const h = loadSite("user", "_Layout", "", fixture());
        expect(h.win.formatAmountInput(text, allowNegative)).toBe(expected);
    });

    it("formats a stored amount the page reads back as a number", () => {
        const h = loadSite("user", "_Layout", "", fixture());
        expect(h.win.formatAmountInput(String(1234567.5), true)).toBe("1,234,567.5");
        expect(h.win.formatAmountInput(String(-0.0001), true)).toBe("-0.0001");
    });

    /** A bound amount input holding `value` with the caret at `caret`, after an `input` event. */
    const typed = (value: string, caret: number, allowNegative = true): HTMLInputElement => {
        const h = loadSite("user", "_Layout", "", fixture() + `<input id="amount" />`);
        h.win.bindAmountInput(h.$("#amount"), allowNegative);
        const field = instanceOfType(h.win.document.getElementById("amount"), HTMLInputElement);
        field.value = value;
        field.setSelectionRange(caret, caret);
        h.$(field).trigger("input");
        return field;
    };

    it("reformats a bound input on every keystroke", () => {
        expect(typed("12345", 5).value).toBe("12,345");
        expect(typed("-12345", 6, false).value).toBe("12,345");
    });

    it("keeps the caret after the digit just typed when a comma is added before it", () => {
        const field = typed("1234", 4);
        expect(field.value).toBe("1,234");
        expect(field.selectionStart).toBe(5);
    });

    it("keeps the caret in place when a digit is typed in the middle", () => {
        // "1,234|567" -> the user types "9" after "1,234": "1,2349|567"
        const field = typed("1,2349567", 6);
        expect(field.value).toBe("12,349,567");
        expect(field.selectionStart).toBe(6);
        expect(field.value.slice(0, field.selectionStart ?? 0)).toBe("12,349");
    });

    it("keeps the caret in front of the same digits when a comma disappears", () => {
        // "1,234" -> the user deletes the "1": ",234" with the caret at 0
        const field = typed(",234", 0);
        expect(field.value).toBe("234");
        expect(field.selectionStart).toBe(0);
    });
});

describeLayoutScript("user");
describeToolkit("user");
