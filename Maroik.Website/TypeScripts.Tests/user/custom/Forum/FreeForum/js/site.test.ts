import { describe, it, expect } from "vitest";
import { describeRequiredServerConstants } from "@tests/_common/missingConfigSuite";
import describeBoardScript from "@tests/_common/boardSuite";
import { loadSite, hidden, antiForgery, lastOf, instanceOfType } from "@tests/_common/harness";

// wwwroot/user/custom/Forum/FreeForum/js/site.js
const fixture =
    antiForgery +
    hidden("maxAttachedFileSizeBytes", "1048576") +
    hidden("localizerIETFLanguageTag", "en-US") +
    `<div id="writeBoardContent"></div><div id="editBoardContent"></div>
   <div id="divEditBoardContent"></div><div id="detailBoardContent"></div>
   <div id="confirmDeleteBoardDialogModal"></div>
   <select id="searchType"><option value="Title" selected>Title</option></select>
   <input id="btnFreeForumSearchText" value="q1" />
   <input id="writeBoardTitle" value="hi" /><input id="editBoardTitle" />
   <input type="checkbox" id="writeBoardLocked" /><input type="checkbox" id="editBoardLocked" />
   <div id="loading"></div><textarea id="writeFreeCommentContent"></textarea>
   <form id="formWriteBoard"></form><form id="formEditBoard"></form><form id="formWriteFreeComment"></form>
   <input type="file" id="writeUploadedFile" data-errorMessage="big" />
   <input type="file" id="editUploadedFile" data-errorMessage="big" />
   <button id="btnFreeForumShowWriteBoardLoading"></button>
   <a id="btnFreeForumModify" data-link="/m"></a><a id="btnFreeForumList" data-link="/l"></a>
   <button id="btnFreeForumConfirmDeleteBoard"></button><button id="btnFreeForumSubmitModify"></button>
   <button id="btnFreeForumWrite"></button><button id="btnFreeForumSearchBoard"></button>
   <button id="btnFreeForumDeleteBoard" data-boardId="3"></button>
   <a id="aDetailBoardAttachedFile"></a><a id="aEditBoardAttachedFile"></a>
   <span class="aFreeForumDeleteComment"></span>`;

describe("user/Forum/FreeForum", () => {
    it("search navigates to /Forum/FreeForum with the encoded query", () => {
        const h = loadSite("user", "Forum", "FreeForum", fixture);
        h.$("#btnFreeForumSearchBoard").trigger("click");
        const url = lastOf(h.navigations);
        expect(url).toContain("/Forum/FreeForum?searchType=Title");
        expect(url).toContain("searchText=q1");
    });

    it("the write form posts multipart FormData to /Forum/WriteFreeBoard", () => {
        const h = loadSite("user", "Forum", "FreeForum", fixture);
        h.$("#formWriteBoard").trigger("submit");
        const call = h.lastAjax();
        expect(call.url).toBe("/Forum/WriteFreeBoard");
        expect(call.data).toBeInstanceOf(h.win.FormData);
        expect((instanceOfType(call.data, FormData)).get("Title")).toBe("hi");
    });

    it("WriteUploadFile/EditUploadFile do not throw when change fires with no file selected", () => {
        const h = loadSite("user", "Forum", "FreeForum", fixture);
        expect(() => h.$("#writeUploadedFile").trigger("change")).not.toThrow();
        expect(() => h.$("#editUploadedFile").trigger("change")).not.toThrow();
    });
});

describeBoardScript({
    area: "user",
    feature: "Forum",
    page: "FreeForum",
    idInfix: "FreeForum",
    listUrl: "/Forum/FreeForum",
    actionPrefix: "/Forum",
    commentInfix: "Free",
    writeAction: "WriteFreeBoard",
    editAction: "EditFreeBoard",
    commentAction: "WriteFreeComment",
    existsKey: "freeBoard",
    hasNoticed: false,
    downloadAction: "/Forum/DownloadFreeBoardAttachedFile",
});

describeRequiredServerConstants("user", "Forum", "FreeForum", () => fixture, ["maxAttachedFileSizeBytes"]);
