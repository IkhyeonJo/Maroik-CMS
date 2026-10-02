/*
 * Shared behavior suite for the two identical Management/Profile scripts (admin and user): the avatar
 * upload's client-side checks (mirroring ImageUploadPolicy / the server limit) and the password change.
 */
import { describe, it, expect } from "vitest";
import { loadSite, hidden, antiForgery, type SiteHandle } from "@tests/_common/harness";

/** The profile page DOM: size limit and allowed types, the avatar and password forms. */
const fixture =
    antiForgery +
    hidden("maxAttachedFileSizeBytes", "1048576") +
    hidden("allowedImageContentTypes", JSON.stringify(["image/png", "IMAGE/JPEG"]).replace(/"/g, "&quot;")) +
    `<form id="formUpdateProfileAvatar"><input type="file" id="ProfileAvatarFiles" name="ProfileAvatarFiles"
          data-noFileAttachedErrorMessage="attach a file"
          data-fileSizeLimitationErrorMessage="too big"
          data-fileTypeErrorMessage="wrong type" /></form>
   <form id="formUpdateProfilePassword"></form>
   <input id="Password" /><input id="NewPassword" />`;

/** Registers the shared profile-page tests for one area. */
export function describeProfileScript(area: "admin" | "user"): void {
    /** Loads the area's profile script over {@link fixture}. */
    const load = () => loadSite(area, "Management", "Profile", fixture);
    /** Selects a `size`-byte file of MIME `type` in the avatar input and fires `change`. */
    const choose = (h: SiteHandle, size: number, type: string) => {
        const file = new h.win.File([new Uint8Array(size)], "a.bin", { type });
        Object.defineProperty(h.$("#ProfileAvatarFiles")[0], "files", { configurable: true, value: [file] });
        h.$("#ProfileAvatarFiles").trigger("change");
    };

    describe(`${area}/Management/Profile — shared profile behaviour`, () => {
        it.each([
            ["a file over the size limit", 1_048_577, "image/png", "too big"],
            ["an empty file", 0, "image/png", "too big"],
            ["a file of a type the server refuses", 10, "image/gif", "wrong type"],
            ["an SVG", 10, "image/svg+xml", "wrong type"],
            ["a file without a type", 10, "", "wrong type"],
        ])("%s is refused with its localized message and sends nothing", (_what, size, type, message) => {
            const h = load();
            choose(h, size, type);
            expect(h.win.alert).toHaveBeenCalledWith(message);
            expect(h.navigations.at(-1)).toBe("/Management/Profile");
            expect(h.ajaxCalls).toHaveLength(0);
        });

        it("an avatar of an allowed type (matched case-insensitively) and size is posted as multipart form data", () => {
            const h = load();
            choose(h, 1_048_576, "image/jpeg");
            const call = h.lastAjax();
            expect(call.url).toBe("/Management/UpdateProfileAvatar");
            expect(call.data).toBeInstanceOf(h.win.FormData);
            expect(call.type).toBe("POST");
        });

        it("a successful avatar upload reloads the profile page; a refusal alerts the server's message first", () => {
            const h = load();
            choose(h, 10, "image/png");
            h.respond(0, { result: true });
            expect(h.navigations.at(-1)).toBe("/Management/Profile");
            expect(h.win.alert).not.toHaveBeenCalled();

            choose(h, 10, "image/png");
            h.respond(0, { result: false, errorMessage: "virus found" });
            expect(h.win.alert).toHaveBeenCalledWith("virus found");
            expect(h.navigations.at(-1)).toBe("/Management/Profile");

            choose(h, 10, "image/png");
            h.respond(0, { result: false, errorMessage: "A temporary error occurred. Please try again later." });
            expect(h.win.alert).toHaveBeenCalledWith("A temporary error occurred. Please try again later.");
        });

        it("selecting nothing is reported with the 'attach a file' message", () => {
            const h = load();
            Object.defineProperty(h.$("#ProfileAvatarFiles")[0], "files", { configurable: true, value: [] });
            h.$("#ProfileAvatarFiles").trigger("change");
            expect(h.win.alert).toHaveBeenCalledWith("attach a file");
        });

        it("the password form never navigates itself and sends the anti-forgery header", () => {
            const h = load();
            h.$("#Password").val("old");
            h.$("#NewPassword").val("new");
            const submit = h.$.Event("submit");
            h.$("#formUpdateProfilePassword").trigger(submit);
            expect(submit.isDefaultPrevented()).toBe(true);
            expect(h.lastAjax().headers).toEqual({ RequestVerificationToken: "tok" });
        });
    });
}
