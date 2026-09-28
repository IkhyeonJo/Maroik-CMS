import { describe, it, expect, vi } from "vitest";
import { describeMissingServerConstants } from "@tests/_common/missingConfigSuite";
import { describeAmountLabel } from "@tests/_common/amountLabelSuite";
import { describeGridCrudScript } from "@tests/_common/gridCrudSuite";
import { loadSite, hidden, antiForgery, hiddenByStyle, fireNative } from "@tests/_common/harness";

// wwwroot/user/custom/AccountBook/Expenditure/js/site.js
const subClassMap = { Living: ["Food", "Rent"], Transfer: ["ToSavings"] };
const depositAssetSubClasses = ["ToSavings"];

function fixture(): string {
    return (
        antiForgery +
        hidden("expenditureSubClassMap", JSON.stringify(subClassMap).replace(/"/g, "&quot;")) +
        hidden("expenditureDepositAssetSubClasses", JSON.stringify(depositAssetSubClasses).replace(/"/g, "&quot;")) +
        hidden("localizerFailedToLoadAmountLabel", "Failed to load the amount label.") +
        `<div id="createExpenditureTabs"></div><div id="editExpenditureTabs"></div>
     <input id="createExpenditureDate" value="2024-05-04" /><input id="editExpenditureDate" /><input id="gridSearch" />
     <select id="createExpenditureMainClass"><option value="Living">Living</option><option value="Transfer">Transfer</option></select>
     <select id="createExpenditureSubClass"><option value="Food">Food</option><option value="Rent">Rent</option><option value="ToSavings">ToSavings</option></select>
     <select id="editExpenditureMainClass"></select><select id="editExpenditureSubClass"></select>
     <div id="divCreateExpenditureMyDepositAsset" style="display:none"></div>
     <div id="divEditExpenditureMyDepositAsset" style="display:none"></div>
     <form id="formCreateExpenditure"></form><form id="formEditExpenditure"></form>
     <button id="btnExportExcelExpenditure"></button>
     <button id="btnEditExpenditureGridRow"></button><button id="btnConfirmDeleteExpenditure"></button>
     <button id="btnDeleteExpenditure"></button>
     <select id="createExpenditurePaymentMethod"><option value="Card" selected>Card</option></select>
     <select id="editExpenditurePaymentMethod"><option value="Card">Card</option><option value="Cash">Cash</option></select>
     <span id="labelCreateExpenditureAmount"></span><span id="labelEditExpenditureAmount"></span>`
    );
}

describe("AccountBook/Expenditure", () => {
    it("restricts sub-class options to the chosen main class", () => {
        const h = loadSite("user", "AccountBook", "Expenditure", fixture());
        h.$("#createExpenditureMainClass").val("Living").trigger("change");
        const enabled = [...h.win.document.querySelectorAll<HTMLOptionElement>("#createExpenditureSubClass option")]
            .filter((o) => !o.disabled).map((o) => o.value).sort();
        expect(enabled).toEqual(["Food", "Rent"]);
    });

    it("only reveals the deposit-asset block for a transfer sub-class", () => {
        const h = loadSite("user", "AccountBook", "Expenditure", fixture());
        h.$("#createExpenditureMainClass").val("Transfer").trigger("change"); // subclass becomes ToSavings
        expect(hiddenByStyle(h.win.document.getElementById("divCreateExpenditureMyDepositAsset"))).toBe(false);

        h.$("#createExpenditureMainClass").val("Living").trigger("change"); // subclass becomes Food
        expect(hiddenByStyle(h.win.document.getElementById("divCreateExpenditureMyDepositAsset"))).toBe(true);
    });

    it("Excel export targets /AccountBook/ExportExcelExpenditure", () => {
        const h = loadSite("user", "AccountBook", "Expenditure", fixture());
        h.$("#btnExportExcelExpenditure").trigger("click");
        expect(h.submittedForms.at(-1)!.action).toContain("/AccountBook/ExportExcelExpenditure");
    });

    it("shows a toastr error when the create-form amount-label lookup fails", () => {
        const h = loadSite("user", "AccountBook", "Expenditure", fixture());
        // loadSite's onload $(function(){...}) already fired one GetExpenditureAmountLabel lookup
        // for the preselected create-form payment method.
        const call = h.ajaxCalls.find((c) => String(c.url).includes("GetExpenditureAmountLabel"));

        call?.error?.();

        expect(h.toastr.error).toHaveBeenCalledWith("Failed to load the amount label.");
    });

    it("shows a toastr error when the edit-form amount-label lookup fails", () => {
        const h = loadSite("user", "AccountBook", "Expenditure", fixture());

        h.$("#editExpenditurePaymentMethod").val("Cash").trigger("change");
        const call = h.lastAjax();
        expect(call.url).toContain("GetExpenditureAmountLabel");

        call.error?.();

        expect(h.toastr.error).toHaveBeenCalledWith("Failed to load the amount label.");
    });
});

describeGridCrudScript({
    area: "user", feature: "AccountBook", page: "Expenditure", entity: "Expenditure", controller: "AccountBook", existsKey: "expenditure", rowKey: "Id",
    fixture: () => fixture(),
    record: {
        id: 1,
        mainClass: "Living",
        subClass: "Food",
        content: "c",
        amount: 1,
        created: "2025-01-02T03:04:05",
        paymentMethod: "A",
        myDepositAsset: null,
        note: ""
    },
});

describeAmountLabel({
    label: "user/AccountBook/Expenditure",
    load: () => loadSite("user", "AccountBook", "Expenditure", fixture()),
    url: "GetExpenditureAmountLabel",
    create: { trigger: "#createExpenditurePaymentMethod", span: "#labelCreateExpenditureAmount" },
    edit: { trigger: "#editExpenditurePaymentMethod", span: "#labelEditExpenditureAmount" },
});

describe("AccountBook/Expenditure — date pickers and filling the edit form", () => {
    it("both date pickers' repaint hooks run (they only schedule a no-op nudge)", () => {
        const h = loadSite("user", "AccountBook", "Expenditure", fixture());
        vi.useFakeTimers(); // after load: loadSite manages its own fake timers while it evaluates the script
        try {
            for (const id of ["createExpenditureDate", "editExpenditureDate"]) {
                const init = h.datepickerInits.find((i) => i.el?.id === id)!;
                expect(() => {
                    init.options.beforeShow();
                    init.options.onChangeMonthYear();
                    vi.runAllTimers();
                }).not.toThrow();
            }
        } finally {
            vi.useRealTimers();
        }
    });

    const edit = (record: Record<string, unknown>) => {
        const h = loadSite("user", "AccountBook", "Expenditure", fixture()
            + `<div class="mvc-grid"></div><table><tbody><tr class="clsGridRow" data-id="1"></tr></tbody></table>
         <div id="editExpenditureDialogModal"></div><div id="confirmDeleteExpenditureDialogModal"></div>
         <input id="editExpenditureId" /><input id="editExpenditureHour" /><select id="editExpenditureMyDepositAsset"><option value="acct">acct</option><option value="Card">Card</option></select>`);
        (h.$.fn as any).modal = function(this: any) {
            return this;
        };
        fireNative(h.win.document, "rowclick", { data: { Id: "1" } });
        h.$("#btnEditExpenditureGridRow").trigger("click");
        h.ajaxCalls.find((a) => String(a.url).includes("IsExpenditureExists"))!.success!({
            result: true, expenditure: {
                id: 1, mainClass: "Transfer", subClass: "ToSavings", content: "c", amount: 5, created: "2024-05-04T09:08:07", note: "n",
                paymentMethod: "Card", myDepositAsset: null, ...record
            }
        });
        return h;
    };

    it("a stored deposit asset is selected in the edit form", () => {
        const h = edit({ myDepositAsset: "acct" });
        expect(h.$("#editExpenditureMyDepositAsset").val()).toBe("acct");
        expect(h.$("#editExpenditureDate").val()).toBe("2024-05-04");
    });

    it("without a stored deposit asset the edit form falls back to the payment method", () => {
        const h = edit({});
        expect(h.$("#editExpenditureMyDepositAsset").val()).toBe("Card");
    });
});

describeMissingServerConstants("user", "AccountBook", "Expenditure", () => fixture());
