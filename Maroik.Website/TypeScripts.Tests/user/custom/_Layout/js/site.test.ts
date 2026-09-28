import { describe, it, expect } from "vitest";
import { describeLayoutScript } from "@tests/_common/layoutSuite";
import { loadSite, antiForgery, hidden } from "@tests/_common/harness";

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
        const btn = h.win.document.getElementById("sbmt") as HTMLButtonElement;
        h.$(h.win.document).trigger("ajaxSend", [{}, { type: "POST" }]);
        expect(btn.disabled).toBe(true);
        h.$(h.win).trigger("pageshow");
        expect(btn.disabled).toBe(false);
    });
});

describeLayoutScript("user");
