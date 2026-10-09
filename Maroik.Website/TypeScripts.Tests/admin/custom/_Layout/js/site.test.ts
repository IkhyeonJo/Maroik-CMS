import { describe, it, expect } from "vitest";
import { describeLayoutScript } from "@tests/_common/layoutSuite";
import { describeToolkit } from "@tests/_common/toolkitSuite";
import { loadSite, antiForgery, hidden } from "@tests/_common/harness";

// wwwroot/admin/custom/_Layout/js/site.js
function fixture(): string {
    return (
        antiForgery +
        hidden("returnUri", "/back") +
        `<a id="aChangeCultureEnUS"></a><a id="aChangeCultureKoKR"></a>
     <div id="loading"></div>
     <form><button type="submit" id="sbmt">go</button></form>`
    );
}

describe("admin/_Layout", () => {
    it("posts the chosen culture to /Dashboard/CultureManagement", () => {
        const h = loadSite("admin", "_Layout", "", fixture());

        h.$("#aChangeCultureKoKR").trigger("click");

        const call = h.lastAjax();
        expect(call.url).toBe("/Dashboard/CultureManagement");
        expect(call.type).toBe("POST");
        expect(JSON.parse(String(call.data))).toEqual({ Culture: "ko-KR" });
        expect(call.headers!.RequestVerificationToken).toBe("tok");
    });

    it("disables submit buttons while a POST is in flight and re-enables them after", () => {
        const h = loadSite("admin", "_Layout", "", fixture());
        const btn = h.win.document.getElementById("sbmt") as HTMLButtonElement;

        h.$(h.win.document).trigger("ajaxSend", [{}, { type: "POST" }]);
        expect(btn.disabled).toBe(true);

        h.$(h.win.document).trigger("ajaxComplete", [{}, { type: "POST" }]);
        expect(btn.disabled).toBe(false);
    });
});

describeLayoutScript("admin");
describeToolkit("admin");
