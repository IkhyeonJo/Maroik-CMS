import { describe, it, expect } from "vitest";
import { describeMissingServerConstants } from "@tests/_common/missingConfigSuite";
import { describeAmountLabel } from "@tests/_common/amountLabelSuite";
import { describeGridCrudScript } from "@tests/_common/gridCrudSuite";
import { loadSite, hidden, antiForgery, hiddenByStyle, fireNative } from "@tests/_common/harness";

// wwwroot/user/custom/Notice/FixedExpenditure/js/site.js
/** Main class → sub-classes, as the page publishes it. */
const subClassMap = { Living: ["Rent"], Transfer: ["ToSavings"] };
/** Sub-classes that need a deposit (transfer target) asset. */
const depositAssetSubClasses = ["ToSavings"];
/** Last selectable deposit day per month (a month missing here gets no day options). */
const maxDepositDayByMonth = { "2": "28" };

/** The fixed-expenditure page DOM (server constants, create/edit forms). */
function fixture(): string {
    return (
        antiForgery +
        hidden("expenditureSubClassMap", JSON.stringify(subClassMap).replace(/"/g, "&quot;")) +
        hidden("expenditureDepositAssetSubClasses", JSON.stringify(depositAssetSubClasses).replace(/"/g, "&quot;")) +
        hidden("maxDepositDayByMonth", JSON.stringify(maxDepositDayByMonth).replace(/"/g, "&quot;")) +
        hidden("noMaturityDate", "9999-12-31") +
        hidden("localizerFailedToLoadAmountLabel", "Failed to load the amount label.") +
        `<div id="createFixedExpenditureTabs"></div><div id="editFixedExpenditureTabs"></div><input id="gridSearch" />
     <input id="createFixedExpenditureMaturityDate" /><input id="editFixedExpenditureMaturityDate" />
     <select id="createFixedExpenditureMainClass"><option value="Living">Living</option><option value="Transfer">Transfer</option></select>
     <select id="createFixedExpenditureSubClass"><option value="Rent">Rent</option><option value="ToSavings">ToSavings</option></select>
     <select id="editFixedExpenditureMainClass"></select><select id="editFixedExpenditureSubClass"></select>
     <select id="createFixedExpenditureDepositMonth"><option value="2">2</option></select>
     <select id="editFixedExpenditureDepositMonth"></select>
     <select id="createFixedExpenditureDepositDay">${Array.from({ length: 31 }, (_, i) => `<option value="${i + 1}">${i + 1}</option>`).join("")}</select>
     <select id="editFixedExpenditureDepositDay"></select>
     <div id="divCreateFixedExpenditureMyDepositAsset" style="display:none"></div>
     <div id="divEditFixedExpenditureMyDepositAsset" style="display:none"></div>
     <form id="formCreateFixedExpenditure"></form><form id="formEditFixedExpenditure"></form>
     <button id="btnExportExcelFixedExpenditure"></button><button id="btnEditFixedExpenditureGridRow"></button>
     <button id="btnConfirmDeleteFixedExpenditure"></button><button id="btnDeleteFixedExpenditure"></button>
     <select id="createFixedExpenditurePaymentMethod"><option value="Card" selected>Card</option></select>
     <select id="editFixedExpenditurePaymentMethod"><option value="Card">Card</option><option value="Cash">Cash</option></select>
     <span id="labelCreateFixedExpenditureAmount"></span><span id="labelEditFixedExpenditureAmount"></span>`
    );
}

describe("Notice/FixedExpenditure", () => {
    it("reveals the deposit-asset block only for a transfer sub-class", () => {
        const h = loadSite("user", "Notice", "FixedExpenditure", fixture());
        h.$("#createFixedExpenditureSubClass").val("ToSavings").trigger("change");
        expect(hiddenByStyle(h.win.document.getElementById("divCreateFixedExpenditureMyDepositAsset"))).toBe(false);
        h.$("#createFixedExpenditureSubClass").val("Rent").trigger("change");
        expect(hiddenByStyle(h.win.document.getElementById("divCreateFixedExpenditureMyDepositAsset"))).toBe(true);
    });

    it("Excel export targets /Notice/ExportExcelFixedExpenditure", () => {
        const h = loadSite("user", "Notice", "FixedExpenditure", fixture());
        h.$("#btnExportExcelFixedExpenditure").trigger("click");
        expect(h.submittedForms.at(-1)!.action).toContain("/Notice/ExportExcelFixedExpenditure");
    });

    it("shows a toastr error when the create-form amount-label lookup fails", () => {
        const h = loadSite("user", "Notice", "FixedExpenditure", fixture());
        // loadSite's onload $(function(){...}) already fired one GetFixedExpenditureAmountLabel
        // lookup for the preselected create-form payment method.
        const call = h.ajaxCalls.find((c) => String(c.url).includes("GetFixedExpenditureAmountLabel"));

        call?.error?.();

        expect(h.toastr.error).toHaveBeenCalledWith("Failed to load the amount label.");
    });

    it("shows a toastr error when the edit-form amount-label lookup fails", () => {
        const h = loadSite("user", "Notice", "FixedExpenditure", fixture());

        h.$("#editFixedExpenditurePaymentMethod").val("Cash").trigger("change");
        const call = h.lastAjax();
        expect(call.url).toContain("GetFixedExpenditureAmountLabel");

        call.error?.();

        expect(h.toastr.error).toHaveBeenCalledWith("Failed to load the amount label.");
    });
});

describeGridCrudScript({
    area: "user", feature: "Notice", page: "FixedExpenditure", entity: "FixedExpenditure", controller: "Notice", existsKey: "fixedExpenditure", rowKey: "Id",
    fixture: () => fixture(),
    maturityPickers: ["createFixedExpenditureMaturityDate", "editFixedExpenditureMaturityDate"], noMaturityDate: "9999-12-31",
    record: {
        id: 1,
        mainClass: "Living",
        subClass: "Rent",
        content: "c",
        amount: 1,
        depositMonth: 1,
        depositDay: 2,
        maturityDate: "2030-01-01",
        note: "",
        paymentMethod: "A",
        myDepositAsset: null,
        unpunctuality: false
    },
});

describeAmountLabel({
    label: "user/Notice/FixedExpenditure",
    load: () => loadSite("user", "Notice", "FixedExpenditure", fixture()),
    url: "GetFixedExpenditureAmountLabel",
    create: { trigger: "#createFixedExpenditurePaymentMethod", span: "#labelCreateFixedExpenditureAmount" },
    edit: { trigger: "#editFixedExpenditurePaymentMethod", span: "#labelEditFixedExpenditureAmount" },
});

describe("Notice/FixedExpenditure — class and deposit-month changes, and filling the edit form", () => {
    /** Values of the options of `sel` that are neither disabled nor hidden. */
    const enabled = (h: ReturnType<typeof loadSite>, sel: string) =>
        [...h.win.document.querySelectorAll<HTMLOptionElement>(`${sel} option`)].filter((o) => !o.disabled && !o.hidden).map((o) => o.value);

    it("choosing a create main class limits the sub-classes to it, selects the first, and shows the deposit-asset block only for a transfer", () => {
        const h = loadSite("user", "Notice", "FixedExpenditure", fixture());
        h.$("#createFixedExpenditureMainClass").val("Transfer").trigger("change");
        expect(enabled(h, "#createFixedExpenditureSubClass")).toEqual(["ToSavings"]);
        expect(h.$("#createFixedExpenditureSubClass").val()).toBe("ToSavings");
        expect(hiddenByStyle(h.win.document.getElementById("divCreateFixedExpenditureMyDepositAsset"))).toBe(false);

        h.$("#createFixedExpenditureMainClass").val("Living").trigger("change");
        expect(enabled(h, "#createFixedExpenditureSubClass")).toEqual(["Rent"]);
        expect(hiddenByStyle(h.win.document.getElementById("divCreateFixedExpenditureMyDepositAsset"))).toBe(true);
    });

    it("choosing a create deposit month limits the deposit days to that month's maximum (February → 28)", () => {
        const h = loadSite("user", "Notice", "FixedExpenditure", fixture());
        h.$("#createFixedExpenditureDepositMonth").val("2").trigger("change");
        const days = enabled(h, "#createFixedExpenditureDepositDay").map(Number);
        expect(days).toHaveLength(28);
        expect(Math.max(...days)).toBe(28);
    });

    /** Opens the edit form for row 1 and answers IsFixedExpenditureExists with a transfer record, `record` merged in. */
    const edit = (record: Record<string, unknown>) => {
        const h = loadSite("user", "Notice", "FixedExpenditure", fixture()
            + `<div class="mvc-grid"></div><table><tbody><tr class="clsGridRow" data-id="1"></tr></tbody></table>
         <div id="editFixedExpenditureDialogModal"></div><div id="confirmDeleteFixedExpenditureDialogModal"></div>
         <select id="editFixedExpenditureMyDepositAsset"><option value="acct">acct</option><option value="Card">Card</option></select>`);
        (h.$.fn as any).modal = function(this: any) {
            return this;
        };
        fireNative(h.win.document, "rowclick", { data: { Id: "1" } });
        h.$("#btnEditFixedExpenditureGridRow").trigger("click");
        h.ajaxCalls.find((a) => String(a.url).includes("IsFixedExpenditureExists"))!.success!({
            result: true, fixedExpenditure: {
                id: 1, mainClass: "Transfer", subClass: "ToSavings", content: "c", amount: 5, depositMonth: 2, depositDay: 28, maturityDate: "2030-01-01",
                note: "n", paymentMethod: "Card", myDepositAsset: null, ...record
            }
        });
        return h;
    };

    it("fills the edit form and reveals the deposit-asset block for a transfer; a stored deposit asset is selected", () => {
        const h = edit({ myDepositAsset: "acct" });
        expect(h.$("#editFixedExpenditureMyDepositAsset").val()).toBe("acct");
        expect(hiddenByStyle(h.win.document.getElementById("divEditFixedExpenditureMyDepositAsset"))).toBe(false);
    });

    it("without a stored deposit asset the edit form falls back to the payment method", () => {
        const h = edit({});
        expect(h.$("#editFixedExpenditureMyDepositAsset").val()).toBe("Card");
    });
});

describeMissingServerConstants("user", "Notice", "FixedExpenditure", () => fixture());
