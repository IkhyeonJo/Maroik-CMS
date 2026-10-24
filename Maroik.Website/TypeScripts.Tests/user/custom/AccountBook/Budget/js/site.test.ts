import { describe, it, expect, vi, afterEach } from "vitest";
import { loadSite, hidden, antiForgery, present, successOf, stubPlugin, type SiteHandle, type AjaxCall, type FakeCodeMirror } from "@tests/_common/harness";

// wwwroot/user/custom/AccountBook/Budget/js/site.js — the Income Budget / Expenditure Budget sheet: a spreadsheet grid drawn from the
// server's sheet, a CodeMirror formula bar painted with the server's colouring, keyboard / mouse editing, paste / fill, undo / redo,
// the Budget / Actual / Remaining views, the year, the lines and the Excel download.

/** The page shell, as Budget.cshtml renders it. */
function fixture(kind = "Expenditure", year = 2026): string {
    return (
        antiForgery +
        hidden("budgetKind", kind) +
        hidden("budgetYear", String(year)) +
        `<input type="hidden" id="budgetFunctions" value='${JSON.stringify([
            { Name: "SUM", MinArguments: 1, MaxArguments: 255, Signature: "SUM(number1, [number2], ...)" },
            { Name: "ROUND", MinArguments: 2, MaxArguments: 2, Signature: "ROUND(number, num_digits)" },
            { Name: "ROUNDUP", MinArguments: 2, MaxArguments: 2, Signature: "ROUNDUP(number, num_digits)" },
        ])}' />` +
        `<input type="hidden" id="budgetSubClassMap" value='${JSON.stringify({ ConsumerSpending: ["MealOrEatOutExpenses", "PinMoney"], RegularSavings: ["Deposit", "Investment"] })}' />` +
        `<input type="hidden" id="budgetClassLabels" value='${JSON.stringify({ ConsumerSpending: "Consumer", MealOrEatOutExpenses: "Meal", PinMoney: "Pin", RegularSavings: "Savings", Deposit: "Deposit" })}' />` +
        `<input type="hidden" id="budgetColumnCaptions" value='${JSON.stringify({ B: "Jan", M: "Dec", N: "Total" })}' />` +
        hidden("localizerConfirmRemoveLine", "Remove {0} ({1})?") +
        hidden("localizerNotBudgeted", "Not budgeted") +
        hidden("localizerAddLine", "Add line") +
        hidden("localizerRemoveLine", "Remove line") +
        `<button id="btnBudgetPrevYear"></button><span id="budgetYearLabel"></span><button id="btnBudgetNextYear"></button><button id="btnBudgetThisYear"></button>
         <div id="budgetViewSwitch"><button data-view="budget" class="active"></button><button data-view="actual"></button><button data-view="remaining"></button></div>
         <button id="btnBudgetCarryOver"></button><button id="btnBudgetAddLine"></button><button id="btnBudgetExcel"></button>
         <span id="budgetNameBox"></span><textarea id="budgetFormula"></textarea><ul id="budgetAutocomplete" class="d-none"></ul>
         <span id="budgetPreviewValue"></span><div id="budgetProblems"></div>
         <div id="budgetGridWrap" tabindex="0"><table id="budgetGrid"></table></div><div id="budgetUnbudgeted"></div>
         <div id="budgetLineModal"><form id="formBudgetLine"><select id="budgetLineMainClass"><option value="ConsumerSpending">Consumer</option><option value="RegularSavings">Savings</option><option value="Unknown">?</option></select>
           <select id="budgetLineSubClass"></select><input id="budgetLineCurrency" /></form></div>
         <div id="confirmRemoveLineModal"><p id="confirmRemoveLineText"></p><button id="btnConfirmRemoveLine"></button></div>`
    );
}

/** The editable columns of a line row and of a header / subtotal / total row. */
const LINE_COLUMNS = "BCDEFGHIJKLMOPQRSTUVWXYZ";
const FIXED_COLUMNS = "OPQRSTUVWXYZ";

/** A row as the controller sends it. */
function row(number: number, kind: string, label: string, over: Record<string, unknown> = {}): Record<string, unknown> {
    return {
        number, kind, label, mainClass: "", subClass: "", currency: "",
        editable: kind === "Line" ? LINE_COLUMNS : kind === "Free" ? "ABCDEFGHIJKLMNOPQRSTUVWXYZ" : FIXED_COLUMNS, ...over,
    };
}

/** A cell as the controller sends it. */
function cell(address: string, role: string, display: string, over: Record<string, unknown> = {}): Record<string, unknown> {
    return { address, role, input: "", display, valueKind: display === "" ? "Empty" : "Number", actual: "", remaining: "", state: "", ...over };
}

