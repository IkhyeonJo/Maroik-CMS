/*
 * Shared behavior suite for the four near-identical "board" page scripts:
 *   user/admin Forum/FreeForum and user/admin Management/PrivateNote.
 * They differ only in element-id prefix, endpoint URLs and the JSON key the IsBoardExists reply
 * uses (and the admin builds add a "Noticed" checkbox), so each site.test.ts calls
 * `describeBoardScript(...)` with its own names — the assertions are the same, the script under
 * test is the compiled one at the matching wwwroot path.
 */
import { describe, it, expect, vi } from "vitest";
import { loadSite, hidden, antiForgery, type SiteHandle } from "@tests/_common/harness";

/** What one board page supplies to `describeBoardScript`. */
export interface BoardScriptConfig {
    /** area folder of the compiled script */
    area: "admin" | "user";
    /** feature (controller) folder, e.g. "Forum" */
    feature: string;
    /** page folder, e.g. "FreeForum" */
    page: string;
    /** e.g. "FreeForum" → ids `btnFreeForumWrite`, `formWriteFreeComment`, … ; "PrivateNote" likewise */
    idInfix: string;
    /** e.g. "/Forum/FreeForum" */
    listUrl: string;
    /** e.g. "/Forum" — prefix of the UploadImageFile / IsBoardExists / DeleteBoard / DeleteComment endpoints */
    actionPrefix: string;
    /** URL of the write-post endpoint */
    writeAction: string;
    /** URL of the edit-post endpoint */
    editAction: string;
    /** URL of the write-comment endpoint */
    commentAction: string;
    /** infix of the comment form / textarea ids: `formWrite${commentInfix}Comment` ("Free" for the forum, "PrivateNote") */
    commentInfix: string;
    /** property of the IsBoardExists reply that carries the post, e.g. "freeBoard" */
    existsKey: string;
    /** admin builds send a Noticed flag on write */
    hasNoticed: boolean;
    /** URL of the attachment-download endpoint */
    downloadAction: string;
}

/** The shared board-page DOM (list, write/edit forms, comment form, delete controls) with ids built from `c`, plus `extra`. */
export function boardFixture(c: BoardScriptConfig, extra = ""): string {
    const i = c.idInfix;
    return (
        antiForgery +
        hidden("maxAttachedFileSizeBytes", "1000") +
        hidden("localizerIETFLanguageTag", "en-US") +
        `<div id="writeBoardContent"></div><div id="editBoardContent"></div>
     <div id="divEditBoardContent"></div><div id="detailBoardContent" style="display:none"></div>
     <div id="confirmDeleteBoardDialogModal"></div>
     <select id="searchType"><option value="Title" selected>Title</option></select>
     <input id="btn${i}SearchText" value="q 1&2" />
     <input id="writeBoardTitle" value="hello" /><input id="editBoardTitle" value="edited" />
     <input type="checkbox" id="writeBoardLocked" /><input type="checkbox" id="editBoardLocked" />
     <input type="checkbox" id="writeBoardNoticed" />
     <div id="loading" style="display:none"></div><textarea id="write${c.commentInfix}CommentContent"></textarea>
     <form id="formWriteBoard"></form>
     <form id="formEditBoard" data-editBoardId="7" data-editCurrentPage="3"></form>
     <form id="formWrite${c.commentInfix}Comment" data-detailBoardId="5" data-detailCurrentPage="2" data-errorMessage="write something"></form>
     <input type="file" id="writeUploadedFile" data-errorMessage="too big" />
     <input type="file" id="editUploadedFile" data-errorMessage="too big" />
     <button id="btn${i}ShowWriteBoardLoading"></button>
     <a id="btn${i}Modify" data-link="/modify-me"></a><a id="btn${i}List" data-link="/list-me"></a>
     <button id="btn${i}ConfirmDeleteBoard"></button><button id="btn${i}SubmitModify"></button>
     <button id="btn${i}Write"></button><button id="btn${i}SearchBoard"></button>
     <button id="btn${i}DeleteBoard" data-boardId="3"></button>
     <a class="a${i}DeleteComment" data-commentId="11" data-detailBoardId="5" data-detailCurrentPage="2"></a>
     ${extra}`
    );
}

