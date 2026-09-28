import { describe, it, expect } from "vitest";
import { loadSite, antiForgery, hidden, hiddenByStyle } from "@tests/_common/harness";

// wwwroot/anonymous/custom/Account/js/site.js
function fixture(): string {
    return (
        antiForgery +
        hidden("returnUri", "/back") +
        `<a id="aChangeCultureEnUS"></a><a id="aChangeCultureKoKR"></a>
     <div id="loading" style="display:none"></div>
     <form id="loginForm"></form><form id="registerForm"></form>
     <form id="forgotPasswordForm"></form><form id="resetPasswordForm"></form>
     <button id="btnSignIn"></button><button id="btnRegister"></button>
     <button id="btnResend"></button><button id="btnChangePassword"></button>
     <button id="btnRequestNewPassword"></button>`
    );
}

describe("anonymous/Account", () => {
    it("switches culture via /Dashboard/CultureManagement", () => {
        const h = loadSite("anonymous", "Account", "", fixture());
        h.$("#aChangeCultureEnUS").trigger("click");
        expect(h.lastAjax().url).toBe("/Dashboard/CultureManagement");
        expect(JSON.parse(String(h.lastAjax().data))).toEqual({ Culture: "en-US" });
    });

    it("hides the loading spinner when the login form is invalid", () => {
        const h = loadSite("anonymous", "Account", "", fixture());
        (h.$.fn as any).valid = () => false;
        h.$("#loading").show();

        h.$("#btnSignIn").trigger("click");

        expect(hiddenByStyle(h.win.document.getElementById("loading"))).toBe(true);
    });

    it("posts the ko-KR culture with the anti-forgery header, then returns to the page it came from", () => {
        const h = loadSite("anonymous", "Account", "", fixture());
        h.$("#aChangeCultureKoKR").trigger("click");
        const call = h.lastAjax();
        expect(call.headers).toEqual({ RequestVerificationToken: "tok" });
        expect(JSON.parse(String(call.data))).toEqual({ Culture: "ko-KR" });
        call.success!({});
        expect(h.navigations.at(-1)).toBe("/back");
    });

    // Each button drives the loading overlay for ITS OWN form: valid → shown, invalid → hidden.
    it.each([
        ["#btnSignIn", "loginForm"],
        ["#btnRegister", "registerForm"],
        ["#btnResend", "registerForm"],
        ["#btnRequestNewPassword", "forgotPasswordForm"],
        ["#btnChangePassword", "resetPasswordForm"],
    ])("%s shows the loading overlay only when %s validates", (button, formId) => {
        const h = loadSite("anonymous", "Account", "", fixture());
        const validated: string[] = [];
        (h.$.fn as any).valid = function(this: JQuery) {
            validated.push(this.attr("id")!);
            return validated.length > 1; // first call: invalid, second call: valid
        };

        h.$("#loading").show();
        h.$(button).trigger("click");
        expect(validated).toEqual([formId]);
        expect(hiddenByStyle(h.win.document.getElementById("loading"))).toBe(true);

        h.$(button).trigger("click");
        expect(validated).toEqual([formId, formId]);
        expect(hiddenByStyle(h.win.document.getElementById("loading"))).toBe(false);
    });
});
