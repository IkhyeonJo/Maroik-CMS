import { describe, it, expect } from "vitest";
import { describeGridCrudScript } from "@tests/_common/gridCrudSuite";
import { loadSite, antiForgery, fireNative, present, lastOf } from "@tests/_common/harness";

// wwwroot/user/custom/AccountBook/Asset/js/site.js — grid CRUD, no policy map.
function fixture(): string {
    return (
        antiForgery +
        `<div id="createAssetTabs"></div><div id="editAssetTabs"></div><input id="gridSearch" />
     <div class="mvc-grid"></div>
     <table><tbody>
       <tr class="clsGridRow" data-productName="Gold"></tr>
       <tr class="clsGridRow" data-productName="Cash"></tr>
       <tr class="clsGridRow" data-productName="A&amp;B Bank #1"></tr>
     </tbody></table>
     <form id="formCreateAsset"></form><form id="formEditAsset"></form>
     <button id="btnExportExcelAsset"></button>
     <button id="btnEditAssetGridRow" data-errorMessageSelectGridRow="pick a row"></button>
     <button id="btnConfirmDeleteAsset" data-errorMessageSelectGridRow="pick a row"></button>
     <button id="btnDeleteAsset" data-errorMessageSelectGridRow="pick a row"></button>`
    );
}

describe("AccountBook/Asset", () => {
    it("highlights the row matching the grid rowclick detail", () => {
        const h = loadSite("user", "AccountBook", "Asset", fixture());

        fireNative(h.win.document, "rowclick", { data: { ProductName: "Cash" } });

        const rows = [...h.win.document.querySelectorAll(".clsGridRow")];
        expect(rows.map((r) => r.classList.contains("table-primary"))).toEqual([false, true, false]);
    });

    it("shows an error toast when Edit is clicked with no row selected", () => {
        const h = loadSite("user", "AccountBook", "Asset", fixture());
        h.$("#btnEditAssetGridRow").trigger("click");
        expect(h.toastr.error).toHaveBeenCalledWith("pick a row");
        expect(h.ajaxCalls).toHaveLength(0);
    });

    it("Excel export posts a form to /AccountBook/ExportExcelAsset (fileName=Asset)", () => {
        const h = loadSite("user", "AccountBook", "Asset", fixture());
        h.$("#btnExportExcelAsset").trigger("click");
        const form = lastOf(h.submittedForms);
        expect(form.action).toContain("/AccountBook/ExportExcelAsset");
        expect(present(form.querySelector<HTMLInputElement>("input[name=\"fileName\"]")).value).toBe("Asset");
    });

    it("percent-encodes the selected product name in the IsAssetExists query", () => {
        const h = loadSite("user", "AccountBook", "Asset", fixture());
        fireNative(h.win.document, "rowclick", { data: { ProductName: "A&B Bank #1" } });

        h.$("#btnEditAssetGridRow").trigger("click");

        expect(h.lastAjax().url).toBe("/AccountBook/IsAssetExists?productName=A%26B%20Bank%20%231");
    });
});

describeGridCrudScript({
    area: "user", feature: "AccountBook", page: "Asset", entity: "Asset", controller: "AccountBook", existsKey: "asset", rowKey: "ProductName",
    fixture: () => fixture(),
    record: { productName: "suite", item: "FreeDepositAndWithdrawal", amount: 1, monetaryUnit: "KRW", note: "", deleted: false },
});
