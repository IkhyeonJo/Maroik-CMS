import { describe, it, expect } from "vitest";
import { describeMissingServerConstants } from "@tests/_common/missingConfigSuite";
import { describeAmountLabel } from "@tests/_common/amountLabelSuite";
import { describeGridCrudScript } from "@tests/_common/gridCrudSuite";
import { loadSite, hidden, antiForgery, lastOf } from "@tests/_common/harness";

// wwwroot/user/custom/Notice/FixedIncome/js/site.js
/** Main class → sub-classes, as the page publishes it. */
const subClassMap = { Labor: ["Salary"] };
/** Last selectable deposit day per month (a month missing here gets no day options). */
const maxDepositDayByMonth = { "2": 28, "1": 31 };

/** The fixed-income page DOM (server constants, create/edit forms). */
function fixture(): string {
    return (
        antiForgery +
        hidden("incomeSubClassMap", JSON.stringify(subClassMap).replace(/"/g, "&quot;")) +
        hidden("maxDepositDayByMonth", JSON.stringify(maxDepositDayByMonth).replace(/"/g, "&quot;")) +
        hidden("noMaturityDate", "9999-12-31") +
        hidden("localizerFailedToLoadAmountLabel", "Failed to load the amount label.") +
        `<div id="createFixedIncomeTabs"></div><div id="editFixedIncomeTabs"></div><input id="gridSearch" />
     <input id="createFixedIncomeMaturityDate" /><input id="editFixedIncomeMaturityDate" />
     <select id="createFixedIncomeMainClass"><option value="Labor">Labor</option></select>
     <select id="createFixedIncomeSubClass"><option value="Salary">Salary</option></select>
     <select id="editFixedIncomeMainClass"></select><select id="editFixedIncomeSubClass"></select>
     <select id="createFixedIncomeDepositMonth"><option value="1">1</option><option value="2">2</option></select>
     <select id="editFixedIncomeDepositMonth"></select>
     <select id="createFixedIncomeDepositDay">${Array.from({ length: 31 }, (_, i) => `<option value="${i + 1}">${i + 1}</option>`).join("")}</select>
     <select id="editFixedIncomeDepositDay"></select>
     <form id="formCreateFixedIncome"></form><form id="formEditFixedIncome"></form>
     <button id="btnExportExcelFixedIncome"></button>
     <button id="btnEditFixedIncomeGridRow" data-errorMessageSelectGridRow="Please select grid row"></button>
     <button id="btnConfirmDeleteFixedIncome" data-errorMessageSelectGridRow="L_SelectRow"></button><button id="btnDeleteFixedIncome" data-errorMessageSelectGridRow="L_SelectRow"></button>
     <select id="createFixedIncomeDepositMyAssetProductName"><option value="A" selected>A</option></select>
     <select id="editFixedIncomeDepositMyAssetProductName"><option value="A">A</option><option value="B">B</option></select>
     <span id="labelCreateFixedIncomeAmount"></span><span id="labelEditFixedIncomeAmount"></span>`
    );
}

describe("Notice/FixedIncome", () => {
    it("limits the deposit-day options to the max day for the chosen month (Feb → 28)", () => {
        const h = loadSite("user", "Notice", "FixedIncome", fixture());
        h.$("#createFixedIncomeDepositMonth").val("2").trigger("change");
        const enabled = [...h.win.document.querySelectorAll<HTMLOptionElement>("#createFixedIncomeDepositDay option")]
            .filter((o) => !o.disabled).map((o) => Number(o.value));
        expect(Math.max(...enabled)).toBe(28);
        expect(enabled).toHaveLength(28);
    });

    it("Excel export targets /Notice/ExportExcelFixedIncome", () => {
        const h = loadSite("user", "Notice", "FixedIncome", fixture());
        h.$("#btnExportExcelFixedIncome").trigger("click");
        expect(lastOf(h.submittedForms).action).toContain("/Notice/ExportExcelFixedIncome");
    });

    // Regression: the EDIT button's cached jQuery selector used to target a nonexistent id
    // (`#btnEditFixedExpenditureGridRow`) that didn't match the real markup's
    // `#btnEditFixedIncomeGridRow`, so the click handler was never wired and clicking EDIT did
    // nothing at all -- not even the "select a row first" error below.
    it("wires the EDIT button's click handler to the real #btnEditFixedIncomeGridRow markup id", () => {
        const h = loadSite("user", "Notice", "FixedIncome", fixture());
        h.$("#btnEditFixedIncomeGridRow").trigger("click");
        expect(h.toastr.error).toHaveBeenCalledWith("Please select grid row");
    });

    it("shows a toastr error when the create-form amount-label lookup fails", () => {
        const h = loadSite("user", "Notice", "FixedIncome", fixture());
        // loadSite's onload $(function(){...}) already fired one GetFixedIncomeAmountLabel lookup
        // for the preselected create-form deposit asset.
        const call = h.ajaxCalls.find((c) => String(c.url).includes("GetFixedIncomeAmountLabel"));

        call?.error?.();

        expect(h.toastr.error).toHaveBeenCalledWith("Failed to load the amount label.");
    });

    it("shows a toastr error when the edit-form amount-label lookup fails", () => {
        const h = loadSite("user", "Notice", "FixedIncome", fixture());

        h.$("#editFixedIncomeDepositMyAssetProductName").val("B").trigger("change");
        const call = h.lastAjax();
        expect(call.url).toContain("GetFixedIncomeAmountLabel");

        call.error?.();

        expect(h.toastr.error).toHaveBeenCalledWith("Failed to load the amount label.");
    });
});

describeGridCrudScript({
    area: "user", feature: "Notice", page: "FixedIncome", entity: "FixedIncome", controller: "Notice", existsKey: "fixedIncome", rowKey: "Id",
    fixture: () => fixture(),
    maturityPickers: ["createFixedIncomeMaturityDate", "editFixedIncomeMaturityDate"], noMaturityDate: "9999-12-31",
    record: {
        id: 1,
        mainClass: "Labor",
        subClass: "Salary",
        content: "c",
        amount: 1,
        depositMonth: 1,
        depositDay: 2,
        maturityDate: "2030-01-01",
        note: "",
        depositMyAssetProductName: "A",
        unpunctuality: true
    },
});

describeAmountLabel({
    label: "user/Notice/FixedIncome",
    load: () => loadSite("user", "Notice", "FixedIncome", fixture()),
    loadWithoutCreateChoices: () => loadSite("user", "Notice", "FixedIncome", fixture().replace('<select id="createFixedIncomeDepositMyAssetProductName"><option value="A" selected>A</option></select>', '<select id="createFixedIncomeDepositMyAssetProductName"></select>')),
    url: "GetFixedIncomeAmountLabel",
    create: { trigger: "#createFixedIncomeDepositMyAssetProductName", span: "#labelCreateFixedIncomeAmount" },
    edit: { trigger: "#editFixedIncomeDepositMyAssetProductName", span: "#labelEditFixedIncomeAmount" },
});

describe("Notice/FixedIncome — main class change", () => {
    it("choosing a create main class limits the sub-classes to it and selects the first", () => {
        const h = loadSite("user", "Notice", "FixedIncome", fixture());
        h.$("#createFixedIncomeMainClass").val("Labor").trigger("change");
        const enabled = [...h.win.document.querySelectorAll<HTMLOptionElement>("#createFixedIncomeSubClass option")].filter((o) => !o.disabled).map((o) => o.value);
        expect(enabled.length).toBeGreaterThan(0);
        expect(h.$("#createFixedIncomeSubClass").val()).toBe(enabled[0]);
    });
});

describeMissingServerConstants("user", "Notice", "FixedIncome", () => fixture());
