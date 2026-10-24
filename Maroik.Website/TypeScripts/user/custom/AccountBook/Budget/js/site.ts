/**
 * Script for the **Income Budget / Expenditure Budget** pages (`Views/AccountBook/Budget.cshtml`): the account's budget sheet of a year,
 * handled like a spreadsheet.
 *
 *   • The grid (column letters A..Z, row numbers) is drawn from the sheet the server sends: every row with its label and which of its
 *     columns can be edited, every cell with its input, its value as shown and — for a budget cell — its actual total, what remains and
 *     its colour. The Budget / Actual / Remaining switch only chooses which of those texts a budget cell shows.
 *   • The formula bar (CodeMirror) shows the active cell's input. While a formula is typed, the server colours it (PreviewBudgetCell):
 *     the page paints its tokens, underlines its problems, outlines the referenced cells and shows the value it would have. While a
 *     formula waits for a reference, clicking a cell (Shift for a range) or the arrows insert it (Excel's point mode); a function name
 *     being typed offers the server's function list with its argument hints.
 *   • Keyboard: arrows (Shift extends), Enter / Tab (Shift backwards), F2 or typing to edit, Escape, Delete, Ctrl+C / Ctrl+V (paste),
 *     Ctrl+D / Ctrl+R (fill down / right), Ctrl+Z / Ctrl+Y (undo / redo for this page).
 *   • Each committed edit is saved at once (SetBudgetCells); paste and fill go to CopyBudgetCells, which moves the formulas' relative
 *     references; either reply lists the cells whose value changed, and only those are redrawn.
 *
 * Every rule is the server's — the formula language, what a cell accepts, the sums, the actuals, the states; the script draws and sends.
 * IIFE-wrapped, no `import` / `export`.
 */
