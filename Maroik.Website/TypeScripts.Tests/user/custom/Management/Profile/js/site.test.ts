import { describe, it, expect } from "vitest";
import { describeMissingServerConstants } from "@tests/_common/missingConfigSuite";
import { describeProfileScript } from "@tests/_common/profileSuite";
import { loadSite, hidden, antiForgery, stubPlugin } from "@tests/_common/harness";

// wwwroot/user/custom/Management/Profile/js/site.js
// (identical behavior to admin/Management/Profile; the two site.js differ only in
//  declaration order / quote style.)
const fixture =
    antiForgery +
    hidden("maxAttachedFileSizeBytes", "1048576") +
    hidden("allowedImageContentTypes", JSON.stringify(["image/png"]).replace(/"/g, "&quot;")) +
    `<form id="formUpdateProfileAvatar"></form>
   <form id="formUpdateProfilePassword"></form>
   <input type="file" id="ProfileAvatarFiles"
          data-noFileAttachedErrorMessage="attach a file"
          data-fileSizeLimitationErrorMessage="too big"
          data-fileTypeErrorMessage="wrong type" />
   <input id="Password" /><input id="NewPassword" />`;

describe("user/Management/Profile", () => {
    it("alerts 'attach a file' when the avatar input fires change with no file", () => {
        const h = loadSite("user", "Management", "Profile", fixture);
        h.$("#ProfileAvatarFiles").trigger("change");
        expect(h.win.alert).toHaveBeenCalledWith("attach a file");
        expect(h.ajaxCalls).toHaveLength(0);
    });

    it("makes no request when the password form is invalid", () => {
        const h = loadSite("user", "Management", "Profile", fixture);
        stubPlugin(h, "valid", () => false);
        h.$("#formUpdateProfilePassword").trigger("submit");
        expect(h.ajaxCalls).toHaveLength(0);
    });

    it("posts to /Management/UpdateProfilePassword when the password form is valid", () => {
        const h = loadSite("user", "Management", "Profile", fixture);
        h.$("#Password").val("old");
        h.$("#NewPassword").val("new");
        h.$("#formUpdateProfilePassword").trigger("submit");
        const call = h.lastAjax();
        expect(call.url).toBe("/Management/UpdateProfilePassword");
        expect(JSON.parse(String(call.data))).toEqual({ Password: "old", NewPassword: "new" });
    });

    it("alerts the message and goes to the sign-in page after a successful change", () => {
        const h = loadSite("user", "Management", "Profile", fixture);
        h.$("#formUpdateProfilePassword").trigger("submit");
        h.respond(0, { result: true, message: "ok" });
        expect(h.win.alert).toHaveBeenCalledWith("ok");
        expect(h.navigations).toEqual(["/Account/Login"]);
    });

    it("stays on the page and shows the error when the change is refused", () => {
        const h = loadSite("user", "Management", "Profile", fixture);
        h.$("#formUpdateProfilePassword").trigger("submit");
        h.respond(0, { result: false, error: "wrong password" });
        expect(h.toastr.error).toHaveBeenCalledWith("wrong password");
        expect(h.win.alert).not.toHaveBeenCalled();
        expect(h.navigations).toHaveLength(0);
    });
});

describeProfileScript("user");

describeMissingServerConstants("user", "Management", "Profile", () => fixture);
