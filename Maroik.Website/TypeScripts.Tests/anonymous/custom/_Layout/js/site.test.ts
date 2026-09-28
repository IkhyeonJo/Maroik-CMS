import { describe, it, expect } from "vitest";
import { describeLayoutScript } from "@tests/_common/layoutSuite";
import { loadSite, antiForgery, hidden } from "@tests/_common/harness";

// wwwroot/anonymous/custom/_Layout/js/site.js
function fixture(): string {
    return (
        antiForgery +
        hidden("returnUri", "/back") +
        `<a id="aChangeCultureEnUS"></a><a id="aChangeCultureKoKR"></a>
     <a id="btnMainTopBarLogin"></a>
     <div id="loading"></div>
     <form><button type="submit" id="sbmt">go</button></form>`
    );
}

describe("anonymous/_Layout", () => {
    it("posts the chosen culture to /Dashboard/CultureManagement", () => {
        const h = loadSite("anonymous", "_Layout", "", fixture());
        h.$("#aChangeCultureKoKR").trigger("click");
        expect(JSON.parse(String(h.lastAjax().data))).toEqual({ Culture: "ko-KR" });
    });

    it("navigates to the login page from the top-bar login link", () => {
        const h = loadSite("anonymous", "_Layout", "", fixture());
        h.$("#btnMainTopBarLogin").trigger("click");
        expect(h.navigations).toContain("/Account/Login");
    });
});

describeLayoutScript("anonymous");