(function() {
    // The runtime-check helpers the _Layout script defines (see TypeScripts/global.d.ts).
    const { check, replies, onReply, fieldValue, selectValue, parseJson, byId, elementOf } = window;

    // The replies this page reads, mirroring the controller's Json(...) results.
    const cellCheck = check.object({
        address: check.string, role: check.string, input: check.string, display: check.string, valueKind: check.string,
        actual: check.string, remaining: check.string, state: check.string,
    });
    type Cell = Checked<typeof cellCheck>;
    const rowCheck = check.object({
        number: check.number, kind: check.string, label: check.string, mainClass: check.string, subClass: check.string, currency: check.string,
        editable: check.string,
    });
    type Row = Checked<typeof rowCheck>;
    const unbudgetedCheck = check.object({ label: check.string, mainClass: check.string, subClass: check.string, currency: check.string, months: check.array(check.string) });
    type Unbudgeted = Checked<typeof unbudgetedCheck>;
    const sheetCheck = check.object({
        kind: check.string, year: check.number, exists: check.boolean, rows: check.array(rowCheck), cells: check.array(cellCheck), unbudgeted: check.array(unbudgetedCheck),
    });
    type Sheet = Checked<typeof sheetCheck>;
    const previewCheck = check.object({
        tokens: check.array(check.object({ start: check.number, length: check.number, class: check.string, reference: check.number, partner: check.number })),
        references: check.array(check.object({ index: check.number, topLeft: check.string, bottomRight: check.string })),
        problems: check.array(check.object({ start: check.number, length: check.number, message: check.string, blocking: check.boolean })),
        canSave: check.boolean,
        value: check.string,
    });
    type Preview = Checked<typeof previewCheck>;
    const sheetReply = replies.read({ sheet: sheetCheck });
    const changedReply = replies.read({ changed: check.array(cellCheck) });
    const previewReply = replies.read({ preview: previewCheck });
    const sheetWriteReply = replies.write({ sheet: sheetCheck });

    // What the view renders: the sheet's kind and year, the server's function list and class taxonomy, the localized texts.
    const kind = fieldValue($("#budgetKind"));
    const thisYear = Number(fieldValue($("#budgetYear")));
    const functions = parseJson(fieldValue($("#budgetFunctions")), check.array(check.object({ Name: check.string, Signature: check.string })), "#budgetFunctions");
    const subClassMap = parseJson(fieldValue($("#budgetSubClassMap")), check.record(check.array(check.string)), "#budgetSubClassMap");
    const classLabels = parseJson(fieldValue($("#budgetClassLabels")), check.record(check.string), "#budgetClassLabels");
    const text = {
        confirmRemoveLine: fieldValue($("#localizerConfirmRemoveLine")),
        notBudgeted: fieldValue($("#localizerNotBudgeted")),
        addLine: fieldValue($("#localizerAddLine")),
        removeLine: fieldValue($("#localizerRemoveLine")),
    };

    // Cached references.
    const $grid = $("#budgetGrid");
    const $wrap = $("#budgetGridWrap");
    const $nameBox = $("#budgetNameBox");
    const $yearLabel = $("#budgetYearLabel");
    const $previewValue = $("#budgetPreviewValue");
    const $problems = $("#budgetProblems");
    const $autocomplete = $("#budgetAutocomplete");
    const $unbudgeted = $("#budgetUnbudgeted");
    const $lineModal = $("#budgetLineModal");
    const $formLine = $("#formBudgetLine");
    const $lineMainClass = $("#budgetLineMainClass");
    const $lineSubClass = $("#budgetLineSubClass");
    const $lineCurrency = $("#budgetLineCurrency");
    const $confirmRemoveModal = $("#confirmRemoveLineModal");
    const $__RequestVerificationToken = $("input[name=\"__RequestVerificationToken\"]");
    const editor = CodeMirror.fromTextArea(byId("budgetFormula", HTMLTextAreaElement), { lineWrapping: false, viewportMargin: Infinity });

    /** The column letters. */
    const COLUMNS = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    /** The characters after which a formula waits for a reference (Excel's point mode). */
    const WAITS_FOR_REFERENCE = /[=+\-*/^&(,:<>]$/;
    /** Reference colours cycle through this many classes. */
    const REFERENCE_COLOURS = 8;

    // -- State ------------------------------------------------------------------------

    /** A cell edit for the undo history: the input before and after. */
    interface Edit { address: string; before: string; after: string; }

    /** A position in the grid: the row number (1-based) and the column index (A = 0). */
    interface Position { row: number; col: number; }

    let year = thisYear;
    let rows: Row[] = [];
    const cells = new Map<string, Cell>();
    let unbudgeted: Unbudgeted[] = [];
    let view: "budget" | "actual" | "remaining" = "budget";
    let anchor: Position = { row: 2, col: 1 };
    let focus: Position = { row: 2, col: 1 };
    /** A cell being edited and its input before the edit. */
    interface Editing { address: string; original: string; }

    /** The cell being edited, or null. */
    let editing: Editing | null = null;
    /** The reference the point mode last inserted (and where), or null. */
    let point: { anchor: string; address: string; start: number; end: number } | null = null;
    /** True while the page itself changes the editor's text (not the user). */
    let programmatic = false;
    /** The copied block ("B2" or "B2:C3"), or null. */
    let copied: string | null = null;
    const undoStack: Edit[][] = [];
    const redoStack: Edit[][] = [];
    let previewTimer: ReturnType<typeof setTimeout> | undefined;
    let previewSeq = 0;
    /** The tokens of the last preview (for the parenthesis matching). */
    let lastTokens: Preview["tokens"] = [];
    /** The marks of the parenthesis matching, cleared on every caret move. */
    let parenMarks: CodeMirrorTextMarker[] = [];
    /** The functions offered for the name being typed, the highlighted one and the length of the word they replace. */
    let suggestions: { Name: string; Signature: string }[] = [];
    let suggestionIndex = 0;
    let suggestionWordLength = 0;
    /** The line a remove confirmation is about. */
    let pendingRemove: Row | null = null;

    // -- Addresses ----------------------------------------------------------------------

    /** The A1 address of a position. */
    function addressOf(position: Position): string {
        return COLUMNS.charAt(position.col) + String(position.row);
    }

    /** The position of an A1 address. */
    function positionOf(address: string): Position {
        return { row: Number(address.slice(1)), col: COLUMNS.indexOf(address.charAt(0)) };
    }

    /** "B2", or "B2:C3" for a block. */
    function blockText(top: number, left: number, bottom: number, right: number): string {
        const first = addressOf({ row: top, col: left });
        return top === bottom && left === right ? first : `${first}:${addressOf({ row: bottom, col: right })}`;
    }

    /** The selection's bounds. */
    function bounds(): { top: number; left: number; bottom: number; right: number } {
        return {
            top: Math.min(anchor.row, focus.row), bottom: Math.max(anchor.row, focus.row),
            left: Math.min(anchor.col, focus.col), right: Math.max(anchor.col, focus.col),
        };
    }

    /** The row with a number. */
    function rowAt(number: number): Row | undefined {
        return rows.find((r) => r.number === number);
    }

    /** Whether the server lets the cell be edited. */
    function editable(address: string): boolean {
        const position = positionOf(address);
        return rowAt(position.row)?.editable.includes(COLUMNS.charAt(position.col)) ?? false;
    }

    /** A cell's input (empty when it has none). */
    function inputOf(address: string): string {
        return cells.get(address)?.input ?? "";
    }

    /** The grid cell of an address. */
    function tdOf(address: string): JQuery {
        return $grid.find(`td[data-address="${address}"]`);
    }

    /** Every address of a block. */
    function addressesOf(top: number, left: number, bottom: number, right: number): string[] {
        const list: string[] = [];
        for (let r = top; r <= bottom; r++)
            for (let c = left; c <= right; c++) list.push(addressOf({ row: r, col: c }));
        return list;
    }

    /** The CodeMirror position of a character index. */
    function at(index: number): CodeMirrorPosition {
        return editor.posFromIndex(index);
    }

    // -- Requests ---------------------------------------------------------------------

    /** POSTs `body` as JSON; `handler` gets a reply that passes `reply` (any other shows the generic error). */
    function post<T>(url: string, body: unknown, reply: Check<T>, handler: (data: T) => void): void {
        $.ajax({
            url: url,
            type: "POST",
            headers: { "RequestVerificationToken": fieldValue($__RequestVerificationToken) },
            dataType: "json",
            data: JSON.stringify(body),
            contentType: "application/json; charset=utf-8",
            success: onReply(reply, handler)
        });
    }

    // -- Drawing ----------------------------------------------------------------------

    /** Loads the sheet of a year. */
    function LoadSheet(target: number) {
        year = target;
        $yearLabel.text(String(year));
        post("/AccountBook/GetBudgetSheet", { Kind: kind, Year: year }, sheetReply, function(data) {
            if (!data.result) {
                toastr.error(data.error);
                return;
            }
            ApplySheet(data.sheet);
        });
    }

    /** Replaces the sheet on screen (the undo history and the copy do not survive it). */
    function ApplySheet(sheet: Sheet) {
        if (editing) EndEdit(editing);
        rows = sheet.rows;
        cells.clear();
        for (const c of sheet.cells) cells.set(c.address, c);
        unbudgeted = sheet.unbudgeted;
        undoStack.length = 0;
        redoStack.length = 0;
        copied = null;
        Render();
        Select(anchor.row, anchor.col, false);
    }

    /** Draws the whole grid and the unbudgeted records. */
    function Render() {
        $grid.empty();
        const $head = $("<tr>").append($("<th>").addClass("budget-corner"));
        for (const letter of COLUMNS) $head.append($("<th>").attr("data-column", letter).text(letter));
        const $body = $("<tbody>");
        for (const r of rows) {
            const $tr = $("<tr>").addClass(`budget-row-${r.kind.toLowerCase()}`).attr("data-row", String(r.number));
            $tr.append($("<th>").addClass("budget-row-number").text(String(r.number)));
            for (let c = 0; c < COLUMNS.length; c++) {
                const $td = $("<td>").attr("data-address", addressOf({ row: r.number, col: c }));
                PaintCell($td, r, c);
                $tr.append($td);
            }
            $body.append($tr);
        }
        $grid.append($("<thead>").append($head), $body);
        RenderUnbudgeted();
    }

    /** Draws one cell: a fixed row's label in column A, otherwise the value the current view shows. */
    function PaintCell($td: JQuery, r: Row, c: number) {
        const address = addressOf({ row: r.number, col: c });
        $td.attr("class", "budget-cell").empty().toggleClass("budget-readonly", !r.editable.includes(COLUMNS.charAt(c)));
        if (c === 0 && r.kind !== "Free") {
            $td.append($("<span>").addClass("budget-label").text(r.label));
            if (r.currency !== "") $td.append($("<span>").addClass("budget-currency").text(r.currency));
            if (r.kind === "Line") {
                $td.append($("<button>").attr("type", "button").attr("title", text.removeLine).addClass("budget-remove-line btn btn-xs btn-link")
                    .append($("<i>").addClass("fas fa-times")).on("click", function(event) {
                        event.stopPropagation();
                        ConfirmRemove(r);
                    }));
            }
            return;
        }

        const data = cells.get(address);
        if (!data) return;
        const status = view !== "budget" && data.role === "Budget";
        const shown = !status ? data.display : view === "actual" ? data.actual : data.remaining;
        $td.text(shown);
        $td.toggleClass("budget-number", status ? shown !== "" : data.valueKind === "Number");
        $td.toggleClass("budget-error", !status && data.valueKind === "Error");
        if (view === "remaining" && data.role === "Budget" && data.state !== "") $td.addClass(`budget-state-${data.state}`);
    }

    /** Redraws one cell in place (its selection, edit and outline classes kept). */
    function RepaintCell(address: string) {
        const position = positionOf(address);
        const r = rowAt(position.row);
        if (!r) return;
        const $td = tdOf(address);
        const kept = ["budget-selected", "budget-active", "budget-copied", ...Array.from({ length: REFERENCE_COLOURS }, (_, i) => `budget-ref-${i}`)]
            .filter((c) => $td.hasClass(c));
        PaintCell($td, r, position.col);
        $td.addClass(kept.join(" "));
    }

    /** The records of lines the sheet does not have, each with a button that adds its line. */
    function RenderUnbudgeted() {
        $unbudgeted.empty();
        if (unbudgeted.length === 0) return;
        const $table = $("<table>").addClass("table table-sm budget-unbudgeted-table");
        $table.append($("<caption>").text(text.notBudgeted));
        const $body = $("<tbody>");
        for (const u of unbudgeted) {
            const $tr = $("<tr>").append($("<td>").text(u.label), $("<td>").text(u.currency));
            for (const month of u.months) $tr.append($("<td>").addClass("budget-number").text(month));
            $tr.append($("<td>").append($("<button>").attr("type", "button").addClass("btn btn-xs btn-outline-warning budget-add-unbudgeted").text(text.addLine)
                .on("click", () => WriteSheet("/AccountBook/AddBudgetLine", { MainClass: u.mainClass, SubClass: u.subClass, Currency: u.currency }))));
            $body.append($tr);
        }
        $unbudgeted.append($table.append($body));
    }

    /** Applies the changed cells of an edit's reply. */
    function ApplyChanged(changed: Cell[]) {
        for (const c of changed) {
            cells.set(c.address, c);
            RepaintCell(c.address);
        }
        ShowActive();
    }

    // -- Selection --------------------------------------------------------------------

    /** Selects a cell (or, with `extend`, moves the selection's far corner), kept inside the sheet. */
    function Select(row: number, col: number, extend: boolean) {
        const position = { row: Math.min(Math.max(row, 1), Math.max(rows.length, 1)), col: Math.min(Math.max(col, 0), COLUMNS.length - 1) };
        if (extend) {
            focus = position;
        } else {
            anchor = position;
            focus = { ...position };
        }
        RenderSelection();
        ShowActive();
    }

    /** Moves the active cell (or, with `extend`, the far corner). */
    function Move(rowsBy: number, colsBy: number, extend: boolean) {
        const from = extend ? focus : anchor;
        Select(from.row + rowsBy, from.col + colsBy, extend);
    }

    /** Marks the selected cells and the active one. */
    function RenderSelection() {
        $grid.find("td.budget-selected, td.budget-active").removeClass("budget-selected budget-active");
        const b = bounds();
        for (const address of addressesOf(b.top, b.left, b.bottom, b.right)) tdOf(address).addClass("budget-selected");
        tdOf(addressOf(anchor)).addClass("budget-active");
    }

    /** Shows the active cell's address and input (read-only when the cell cannot be edited). */
    function ShowActive() {
        if (editing) return;
        const address = addressOf(anchor);
        $nameBox.text(address);
        programmatic = true;
        editor.setValue(inputOf(address));
        programmatic = false;
        editor.setOption("readOnly", !editable(address));
    }

    // -- Editing ----------------------------------------------------------------------

    /**
     * Starts editing the active cell, with `initial` (a typed character) or its input as it is; a read-only cell is not edited.
     * `original` is the input before the edit when it is not the stored one (a refused edit reopened).
     */
    function StartEdit(initial: string | null, original: string = inputOf(addressOf(anchor))) {
        const address = addressOf(anchor);
        if (!editable(address)) return;
        editing = { address: address, original: original };
        point = null;
        editor.setOption("readOnly", false);
        tdOf(address).addClass("budget-editing");
        const value = initial ?? original;
        editor.setValue(value);
        // CodeMirror puts the caret at the start of a new text; typing goes on after what is there.
        editor.setCursor(at(value.length));
        editor.focus();
    }

    /** Leaves the edit: the cell and the formula bar show the stored input again, the colouring is cleared. */
    function EndEdit(current: Editing) {
        const address = current.address;
        editing = null;
        point = null;
        clearTimeout(previewTimer);
        previewSeq++;
        CloseSuggestions();
        ClearColouring();
        tdOf(address).removeClass("budget-editing");
        RepaintCell(address);
        ShowActive();
        elementOf($wrap, HTMLElement).focus();
    }

    /** Saves the edit (when the input changed) and moves the active cell. */
    function Commit(current: Editing, rowsBy: number, colsBy: number) {
        const edit: Edit = { address: current.address, before: current.original, after: editor.getValue() };
        EndEdit(current);
        Move(rowsBy, colsBy, false);
        if (edit.after === edit.before) return;
        Save([edit], function() {
            undoStack.push([edit]);
            redoStack.length = 0;
        }, edit);
    }

    /** Sends edits; on success applies the changed cells and calls `done`; a refused `retry` edit is reopened with what was typed. */
    function Save(edits: Edit[], done: () => void, retry: Edit | null = null) {
        post("/AccountBook/SetBudgetCells", { Kind: kind, Year: year, Edits: edits.map((e) => ({ Address: e.address, Input: e.after })) }, changedReply, function(data) {
            if (!data.result) {
                toastr.error(data.error);
                if (retry) {
                    const position = positionOf(retry.address);
                    Select(position.row, position.col, false);
                    StartEdit(retry.after, retry.before);
                }
                return;
            }
            ApplyChanged(data.changed);
            done();
        });
    }

    /** Clears the editable cells of the selection. */
    function ClearSelection() {
        const b = bounds();
        const edits = addressesOf(b.top, b.left, b.bottom, b.right).filter(editable).map((address) => ({ address: address, before: inputOf(address), after: "" }));
        if (edits.length === 0) return;
        Save(edits, function() {
            undoStack.push(edits);
            redoStack.length = 0;
        });
    }

    /** Undoes the last edit or paste. */
    function Undo() {
        const edits = undoStack.pop();
        if (!edits) return;
        Save(edits.map((e) => ({ address: e.address, before: e.after, after: e.before })), () => redoStack.push(edits));
    }

    /** Redoes the last undone edit. */
    function Redo() {
        const edits = redoStack.pop();
        if (!edits) return;
        Save(edits, () => undoStack.push(edits));
    }

    // -- Paste and fill -----------------------------------------------------------------

    /** Remembers the selection as the copied block. */
    function Copy() {
        const b = bounds();
        ClearCopied();
        copied = blockText(b.top, b.left, b.bottom, b.right);
        for (const address of addressesOf(b.top, b.left, b.bottom, b.right)) tdOf(address).addClass("budget-copied");
    }

    /** Forgets the copied block. */
    function ClearCopied() {
        copied = null;
        $grid.find("td.budget-copied").removeClass("budget-copied");
    }

    /** Sends a paste of `source` over the `target` block (top-left and bottom-right); the targets' inputs before it are kept for undo. */
    function Paste(source: string, top: number, left: number, bottom: number, right: number) {
        const targets = addressesOf(top, left, bottom, right);
        const before = new Map(targets.map((a) => [a, inputOf(a)]));
        post("/AccountBook/CopyBudgetCells", { Kind: kind, Year: year, Source: source, Target: blockText(top, left, bottom, right) }, changedReply, function(data) {
            if (!data.result) {
                toastr.error(data.error);
                return;
            }
            ApplyChanged(data.changed);
            const edits = data.changed.flatMap((c) => {
                const previous = before.get(c.address);
                return previous === undefined ? [] : [{ address: c.address, before: previous, after: c.input }];
            });
            if (edits.length > 0) {
                undoStack.push(edits);
                redoStack.length = 0;
            }
        });
    }

    /** Fills the selection from its first row (down) or first column (right). */
    function Fill(direction: "down" | "right") {
        const b = bounds();
        if (direction === "down" && b.bottom > b.top) Paste(blockText(b.top, b.left, b.top, b.right), b.top + 1, b.left, b.bottom, b.right);
        if (direction === "right" && b.right > b.left) Paste(blockText(b.top, b.left, b.bottom, b.left), b.top, b.left + 1, b.bottom, b.right);
    }

    // -- The formula bar ------------------------------------------------------------------

    /** Whether the formula being typed waits for a reference at the caret (point mode). */
    function AwaitsReference(): boolean {
        const value = editor.getValue();
        const caret = editor.indexFromPos(editor.getCursor());
        if (!editing || !value.startsWith("=") || caret !== value.length) return false;
        return (point !== null && caret === point.end) || WAITS_FOR_REFERENCE.test(value.slice(0, caret));
    }

    /** Inserts (or replaces the last pointed) reference at the caret; `extend` makes a range from the first pointed cell. */
    function PointAt(current: Editing, address: string, extend: boolean) {
        const insertion = extend && point ? `${point.anchor}:${address}` : address;
        const first = extend && point ? point.anchor : address;
        const caret = editor.indexFromPos(editor.getCursor());
        const start = point && caret === point.end ? point.start : caret;
        programmatic = true;
        editor.replaceRange(insertion, at(start), point && caret === point.end ? at(point.end) : undefined);
        programmatic = false;
        point = { anchor: first, address: address, start: start, end: start + insertion.length };
        OnEdited(current);
    }

    /** Point mode with an arrow key: the pointed cell moves from the last pointed (or the edited) cell. */
    function PointWithArrow(current: Editing, key: string) {
        const from = positionOf(point ? point.address : current.address);
        const moved = {
            row: Math.min(Math.max(from.row + (key === "ArrowDown" ? 1 : key === "ArrowUp" ? -1 : 0), 1), rows.length),
            col: Math.min(Math.max(from.col + (key === "ArrowRight" ? 1 : key === "ArrowLeft" ? -1 : 0), 0), COLUMNS.length - 1),
        };
        PointAt(current, addressOf(moved), false);
    }

    /** After the text changed: mirror it into the cell, offer functions, ask for the colouring. */
    function OnEdited(current: Editing) {
        tdOf(current.address).text(editor.getValue());
        UpdateSuggestions();
        SchedulePreview(current.address);
    }

    /** Asks for the colouring of the formula after a short pause; a value that is not a formula has none. */
    function SchedulePreview(address: string) {
        clearTimeout(previewTimer);
        const value = editor.getValue();
        if (!value.startsWith("=")) {
            previewSeq++;
            ClearColouring();
            return;
        }

        previewTimer = setTimeout(function() {
            const seq = ++previewSeq;
            post("/AccountBook/PreviewBudgetCell", { Kind: kind, Year: year, Address: address, Input: value }, previewReply, function(data) {
                // A reply for an older text, or one that arrives after the edit ended, is dropped.
                if (seq !== previewSeq || !editing || !data.result) return;
                PaintPreview(data.preview);
            });
        }, 150);
    }

    /** Paints the server's colouring: token classes, reference colours and outlines, problems, the value. */
    function PaintPreview(preview: Preview) {
        ClearColouring();
        lastTokens = preview.tokens;
        for (const t of preview.tokens) {
            const colour = t.reference >= 0 ? ` cm-budget-ref-${t.reference % REFERENCE_COLOURS}` : "";
            editor.markText(at(t.start), at(t.start + t.length), { className: `cm-budget-${t.class}${colour}` });
        }
        for (const p of preview.problems) {
            // A zero-length problem ("something is missing here") marks the character before it.
            const from = p.length === 0 ? Math.max(p.start - 1, 0) : p.start;
            editor.markText(at(from), at(p.length === 0 ? p.start : p.start + p.length),
                { className: p.blocking ? "cm-budget-problem cm-budget-blocking" : "cm-budget-problem", title: p.message });
            $problems.append($("<li>").addClass(p.blocking ? "text-danger" : "text-warning").text(p.message));
        }
        for (const r of preview.references) {
            if (r.topLeft === "") continue;
            const a = positionOf(r.topLeft);
            const b = positionOf(r.bottomRight);
            for (const address of addressesOf(a.row, a.col, b.row, b.col)) tdOf(address).addClass(`budget-ref-${r.index % REFERENCE_COLOURS}`);
        }
        $previewValue.text(preview.value);
    }

    /** Removes every mark, outline and problem of the formula bar. */
    function ClearColouring() {
        for (const mark of editor.getAllMarks()) mark.clear();
        parenMarks = [];
        lastTokens = [];
        for (let i = 0; i < REFERENCE_COLOURS; i++) $grid.find(`td.budget-ref-${i}`).removeClass(`budget-ref-${i}`);
        $problems.empty();
        $previewValue.text("");
    }

    /** Highlights the parenthesis next to the caret and its partner. */
    function MatchParentheses() {
        for (const mark of parenMarks) mark.clear();
        parenMarks = [];
        const caret = editor.indexFromPos(editor.getCursor());
        const token = lastTokens.find((t) => t.class === "paren" && t.partner >= 0 && (t.start === caret || t.start + 1 === caret));
        const partner = token ? lastTokens[token.partner] : undefined;
        if (!token || !partner) return;
        for (const t of [token, partner]) parenMarks.push(editor.markText(at(t.start), at(t.start + 1), { className: "cm-budget-paren-match" }));
    }

    /** Offers the functions whose name starts with the word being typed. */
    function UpdateSuggestions() {
        const value = editor.getValue();
        const before = value.slice(0, editor.indexFromPos(editor.getCursor()));
        const word = /(?:^=|[=+\-*/^&(,:<>\s])([A-Za-z][A-Za-z0-9]*)$/.exec(before)?.[1] ?? "";
        suggestions = value.startsWith("=") && word !== "" ? functions.filter((f) => f.Name.startsWith(word.toUpperCase())) : [];
        suggestionIndex = 0;
        suggestionWordLength = word.length;
        RenderSuggestions();
    }

    /** Draws the offered functions with their argument hints. */
    function RenderSuggestions() {
        $autocomplete.empty().toggleClass("d-none", suggestions.length === 0);
        suggestions.forEach((f, index) => {
            $autocomplete.append($("<li>").toggleClass("active", index === suggestionIndex).text(f.Signature).on("mousedown", function(event) {
                event.preventDefault();
                TakeSuggestion(f);
            }));
        });
    }

    /** Closes the offered functions. */
    function CloseSuggestions() {
        suggestions = [];
        RenderSuggestions();
    }

    /** Replaces the word being typed with the chosen function and its "(". */
    function TakeSuggestion(chosen: { Name: string }) {
        const caret = editor.indexFromPos(editor.getCursor());
        editor.replaceRange(`${chosen.Name}(`, at(caret - suggestionWordLength), at(caret));
        CloseSuggestions();
    }

    editor.on("change", function() {
        if (programmatic || !editing) return;
        point = null;
        OnEdited(editing);
    });
    editor.on("cursorActivity", MatchParentheses);
    editor.on("focus", function() {
        if (!editing) StartEdit(null);
    });
    editor.on("keydown", function(_editor, event) {
        const current = editing;
        if (!current) return;
        const chosen = suggestions[suggestionIndex];
        if (chosen) {
            if (event.key === "ArrowDown" || event.key === "ArrowUp") {
                suggestionIndex = (suggestionIndex + (event.key === "ArrowDown" ? 1 : suggestions.length - 1)) % suggestions.length;
                RenderSuggestions();
                event.preventDefault();
                return;
            }
            if (event.key === "Enter" || event.key === "Tab") {
                TakeSuggestion(chosen);
                event.preventDefault();
                return;
            }
            if (event.key === "Escape") {
                CloseSuggestions();
                event.preventDefault();
                return;
            }
        }
        if (event.key.startsWith("Arrow") && AwaitsReference()) {
            PointWithArrow(current, event.key);
            event.preventDefault();
            return;
        }
        switch (event.key) {
            case "Enter": Commit(current, event.shiftKey ? -1 : 1, 0); break;
            case "Tab": Commit(current, 0, event.shiftKey ? -1 : 1); break;
            case "Escape": EndEdit(current); break;
            default: return;
        }
        event.preventDefault();
    });

    // -- Grid wiring ----------------------------------------------------------------------

    $grid.on("mousedown", "td", function(event) {
        const address = String($(this).attr("data-address"));
        if (editing && AwaitsReference()) {
            event.preventDefault();
            PointAt(editing, address, event.shiftKey);
            return;
        }
        if (editing) Commit(editing, 0, 0);
        const position = positionOf(address);
        Select(position.row, position.col, event.shiftKey);
        elementOf($wrap, HTMLElement).focus();
    });
    $grid.on("dblclick", "td", function() {
        const position = positionOf(String($(this).attr("data-address")));
        Select(position.row, position.col, false);
        StartEdit(null);
    });

    $wrap.on("keydown", function(event) {
        if (editing) return;
        const key = event.key ?? "";
        if (event.ctrlKey || event.metaKey) {
            switch (key.toLowerCase()) {
                case "c": Copy(); break;
                case "v": if (copied) { const b = bounds(); Paste(copied, b.top, b.left, b.bottom, b.right); } break;
                case "d": Fill("down"); break;
                case "r": Fill("right"); break;
                case "z": Undo(); break;
                case "y": Redo(); break;
                default: return;
            }
            event.preventDefault();
            return;
        }
        switch (key) {
            case "ArrowUp": Move(-1, 0, event.shiftKey); break;
            case "ArrowDown": Move(1, 0, event.shiftKey); break;
            case "ArrowLeft": Move(0, -1, event.shiftKey); break;
            case "ArrowRight": Move(0, 1, event.shiftKey); break;
            case "Enter": Move(event.shiftKey ? -1 : 1, 0, false); break;
            case "Tab": Move(0, event.shiftKey ? -1 : 1, false); break;
            case "F2": StartEdit(null); break;
            case "Delete":
            case "Backspace": ClearSelection(); break;
            case "Escape": ClearCopied(); break;
            default:
                if (key.length !== 1) return;
                StartEdit(key);
        }
        event.preventDefault();
    });

    // -- Toolbar, lines, Excel ----------------------------------------------------------------

    /** Asks whether to remove a line. */
    function ConfirmRemove(r: Row) {
        pendingRemove = r;
        $("#confirmRemoveLineText").text(text.confirmRemoveLine.replace("{0}", r.label).replace("{1}", r.currency));
        $confirmRemoveModal.modal("show");
    }

    $("#budgetViewSwitch").on("click", "[data-view]", function() {
        const chosen = $(this).attr("data-view");
        view = chosen === "actual" || chosen === "remaining" ? chosen : "budget";
        $("#budgetViewSwitch [data-view]").removeClass("active");
        $(this).addClass("active");
        for (const r of rows)
            for (let c = 0; c < COLUMNS.length; c++) RepaintCell(addressOf({ row: r.number, col: c }));
    });
    $("#btnBudgetPrevYear").on("click", () => LoadSheet(year - 1));
    $("#btnBudgetNextYear").on("click", () => LoadSheet(year + 1));
    $("#btnBudgetThisYear").on("click", () => LoadSheet(thisYear));

    /** Fills the sub-class options of the chosen main class, by name. */
    function FillSubClasses() {
        $lineSubClass.empty();
        for (const sub of subClassMap[fieldValue($lineMainClass)] ?? [])
            $lineSubClass.append($("<option>").val(sub).text(classLabels[sub] ?? sub));
    }

    /** Sends a sheet-changing write (a line, the carry-over) and shows the sheet it returns. */
    function WriteSheet(url: string, body: Record<string, unknown>, then: () => void = () => undefined) {
        post(url, { Kind: kind, Year: year, ...body }, sheetWriteReply, function(data) {
            if (!data.result) {
                toastr.error(data.error);
                return;
            }
            toastr.success(data.message);
            then();
            ApplySheet(data.sheet);
        });
    }

    $("#btnBudgetAddLine").on("click", function() {
        FillSubClasses();
        $lineModal.modal("show");
    });
    $lineMainClass.on("change", FillSubClasses);
    $formLine.on("submit", function() {
        if (!$formLine.valid()) return false;
        WriteSheet("/AccountBook/AddBudgetLine", { MainClass: fieldValue($lineMainClass), SubClass: selectValue($lineSubClass) ?? "", Currency: fieldValue($lineCurrency) },
            () => $lineModal.modal("hide"));
        return false;
    });
    $("#btnConfirmRemoveLine").on("click", function() {
        const r = pendingRemove;
        if (!r) return;
        $confirmRemoveModal.modal("hide");
        WriteSheet("/AccountBook/RemoveBudgetLine", { MainClass: r.mainClass, SubClass: r.subClass, Currency: r.currency });
    });
    $("#btnBudgetCarryOver").on("click", () => WriteSheet("/AccountBook/CarryOverBudget", {}));

    $("#btnBudgetExcel").on("click", function() {
        // A plain form POST, so the browser downloads the binary response ($.ajax can't save it).
        const form = document.createElement("form");
        form.method = "POST";
        form.action = "/AccountBook/ExportExcelBudget";
        const fields: [string, string][] = [["__RequestVerificationToken", fieldValue($__RequestVerificationToken)], ["kind", kind], ["year", String(year)], ["fileName", kind + String(year)]];
        for (const [name, value] of fields) {
            const input = document.createElement("input");
            input.name = name;
            input.value = value;
            form.appendChild(input);
        }
        document.body.appendChild(form);
        form.submit();
        form.remove();
    });

    LoadSheet(year);
})();
