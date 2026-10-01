import { describe, it, expect, vi } from "vitest";
import { loadSite, antiForgery } from "@tests/_common/harness";

// wwwroot/anonymous/custom/Forum/FreeForum/js/site.js — read-only board view.
function fixture(): string {
    return (
        antiForgery +
        `<span id="free_forum_detail_updated">2024-01-02T03:04:05Z</span>
     <table id="tblFreeForum"><tbody><tr>
       <td class="free_forum_board_created">2024-06-07T08:09:10Z</td>
     </tr></tbody></table>
     <span class="free_forum_detail_comment_created">2024-03-04T05:06:07Z</span>
     <select id="searchType"><option value="Title" selected>Title</option></select>
     <input id="btnFreeForumSearchText" value="term" />
     <button id="btnFreeForumSearchBoard"></button><button id="btnFreeForumWrite"></button>
     <a id="btnFreeForumList" data-link="/list"></a>
     <form id="formWriteFreeComment"></form>
     <div id="detailBoardContent"></div><a id="aDetailBoardAttachedFile"></a>`
    );
}

describe("Forum/FreeForum (anonymous)", () => {
    it("rewrites the UTC detail timestamp to the browser locale string", () => {
        const h = loadSite("anonymous", "Forum", "FreeForum", fixture());
        const shown = h.$("#free_forum_detail_updated").text();
        expect(shown).toBe(new Date("2024-01-02T03:04:05Z").toLocaleString());
    });

    it("search navigates to /Forum/FreeForum with the encoded query", () => {
        const h = loadSite("anonymous", "Forum", "FreeForum", fixture());
        h.$("#btnFreeForumSearchBoard").trigger("click");
        expect(h.navigations.at(-1)).toContain("/Forum/FreeForum?searchType=Title");
        expect(h.navigations.at(-1)).toContain("searchText=term");
    });

    it("blocks Enter inside the comment form", () => {
        const h = loadSite("anonymous", "Forum", "FreeForum", fixture());
        const ev: any = h.$.Event("keydown", { key: "Enter" });
        h.$("#formWriteFreeComment").trigger(ev);
        // jQuery stashes the last handler's return value on event.result
        expect(ev.result).toBe(false);
    });
});

describe("Forum/FreeForum (anonymous) — pages without the optional parts", () => {
    it("a list page (no detail timestamp, body or attachment link) initialises without touching them", () => {
        const html = fixture()
            .replace("<span id=\"free_forum_detail_updated\">2024-01-02T03:04:05Z</span>", "")
            .replace("<div id=\"detailBoardContent\"></div><a id=\"aDetailBoardAttachedFile\"></a>", "");
        const h = loadSite("anonymous", "Forum", "FreeForum", html);
        expect(h.$("#tblFreeForum .free_forum_board_created").text()).toBe(new Date("2024-06-07T08:09:10Z").toLocaleString());
    });

    it("a detail body that is already visible is left alone", () => {
        const boxes = vi.spyOn(window.HTMLElement.prototype, "getClientRects").mockReturnValue([{}] as unknown as DOMRectList); // jsdom has no layout: give every element a box
        try {
            const html = fixture().replace("<div id=\"detailBoardContent\"></div>", `<div id="detailBoardContent" style="display:block"><p>shown</p><img data-file="QUJD" data-contenttype="image/png" alt=""></div>`);
            const h = loadSite("anonymous", "Forum", "FreeForum", html);
            expect(h.$("#detailBoardContent").html()).toContain("data-file=\"QUJD\""); // not rehydrated: the body was not hidden
        } finally {
            boxes.mockRestore();
        }
    });
});