/** The test sheet: 1 header, 2 Meal (KRW), 3 Pin (KRW), 4 subtotal, 5 total, 6 and 7 free rows. */
function sheet(over: Record<string, unknown> = {}): Record<string, unknown> {
    return {
        kind: "Expenditure", year: 2026, exists: true,
        rows: [
            row(1, "Header", "Consumer", { mainClass: "ConsumerSpending" }),
            row(2, "Line", "Meal", { mainClass: "ConsumerSpending", subClass: "MealOrEatOutExpenses", currency: "KRW" }),
            row(3, "Line", "Pin", { mainClass: "ConsumerSpending", subClass: "PinMoney", currency: "KRW" }),
            row(4, "Subtotal", "Subtotal", { mainClass: "ConsumerSpending", currency: "KRW" }),
            row(5, "Total", "Total", { currency: "KRW" }),
            row(6, "Free", ""),
            row(7, "Free", ""),
        ],
        cells: [
            cell("B2", "Budget", "300,000", { input: "300000", actual: "120,000", remaining: "180,000", state: "good" }),
            cell("C2", "Budget", "315,000", { input: "=B2*1.05", actual: "400,000", remaining: "-85,000", state: "bad" }),
            cell("B3", "Budget", "", { actual: "5", state: "unbudgeted" }),
            cell("N2", "Computed", "615,000", { input: "=SUM(B2:M2)" }),
            cell("B4", "Computed", "300,000", { input: "=SUM(B2,B3)" }),
            cell("A6", "Free", "Tax rate", { input: "Tax rate", valueKind: "Text" }),
            cell("B6", "Free", "#DIV/0!", { input: "=1/0", valueKind: "Error" }),
        ],
        unbudgeted: [{ label: "Taxi", mainClass: "ConsumerSpending", subClass: "TransportationCost", currency: "USD", months: ["0", "12", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0"] }],
        ...over,
    };
}

/** The calls to `url` so far. */
function callsTo(h: SiteHandle, url: string): AjaxCall[] {
    return h.ajaxCalls.filter((c) => c.url === url);
}

/** The latest call to `url`. */
function callTo(h: SiteHandle, url: string): AjaxCall {
    return present([...callsTo(h, url)].reverse()[0], `a call to ${url}`);
}

/** The JSON body of a call. */
function bodyOf(call: AjaxCall): unknown {
    return JSON.parse(String(call.data));
}

/** The grid cell at an address. */
function td(h: SiteHandle, address: string): JQuery {
    return h.$(`#budgetGrid td[data-address='${address}']`);
}

/** The formula bar's editor. */
function editor(h: SiteHandle): FakeCodeMirror {
    return present(h.codeMirrors[0], "the formula bar editor");
}

/** A key press on the grid. */
function gridKey(h: SiteHandle, key: string, init: Record<string, unknown> = {}): void {
    h.$("#budgetGridWrap").trigger(h.$.Event("keydown", { key, ...init }));
}

/** Loads the page and answers its sheet request. */
function load(kind = "Expenditure", data: Record<string, unknown> = sheet()): SiteHandle {
    const h = loadSite("user", "AccountBook", "Budget", fixture(kind));
    successOf(callTo(h, "/AccountBook/GetBudgetSheet"))({ result: true, sheet: data });
    return h;
}

afterEach(() => vi.useRealTimers());

describe("AccountBook/Budget — loading and drawing", () => {
    it("asks for the sheet of its kind and year, with the antiforgery token", () => {
        const h = loadSite("user", "AccountBook", "Budget", fixture("Income", 2030));

        expect(bodyOf(callTo(h, "/AccountBook/GetBudgetSheet"))).toEqual({ Kind: "Income", Year: 2030 });
        expect(callTo(h, "/AccountBook/GetBudgetSheet").headers).toEqual({ RequestVerificationToken: "tok" });
        expect(h.$("#budgetYearLabel").text()).toBe("2030");
    });

    it("draws the column letters, the row numbers and every cell", () => {
        const h = load();

        expect(h.$("#budgetGrid thead th[data-column]").map((_, el) => h.$(el).attr("data-column")).get().join("")).toBe("ABCDEFGHIJKLMNOPQRSTUVWXYZ");
        expect(h.$("#budgetGrid thead th[data-column]").map((_, el) => h.$(el).contents().first().text()).get().join("")).toBe("ABCDEFGHIJKLMNOPQRSTUVWXYZ");
        expect(h.$("#budgetGrid tbody tr")).toHaveLength(7);
        expect(h.$("#budgetGrid tbody th.budget-row-number").map((_, el) => h.$(el).text()).get()).toEqual(["1", "2", "3", "4", "5", "6", "7"]);
        expect(h.$("#budgetGrid tbody tr").first().find("td")).toHaveLength(26);
    });

    // Without a caption the month columns were bare letters: nothing said B..M are January..December and N the year total.
    it("captions the columns the server names (the months and the year total) under their letters; the others have none", () => {
        const h = load();
        const caption = (column: string) => h.$(`#budgetGrid thead th[data-column="${column}"] .budget-column-caption`);

        expect([caption("B").text(), caption("M").text(), caption("N").text()]).toEqual(["Jan", "Dec", "Total"]);
        expect([caption("A"), caption("O")].map((c) => c.length)).toEqual([0, 0]);
    });

    it("column A of a fixed row shows its label (and currency); of a free row, its cell", () => {
        const h = load();

        expect(td(h, "A1").text()).toBe("Consumer");
        expect(td(h, "A2").find(".budget-label").text()).toBe("Meal");
        expect(td(h, "A2").find(".budget-currency").text()).toBe("KRW");
        expect(td(h, "A6").text()).toBe("Tax rate");
        expect(h.$("#budgetGrid tbody tr").eq(0).hasClass("budget-row-header")).toBe(true);
        expect(h.$("#budgetGrid tbody tr").eq(3).hasClass("budget-row-subtotal")).toBe(true);
    });

    it("values are drawn as text (never HTML), numbers right-aligned, errors marked, read-only cells marked", () => {
        const h = load("Expenditure", sheet({ cells: [cell("B6", "Free", "<b>x</b>", { valueKind: "Text" }), cell("C6", "Free", "#REF!", { valueKind: "Error" }), cell("D6", "Free", "1")] }));

        expect(td(h, "B6").text()).toBe("<b>x</b>");
        expect(td(h, "B6").find("b")).toHaveLength(0);
        expect(td(h, "C6").hasClass("budget-error")).toBe(true);
        expect(td(h, "D6").hasClass("budget-number")).toBe(true);
        expect(td(h, "N2").hasClass("budget-readonly")).toBe(true);
        expect(td(h, "B2").hasClass("budget-readonly")).toBe(false);
        expect(td(h, "A2").hasClass("budget-readonly")).toBe(true);
    });

    it("only a line row has a remove button in column A", () => {
        const h = load();

        expect(h.$(".budget-remove-line")).toHaveLength(2);
        expect(td(h, "A2").find(".budget-remove-line").attr("title")).toBe("Remove line");
    });

    it("records of a line the sheet does not have are listed with an add button", () => {
        const h = load();

        expect(h.$("#budgetUnbudgeted").text()).toContain("Not budgeted");
        expect(h.$("#budgetUnbudgeted").text()).toContain("Taxi");
        expect(h.$("#budgetUnbudgeted").text()).toContain("USD");
        expect(h.$("#budgetUnbudgeted .budget-add-unbudgeted")).toHaveLength(1);
        expect(load("Expenditure", sheet({ unbudgeted: [] })).$("#budgetUnbudgeted").children()).toHaveLength(0);
    });

    it("a refused sheet is toasted; a reply of another shape shows the generic error", () => {
        const h = loadSite("user", "AccountBook", "Budget", fixture());
        successOf(callTo(h, "/AccountBook/GetBudgetSheet"))({ result: false, error: "bad year" });
        expect(h.toastr.error).toHaveBeenCalledWith("bad year");

        const other = loadSite("user", "AccountBook", "Budget", fixture());
        successOf(callTo(other, "/AccountBook/GetBudgetSheet"))({ result: true });
        expect(other.toastr.error).toHaveBeenCalledWith("L_TemporaryError");
    });
});

describe("AccountBook/Budget — selection", () => {
    it("starts on B2: the name box shows the address and the formula bar the input", () => {
        const h = load();

        expect(td(h, "B2").hasClass("budget-active")).toBe(true);
        expect(h.$("#budgetNameBox").text()).toBe("B2");
        expect(editor(h).getValue()).toBe("300000");
    });

    it("a click selects a cell; a read-only cell's input is shown read-only", () => {
        const h = load();

        td(h, "N2").trigger("mousedown");

        expect(td(h, "N2").hasClass("budget-active")).toBe(true);
        expect(td(h, "B2").hasClass("budget-active")).toBe(false);
        expect(editor(h).getValue()).toBe("=SUM(B2:M2)");
        expect(editor(h).readOnly).toBe(true);
        td(h, "C2").trigger("mousedown");
        expect(editor(h).readOnly).toBe(false);
    });

    it("arrows move, Shift+arrows and Shift+click extend a range, Enter and Tab move down and right", () => {
        const h = load();

        gridKey(h, "ArrowRight");
        expect(h.$("#budgetNameBox").text()).toBe("C2");
        gridKey(h, "ArrowDown", { shiftKey: true });
        expect(h.$("#budgetGrid td.budget-selected").map((_, el) => h.$(el).attr("data-address")).get()).toEqual(["C2", "C3"]);
        gridKey(h, "Enter");
        expect(h.$("#budgetNameBox").text()).toBe("C3");
        gridKey(h, "Tab");
        expect(h.$("#budgetNameBox").text()).toBe("D3");
        gridKey(h, "Tab", { shiftKey: true });
        gridKey(h, "Enter", { shiftKey: true });
        expect(h.$("#budgetNameBox").text()).toBe("C2");
        td(h, "D3").trigger(h.$.Event("mousedown", { shiftKey: true }));
        expect(h.$("#budgetGrid td.budget-selected")).toHaveLength(4);
    });

    it("the selection stays inside the sheet", () => {
        const h = load();

        gridKey(h, "ArrowUp");
        gridKey(h, "ArrowUp");
        gridKey(h, "ArrowLeft");
        gridKey(h, "ArrowLeft");
        expect(h.$("#budgetNameBox").text()).toBe("A1");
        for (let i = 0; i < 30; i++) gridKey(h, "ArrowRight");
        for (let i = 0; i < 10; i++) gridKey(h, "ArrowDown");
        expect(h.$("#budgetNameBox").text()).toBe("Z7");
    });
});

describe("AccountBook/Budget — editing", () => {
    it("typing starts an edit with that character; Enter saves the input and moves down", () => {
        const h = load();

        gridKey(h, "5");
        expect(editor(h).getValue()).toBe("5");
        expect(editor(h).cursor).toBe(1);
        expect(editor(h).focusCount).toBeGreaterThan(0);
        editor(h).type("500");
        const event = editor(h).key("Enter");

        expect(event.defaultPrevented).toBe(true);
        expect(bodyOf(callTo(h, "/AccountBook/SetBudgetCells"))).toEqual({ Kind: "Expenditure", Year: 2026, Edits: [{ Address: "B2", Input: "500" }] });
        expect(h.$("#budgetNameBox").text()).toBe("B3");
    });

    it("the reply's changed cells are drawn", () => {
        const h = load();
        gridKey(h, "F2");
        editor(h).type("=1+1");
        editor(h).key("Tab");

        successOf(callTo(h, "/AccountBook/SetBudgetCells"))({ result: true, changed: [cell("B2", "Budget", "2", { input: "=1+1" }), cell("N2", "Computed", "317,002", { input: "=SUM(B2:M2)" })] });

        expect(td(h, "B2").text()).toBe("2");
        expect(td(h, "N2").text()).toBe("317,002");
        expect(h.$("#budgetNameBox").text()).toBe("C2");
    });

    it("F2 and a double click edit the input as it is; Escape cancels without saving", () => {
        const h = load();

        gridKey(h, "F2");
        expect(editor(h).getValue()).toBe("300000");
        editor(h).type("1");
        editor(h).key("Escape");
        expect(editor(h).getValue()).toBe("300000");
        td(h, "C2").trigger("dblclick");
        expect(editor(h).getValue()).toBe("=B2*1.05");
        expect(callsTo(h, "/AccountBook/SetBudgetCells")).toHaveLength(0);
    });

    it("an unchanged input is not saved", () => {
        const h = load();
        gridKey(h, "F2");
        editor(h).key("Enter");

        expect(callsTo(h, "/AccountBook/SetBudgetCells")).toHaveLength(0);
    });

    it("a read-only cell does not start an edit", () => {
        const h = load();
        td(h, "N2").trigger("mousedown");

        gridKey(h, "7");
        gridKey(h, "F2");
        td(h, "N2").trigger("dblclick");

        expect(editor(h).getValue()).toBe("=SUM(B2:M2)");
        expect(td(h, "N2").hasClass("budget-editing")).toBe(false);
    });

    it("a refused edit is toasted and reopens the cell with what was typed", () => {
        const h = load();
        gridKey(h, "x");
        editor(h).key("Enter");

        successOf(callTo(h, "/AccountBook/SetBudgetCells"))({ result: false, error: "A budget cell takes a number" });

        expect(h.toastr.error).toHaveBeenCalledWith("A budget cell takes a number");
        expect(h.$("#budgetNameBox").text()).toBe("B2");
        expect(editor(h).getValue()).toBe("x");
        expect(td(h, "B2").hasClass("budget-editing")).toBe(true);
    });

    it("Delete clears the editable cells of the selection (read-only ones are left out)", () => {
        const h = load();
        gridKey(h, "ArrowRight", { shiftKey: true });
        td(h, "N2").trigger(h.$.Event("mousedown", { shiftKey: true }));

        gridKey(h, "Delete");

        const edits = bodyOf(callTo(h, "/AccountBook/SetBudgetCells"));
        expect(edits).toMatchObject({ Kind: "Expenditure", Year: 2026 });
        expect(JSON.stringify(edits)).not.toContain("N2");
        expect(JSON.stringify(edits)).toContain("{\"Address\":\"M2\",\"Input\":\"\"}");
    });
});

describe("AccountBook/Budget — the formula bar", () => {
    /** Starts editing B2 with `text` and lets the preview request go out. */
    function typeFormula(h: SiteHandle, text: string): void {
        vi.useFakeTimers();
        gridKey(h, "F2");
        editor(h).type(text);
        vi.advanceTimersByTime(300);
    }

    /** A preview reply. */
    function preview(over: Record<string, unknown> = {}): Record<string, unknown> {
        return {
            result: true,
            preview: {
                tokens: [
                    { start: 0, length: 1, class: "operator", reference: -1, partner: -1 },
                    { start: 1, length: 3, class: "function", reference: -1, partner: -1 },
                    { start: 4, length: 1, class: "paren", reference: -1, partner: 6 },
                    { start: 5, length: 5, class: "reference", reference: 0, partner: -1 },
                    { start: 10, length: 1, class: "paren", reference: -1, partner: 2 },
                ],
                references: [{ index: 0, topLeft: "B2", bottomRight: "C3" }],
                problems: [],
                canSave: true,
                value: "615,000",
                ...over,
            },
        };
    }

    it("asks the server to colour the formula being typed (after a pause)", () => {
        const h = load();
        typeFormula(h, "=SUM(B2:C3)");

        expect(bodyOf(callTo(h, "/AccountBook/PreviewBudgetCell"))).toEqual({ Kind: "Expenditure", Year: 2026, Address: "B2", Input: "=SUM(B2:C3)" });
        expect(callsTo(h, "/AccountBook/PreviewBudgetCell")).toHaveLength(1);
    });

    it("paints the tokens with their classes and each reference's colour, and outlines the referenced cells", () => {
        const h = load();
        typeFormula(h, "=SUM(B2:C3)");
        successOf(callTo(h, "/AccountBook/PreviewBudgetCell"))(preview());

        expect(editor(h).liveMarks().map((m) => [m.from, m.to, m.className])).toEqual([
            [0, 1, "cm-budget-operator"], [1, 4, "cm-budget-function"], [4, 5, "cm-budget-paren"], [5, 10, "cm-budget-reference cm-budget-ref-0"], [10, 11, "cm-budget-paren"],
        ]);
        expect(["B2", "C2", "B3", "C3"].every((a) => td(h, a).hasClass("budget-ref-0"))).toBe(true);
        expect(td(h, "D2").hasClass("budget-ref-0")).toBe(false);
        expect(h.$("#budgetPreviewValue").text()).toBe("615,000");
    });

    it("problems are underlined with their message and listed; a zero-length one marks the character before it", () => {
        const h = load();
        typeFormula(h, "=SUM(");
        successOf(callTo(h, "/AccountBook/PreviewBudgetCell"))(preview({
            tokens: [], references: [], canSave: false, value: "",
            problems: [{ start: 5, length: 0, message: "')' is expected here.", blocking: true }, { start: 1, length: 3, message: "Unknown <b>", blocking: false }],
        }));

        const problems = editor(h).liveMarks().filter((m) => m.className.startsWith("cm-budget-problem"));
        expect(problems.map((m) => [m.from, m.to, m.className, m.title])).toEqual([[4, 5, "cm-budget-problem cm-budget-blocking", "')' is expected here."], [1, 4, "cm-budget-problem", "Unknown <b>"]]);
        expect(h.$("#budgetProblems li").map((_, el) => h.$(el).text()).get()).toEqual(["')' is expected here.", "Unknown <b>"]);
        expect(h.$("#budgetProblems b")).toHaveLength(0);
    });

    it("an older preview reply that arrives after a newer request is ignored", () => {
        const h = load();
        typeFormula(h, "=1");
        editor(h).type("=12");
        vi.advanceTimersByTime(300);

        successOf(callsTo(h, "/AccountBook/PreviewBudgetCell")[0])(preview({ value: "old" }));
        expect(h.$("#budgetPreviewValue").text()).toBe("");
        successOf(callsTo(h, "/AccountBook/PreviewBudgetCell")[1])(preview({ value: "12" }));
        expect(h.$("#budgetPreviewValue").text()).toBe("12");
    });

    it("the caret next to a parenthesis highlights it and its partner", () => {
        const h = load();
        typeFormula(h, "=SUM(B2:C3)");
        successOf(callTo(h, "/AccountBook/PreviewBudgetCell"))(preview());

        editor(h).setCursor({ line: 0, ch: 11 });

        expect(editor(h).liveMarks().filter((m) => m.className === "cm-budget-paren-match").map((m) => m.from).sort((a, b) => a - b)).toEqual([4, 10]);
    });

    it("leaving the edit clears the colouring and the outlines", () => {
        const h = load();
        typeFormula(h, "=SUM(B2:C3)");
        successOf(callTo(h, "/AccountBook/PreviewBudgetCell"))(preview());

        editor(h).key("Escape");

        expect(editor(h).liveMarks()).toHaveLength(0);
        expect(h.$("#budgetGrid .budget-ref-0")).toHaveLength(0);
        expect(h.$("#budgetProblems").children()).toHaveLength(0);
    });

    it("while a formula waits for a reference, clicking a cell inserts it; Shift+click makes a range", () => {
        const h = load();
        gridKey(h, "=");

        td(h, "C3").trigger("mousedown");
        expect(editor(h).getValue()).toBe("=C3");
        td(h, "D4").trigger(h.$.Event("mousedown", { shiftKey: true }));
        expect(editor(h).getValue()).toBe("=C3:D4");
        editor(h).type("=C3:D4+");
        td(h, "B6").trigger("mousedown");
        expect(editor(h).getValue()).toBe("=C3:D4+B6");
        expect(h.$("#budgetNameBox").text()).toBe("B2");
    });

    it("while a formula waits for a reference, arrows point at cells", () => {
        const h = load();
        gridKey(h, "=");

        editor(h).key("ArrowRight");
        expect(editor(h).getValue()).toBe("=C2");
        editor(h).key("ArrowDown");
        expect(editor(h).getValue()).toBe("=C3");
    });

    it("clicking another cell when the formula does not wait for a reference saves it and selects that cell", () => {
        const h = load();
        gridKey(h, "F2");
        editor(h).type("42");

        td(h, "D3").trigger("mousedown");

        expect(bodyOf(callTo(h, "/AccountBook/SetBudgetCells"))).toMatchObject({ Edits: [{ Address: "B2", Input: "42" }] });
        expect(h.$("#budgetNameBox").text()).toBe("D3");
    });

    it("a function name being typed offers the matching functions with their hints; Enter or a click takes one", () => {
        const h = load();
        gridKey(h, "=");
        editor(h).type("=RO");

        expect(h.$("#budgetAutocomplete").hasClass("d-none")).toBe(false);
        expect(h.$("#budgetAutocomplete li").map((_, el) => h.$(el).text()).get()).toEqual(["ROUND(number, num_digits)", "ROUNDUP(number, num_digits)"]);
        editor(h).key("ArrowDown");
        expect(h.$("#budgetAutocomplete li.active").text()).toBe("ROUNDUP(number, num_digits)");
        editor(h).key("Enter");
        expect(editor(h).getValue()).toBe("=ROUNDUP(");
        expect(h.$("#budgetAutocomplete").hasClass("d-none")).toBe(true);

        editor(h).type("=ROUNDUP(su");
        h.$("#budgetAutocomplete li").first().trigger("mousedown");
        expect(editor(h).getValue()).toBe("=ROUNDUP(SUM(");
        expect(callsTo(h, "/AccountBook/SetBudgetCells")).toHaveLength(0);
    });

    it("the list closes when no function matches and with Escape", () => {
        const h = load();
        gridKey(h, "=");
        editor(h).type("=ZZ");
        expect(h.$("#budgetAutocomplete").hasClass("d-none")).toBe(true);
        editor(h).type("=S");
        editor(h).key("ArrowUp");
        expect(h.$("#budgetAutocomplete li.active").text()).toBe("SUM(number1, [number2], ...)");
        editor(h).key("Escape");
        expect(h.$("#budgetAutocomplete").hasClass("d-none")).toBe(true);
        expect(td(h, "B2").hasClass("budget-editing")).toBe(true);
    });

    it("clicking into the formula bar of an editable cell starts editing it", () => {
        const h = load();

        editor(h).focus();

        expect(td(h, "B2").hasClass("budget-editing")).toBe(true);
        editor(h).type("7");
        expect(td(h, "B2").text()).toBe("7");
    });
});

describe("AccountBook/Budget — paste, fill, undo", () => {
    it("Ctrl+C then Ctrl+V pastes the copied block over the selection", () => {
        const h = load();
        gridKey(h, "c", { ctrlKey: true });
        expect(td(h, "B2").hasClass("budget-copied")).toBe(true);
        td(h, "C3").trigger("mousedown");
        td(h, "D3").trigger(h.$.Event("mousedown", { shiftKey: true }));

        gridKey(h, "v", { ctrlKey: true });

        expect(bodyOf(callTo(h, "/AccountBook/CopyBudgetCells"))).toEqual({ Kind: "Expenditure", Year: 2026, Source: "B2", Target: "C3:D3" });
        successOf(callTo(h, "/AccountBook/CopyBudgetCells"))({ result: true, changed: [cell("C3", "Budget", "9", { input: "9" })] });
        expect(td(h, "C3").text()).toBe("9");
    });

    it("Ctrl+V with nothing copied does nothing; Escape forgets the copy", () => {
        const h = load();
        gridKey(h, "v", { ctrlKey: true });
        gridKey(h, "c", { metaKey: true });
        gridKey(h, "Escape");
        gridKey(h, "v", { ctrlKey: true });

        expect(callsTo(h, "/AccountBook/CopyBudgetCells")).toHaveLength(0);
        expect(h.$(".budget-copied")).toHaveLength(0);
    });

    it("Ctrl+R fills right from the first column of the selection; Ctrl+D fills down from its first row", () => {
        const h = load();
        gridKey(h, "ArrowRight", { shiftKey: true });
        gridKey(h, "ArrowRight", { shiftKey: true });
        gridKey(h, "r", { ctrlKey: true });
        expect(bodyOf(callTo(h, "/AccountBook/CopyBudgetCells"))).toMatchObject({ Source: "B2", Target: "C2:D2" });

        td(h, "B2").trigger("mousedown");
        td(h, "C3").trigger(h.$.Event("mousedown", { shiftKey: true }));
        gridKey(h, "d", { ctrlKey: true });
        expect(bodyOf(callTo(h, "/AccountBook/CopyBudgetCells"))).toMatchObject({ Source: "B2:C2", Target: "B3:C3" });

        const calls = callsTo(h, "/AccountBook/CopyBudgetCells").length;
        td(h, "B2").trigger("mousedown");
        gridKey(h, "d", { ctrlKey: true });
        expect(callsTo(h, "/AccountBook/CopyBudgetCells")).toHaveLength(calls);
    });

    it("a refused paste is toasted", () => {
        const h = load();
        gridKey(h, "c", { ctrlKey: true });
        td(h, "N2").trigger("mousedown");
        gridKey(h, "v", { ctrlKey: true });
        successOf(callTo(h, "/AccountBook/CopyBudgetCells"))({ result: false, error: "read-only" });

        expect(h.toastr.error).toHaveBeenCalledWith("read-only");
    });

    it("Ctrl+Z puts the previous inputs back and Ctrl+Y redoes the edit", () => {
        const h = load();
        gridKey(h, "F2");
        editor(h).type("1");
        editor(h).key("Enter");
        successOf(callTo(h, "/AccountBook/SetBudgetCells"))({ result: true, changed: [cell("B2", "Budget", "1", { input: "1" })] });

        gridKey(h, "z", { ctrlKey: true });
        expect(bodyOf(callTo(h, "/AccountBook/SetBudgetCells"))).toMatchObject({ Edits: [{ Address: "B2", Input: "300000" }] });
        successOf(callTo(h, "/AccountBook/SetBudgetCells"))({ result: true, changed: [cell("B2", "Budget", "300,000", { input: "300000" })] });

        gridKey(h, "y", { ctrlKey: true });
        expect(bodyOf(callTo(h, "/AccountBook/SetBudgetCells"))).toMatchObject({ Edits: [{ Address: "B2", Input: "1" }] });

        const count = callsTo(h, "/AccountBook/SetBudgetCells").length;
        gridKey(h, "y", { ctrlKey: true });
        expect(callsTo(h, "/AccountBook/SetBudgetCells")).toHaveLength(count);
    });

    it("a paste is undone too (its targets' previous inputs)", () => {
        const h = load();
        gridKey(h, "c", { ctrlKey: true });
        td(h, "C2").trigger("mousedown");
        gridKey(h, "v", { ctrlKey: true });
        successOf(callTo(h, "/AccountBook/CopyBudgetCells"))({ result: true, changed: [cell("C2", "Budget", "300,000", { input: "300000" }), cell("N2", "Computed", "600,000")] });

        gridKey(h, "z", { ctrlKey: true });

        expect(bodyOf(callTo(h, "/AccountBook/SetBudgetCells"))).toMatchObject({ Edits: [{ Address: "C2", Input: "=B2*1.05" }] });
    });

    it("Ctrl+Z with nothing to undo does nothing", () => {
        const h = load();
        gridKey(h, "z", { ctrlKey: true });

        expect(callsTo(h, "/AccountBook/SetBudgetCells")).toHaveLength(0);
    });
});

describe("AccountBook/Budget — views, year, lines, Excel", () => {
    it("the Actual view shows the actual totals; Remaining shows what remains, coloured by state", () => {
        const h = load();

        h.$("#budgetViewSwitch [data-view='actual']").trigger("click");
        expect(td(h, "B2").text()).toBe("120,000");
        expect(h.$("#budgetViewSwitch [data-view='actual']").hasClass("active")).toBe(true);
        expect(td(h, "N2").text()).toBe("615,000");

        h.$("#budgetViewSwitch [data-view='remaining']").trigger("click");
        expect(td(h, "C2").text()).toBe("-85,000");
        expect(td(h, "C2").hasClass("budget-state-bad")).toBe(true);
        expect(td(h, "B3").hasClass("budget-state-unbudgeted")).toBe(true);

        h.$("#budgetViewSwitch [data-view='budget']").trigger("click");
        expect(td(h, "C2").text()).toBe("315,000");
        expect(td(h, "C2").hasClass("budget-state-bad")).toBe(false);
    });

    it("the year buttons load the previous, next and current year", () => {
        const h = load();

        h.$("#btnBudgetNextYear").trigger("click");
        expect(bodyOf(callTo(h, "/AccountBook/GetBudgetSheet"))).toEqual({ Kind: "Expenditure", Year: 2027 });
        expect(h.$("#budgetYearLabel").text()).toBe("2027");
        h.$("#btnBudgetPrevYear").trigger("click");
        h.$("#btnBudgetPrevYear").trigger("click");
        expect(bodyOf(callTo(h, "/AccountBook/GetBudgetSheet"))).toEqual({ Kind: "Expenditure", Year: 2025 });
        h.$("#btnBudgetThisYear").trigger("click");
        expect(bodyOf(callTo(h, "/AccountBook/GetBudgetSheet"))).toEqual({ Kind: "Expenditure", Year: 2026 });
    });

    it("Add currency line offers the main class's sub-classes by name and adds the line", () => {
        const h = load();
        h.$("#btnBudgetAddLine").trigger("click");
        expect(h.$("#budgetLineSubClass option").map((_, el) => h.$(el).text()).get()).toEqual(["Meal", "Pin"]);
        h.$("#budgetLineMainClass").val("RegularSavings").trigger("change");
        expect(h.$("#budgetLineSubClass option").map((_, el) => h.$(el).text()).get()).toEqual(["Deposit", "Investment"]);

        h.$("#budgetLineCurrency").val("USD");
        h.$("#formBudgetLine").trigger("submit");
        expect(bodyOf(callTo(h, "/AccountBook/AddBudgetLine"))).toEqual({ Kind: "Expenditure", Year: 2026, MainClass: "RegularSavings", SubClass: "Deposit", Currency: "USD" });

        successOf(callTo(h, "/AccountBook/AddBudgetLine"))({ result: true, message: "added", sheet: sheet({ rows: [row(1, "Free", "")], cells: [] }) });
        expect(h.toastr.success).toHaveBeenCalledWith("added");
        expect(h.$("#budgetGrid tbody tr")).toHaveLength(1);
    });

    it("a refused line is toasted", () => {
        const h = load();
        h.$("#btnBudgetAddLine").trigger("click");
        h.$("#formBudgetLine").trigger("submit");
        successOf(callTo(h, "/AccountBook/AddBudgetLine"))({ result: false, error: "exists" });

        expect(h.toastr.error).toHaveBeenCalledWith("exists");
    });

    it("an unbudgeted line's add button adds that line", () => {
        const h = load();
        h.$("#budgetUnbudgeted .budget-add-unbudgeted").trigger("click");

        expect(bodyOf(callTo(h, "/AccountBook/AddBudgetLine"))).toEqual({ Kind: "Expenditure", Year: 2026, MainClass: "ConsumerSpending", SubClass: "TransportationCost", Currency: "USD" });
    });

    it("a line's remove button asks, then removes the line", () => {
        const h = load();
        td(h, "A3").find(".budget-remove-line").trigger("click");
        expect(h.$("#confirmRemoveLineText").text()).toBe("Remove Pin (KRW)?");

        h.$("#btnConfirmRemoveLine").trigger("click");
        expect(bodyOf(callTo(h, "/AccountBook/RemoveBudgetLine"))).toEqual({ Kind: "Expenditure", Year: 2026, MainClass: "ConsumerSpending", SubClass: "PinMoney", Currency: "KRW" });
        successOf(callTo(h, "/AccountBook/RemoveBudgetLine"))({ result: true, message: "removed", sheet: sheet() });
        expect(h.toastr.success).toHaveBeenCalledWith("removed");
    });

    it("Copy from last year fills the sheet and says how many cells", () => {
        const h = load();
        h.$("#btnBudgetCarryOver").trigger("click");

        expect(bodyOf(callTo(h, "/AccountBook/CarryOverBudget"))).toEqual({ Kind: "Expenditure", Year: 2026 });
        successOf(callTo(h, "/AccountBook/CarryOverBudget"))({ result: true, message: "3 cells", sheet: sheet() });
        expect(h.toastr.success).toHaveBeenCalledWith("3 cells");
    });

    it("Excel posts a form for the open sheet", () => {
        const h = load();
        h.$("#btnBudgetExcel").trigger("click");

        const form = present(h.submittedForms[0], "the Excel form");
        expect(form.action).toContain("/AccountBook/ExportExcelBudget");
        expect([...new FormData(form).entries()].map(([k, v]) => [k, String(v)])).toEqual([["__RequestVerificationToken", "tok"], ["kind", "Expenditure"], ["year", "2026"], ["fileName", "Expenditure2026"]]);
    });

    it("a reply of another shape to an edit shows the generic error", () => {
        const h = load();
        gridKey(h, "1");
        editor(h).key("Enter");
        successOf(callTo(h, "/AccountBook/SetBudgetCells"))({ result: true });

        expect(h.toastr.error).toHaveBeenCalledWith("L_TemporaryError");
    });
});

describe("AccountBook/Budget — the less common paths", () => {
    it("Shift+Enter and Shift+Tab save and move up and left", () => {
        const h = load();
        td(h, "C3").trigger("mousedown");
        gridKey(h, "1");
        editor(h).key("Enter", { shiftKey: true });
        expect(h.$("#budgetNameBox").text()).toBe("C2");
        gridKey(h, "2");
        editor(h).key("Tab", { shiftKey: true });
        expect(h.$("#budgetNameBox").text()).toBe("B2");
        expect(callsTo(h, "/AccountBook/SetBudgetCells")).toHaveLength(2);
    });

    it("other keys in the formula bar are left to the editor; keys there while nothing is edited do nothing", () => {
        const h = load();
        td(h, "N2").trigger("mousedown");
        expect(editor(h).key("Enter").defaultPrevented).toBe(false);
        editor(h).type("typed into a read-only cell");
        expect(td(h, "N2").text()).toBe("615,000");

        td(h, "B2").trigger("mousedown");
        gridKey(h, "=");
        editor(h).type("=S");
        expect(editor(h).key("x").defaultPrevented).toBe(false);
        expect(editor(h).key("Home").defaultPrevented).toBe(false);
    });

    it("the grid ignores keys while a cell is edited, keys it does not know and a key event without a key", () => {
        const h = load();
        gridKey(h, "Shift");
        gridKey(h, "x", { ctrlKey: true });
        h.$("#budgetGridWrap").trigger("keydown");
        expect(h.$("#budgetNameBox").text()).toBe("B2");

        gridKey(h, "F2");
        gridKey(h, "ArrowDown");
        expect(h.$("#budgetNameBox").text()).toBe("B2");
        expect(td(h, "B2").hasClass("budget-editing")).toBe(true);
    });

    it("point mode moves left and up too", () => {
        const h = load();
        td(h, "C3").trigger("mousedown");
        gridKey(h, "=");

        editor(h).key("ArrowLeft");
        expect(editor(h).getValue()).toBe("=B3");
        editor(h).key("ArrowUp");
        expect(editor(h).getValue()).toBe("=B2");
    });

    it("a reference outside the sheet is coloured but outlines nothing; moving the caret again moves the parenthesis highlight", () => {
        const h = load();
        vi.useFakeTimers();
        gridKey(h, "F2");
        editor(h).type("=(Income!B2)");
        vi.advanceTimersByTime(300);
        successOf(callTo(h, "/AccountBook/PreviewBudgetCell"))({
            result: true,
            preview: {
                tokens: [{ start: 1, length: 1, class: "paren", reference: -1, partner: 3 }, { start: 2, length: 7, class: "sheet", reference: 1, partner: -1 },
                    { start: 9, length: 2, class: "reference", reference: 1, partner: -1 }, { start: 11, length: 1, class: "paren", reference: -1, partner: 0 }],
                references: [{ index: 1, topLeft: "", bottomRight: "" }], problems: [], canSave: true, value: "",
            },
        });

        expect(h.$("#budgetGrid [class*='budget-ref-']")).toHaveLength(0);
        editor(h).setCursor({ line: 0, ch: 1 });
        editor(h).setCursor({ line: 0, ch: 12 });
        expect(editor(h).liveMarks().filter((m) => m.className === "cm-budget-paren-match").map((m) => m.from).sort((a, b) => a - b)).toEqual([1, 11]);
    });

    it("a preview of something that is not a formula is not asked for; a refused preview paints nothing", () => {
        const h = load();
        vi.useFakeTimers();
        gridKey(h, "F2");
        editor(h).type("12");
        vi.advanceTimersByTime(300);
        expect(callsTo(h, "/AccountBook/PreviewBudgetCell")).toHaveLength(0);

        editor(h).type("=1");
        vi.advanceTimersByTime(300);
        successOf(callTo(h, "/AccountBook/PreviewBudgetCell"))({ result: false, error: "bad year" });
        expect(editor(h).liveMarks()).toHaveLength(0);
        expect(h.toastr.error).not.toHaveBeenCalled();
    });

    it("loading another year while editing ends the edit", () => {
        const h = load();
        gridKey(h, "F2");
        h.$("#btnBudgetNextYear").trigger("click");
        successOf(callTo(h, "/AccountBook/GetBudgetSheet"))({ result: true, sheet: sheet({ year: 2027 }) });

        expect(td(h, "B2").hasClass("budget-editing")).toBe(false);
        expect(editor(h).getValue()).toBe("300000");
    });

    it("a reply that arrives while another cell is edited leaves that edit alone; one for a cell no longer on the sheet is skipped", () => {
        const h = load();
        gridKey(h, "1");
        editor(h).key("Enter");
        gridKey(h, "7");

        successOf(callTo(h, "/AccountBook/SetBudgetCells"))({ result: true, changed: [cell("B2", "Budget", "1", { input: "1" }), cell("B99", "Free", "x")] });

        expect(td(h, "B2").text()).toBe("1");
        expect(editor(h).getValue()).toBe("7");
        expect(td(h, "B3").hasClass("budget-editing")).toBe(true);
    });

    it("Delete is undone; a selection with nothing editable sends nothing; a refused clear is only toasted", () => {
        const h = load();
        gridKey(h, "Delete");
        successOf(callTo(h, "/AccountBook/SetBudgetCells"))({ result: true, changed: [cell("B2", "Budget", "", { valueKind: "Empty" })] });
        gridKey(h, "z", { ctrlKey: true });
        expect(bodyOf(callTo(h, "/AccountBook/SetBudgetCells"))).toMatchObject({ Edits: [{ Address: "B2", Input: "300000" }] });
        successOf(callTo(h, "/AccountBook/SetBudgetCells"))({ result: true, changed: [cell("B2", "Budget", "300,000", { input: "300000" })] });
        gridKey(h, "y", { ctrlKey: true });
        successOf(callTo(h, "/AccountBook/SetBudgetCells"))({ result: true, changed: [cell("B2", "Budget", "", { valueKind: "Empty" })] });
        gridKey(h, "z", { ctrlKey: true });
        expect(bodyOf(callTo(h, "/AccountBook/SetBudgetCells"))).toMatchObject({ Edits: [{ Address: "B2", Input: "300000" }] });

        const count = callsTo(h, "/AccountBook/SetBudgetCells").length;
        td(h, "N2").trigger("mousedown");
        gridKey(h, "Delete");
        expect(callsTo(h, "/AccountBook/SetBudgetCells")).toHaveLength(count);

        td(h, "B2").trigger("mousedown");
        gridKey(h, "Backspace");
        successOf(callTo(h, "/AccountBook/SetBudgetCells"))({ result: false, error: "no" });
        expect(h.toastr.error).toHaveBeenCalledWith("no");
        expect(td(h, "B2").hasClass("budget-editing")).toBe(false);
    });

    it("a paste whose reply changes none of its targets adds nothing to undo", () => {
        const h = load();
        gridKey(h, "c", { ctrlKey: true });
        td(h, "C2").trigger("mousedown");
        gridKey(h, "v", { ctrlKey: true });
        successOf(callTo(h, "/AccountBook/CopyBudgetCells"))({ result: true, changed: [cell("N2", "Computed", "1")] });

        gridKey(h, "z", { ctrlKey: true });

        expect(callsTo(h, "/AccountBook/SetBudgetCells")).toHaveLength(0);
    });

    it("a main class the taxonomy does not list offers no sub-class (the server refuses it); a sub-class without a name shows its key", () => {
        const h = load();
        h.$("#btnBudgetAddLine").trigger("click");
        h.$("#budgetLineMainClass").val("Unknown").trigger("change");
        expect(h.$("#budgetLineSubClass option")).toHaveLength(0);
        h.$("#formBudgetLine").trigger("submit");
        expect(bodyOf(callTo(h, "/AccountBook/AddBudgetLine"))).toMatchObject({ MainClass: "Unknown", SubClass: "" });

        h.$("#budgetLineMainClass").val("RegularSavings").trigger("change");
        expect(h.$("#budgetLineSubClass option").map((_, el) => h.$(el).text()).get()).toEqual(["Deposit", "Investment"]);
    });

    it("an invalid line form sends nothing; confirming with no line asked about does nothing", () => {
        const h = load();
        stubPlugin(h, "valid", () => false);
        h.$("#formBudgetLine").trigger("submit");
        h.$("#btnConfirmRemoveLine").trigger("click");

        expect(callsTo(h, "/AccountBook/AddBudgetLine")).toHaveLength(0);
        expect(callsTo(h, "/AccountBook/RemoveBudgetLine")).toHaveLength(0);
    });
});
