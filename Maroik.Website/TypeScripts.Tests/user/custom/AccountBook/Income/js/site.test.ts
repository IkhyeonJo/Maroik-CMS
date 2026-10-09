import { describe, it, expect, vi } from "vitest";
import { describeMissingServerConstants } from "@tests/_common/missingConfigSuite";
import { describeAmountLabel } from "@tests/_common/amountLabelSuite";
import { describeGridCrudScript } from "@tests/_common/gridCrudSuite";
import { loadSite, hidden, antiForgery, stubPlugin, present } from "@tests/_common/harness";

// wwwroot/user/custom/AccountBook/Income/js/site.js
//
// Worth locking down: the income main→subclass UX mirror (ApplyAllowedOptions),
// the Excel export form, and that an invalid create form makes no request.

/** Main class → sub-classes, as the page publishes it. */
const subClassMap = { Labor: ["Salary", "Bonus"], Business: ["Sales"] };

/** The income page DOM (server constants, create/edit forms), plus `extra`. */
function fixture(extra = ""): string {
    return (
        antiForgery +
        hidden("incomeSubClassMap", JSON.stringify(subClassMap).replace(/"/g, "&quot;")) +
        hidden("localizerFailedToLoadAmountLabel", "Failed to load the amount label.") +
        `<div id="createIncomeTabs"></div><div id="editIncomeTabs"></div>
     <input id="createIncomeDate" /><input id="editIncomeDate" />
     <select id="createIncomeHour"><option value="9" selected>9</option></select><select id="createIncomeMinute"><option value="30" selected>30</option></select><select id="createIncomeSecond"><option value="0" selected>0</option></select><select id="editIncomeHour"><option value="9" selected>9</option></select><select id="editIncomeMinute"><option value="30" selected>30</option></select><select id="editIncomeSecond"><option value="0" selected>0</option></select><input id="gridSearch" />
     <select id="createIncomeMainClass">
       <option value="Labor">Labor</option><option value="Business">Business</option>
     </select>
     <select id="createIncomeSubClass">
       <option value="Salary">Salary</option><option value="Bonus">Bonus</option><option value="Sales">Sales</option>
     </select>
     <select id="editIncomeMainClass"></select><select id="editIncomeSubClass"></select>
     <form id="formCreateIncome"></form><form id="formEditIncome"></form>
     <button id="btnExportExcelIncome"></button>
     <button id="btnEditIncomeGridRow" data-errorMessageSelectGridRow="L_SelectRow"></button><button id="btnConfirmDeleteIncome" data-errorMessageSelectGridRow="L_SelectRow"></button>
     <button id="btnDeleteIncome" data-errorMessageSelectGridRow="L_SelectRow"></button>
     <select id="createIncomeDepositMyAssetProductName"><option value="A" selected>A</option></select>
     <select id="editIncomeDepositMyAssetProductName"><option value="A">A</option><option value="B">B</option><option value="A&amp;B Bank">A&amp;B Bank</option></select>
     <span id="labelCreateIncomeAmount"></span><span id="labelEditIncomeAmount"></span>` +
        extra
    );
}

describe("AccountBook/Income", () => {
    it("filters the create sub-class options to the selected main class", () => {
        const h = loadSite("user", "AccountBook", "Income", fixture());

        h.$("#createIncomeMainClass").val("Business").trigger("change");

        const opts = [...h.win.document.querySelectorAll<HTMLOptionElement>("#createIncomeSubClass option")];
        const enabled = opts.filter((o) => !o.disabled).map((o) => o.value);
        expect(enabled).toEqual(["Sales"]);
        expect(opts.filter((o) => o.disabled).map((o) => o.value).sort()).toEqual(["Bonus", "Salary"]);
    });

    it("Excel export posts a form to /AccountBook/ExportExcelIncome with fileName=Income", () => {
        const h = loadSite("user", "AccountBook", "Income", fixture());

        h.$("#btnExportExcelIncome").trigger("click");

        expect(h.submittedForms).toHaveLength(1);
        const form = present(h.submittedForms[0]);
        expect(form.method.toUpperCase()).toBe("POST");
        expect(form.action).toContain("/AccountBook/ExportExcelIncome");
        expect(present(form.querySelector<HTMLInputElement>("input[name=\"fileName\"]")).value).toBe("Income");
        expect(present(form.querySelector<HTMLInputElement>("input[name=\"__RequestVerificationToken\"]")).value).toBe("tok");
    });

    it("does not call $.ajax when the create form is invalid", () => {
        const h = loadSite("user", "AccountBook", "Income", fixture());
        stubPlugin(h, "valid", () => false);

        h.$("#formCreateIncome").trigger("submit");

        expect(h.ajaxCalls.filter((c) => String(c.url).includes("CreateIncome"))).toHaveLength(0);
    });

    it("shows a toastr error when the create-form amount-label lookup fails", () => {
        const h = loadSite("user", "AccountBook", "Income", fixture());
        // loadSite's onload $(function(){...}) already fired one GetIncomeAmountLabel lookup
        // for the preselected create-form asset.
        const call = h.ajaxCalls.find((c) => String(c.url).includes("GetIncomeAmountLabel"));

        call?.error?.();

        expect(h.toastr.error).toHaveBeenCalledWith("Failed to load the amount label.");
    });

    it("shows a toastr error when the edit-form amount-label lookup fails", () => {
        const h = loadSite("user", "AccountBook", "Income", fixture());

        h.$("#editIncomeDepositMyAssetProductName").val("B").trigger("change");
        const call = h.lastAjax();
        expect(call.url).toContain("GetIncomeAmountLabel");

        call.error?.();

        expect(h.toastr.error).toHaveBeenCalledWith("Failed to load the amount label.");
    });

    it("sets the create-form amount label from a successful lookup", () => {
        const h = loadSite("user", "AccountBook", "Income", fixture());
        const call = h.ajaxCalls.find((c) => String(c.url).includes("GetIncomeAmountLabel"));

        call?.success?.({ result: true, label: "KRW" });

        expect(present(h.win.document.querySelector("#labelCreateIncomeAmount")).textContent).toBe("KRW");
    });

    it("percent-encodes the chosen deposit asset in the amount-label lookup query", () => {
        const h = loadSite("user", "AccountBook", "Income", fixture());

        h.$("#editIncomeDepositMyAssetProductName").val("A&B Bank").trigger("change");

        expect(h.lastAjax().url).toBe("/AccountBook/GetIncomeAmountLabel?productName=A%26B%20Bank");
    });
});

describeGridCrudScript({
    area: "user", feature: "AccountBook", page: "Income", entity: "Income", controller: "AccountBook", existsKey: "income", rowKey: "Id",
    fixture: () => fixture(),
    record: {
        id: 1,
        mainClass: "Labor",
        subClass: "Salary",
        content: "c",
        amount: 1,
        created: "2025-01-02T03:04:05",
        depositMyAssetProductName: "A",
        note: ""
    },
});

describeAmountLabel({
    label: "user/AccountBook/Income",
    load: () => loadSite("user", "AccountBook", "Income", fixture()),
    loadWithoutCreateChoices: () => loadSite("user", "AccountBook", "Income", fixture().replace('<select id="createIncomeDepositMyAssetProductName"><option value="A" selected>A</option></select>', '<select id="createIncomeDepositMyAssetProductName"></select>')),
    url: "GetIncomeAmountLabel",
    create: { trigger: "#createIncomeDepositMyAssetProductName", span: "#labelCreateIncomeAmount" },
    edit: { trigger: "#editIncomeDepositMyAssetProductName", span: "#labelEditIncomeAmount" },
});

describe("AccountBook/Income — date pickers", () => {
    it("both date pickers' repaint hooks run (they only schedule a no-op nudge)", () => {
        const h = loadSite("user", "AccountBook", "Income", fixture());
        vi.useFakeTimers(); // after load: loadSite manages its own fake timers while it evaluates the script
        try {
            for (const id of ["createIncomeDate", "editIncomeDate"]) {
                const init = present(h.datepickerInits.find((i) => i.el?.id === id));
                expect(init, `a datepicker is attached to #${id}`).toBeDefined();
                expect(() => {
                    present(init.options.beforeShow)();
                    present(init.options.onChangeMonthYear)();
                    vi.runAllTimers();
                }).not.toThrow();
            }
        } finally {
            vi.useRealTimers();
        }
    });
});

describeMissingServerConstants("user", "AccountBook", "Income", () => fixture());