describe("Forum/FreeForum (anonymous) — navigation, images and attachments", () => {
    /** Loads the anonymous forum script over `html`. */
    const load = (html = fixture()) => loadSite("anonymous", "Forum", "FreeForum", html);

    it("the write button opens the write view and the list button its data-link", () => {
        const h = load();
        h.$("#btnFreeForumWrite").trigger("click");
        h.$("#btnFreeForumList").trigger("click");
        expect(h.navigations).toEqual(["/Forum/FreeForum?method=write", "/list"]);
    });

    it("Enter in the search box searches; other keys do not", () => {
        const h = load();
        h.$("#btnFreeForumSearchText").trigger(h.$.Event("keydown", { key: "a" }));
        expect(h.navigations).toHaveLength(0);
        h.$("#btnFreeForumSearchText").trigger(h.$.Event("keydown", { key: "Enter" }));
        expect(h.navigations.at(-1)).toContain("searchText=term");
    });

    it("rewrites the UTC list and comment timestamps to the browser locale string", () => {
        const h = load();
        expect(h.$(".free_forum_detail_comment_created").text()).toBe(new Date("2024-03-04T05:06:07Z").toLocaleString());
        expect(h.$("#tblFreeForum .free_forum_board_created").text()).toBe(new Date("2024-06-07T08:09:10Z").toLocaleString());
    });

    it("a timestamp wrapped in an <span> is rewritten inside that span", () => {
        const h = load(fixture().replace(
            "<td class=\"free_forum_board_created\">2024-06-07T08:09:10Z</td>",
            "<td class=\"free_forum_board_created\"><span style=\"color:green\">2024-06-07T08:09:10Z</span></td>"));
        expect(h.$("#tblFreeForum .free_forum_board_created span").html()).toBe(new Date("2024-06-07T08:09:10Z").toLocaleString());
    });

    it("the hidden detail body has its inline base64 images swapped for object URLs and is then revealed", () => {
        const h = load(fixture().replace(
            "<div id=\"detailBoardContent\"></div>",
            `<div id="detailBoardContent" style="display:none"><p>x</p><img data-file="${btoa("abc")}" data-contenttype="image/png" alt=""><img data-file="${btoa("no-type")}" alt=""></div>`));
        const el = h.$("#detailBoardContent")[0] as HTMLElement;
        expect(el.innerHTML).toContain("blob:");
        expect(el.innerHTML).toContain("<p>x</p>");
        expect(el.innerHTML).toContain("data-file"); // the image without a content type is left alone
        expect(el.style.display).not.toBe("none");
    });

    it.each(["onload", "onerror"] as const)("a rebuilt detail image's %s releases the object URL it was given", (event) => {
        const h = load(fixture().replace(
            "<div id=\"detailBoardContent\"></div>",
            `<div id="detailBoardContent" style="display:none"><img data-file="${btoa("abc")}" data-contenttype="image/png" alt=""></div>`));
        const revoke = ((h.win as any).URL.revokeObjectURL = vi.fn());
        const img = h.$("#detailBoardContent img")[0] as HTMLImageElement;

        (img[event] as () => void)();

        expect(revoke).toHaveBeenCalledWith(img.src);
    });

    /** The page with an attachment link for post 42 named "report.zip". */
    const withAttachment = () => load(fixture().replace(
        "<a id=\"aDetailBoardAttachedFile\"></a>",
        `<a id="aDetailBoardAttachedFile" data-boardid="42" data-name="report.zip"></a>`));

    it("clicking the attachment link asks the download endpoint for the post's attachment as a blob", () => {
        const h = withAttachment();
        const ev = h.$.Event("click");
        h.$("#aDetailBoardAttachedFile").trigger(ev);

        expect(ev.isDefaultPrevented()).toBe(true);
        const call = h.lastAjax() as any;
        expect(call.url).toBe("/Forum/DownloadFreeBoardAttachedFile");
        expect(call.type).toBe("POST");
        expect(call.data).toEqual({ boardId: "42" });
        expect(call.headers).toEqual({ RequestVerificationToken: "tok" });
        expect(call.xhrFields).toEqual({ responseType: "blob" });
    });

    it("the returned file is saved under the attachment's name, and its object URL released shortly after", () => {
        const click = vi.spyOn(window.HTMLAnchorElement.prototype, "click").mockImplementation(function() { /* jsdom cannot navigate */
        });
        try {
            const h = withAttachment();
            (h.win as any).URL.createObjectURL = () => "blob:attachment";
            const revoke = ((h.win as any).URL.revokeObjectURL = vi.fn());
            h.$("#aDetailBoardAttachedFile").trigger("click");
            vi.useFakeTimers();
            try {
                h.respond(0, new h.win.Blob(["zip-bytes"], { type: "application/x-zip-compressed" }));
                expect(click).toHaveBeenCalledTimes(1);
                expect((click.mock.contexts[0] as HTMLAnchorElement).download).toBe("report.zip");
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

    it("a refusal is shown as the server's error and nothing is saved; a link without a post id does nothing", async () => {
        const click = vi.spyOn(window.HTMLAnchorElement.prototype, "click").mockImplementation(function() { /* noop */
        });
        try {
            const h = withAttachment();
            h.$("#aDetailBoardAttachedFile").trigger("click");
            h.respond(0, new h.win.Blob([JSON.stringify({ result: false, error: "The post could not be found." })], { type: "application/json; charset=utf-8" }));
            await vi.waitFor(() => expect(h.toastr.error).toHaveBeenCalledWith("The post could not be found."));
            expect(click).not.toHaveBeenCalled();

            const without = load();
            without.$("#aDetailBoardAttachedFile").trigger("click");
            expect(without.ajaxCalls).toHaveLength(0);
        } finally {
            click.mockRestore();
        }
    });
});
