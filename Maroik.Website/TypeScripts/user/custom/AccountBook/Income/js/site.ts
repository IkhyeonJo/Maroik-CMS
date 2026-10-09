/**
 * Script for the account book **Income** page (`Views/AccountBook/Income.cshtml`).
 * A NonFactors MVC.Grid list of income entries with create / edit / delete
 * modals and Excel export — the standard grid-CRUD pattern (see
 * `AccountBook/Asset` for the fullest commentary) plus three page-specific bits:
 *
 *   • A localized jQuery-UI datepicker on the "date" fields.
 *   • A dependent `<select>`: choosing a main income class narrows the
 *     subclass options to those `IncomeClassPolicy` allows for it.
 *   • The amount field's label follows the chosen deposit asset (its currency).
 *
 * Client checks (allowed subclasses, required fields) are UX mirrors — the
 * controller re-validates every field and every class combination on save.
 * IIFE-wrapped, no `import` / `export`.
 */
(function() {
    // The runtime-check helpers the _Layout script defines (see TypeScripts/global.d.ts).
    const { check, parseJson, fieldValue, selectValue, optionalFieldValue, attribute } = window;
    /** Zero-pads a 1-2 digit date/time component to 2 digits (e.g. `5` -> `"05"`). */
    function pad2(n: number): string {
        return n.toString().padStart(2, "0");
    }

    // Cached references: jQuery-UI tab containers, the grid search box, both
    // modals' fields, the amount labels, and the anti-forgery input.
    const $createIncomeTabs = $("#createIncomeTabs");
    const $editIncomeTabs = $("#editIncomeTabs");
    const $createIncomeDate = $("#createIncomeDate");
    const $editIncomeDate = $("#editIncomeDate");
    const $gridSearch = $("#gridSearch");
    const $createIncomeSubClass = $("#createIncomeSubClass");
    const $editIncomeSubClass = $("#editIncomeSubClass");
    const $editIncomeDialogModal = $("#editIncomeDialogModal");
    const $formCreateIncome = $("#formCreateIncome");
    const $__RequestVerificationToken = $("input[name=\"__RequestVerificationToken\"]");
    const $createIncomeMainClass = $("#createIncomeMainClass");
    const $createIncomeContent = $("#createIncomeContent");
    const $createIncomeAmount = $("#createIncomeAmount");
    const $createIncomeHour = $("#createIncomeHour");
    const $createIncomeMinute = $("#createIncomeMinute");
    const $createIncomeSecond = $("#createIncomeSecond");
    const $createIncomeDepositMyAssetProductName = $("#createIncomeDepositMyAssetProductName");
    const $createIncomeNote = $("#createIncomeNote");
    const $createIncomeDialogModal = $("#createIncomeDialogModal");
    const $editIncomeId = $("#editIncomeId");
    const $editIncomeMainClass = $("#editIncomeMainClass");
    const $editIncomeContent = $("#editIncomeContent");
    const $editIncomeAmount = $("#editIncomeAmount");
    const $editIncomeHour = $("#editIncomeHour");
    const $editIncomeMinute = $("#editIncomeMinute");
    const $editIncomeSecond = $("#editIncomeSecond");
    const $editIncomeDepositMyAssetProductName = $("#editIncomeDepositMyAssetProductName");
    const $editIncomeNote = $("#editIncomeNote");
    const $formEditIncome = $("#formEditIncome");
    const $confirmDeleteIncomeDialogModal = $("#confirmDeleteIncomeDialogModal");
    const $labelCreateIncomeAmount = $("#labelCreateIncomeAmount");
    const $labelEditIncomeAmount = $("#labelEditIncomeAmount");
    const $btnEditIncomeGridRow = $("#btnEditIncomeGridRow");
    const $btnConfirmDeleteIncome = $("#btnConfirmDeleteIncome");
    const $btnExportExcelIncome = $("#btnExportExcelIncome");
    const $btnDeleteIncome = $("#btnDeleteIncome");

    // Income main-class -> allowed subclasses. Authoritative in IncomeClassPolicy (server),
    // serialized into the page by Income.cshtml. Mirrored here for form UX only; the server
    // re-validates every combination on save.
    const incomeSubClassMap = parseJson(optionalFieldValue($("#incomeSubClassMap")) || "{}", check.record(check.array(check.string)), "#incomeSubClassMap");
    // Localized toast shown when the amount-label request itself fails (transport error).
    const failedToLoadAmountLabelMessage = fieldValue($("#localizerFailedToLoadAmountLabel"));

    // Enables/shows only the options in allowedValues (disabling+hiding the rest) and selects
    // valueToSelect when it is allowed, otherwise the first allowed option.
    function ApplyAllowedOptions($select: JQuery, allowedValues: string[], valueToSelect: string | null) {
        $select.find("option").each(function(this: HTMLOptionElement) {
            const allowed = allowedValues.indexOf(this.value) !== -1;
            $(this).prop("disabled", !allowed).prop("hidden", !allowed);
        });
        const target = (valueToSelect != null && allowedValues.indexOf(valueToSelect) !== -1)
            ? valueToSelect
            : allowedValues[0];
        if (target != null) {
            $select.val(target).trigger("change");
        }
    }

    // Localized month / day names for the datepicker, each read from a hidden input the view
    // rendered from the resource files (asserted `Record<string, string>`: each holds a string,
    // which `.val()` types as a wider union). `PrevText` / `NextText` are read PascalCase to match
    // the object keys (a casing fix committed separately).
    const localizer = {
        PrevText: $("#localizerPrevText").val(),
        NextText: $("#localizerNextText").val(),
        January: $("#localizerJanuary").val(),
        February: $("#localizerFebruary").val(),
        March: $("#localizerMarch").val(),
        April: $("#localizerApril").val(),
        May: $("#localizerMay").val(),
        June: $("#localizerJune").val(),
        July: $("#localizerJuly").val(),
        August: $("#localizerAugust").val(),
        September: $("#localizerSeptember").val(),
        October: $("#localizerOctober").val(),
        November: $("#localizerNovember").val(),
        December: $("#localizerDecember").val(),
        Jan: $("#localizerJan").val(),
        Feb: $("#localizerFeb").val(),
        Mar: $("#localizerMar").val(),
        Apr: $("#localizerApr").val(),
        Jun: $("#localizerJun").val(),
        Jul: $("#localizerJul").val(),
        Aug: $("#localizerAug").val(),
        Sep: $("#localizerSep").val(),
        Oct: $("#localizerOct").val(),
        Nov: $("#localizerNov").val(),
        Dec: $("#localizerDec").val(),
        Sunday: $("#localizerSunday").val(),
        Monday: $("#localizerMonday").val(),
        Tuesday: $("#localizerTuesday").val(),
        Wednesday: $("#localizerWednesday").val(),
        Thursday: $("#localizerThursday").val(),
        Friday: $("#localizerFriday").val(),
        Saturday: $("#localizerSaturday").val(),
        Sun: $("#localizerSun").val(),
        Mon: $("#localizerMon").val(),
        Tue: $("#localizerTue").val(),
        Wed: $("#localizerWed").val(),
        Thu: $("#localizerThu").val(),
        Fri: $("#localizerFri").val(),
        Sat: $("#localizerSat").val(),
        Su: $("#localizerSu").val(),
        Mo: $("#localizerMo").val(),
        Tu: $("#localizerTu").val(),
        We: $("#localizerWe").val(),
        Th: $("#localizerTh").val(),
        Fr: $("#localizerFr").val(),
        Sa: $("#localizerSa").val(),
        YearSuffix: $("#localizerYearSuffix").val()
    } as Record<string, string>;

    // Apply the localized names to every datepicker on the page.
    $.datepicker.setDefaults({
        dateFormat: "yy-mm-dd",
        prevText: localizer.PrevText,
        nextText: localizer.NextText,
        monthNames: [localizer.January, localizer.February, localizer.March, localizer.April, localizer.May, localizer.June, localizer.July, localizer.August, localizer.September, localizer.October, localizer.November, localizer.December],
        monthNamesShort: [localizer.Jan, localizer.Feb, localizer.Mar, localizer.Apr, localizer.May, localizer.Jun, localizer.Jul, localizer.Aug, localizer.Sep, localizer.Oct, localizer.Nov, localizer.Dec],
        dayNames: [localizer.Sunday, localizer.Monday, localizer.Tuesday, localizer.Wednesday, localizer.Thursday, localizer.Friday, localizer.Saturday],
        dayNamesShort: [localizer.Sun, localizer.Mon, localizer.Tue, localizer.Wed, localizer.Thu, localizer.Fri, localizer.Sat],
        dayNamesMin: [localizer.Su, localizer.Mo, localizer.Tu, localizer.We, localizer.Th, localizer.Fr, localizer.Sa],
        showMonthAfterYear: true,
        yearSuffix: localizer.YearSuffix
    });

    $(function() {
        $createIncomeTabs.tabs();
        $editIncomeTabs.tabs();
        // The empty `setTimeout(fn, 1)` in `beforeShow` / `onChangeMonthYear` is
        // a jQuery-UI datepicker nudge — it defers a repaint so the button panel
        // lays out correctly.
        $createIncomeDate.datepicker({
            showButtonPanel: true,
            beforeShow: function() {
                setTimeout(function() {

                }, 1);
            },
            onChangeMonthYear: function() {
                setTimeout(function() {

                }, 1);
            }
        });

        $editIncomeDate.datepicker({
            showButtonPanel: true,
            beforeShow: function() {
                setTimeout(function() {

                }, 1);
            },
            onChangeMonthYear: function() {
                setTimeout(function() {

                }, 1);
            }
        });

        // Hide the datepicker's built-in "Close" / "Today" buttons via injected CSS.
        $("<style> .ui-datepicker-close { display: none; } </style>").appendTo("head");
        $("<style> .ui-datepicker-current { display: none; } </style>").appendTo("head");
    });

    // Bootstrap table class that marks the currently selected grid row.
    let selectedRowColor = "table-primary";

    // MvcGrid `rowclick` (native CustomEvent): move the highlight to the row
    // whose `data-id` matches the clicked record.
    $(document).off("rowclick.Income").on("rowclick.Income", (e: JQuery.TriggeredEvent) => {
        let selectedRow = e.detail as unknown as MvcGridRowClickDetail;
        let selectedRowId = selectedRow.data.Id;

        let $clsGridRow = $(".clsGridRow");

        $clsGridRow.each(function() {
            $(this).removeClass(selectedRowColor);
        });

        $clsGridRow.each(function() {
            if ((String(selectedRowId).valueOf() === String($(this).attr("data-id")).valueOf())) {
                $(this).addClass(selectedRowColor);
            }
        });
    });

    // MvcGrid lifecycle events — intentional no-ops, bound only as hook points.
    $(document).off("reloadstart.Income").on("reloadstart.Income", _ => {
    });

    $(document).off("reloadend.Income").on("reloadend.Income", _ => {
    });

    $(document).off("reloadfail.Income").on("reloadfail.Income", _ => {
    });

    $(document).off("gridconfigure.Income").on("gridconfigure.Income", _ => {
    });

    // Double-click the highlighted row to open its edit modal.
    $(document).off("dblclick.Income", ".clsGridRow").on("dblclick.Income", ".clsGridRow", function() {
        $(".clsGridRow").each(function() {
            if ($(this).attr("class")!.includes(selectedRowColor)) {
                EditIncomeGridRow();
            }
        });
    });

    // Free-text search: push the term into the grid query string and reload.
    $gridSearch.off("input").on("input", function(event) {
        const grid = new MvcGrid(document.querySelector(".mvc-grid"));
        grid.url.searchParams.set("wholeSearch", (event.currentTarget as HTMLInputElement).value);
        grid.reload();
    });

    /** Create form: on main-class change, narrow the subclass options to those allowed for it. */
    function CreateFormShowIncomeSubClassBySelectedIncomeMainClass(createIncomeMainClass: HTMLSelectElement) {
        ApplyAllowedOptions($createIncomeSubClass, incomeSubClassMap[createIncomeMainClass.value] || [], null);
    }

    /** Edit form: same as above for the edit modal. */
    function EditFormShowIncomeSubClassBySelectedIncomeMainClass(editIncomeMainClass: HTMLSelectElement) {
        ApplyAllowedOptions($editIncomeSubClass, incomeSubClassMap[editIncomeMainClass.value] || [], null);
    }

    /**
     * Create-income modal submit: validate, assemble the `Created` timestamp
     * from the separate date / hour / minute / second fields, POST as JSON, then
     * close / reload / toast.
     *
     * `Created` is sent as the account's own local wall-clock time
     * ("yyyy-MM-dd HH:mm:ss"), not converted to UTC here — the server already
     * knows the account's IANA time zone from the session and does that
     * conversion itself, so the browser's own (possibly different) time zone
     * never enters into it.
     */
    function CreateIncome() {

        if (!$formCreateIncome.valid()) {
            return false;
        }

        let mainClass = $createIncomeMainClass.val();
        let subClass = $createIncomeSubClass.val();
        let content = $createIncomeContent.val();
        let amount = $createIncomeAmount.val();

        let year = parseInt(fieldValue($createIncomeDate).substring(0, 4));
        let month = parseInt(fieldValue($createIncomeDate).substring(5, 7));
        let day = parseInt(fieldValue($createIncomeDate).substring(8, 10));
        let hour = parseInt(fieldValue($createIncomeHour));
        let minute = parseInt(fieldValue($createIncomeMinute));
        let second = parseInt(fieldValue($createIncomeSecond));

        let created = `${year}-${pad2(month)}-${pad2(day)} ${pad2(hour)}:${pad2(minute)}:${pad2(second)}`;
        let depositMyAssetProductName = $createIncomeDepositMyAssetProductName.val();
        let note = $createIncomeNote.val();

        let paramValue = JSON.stringify({
            MainClass: mainClass,
            SubClass: subClass,
            Content: content,
            Amount: amount,
            Created: created,
            DepositMyAssetProductName: depositMyAssetProductName,
            Note: note
        });

        $.ajax({
            url: "/AccountBook/CreateIncome",
            type: "POST",
            headers: { "RequestVerificationToken": fieldValue($__RequestVerificationToken) },
            dataType: "json",
            data: paramValue,
            contentType: "application/json; charset=utf-8",
            success: function(data: ActionReply) {
                if (data.result) {
                    $createIncomeDialogModal.modal("hide");

                    const grid = new MvcGrid(document.querySelector(".mvc-grid"));
                    grid.reload();

                    toastr.success(data.message);
                } else {
                    toastr.error(data.error);
                }
            }
        });

        return false;
    }

    /**
     * Opens the edit modal for the selected grid row. Fetches the record
     * (`IsIncomeExists`) and fills the form; the `Created` ISO string is split
     * back into the separate date / time-part inputs, and `ApplyAllowedOptions`
     * re-populates the subclass select for the record's main class.
     *
     * @param errorMessageSelectGridRow localized "pick a row first" text from a `data-*` attribute.
     */
    function EditIncomeGridRow(errorMessageSelectGridRow?: string) {

        let selectedRowId = "";

        $(".clsGridRow").each(function() {
            if ($(this).attr("class")!.includes(selectedRowColor)) {
                selectedRowId = $(this).attr("data-id")!;
            }
        });

        if (selectedRowId === "") {
            toastr.error(errorMessageSelectGridRow!);
            return false;
        }

        $.ajax({
            url: "/AccountBook/IsIncomeExists" + "?id=" + selectedRowId,
            type: "POST",
            headers: { "RequestVerificationToken": fieldValue($__RequestVerificationToken) },
            dataType: "json",
            contentType: "application/json; charset=utf-8",
            success: function(data: ReadReply<IncomePayload>) {
                if (data.result) {

                    $editIncomeId.val(data.income.id);
                    $editIncomeMainClass.val(data.income.mainClass).trigger("change");

                    ApplyAllowedOptions($editIncomeSubClass, incomeSubClassMap[data.income.mainClass] || [], data.income.subClass);

                    $editIncomeContent.val(data.income.content);
                    $editIncomeAmount.val(data.income.amount);
                    // `created` is `YYYY-MM-DDTHH:mm:ss`; split into the date field
                    // and the hour / minute / second selects (`.trigger("change")` so any
                    // listener fires).
                    $editIncomeDate.val(data.income.created.split("T")[0]);
                    $editIncomeHour.val(parseInt(data.income.created.split("T")[1].substring(0, 2))).trigger("change");
                    $editIncomeMinute.val(parseInt(data.income.created.split("T")[1].substring(3, 5))).trigger("change");
                    $editIncomeSecond.val(parseInt(data.income.created.split("T")[1].substring(6, 8))).trigger("change");
                    $editIncomeDepositMyAssetProductName.val(data.income.depositMyAssetProductName).trigger("change");
                    $editIncomeNote.val(data.income.note);

                    $editIncomeDialogModal.modal({
                        keyboard: false,
                        backdrop: "static"
                    });

                    $editIncomeDialogModal.modal("toggle");
                    $editIncomeDialogModal.modal("show");
                } else {
                    toastr.error(data.error);
                }
            }
        });
    }

    /** Edit-income modal submit: same shape as `CreateIncome` plus `ID`. */
    function UpdateIncome() {

        if (!$formEditIncome.valid()) {
            return false;
        }

        let id = $editIncomeId.val();
        let mainClass = $editIncomeMainClass.val();
        let subClass = $editIncomeSubClass.val();
        let content = $editIncomeContent.val();
        let amount = $editIncomeAmount.val();

        let year = parseInt(fieldValue($editIncomeDate).substring(0, 4));
        let month = parseInt(fieldValue($editIncomeDate).substring(5, 7));
        let day = parseInt(fieldValue($editIncomeDate).substring(8, 10));
        let hour = parseInt(fieldValue($editIncomeHour));
        let minute = parseInt(fieldValue($editIncomeMinute));
        let second = parseInt(fieldValue($editIncomeSecond));

        // See CreateIncome — local wall-clock string, no client-side UTC conversion.
        let created = `${year}-${pad2(month)}-${pad2(day)} ${pad2(hour)}:${pad2(minute)}:${pad2(second)}`;
        let depositMyAssetProductName = $editIncomeDepositMyAssetProductName.val();
        let note = $editIncomeNote.val();

        let paramValue = JSON.stringify({
            Id: id,
            MainClass: mainClass,
            SubClass: subClass,
            Content: content,
            Amount: amount,
            Created: created,
            DepositMyAssetProductName: depositMyAssetProductName,
            Note: note
        });

        $.ajax({
            url: "/AccountBook/UpdateIncome",
            type: "POST",
            headers: { "RequestVerificationToken": fieldValue($__RequestVerificationToken) },
            dataType: "json",
            data: paramValue,
            contentType: "application/json; charset=utf-8",
            success: function(data: ActionReply) {
                if (data.result) {
                    $editIncomeDialogModal.modal("hide");

                    const grid = new MvcGrid(document.querySelector(".mvc-grid"));
                    grid.reload();

                    toastr.success(data.message);
                } else {
                    toastr.error(data.error);
                }
            }
        });
        return false;
    }

    /** "Delete" button: require a selected row, then just open the confirmation modal. */
    function ConfirmDeleteIncome(errorMessageSelectGridRow?: string) {

        let selectedRowId = "";

        $(".clsGridRow").each(function() {
            if ($(this).attr("class")!.includes(selectedRowColor)) {
                selectedRowId = $(this).attr("data-id")!;
            }
        });

        if (selectedRowId === "") {
            toastr.error(errorMessageSelectGridRow!);
            return false;
        }

        $confirmDeleteIncomeDialogModal.modal({
            keyboard: false,
            backdrop: "static"
        });

        $confirmDeleteIncomeDialogModal.modal("toggle");
        $confirmDeleteIncomeDialogModal.modal("show");
    }

    /** Confirmed delete: re-check selection, confirm the record exists, then POST `DeleteIncome`. */
    function DeleteIncome(errorMessageSelectGridRow?: string) {

        let selectedRowId = "";

        $(".clsGridRow").each(function() {
            if ($(this).attr("class")!.includes(selectedRowColor)) {
                selectedRowId = $(this).attr("data-id")!;
            }
        });

        if (selectedRowId === "") {
            toastr.error(errorMessageSelectGridRow!);
            return false;
        }

        $.ajax({
            url: "/AccountBook/IsIncomeExists" + "?id=" + selectedRowId,
            type: "POST",
            headers: { "RequestVerificationToken": fieldValue($__RequestVerificationToken) },
            dataType: "json",
            contentType: "application/json; charset=utf-8",
            success: function(data: ReadReply<IncomePayload>) {
                if (data.result) {

                    let paramValue = JSON.stringify({
                        Id: data.income.id
                    });

                    $.ajax({
                        url: "/AccountBook/DeleteIncome",
                        type: "POST",
                        headers: { "RequestVerificationToken": fieldValue($__RequestVerificationToken) },
                        dataType: "json",
                        data: paramValue,
                        contentType: "application/json; charset=utf-8",
                        success: function(data: ActionReply) {
                            if (data.result) {
                                $confirmDeleteIncomeDialogModal.modal("hide");

                                const grid = new MvcGrid(document.querySelector(".mvc-grid"));
                                grid.reload();

                                toastr.success(data.message);
                            } else {
                                toastr.error(data.error);
                            }
                        }
                    });
                } else {
                    toastr.error(data.error);
                }
            }
        });
    }

    /**
     * "Export to Excel": submit a throwaway `<form>` as a real POST so the
     * browser downloads the binary response (`$.ajax` can't save it).
     */
    function ExportExcelIncome() {
        let form = document.createElement("form");
        let element1 = document.createElement("input");
        let element2 = document.createElement("input");

        form.method = "POST";
        form.action = "/AccountBook/ExportExcelIncome";

        element1.name = "__RequestVerificationToken";
        element1.value = fieldValue($__RequestVerificationToken);
        form.appendChild(element1);

        element2.name = "fileName";
        element2.value = "Income";
        form.appendChild(element2);

        document.body.appendChild(form);

        form.submit();
    }

    /**
     * Updates the create-form's amount label to match the chosen deposit asset
     * (e.g. its currency name). The server returns the label in `data.label` —
     * the plain "Amount" label when the asset has no currency or its lookup failed
     * server-side — so it is applied as-is; a transport failure toasts
     * `failedToLoadAmountLabelMessage` instead.
     */
    function ChangeCreateIncomeAmountLabel(productName: string) {
        $.ajax({
            url: "/AccountBook/GetIncomeAmountLabel" + "?productName=" + encodeURIComponent(productName),
            type: "POST",
            headers: { "RequestVerificationToken": fieldValue($__RequestVerificationToken) },
            dataType: "json",
            contentType: "application/json; charset=utf-8",
            success: function(data: AmountLabelReply) {
                $labelCreateIncomeAmount.text(data.label);
            },
            error: function() {
                toastr.error(failedToLoadAmountLabelMessage);
            }
        });
    }

    /** Same as `ChangeCreateIncomeAmountLabel` for the edit form. */
    function ChangeEditIncomeAmountLabel(productName: string) {
        $.ajax({
            url: "/AccountBook/GetIncomeAmountLabel" + "?productName=" + encodeURIComponent(productName),
            type: "POST",
            headers: { "RequestVerificationToken": fieldValue($__RequestVerificationToken) },
            dataType: "json",
            contentType: "application/json; charset=utf-8",
            success: function(data: AmountLabelReply) {
                $labelEditIncomeAmount.text(data.label);
            },
            error: function() {
                toastr.error(failedToLoadAmountLabelMessage);
            }
        });
    }

    // --- Button / form / select wiring --------------------------------
    $btnEditIncomeGridRow.off("click").on("click", function() {
        EditIncomeGridRow($(this).attr("data-errorMessageSelectGridRow"));
    });

    $btnConfirmDeleteIncome.off("click").on("click", function() {
        ConfirmDeleteIncome($(this).attr("data-errorMessageSelectGridRow"));
    });

    $btnExportExcelIncome.off("click").on("click", function() {
        ExportExcelIncome();
    });

    $btnDeleteIncome.off("click").on("click", function() {
        DeleteIncome($(this).attr("data-errorMessageSelectGridRow"));
    });

    $formCreateIncome.off("submit").on("submit", function() {
        return CreateIncome();
    });

    $formEditIncome.off("submit").on("submit", function() {
        return UpdateIncome();
    });

    $createIncomeMainClass.off("change").on("change", function(event) {
        CreateFormShowIncomeSubClassBySelectedIncomeMainClass(
            event.currentTarget as HTMLSelectElement
        );
    });

    $editIncomeMainClass.off("change").on("change", function(event) {
        EditFormShowIncomeSubClassBySelectedIncomeMainClass(
            event.currentTarget as HTMLSelectElement
        );
    });

    $createIncomeDepositMyAssetProductName.off("change").on("change", function(event) {
        ChangeCreateIncomeAmountLabel(
            (event.currentTarget as HTMLSelectElement).value
        );
    });

    $editIncomeDepositMyAssetProductName.off("change").on("change", function(event) {
        ChangeEditIncomeAmountLabel(
            (event.currentTarget as HTMLSelectElement).value
        );
    });

    // On load, set the create-form amount label for whichever asset is preselected.
    $(function() {
        // An account without assets has nothing preselected, and so no label to ask for.
        const preselected = selectValue($createIncomeDepositMyAssetProductName);
        if (preselected !== null) {
            ChangeCreateIncomeAmountLabel(preselected);
        }
    });
})();
