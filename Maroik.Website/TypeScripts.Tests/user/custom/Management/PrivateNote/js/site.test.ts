import { describe, it, expect } from "vitest";
import { describeMissingServerConstants } from "@tests/_common/missingConfigSuite";
import describeBoardScript from "@tests/_common/boardSuite";
import { loadSite, hidden, antiForgery } from "@tests/_common/harness";

// wwwroot/user/custom/Management/PrivateNote/js/site.js
const fixture =
    antiForgery +
    hidden("maxAttachedFileSizeBytes", "1048576") +
    hidden("localizerIETFLanguageTag", "en-US") +
    `<div id="writeBoardContent"></div><div id="editBoardContent"></div>
   <div id="divEditBoardContent"></div><div id="detailBoardContent"></div>
   <select id="searchType"><option value="Title" selected>Title</option></select>
   <input id="btnPrivateNoteSearchText" value="hello" />
   <input id="writeBoardTitle" value="t" /><input id="editBoardTitle" />
   <input type="checkbox" id="writeBoardLocked" /><input type="checkbox" id="editBoardLocked" />
   <div id="loading"></div><textarea id="writePrivateNoteCommentContent"></textarea>
   <div id="confirmDeleteBoardDialogModal"></div>
   <form id="formWriteBoard"></form><form id="formEditBoard"></form>
   <form id="formWritePrivateNoteComment"></form>
   <input type="file" id="writeUploadedFile" data-errorMessage="too big" />
   <input type="file" id="editUploadedFile" data-errorMessage="too big" />
   <button id="btnPrivateNoteShowWriteBoardLoading"></button>
   <a id="btnPrivateNoteModify" data-link="/m"></a><a id="btnPrivateNoteList" data-link="/l"></a>
   <button id="btnPrivateNoteConfirmDeleteBoard"></button><button id="btnPrivateNoteSubmitModify"></button>
   <button id="btnPrivateNoteWrite"></button><button id="btnPrivateNoteSearchBoard"></button>
   <button id="btnPrivateNoteDeleteBoard" data-boardId="7"></button>
   <a id="aDetailBoardAttachedFile"></a><a id="aEditBoardAttachedFile"></a>`;

describe("user/Management/PrivateNote", () => {
    it("search navigates to /Management/PrivateNote with the encoded query", () => {
        const h = loadSite("user", "Management", "PrivateNote", fixture);
        h.$("#btnPrivateNoteSearchBoard").trigger("click");
        const url = h.navigations.at(-1)!;
        expect(url).toContain("/Management/PrivateNote?searchType=Title");
        expect(url).toContain("searchText=hello");
    });

    it("submitting the write form posts multipart to /Management/WritePrivateNoteBoard", () => {
        const h = loadSite("user", "Management", "PrivateNote", fixture);
        h.$("#formWriteBoard").trigger("submit");
        const call = h.lastAjax();
        expect(call.url).toBe("/Management/WritePrivateNoteBoard");
        expect(call.data).toBeInstanceOf(h.win.FormData);
        expect((call.data as FormData).get("Title")).toBe("t");
    });

    it("the list button navigates to its data-link", () => {
        const h = loadSite("user", "Management", "PrivateNote", fixture);
        h.$("#btnPrivateNoteList").trigger("click");
        expect(h.navigations.at(-1)).toBe("/l");
    });

    it("WriteUploadFile/EditUploadFile do not throw when change fires with no file selected", () => {
        const h = loadSite("user", "Management", "PrivateNote", fixture);
        expect(() => h.$("#writeUploadedFile").trigger("change")).not.toThrow();
        expect(() => h.$("#editUploadedFile").trigger("change")).not.toThrow();
    });
});

describeBoardScript({
    area: "user",
    feature: "Management",
    page: "PrivateNote",
    idInfix: "PrivateNote",
    listUrl: "/Management/PrivateNote",
    actionPrefix: "/Management",
    commentInfix: "PrivateNote",
    writeAction: "WritePrivateNoteBoard",
    editAction: "EditPrivateNoteBoard",
    commentAction: "WritePrivateNoteComment",
    existsKey: "privateNoteBoard",
    hasNoticed: false,
    downloadAction: "/Management/DownloadPrivateNoteAttachedFile",
});

describeMissingServerConstants("user", "Management", "PrivateNote", () => fixture);