/** A jsdom File-backed `files` list on a file input (jsdom does not let a test assign `input.files`). */
function selectFile(h: SiteHandle, selector: string, size: number, name = "a.txt"): File {
    const file = new h.win.File([new Uint8Array(size)], name);
    Object.defineProperty(h.$(selector)[0], "files", { configurable: true, value: [file] });
    return file;
}

/** Registers the shared board-page tests for one page (exported as this module's default). */
function describeBoardScript(c: BoardScriptConfig): void {
    const i = c.idInfix;
    /** Loads the page script over {@link boardFixture} (+ `extra`). */
    const load = (extra = "", opts: Parameters<typeof loadSite>[4] = {}) =>
        loadSite(c.area, c.feature, c.page, boardFixture(c, extra), opts);

    describe(`${c.area}/${c.feature}/${c.page} — shared board behaviour`, () => {
        // ---- navigation / search ------------------------------------------------------------------

        it("the modify and list buttons navigate to their data-link", () => {
            const h = load();
            h.$(`#btn${i}Modify`).trigger("click");
            h.$(`#btn${i}List`).trigger("click");
            expect(h.navigations).toEqual(["/modify-me", "/list-me"]);
        });

        it("the write button opens the write view", () => {
            const h = load();
            h.$(`#btn${i}Write`).trigger("click");
            expect(h.navigations.at(-1)).toBe(`${c.listUrl}?method=write`);
        });

        it("search encodes both the type and the text", () => {
            const h = load();
            h.$(`#btn${i}SearchBoard`).trigger("click");
            expect(h.navigations.at(-1)).toBe(`${c.listUrl}?searchType=Title&searchText=${encodeURIComponent("q 1&2")}`);
        });

        it("Enter in the search box searches, other keys do not", () => {
            const h = load();

            h.$(`#btn${i}SearchText`).trigger(h.$.Event("keydown", { key: "a" }));
            expect(h.navigations).toHaveLength(0);

            h.$(`#btn${i}SearchText`).trigger(h.$.Event("keydown", { key: "Enter" }));
            expect(h.navigations).toHaveLength(1);
        });

        it("the confirm-delete button opens the modal (static, no Esc) and shows it", () => {
            const h = load();
            const modal = vi.fn(function(this: any) {
                return this;
            });
            (h.$.fn as any).modal = modal;
            h.$(`#btn${i}ConfirmDeleteBoard`).trigger("click");
            expect(modal).toHaveBeenCalledWith({ keyboard: false, backdrop: "static" });
            expect(modal).toHaveBeenCalledWith("show");
        });

        // ---- loading overlay -----------------------------------------------------------------------

        it("the write/edit submit buttons show the loading overlay only when the form is valid", () => {
            const h = load();
            const overlay = () => (h.$("#loading")[0] as HTMLElement).style.display;
            for (const button of [`#btn${i}ShowWriteBoardLoading`, `#btn${i}SubmitModify`]) {
                (h.$.fn as any).valid = () => false;
                h.$("#loading").show();
                const invalid = h.$.Event("click");
                h.$(button).trigger(invalid);
                expect(overlay()).toBe("none");
                expect(invalid.isDefaultPrevented()).toBe(true);

                (h.$.fn as any).valid = () => true;
                h.$("#loading").hide();
                h.$(button).trigger("click");
                expect(overlay()).not.toBe("none");
            }
        });

        // ---- attachment inputs ---------------------------------------------------------------------

        it.each(["write", "edit"])("the %s attachment over the size limit is rejected with the localized message and cleared", (which) => {
            const h = load();
            const input = h.$(`#${which}UploadedFile`)[0] as HTMLInputElement;
            selectFile(h, `#${which}UploadedFile`, 5000);
            h.$(input).trigger("change");
            expect(h.win.alert).toHaveBeenCalledWith("too big");
        });

        it("an attachment within the limit is sent with the write form", () => {
            const h = load();
            const file = selectFile(h, "#writeUploadedFile", 10, "ok.zip");
            h.$("#writeUploadedFile").trigger("change");
            h.$("#formWriteBoard").trigger("submit");
            expect((h.lastAjax().data as FormData).get("UploadedFile")).toBeInstanceOf(h.win.File);
            expect(((h.lastAjax().data as FormData).get("UploadedFile") as File).name).toBe(file.name);
        });

        it("an attachment within the limit is sent with the edit form", () => {
            const h = load();
            selectFile(h, "#editUploadedFile", 10, "edit.zip");
            h.$("#editUploadedFile").trigger("change");
            h.$("#formEditBoard").trigger("submit");
            expect(((h.lastAjax().data as FormData).get("UploadedFile") as File).name).toBe("edit.zip");
        });

        // ---- write ---------------------------------------------------------------------------------

        it("write: an invalid form sends nothing", () => {
            const h = load();
            (h.$.fn as any).valid = () => false;
            h.$("#formWriteBoard").trigger("submit");
            expect(h.ajaxCalls).toHaveLength(0);
        });

        it("write: posts title, locked flag and content, and never lets the form navigate itself", () => {
            const h = load();
            h.$("#writeBoardLocked").prop("checked", true);
            const submit = h.$.Event("submit");
            h.$("#formWriteBoard").trigger(submit);
            const call = h.lastAjax();
            const data = call.data as FormData;
            expect(call.url).toBe(`${c.actionPrefix}/${c.writeAction}`);
            expect(data.get("Title")).toBe("hello");
            expect(data.get("Locked")).toBe("true");
            expect(call.headers).toEqual({ RequestVerificationToken: "tok" });
            expect(submit.isDefaultPrevented()).toBe(true);
        });

        if (c.hasNoticed) {
            it("write: the admin build also sends the Noticed flag", () => {
                const h = load();
                h.$("#writeBoardNoticed").prop("checked", true);
                h.$("#formWriteBoard").trigger("submit");
                expect((h.lastAjax().data as FormData).get("Noticed")).toBe("true");
            });
        }

        it("write: success alerts the message and returns to the list", () => {
            const h = load();
            h.$("#formWriteBoard").trigger("submit");
            h.respond(0, { result: true, message: "saved" });
            expect(h.win.alert).toHaveBeenCalledWith("saved");
            expect(h.navigations.at(-1)).toBe(c.listUrl);
        });

        it("write: a refusal is toasted and the loading overlay is hidden", () => {
            const h = load();
            h.$("#loading").show();
            h.$("#formWriteBoard").trigger("submit");
            h.respond(0, { result: false, error: "nope" });
            expect(h.toastr.error).toHaveBeenCalledWith("nope");
            expect((h.$("#loading")[0] as HTMLElement).style.display).toBe("none");
            expect(h.navigations).toHaveLength(0);
        });

        // ---- edit ----------------------------------------------------------------------------------

        it("edit: an invalid form sends nothing", () => {
            const h = load();
            (h.$.fn as any).valid = () => false;
            h.$("#formEditBoard").trigger("submit");
            expect(h.ajaxCalls).toHaveLength(0);
        });

        it("edit: posts the id, title and locked flag from the form's data attributes", () => {
            const h = load();
            h.$("#editBoardLocked").prop("checked", true);
            h.$("#formEditBoard").trigger("submit");
            const call = h.lastAjax();
            const data = call.data as FormData;
            expect(call.url).toBe(`${c.actionPrefix}/${c.editAction}`);
            expect(data.get("Id")).toBe("7");
            expect(data.get("Title")).toBe("edited");
            expect(data.get("Locked")).toBe("true");
        });

        it("edit: success returns to that post's detail view on the same list page", () => {
            const h = load();
            h.$("#formEditBoard").trigger("submit");
            h.respond(0, { result: true, message: "updated" });
            expect(h.win.alert).toHaveBeenCalledWith("updated");
            expect(h.navigations.at(-1)).toBe(`${c.listUrl}?method=detail&boardId=7&page=3`);
        });

        it("edit: a refusal is toasted and the loading overlay is hidden", () => {
            const h = load();
            h.$("#formEditBoard").trigger("submit");
            h.respond(0, { result: false, error: "not yours" });
            expect(h.toastr.error).toHaveBeenCalledWith("not yours");
        });

        // ---- delete post ---------------------------------------------------------------------------

        it("delete: first re-checks the post exists, then deletes the id the server returned (not the one in the markup)", () => {
            const h = load();
            h.$(`#btn${i}DeleteBoard`).trigger("click");
            expect(h.lastAjax().url).toBe(`${c.actionPrefix}/IsBoardExists?id=3`);

            h.respond(0, { result: true, [c.existsKey]: { id: 99 } });

            const del = h.lastAjax();
            expect(del.url).toBe(`${c.actionPrefix}/DeleteBoard`);
            expect(JSON.parse(String(del.data))).toEqual({ Id: 99 });
        });

        it("delete: a successful delete closes the modal, alerts and returns to the list", () => {
            const h = load();
            const modal = vi.fn(function(this: any) {
                return this;
            });
            (h.$.fn as any).modal = modal;
            h.$(`#btn${i}DeleteBoard`).trigger("click");
            h.respond(0, { result: true, [c.existsKey]: { id: 3 } });
            h.respond(0, { result: true, message: "deleted" });
            expect(modal).toHaveBeenCalledWith("hide");
            expect(h.win.alert).toHaveBeenCalledWith("deleted");
            expect(h.navigations.at(-1)).toBe(c.listUrl);
        });

        it("delete: a refused delete, or a post that no longer exists, is toasted and nothing navigates", () => {
            const h = load();
            h.$(`#btn${i}DeleteBoard`).trigger("click");
            h.respond(0, { result: false, error: "gone" });
            expect(h.toastr.error).toHaveBeenCalledWith("gone");
            expect(h.ajaxCalls).toHaveLength(1);

            h.$(`#btn${i}DeleteBoard`).trigger("click");
            h.respond(0, { result: true, [c.existsKey]: { id: 3 } });
            h.respond(0, { result: false, error: "forbidden" });
            expect(h.toastr.error).toHaveBeenCalledWith("forbidden");
            expect(h.navigations).toHaveLength(0);
        });

        // ---- comments ------------------------------------------------------------------------------

        it("comment: an empty comment is refused client-side with the form's message", () => {
            const h = load();
            h.$(`#formWrite${c.commentInfix}Comment`).trigger("submit");
            expect(h.win.alert).toHaveBeenCalledWith("write something");
            expect(h.ajaxCalls).toHaveLength(0);
        });

        it("comment: posts the board id, text and page, then reloads the detail view", () => {
            const h = load();
            h.$(`#write${c.commentInfix}CommentContent`).val("nice post");
            const submit = h.$.Event("submit");
            h.$(`#formWrite${c.commentInfix}Comment`).trigger(submit);
            const call = h.lastAjax();
            expect(call.url).toBe(`${c.actionPrefix}/${c.commentAction}`);
            expect(JSON.parse(String(call.data))).toEqual({ BoardId: "5", Content: "nice post", DetailCurrentPage: "2" });
            expect(submit.isDefaultPrevented()).toBe(true);

            h.respond(0, { result: true, boardId: 5, page: 2 });
            expect(h.navigations.at(-1)).toBe(`${c.listUrl}?method=detail&boardId=5&page=2`);
        });

        it("comment: a refusal is toasted", () => {
            const h = load();
            h.$(`#write${c.commentInfix}CommentContent`).val("x");
            h.$(`#formWrite${c.commentInfix}Comment`).trigger("submit");
            h.respond(0, { result: false, error: "locked" });
            expect(h.toastr.error).toHaveBeenCalledWith("locked");
            expect(h.navigations).toHaveLength(0);
        });

        it("delete comment: posts the comment id and reloads the detail view; a refusal is toasted", () => {
            const h = load();
            const click = h.$.Event("click");
            h.$(`.a${i}DeleteComment`).trigger(click);
            expect(click.isDefaultPrevented()).toBe(true);
            expect(h.lastAjax().url).toBe(`${c.actionPrefix}/DeleteComment?id=11`);

            h.respond(0, { result: true });
            expect(h.navigations.at(-1)).toBe(`${c.listUrl}?method=detail&boardId=5&page=2`);

            h.$(`.a${i}DeleteComment`).trigger("click");
            h.respond(0, { result: false, error: "not allowed" });
            expect(h.toastr.error).toHaveBeenCalledWith("not allowed");
        });

        // ---- summernote image upload ------------------------------------------------------------------

        it.each([
            ["write", 0],
            ["edit", 1],
        ])(`the %s editor uploads each dropped image and inserts it as a <img alt=""> whose alt is the stored path`, (which, editorIndex) => {
            const h = load();
            const options = h.summernoteInits[editorIndex].options;
            const png = new h.win.File([new Uint8Array(4)], "p.png", { type: "image/png" });
            const jpg = new h.win.File([new Uint8Array(4)], "q.jpg", { type: "image/jpeg" });

            options.callbacks.onImageUpload([png, jpg]);

            expect(h.ajaxCalls).toHaveLength(2);
            expect(h.ajaxCalls[0].url).toBe(`${c.actionPrefix}/UploadImageFile`);
            expect((h.ajaxCalls[0].data as FormData).get("summernoteImageFile")).toBeInstanceOf(h.win.File);

            h.respond(1, { result: true, file: { fileContents: btoa("abc"), contentType: "image/png" }, filePath: "enc-path" });

            const insert = h.summernoteCalls.find((x) => x.args[0] === "insertNode");
            expect(insert?.el?.id).toBe(`${which}BoardContent`);
            const img = insert!.args[1] as HTMLImageElement;
            expect(img.getAttribute("alt")).toBe("enc-path");
            expect(img.src).toContain("blob:");
            expect(img.style.maxWidth).toBe("170px");
        });

        it.each([
            ["write", 0],
            ["edit", 1],
        ])("the %s editor releases an uploaded image's object URL once the image has loaded (or failed to)", (_which, editorIndex) => {
            for (const event of ["onload", "onerror"] as const) {
                const h = load();
                (h.win as any).URL.createObjectURL = () => "blob:uploaded";
                const revoke = ((h.win as any).URL.revokeObjectURL = vi.fn());
                h.summernoteInits[editorIndex].options.callbacks.onImageUpload([new h.win.File([new Uint8Array(4)], "p.png", { type: "image/png" })]);
                h.respond(0, { result: true, file: { fileContents: btoa("abc"), contentType: "image/png" }, filePath: "enc-path" });
                const img = h.summernoteCalls.find((x) => x.args[0] === "insertNode")!.args[1] as HTMLImageElement;
                expect(revoke).not.toHaveBeenCalled();
                (img[event] as () => void)();
                expect(revoke).toHaveBeenCalledWith("blob:uploaded");
            }
        });

        it.each([["write", 0], ["edit", 1]])("the %s editor alerts the server's message when an image is refused", (_which, editorIndex) => {
            const h = load();
            h.summernoteInits[editorIndex].options.callbacks.onImageUpload([new h.win.File([new Uint8Array(1)], "p.png")]);
            h.respond(0, { result: false, errorMessage: "png only" });
            expect(h.win.alert).toHaveBeenCalledWith("png only");
            expect(h.summernoteCalls.find((x) => x.args[0] === "insertNode")).toBeUndefined();
        });

        // ---- on-ready rehydration ---------------------------------------------------------------------

        it("detail view: inline base64 images become object URLs and the hidden body is revealed", () => {
            const h = loadSite(c.area, c.feature, c.page,
                boardFixture(c).replace(
                    "<div id=\"detailBoardContent\" style=\"display:none\"></div>",
                    `<div id="detailBoardContent" style="display:none"><p>text</p><img data-file="${btoa("abc")}" data-contenttype="image/png" alt=""><img data-file="${btoa("x")}" alt=""></div>`));
            const html = (h.$("#detailBoardContent")[0] as HTMLElement).innerHTML;
            expect(html).toContain("blob:");
            expect(html).toContain("<p>text</p>");
            // the image without a content type is left as-is (nothing to build a Blob from)
            expect(html).toContain("data-file");
            expect((h.$("#detailBoardContent")[0] as HTMLElement).style.display).not.toBe("none");
        });

        it.each(["onload", "onerror"] as const)("detail view: a rebuilt image's %s releases the object URL it was given", (event) => {
            const h = loadSite(c.area, c.feature, c.page,
                boardFixture(c).replace(
                    "<div id=\"detailBoardContent\" style=\"display:none\"></div>",
                    `<div id="detailBoardContent" style="display:none"><img data-file="${btoa("abc")}" data-contenttype="image/png" alt=""></div>`));
            const revoke = ((h.win as any).URL.revokeObjectURL = vi.fn());
            const img = h.$("#detailBoardContent img")[0] as HTMLImageElement; // the image as it is in the live page

            expect(revoke).not.toHaveBeenCalled();
            (img[event] as () => void)();

            expect(revoke).toHaveBeenCalledTimes(1);
            expect(revoke).toHaveBeenCalledWith(img.src);
        });

        it("edit view: the Summernote body is rehydrated and written back, then the editor container is revealed", () => {
            const body = `<p>hi</p><img data-file="${btoa("abc")}" data-contenttype="image/png" alt="">`;
            const h = loadSite(c.area, c.feature, c.page,
                boardFixture(c).replace("<div id=\"detailBoardContent\" style=\"display:none\"></div>", "")
                    .replace("<div id=\"divEditBoardContent\"></div>", "<div id=\"divEditBoardContent\" style=\"display:none\"></div>"),
                { summernoteCode: body });
            const set = h.summernoteCalls.find((x) => x.args[0] === "code");
            expect(String(set?.args[1])).toContain("blob:");
            expect(String(set?.args[1])).not.toContain("data-file");
            expect((h.$("#divEditBoardContent")[0] as HTMLElement).style.display).not.toBe("none");
        });

        it.each(["onload", "onerror"] as const)("edit view: a rebuilt image's %s releases the object URL it was given", (event) => {
            const h = loadSite(c.area, c.feature, c.page,
                boardFixture(c).replace("<div id=\"detailBoardContent\" style=\"display:none\"></div>", "")
                    .replace("<div id=\"editBoardContent\"></div>", "<div id=\"editBoardContent\"></div><div class=\"note-editor\"><img src=\"blob:live\" alt=\"\"></div>")
                    .replace("<div id=\"divEditBoardContent\"></div>", "<div id=\"divEditBoardContent\" style=\"display:none\"></div>"),
                { summernoteCode: `<img data-file="${btoa("abc")}" data-contenttype="image/png" alt="">` });
            const revoke = ((h.win as any).URL.revokeObjectURL = vi.fn());
            const img = h.$(".note-editor img")[0] as HTMLImageElement; // the editor's own copy of the rebuilt image

            (img[event] as () => void)();

            expect(revoke).toHaveBeenCalledWith("blob:live");
        });

        it("edit view: an inline image with no content type is left as stored", () => {
            const h = loadSite(c.area, c.feature, c.page,
                boardFixture(c).replace("<div id=\"detailBoardContent\" style=\"display:none\"></div>", "")
                    .replace("<div id=\"divEditBoardContent\"></div>", "<div id=\"divEditBoardContent\" style=\"display:none\"></div>"),
                { summernoteCode: `<img data-file="${btoa("abc")}" alt="">` });
            const set = h.summernoteCalls.find((x) => x.args[0] === "code");
            expect(String(set?.args[1])).toContain("data-file");
            expect(String(set?.args[1])).not.toContain("blob:");
        });

        it("a page with no editor container leaves the edit-view set-up alone", () => {
            const h = loadSite(c.area, c.feature, c.page,
                boardFixture(c).replace("<div id=\"editBoardContent\"></div>", "").replace("<div id=\"detailBoardContent\" style=\"display:none\"></div>", ""));
            expect(h.summernoteCalls.find((x) => x.args[0] === "code")).toBeUndefined();
        });

        // ---- attachment download ----------------------------------------------------------------------

        /** An attachment link for post 42 named "report.zip". */
        const attachmentLink = (id: string) => `<a id="${id}" data-boardid="42" data-name="report.zip"></a>`;

        it.each(["aDetailBoardAttachedFile", "aEditBoardAttachedFile"])("clicking #%s asks the download endpoint for the post's attachment as a blob", (id) => {
            const h = loadSite(c.area, c.feature, c.page, boardFixture(c, attachmentLink(id)));
            const ev = h.$.Event("click");
            h.$(`#${id}`).trigger(ev);

            expect(ev.isDefaultPrevented()).toBe(true);
            const call = h.lastAjax() as any;
            expect(call.url).toBe(c.downloadAction);
            expect(call.type).toBe("POST");
            expect(call.data).toEqual({ boardId: "42" });
            expect(call.headers).toEqual({ RequestVerificationToken: "tok" });
            expect(call.xhrFields).toEqual({ responseType: "blob" });
        });

        it.each(["aDetailBoardAttachedFile", "aEditBoardAttachedFile"])("the file returned for #%s is saved under the attachment's name, and its object URL released shortly after", (id) => {
            const click = vi.spyOn(window.HTMLAnchorElement.prototype, "click").mockImplementation(function() { /* no navigation in jsdom */
            });
            try {
                const h = loadSite(c.area, c.feature, c.page, boardFixture(c, attachmentLink(id)));
                (h.win as any).URL.createObjectURL = () => "blob:attachment";
                const revoke = ((h.win as any).URL.revokeObjectURL = vi.fn());
                h.$(`#${id}`).trigger("click");

                vi.useFakeTimers();
                try {
                    h.respond(0, new h.win.Blob(["zip-bytes"], { type: "application/x-zip-compressed" }));
                    expect(click).toHaveBeenCalledTimes(1);
                    expect((click.mock.contexts[0] as HTMLAnchorElement).download).toBe("report.zip");
                    expect((click.mock.contexts[0] as HTMLAnchorElement).href).toBe("blob:attachment");
                    expect(revoke).not.toHaveBeenCalled();
                    vi.advanceTimersByTime(100);
                    expect(revoke).toHaveBeenCalledWith("blob:attachment");
                } finally {
                    vi.useRealTimers();
                }
            } finally {
                click.mockRestore();
            }
        });

        it.each(["aDetailBoardAttachedFile", "aEditBoardAttachedFile"])("a refusal returned for #%s is shown as the server's error, and nothing is saved", async (id) => {
            const click = vi.spyOn(window.HTMLAnchorElement.prototype, "click").mockImplementation(function() { /* noop */
            });
            try {
                const h = loadSite(c.area, c.feature, c.page, boardFixture(c, attachmentLink(id)));
                h.$(`#${id}`).trigger("click");

                h.respond(0, new h.win.Blob([JSON.stringify({ result: false, error: "The post could not be found." })], { type: "application/json; charset=utf-8" }));

                await vi.waitFor(() => expect(h.toastr.error).toHaveBeenCalledWith("The post could not be found."));
                expect(click).not.toHaveBeenCalled();
            } finally {
                click.mockRestore();
            }
        });

        it.each(["aDetailBoardAttachedFile", "aEditBoardAttachedFile"])("the attachment link #%s without a post id does nothing", (id) => {
            const h = loadSite(c.area, c.feature, c.page, boardFixture(c, `<a id="${id}"></a>`));
            const before = h.ajaxCalls.length;
            h.$(`#${id}`).trigger("click");
            expect(h.ajaxCalls.length).toBe(before);
        });
    });
}

export default describeBoardScript;
