/**
 * Script for the account book **Expenditure** page
 * (`Views/AccountBook/Expenditure.cshtml`). A NonFactors MVC.Grid list with
 * create / edit / delete modals and Excel export — the standard grid-CRUD
 * pattern (see `AccountBook/Asset`) plus:
 *
 *   • A localized jQuery-UI datepicker on the "date" fields.
 *   • A dependent `<select>`: choosing a main class narrows the subclass
 *     options to those `ExpenditureClassPolicy` allows.
 *   • A "deposit asset" block that is only shown for transfer-type subclasses
 *     (`expenditureDepositAssetSubClasses`).
 *   • The amount field's label follows the chosen payment method.
 *
 * Client checks are UX mirrors; the controller re-validates every field and
 * class combination on save. IIFE-wrapped, no `import` / `export`.
 */
(function() {
    /** Zero-pads a 1-2 digit date/time component to 2 digits (e.g. `5` -> `"05"`). */
    function pad2(n: number): string {
        return n.toString().padStart(2, "0");
    }

    // Cached references: tab containers, grid search box, the deposit-asset
    // wrapper divs, every field of both modals, the amount labels, and the
    // anti-forgery input.
    const $createExpenditureDate = $("#createExpenditureDate");
    const $createExpenditureTabs = $("#createExpenditureTabs");
    const $editExpenditureTabs = $("#editExpenditureTabs");
    const $editExpenditureDate = $("#editExpenditureDate");
    const $gridSearch = $("#gridSearch");
    const $createExpenditureSubClass = $("#createExpenditureSubClass");
    const $editExpenditureSubClass = $("#editExpenditureSubClass");
    const $divCreateExpenditureMyDepositAsset = $("#divCreateExpenditureMyDepositAsset");
    const $createExpenditureMainClass = $("#createExpenditureMainClass");
    const $formCreateExpenditure = $("#formCreateExpenditure");
    const $createExpenditureContent = $("#createExpenditureContent");
    const $createExpenditureAmount = $("#createExpenditureAmount");
    const $createExpenditureHour = $("#createExpenditureHour");
    const $createExpenditureMinute = $("#createExpenditureMinute");
    const $createExpenditureSecond = $("#createExpenditureSecond");
    const $createExpenditurePaymentMethod = $("#createExpenditurePaymentMethod");
    const $createExpenditureNote = $("#createExpenditureNote");
    const $createExpenditureMyDepositAsset = $("#createExpenditureMyDepositAsset");
    const $__RequestVerificationToken = $("input[name=\"__RequestVerificationToken\"]");
    const failedToLoadAmountLabelMessage = $("#localizerFailedToLoadAmountLabel").val() as string;
    const $createExpenditureDialogModal = $("#createExpenditureDialogModal");
    const $editExpenditureId = $("#editExpenditureId");
    const $editExpenditureMainClass = $("#editExpenditureMainClass");
    const $divEditExpenditureMyDepositAsset = $("#divEditExpenditureMyDepositAsset");
    const $editExpenditureContent = $("#editExpenditureContent");
    const $editExpenditureAmount = $("#editExpenditureAmount");
    const $editExpenditureHour = $("#editExpenditureHour");
    const $editExpenditureMinute = $("#editExpenditureMinute");
    const $editExpenditureSecond = $("#editExpenditureSecond");
    const $editExpenditurePaymentMethod = $("#editExpenditurePaymentMethod");
    const $editExpenditureNote = $("#editExpenditureNote");
    const $editExpenditureMyDepositAsset = $("#editExpenditureMyDepositAsset");
    const $editExpenditureDialogModal = $("#editExpenditureDialogModal");
    const $formEditExpenditure = $("#formEditExpenditure");
    const $confirmDeleteExpenditureDialogModal = $("#confirmDeleteExpenditureDialogModal");
    const $btnDeleteExpenditure = $("#btnDeleteExpenditure");
    const $btnExportExcelExpenditure = $("#btnExportExcelExpenditure");
    const $btnConfirmDeleteExpenditure = $("#btnConfirmDeleteExpenditure");
    const $btnEditExpenditureGridRow = $("#btnEditExpenditureGridRow");
    const $labelCreateExpenditureAmount = $("#labelCreateExpenditureAmount");
    const $labelEditExpenditureAmount = $("#labelEditExpenditureAmount");

    // Server-published expenditure taxonomy (Expenditure.cshtml). Authoritative in
    // ExpenditureClassPolicy; mirrored here for form UX only, the server re-validates on save.
    const expenditureSubClassMap = JSON.parse(($("#expenditureSubClassMap").val() as string) || "{}");
    const expenditureDepositAssetSubClasses = JSON.parse(($("#expenditureDepositAssetSubClasses").val() as string) || "[]");

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

    // The "deposit asset" field is only relevant for transfer-type subclasses.
    function ToggleCreateExpenditureMyDepositAsset(subClassValue: string) {
        expenditureDepositAssetSubClasses.indexOf(subClassValue) !== -1
            ? $divCreateExpenditureMyDepositAsset.show()
            : $divCreateExpenditureMyDepositAsset.hide();
    }

    /** Same as `ToggleCreateExpenditureMyDepositAsset` for the edit modal. */
    function ToggleEditExpenditureMyDepositAsset(subClassValue: string) {
        expenditureDepositAssetSubClasses.indexOf(subClassValue) !== -1
            ? $divEditExpenditureMyDepositAsset.show()
            : $divEditExpenditureMyDepositAsset.hide();
    }

    // Localized month / day names for the datepicker, from hidden inputs the view
    // rendered from the resource files. Typed `any` so the ~40 `.val()` unions
    // don't each need a cast. `PrevText` / `NextText` are read PascalCase to
    // match the object keys.
    const localizer: any = {
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
    };

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
        $createExpenditureTabs.tabs();
        $editExpenditureTabs.tabs();
        // The empty `setTimeout(fn, 1)` callbacks are a jQuery-UI datepicker
        // nudge (defer a repaint so the button panel lays out). Cast `as any`
        // because `@types/jqueryui` insists these return `DatepickerOptions`.
        $createExpenditureDate.datepicker({
            showButtonPanel: true,
            beforeShow: function() {
                setTimeout(function() {

                }, 1);
            },
            onChangeMonthYear: function() {
                setTimeout(function() {

                }, 1);
            }
        } as any);

        $editExpenditureDate.datepicker({
            showButtonPanel: true,
            beforeShow: function() {
                setTimeout(function() {

                }, 1);
            },
            onChangeMonthYear: function() {
                setTimeout(function() {

                }, 1);
            }
        } as any);

        // Hide the datepicker's built-in "Close" / "Today" buttons via injected CSS.
        $("<style> .ui-datepicker-close { display: none; } </style>").appendTo("head");
        $("<style> .ui-datepicker-current { display: none; } </style>").appendTo("head");
    });

    // Bootstrap table class that marks the currently selected grid row.
    let selectedRowColor = "table-primary";

    // MvcGrid `rowclick`: move the highlight to the row whose `data-id` matches.
    $(document).off("rowclick.Expenditure").on("rowclick.Expenditure", (e: any) => {
        let selectedRow = e.detail;
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
    $(document).off("reloadstart.Expenditure").on("reloadstart.Expenditure", _ => {
    });

    $(document).off("reloadend.Expenditure").on("reloadend.Expenditure", _ => {
    });

    $(document).off("reloadfail.Expenditure").on("reloadfail.Expenditure", _ => {
    });

    $(document).off("gridconfigure.Expenditure").on("gridconfigure.Expenditure", _ => {
    });

    // Double-click the highlighted row to open its edit modal.
    $(document).off("dblclick.Expenditure", ".clsGridRow").on("dblclick.Expenditure", ".clsGridRow", function() {
        $(".clsGridRow").each(function() {
            if ($(this).attr("class")!.includes(selectedRowColor)) {
                EditExpenditureGridRow();
            }
        });
    });
    // Free-text search: push the term into the grid query string and reload.
    $gridSearch.off("input").on("input", function(event) {
        const grid = new MvcGrid(document.querySelector(".mvc-grid"));
        grid.url.searchParams.set("wholeSearch", (event.currentTarget as HTMLInputElement).value);
        grid.reload();
    });

    /** Create form main-class change: re-filter the subclass options and re-evaluate the deposit-asset block. */
    function CreateFormShowExpenditureSubClassBySelectedExpenditureMainClass(createExpenditureMainClass: HTMLSelectElement) {
        ApplyAllowedOptions($createExpenditureSubClass, expenditureSubClassMap[createExpenditureMainClass.value] || [], null);
        ToggleCreateExpenditureMyDepositAsset($createExpenditureSubClass.val() as string);
    }

    /** Create form subclass change: show/hide the deposit-asset block for it. */
    function CreateFormShowExpenditureDivCreateExpenditureMyDepositAssetBySelectedExpenditureSubClass(createExpenditureSubClass: HTMLSelectElement) {
        ToggleCreateExpenditureMyDepositAsset(createExpenditureSubClass.value);
    }

    /**
     * Create-expenditure modal submit: validate, assemble the `Created`
     * timestamp from the separate date / time fields (`month - 1` for 0-indexed
     * JS months, `.toISOString()` local -> UTC), POST as JSON, then close /
     * reload / toast.
     */
    function CreateExpenditure() {

        if (!$formCreateExpenditure.valid()) {
            return false;
        }

        let mainClass = $createExpenditureMainClass.val();
        let subClass = $createExpenditureSubClass.val();
        let content = $createExpenditureContent.val();
        let amount = $createExpenditureAmount.val();

        let year = parseInt(($createExpenditureDate.val() as string).substring(0, 4));
        let month = parseInt(($createExpenditureDate.val() as string).substring(5, 7));
        let day = parseInt(($createExpenditureDate.val() as string).substring(8, 10));
        let hour = parseInt($createExpenditureHour.val() as string);
        let minute = parseInt($createExpenditureMinute.val() as string);
        let second = parseInt($createExpenditureSecond.val() as string);

        // Sent as the account's own local wall-clock time ("yyyy-MM-dd HH:mm:ss"), not converted
        // to UTC here: the server already knows the account's IANA time zone from the session and
        // does that conversion itself, so the browser's own (possibly different) time zone is
        // never involved.
        let created = `${year}-${pad2(month)}-${pad2(day)} ${pad2(hour)}:${pad2(minute)}:${pad2(second)}`;

        let paymentMethod = $createExpenditurePaymentMethod.val();
        let note = $createExpenditureNote.val();
        let myDepositAsset = $createExpenditureMyDepositAsset.val();

        let paramValue = JSON.stringify({
            MainClass: mainClass,
            SubClass: subClass,
            Content: content,
            Amount: amount,
            Created: created,
            PaymentMethod: paymentMethod,
            Note: note,
            MyDepositAsset: myDepositAsset
        });

        $.ajax({
            url: "/AccountBook/CreateExpenditure",
            type: "POST",
            headers: { "RequestVerificationToken": $__RequestVerificationToken.val() as string },
            dataType: "json",
            data: paramValue,
            contentType: "application/json; charset=utf-8",
            success: function(data) {
                if (data.result) {
                    $createExpenditureDialogModal.modal("hide");

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
     * (`IsExpenditureExists`), fills the form, re-filters the subclass select
     * and toggles the deposit-asset block. When the record has no explicit
     * `myDepositAsset` the payment method is used as the fallback selection.
     *
     * @param errorMessageSelectGridRow localized "pick a row first" text from a `data-*` attribute.
     */
    function EditExpenditureGridRow(errorMessageSelectGridRow?: string) {

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
            url: "/AccountBook/IsExpenditureExists" + "?id=" + selectedRowId,
            type: "POST",
            headers: { "RequestVerificationToken": $__RequestVerificationToken.val() as string },
            dataType: "json",
            data: null as any,
            contentType: "application/json; charset=utf-8",
            success: function(data) {
                if (data.result) {

                    $editExpenditureId.val(data.expenditure.id);
                    $editExpenditureMainClass.val(data.expenditure.mainClass).trigger("change");

                    ApplyAllowedOptions($editExpenditureSubClass, expenditureSubClassMap[data.expenditure.mainClass] || [], data.expenditure.subClass);
                    ToggleEditExpenditureMyDepositAsset(data.expenditure.subClass);

                    $editExpenditureContent.val(data.expenditure.content);
                    $editExpenditureAmount.val(data.expenditure.amount);
                    // Split the `created` ISO string into the date field and the
                    // hour / minute / second selects.
                    $editExpenditureDate.val(data.expenditure.created.split("T")[0]);
                    $editExpenditureHour.val(parseInt(data.expenditure.created.split("T")[1].substring(0, 2))).trigger("change");
                    $editExpenditureMinute.val(parseInt(data.expenditure.created.split("T")[1].substring(3, 5))).trigger("change");
                    $editExpenditureSecond.val(parseInt(data.expenditure.created.split("T")[1].substring(6, 8))).trigger("change");
                    $editExpenditurePaymentMethod.val(data.expenditure.paymentMethod).trigger("change");
                    $editExpenditureNote.val(data.expenditure.note);

                    if (data.expenditure.myDepositAsset) {
                        $editExpenditureMyDepositAsset.val(data.expenditure.myDepositAsset).trigger("change");
                    } else {
                        $editExpenditureMyDepositAsset.val(data.expenditure.paymentMethod).trigger("change");
                    }

                    $editExpenditureDialogModal.modal({
                        keyboard: false,
                        backdrop: "static"
                    });

                    $editExpenditureDialogModal.modal("toggle");
                    $editExpenditureDialogModal.modal("show");
                } else {
                    toastr.error(data.error);
                }
            }
        });
    }

    /** Edit form main-class change: re-filter the subclass options and re-evaluate the deposit-asset block. */
    function EditFormShowExpenditureSubClassBySelectedExpenditureMainClass(editExpenditureMainClass: HTMLSelectElement) {
        ApplyAllowedOptions($editExpenditureSubClass, expenditureSubClassMap[editExpenditureMainClass.value] || [], null);
        ToggleEditExpenditureMyDepositAsset($editExpenditureSubClass.val() as string);
    }

    /** Edit form subclass change: show/hide the deposit-asset block for it. */
    function EditFormShowExpenditureDivCreateExpenditureMyDepositAssetBySelectedExpenditureSubClass(editExpenditureSubClass: HTMLSelectElement) {
        ToggleEditExpenditureMyDepositAsset(editExpenditureSubClass.value);
    }

    /** Edit-expenditure modal submit: same shape as `CreateExpenditure` plus `ID`. */
    function UpdateExpenditure() {

        if (!$formEditExpenditure.valid()) {
            return false;
        }

        let id = $editExpenditureId.val();
        let mainClass = $editExpenditureMainClass.val();
        let subClass = $editExpenditureSubClass.val();
        let content = $editExpenditureContent.val();
        let amount = $editExpenditureAmount.val();

        let year = parseInt(($editExpenditureDate.val() as string).substring(0, 4));
        let month = parseInt(($editExpenditureDate.val() as string).substring(5, 7));
        let day = parseInt(($editExpenditureDate.val() as string).substring(8, 10));
        let hour = parseInt($editExpenditureHour.val() as string);
        let minute = parseInt($editExpenditureMinute.val() as string);
        let second = parseInt($editExpenditureSecond.val() as string);

        // See CreateExpenditure — local wall-clock string, no client-side UTC conversion.
        let created = `${year}-${pad2(month)}-${pad2(day)} ${pad2(hour)}:${pad2(minute)}:${pad2(second)}`;
        let paymentMethod = $editExpenditurePaymentMethod.val();
        let note = $editExpenditureNote.val();
        let myDepositAsset = $editExpenditureMyDepositAsset.val();

        let paramValue = JSON.stringify({
            Id: id,
            MainClass: mainClass,
            SubClass: subClass,
            Content: content,
            Amount: amount,
            Created: created,
            PaymentMethod: paymentMethod,
            Note: note,
            MyDepositAsset: myDepositAsset
        });

        $.ajax({
            url: "/AccountBook/UpdateExpenditure",
            type: "POST",
            headers: { "RequestVerificationToken": $__RequestVerificationToken.val() as string },
            dataType: "json",
            data: paramValue,
            contentType: "application/json; charset=utf-8",
            success: function(data) {
                if (data.result) {
                    $editExpenditureDialogModal.modal("hide");

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
    function ConfirmDeleteExpenditure(errorMessageSelectGridRow?: string) {

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

        $confirmDeleteExpenditureDialogModal.modal({
            keyboard: false,
            backdrop: "static"
        });

        $confirmDeleteExpenditureDialogModal.modal("toggle");
        $confirmDeleteExpenditureDialogModal.modal("show");
    }

    /** Confirmed delete: re-check selection, confirm the record exists, then POST `DeleteExpenditure`. */
    function DeleteExpenditure(errorMessageSelectGridRow?: string) {

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
            url: "/AccountBook/IsExpenditureExists" + "?id=" + selectedRowId,
            type: "POST",
            headers: { "RequestVerificationToken": $__RequestVerificationToken.val() as string },
            dataType: "json",
            data: null as any,
            contentType: "application/json; charset=utf-8",
            success: function(data) {
                if (data.result) {

                    let paramValue = JSON.stringify({
                        Id: data.expenditure.id
                    });

                    $.ajax({
                        url: "/AccountBook/DeleteExpenditure",
                        type: "POST",
                        headers: { "RequestVerificationToken": $__RequestVerificationToken.val() as string },
                        dataType: "json",
                        data: paramValue,
                        contentType: "application/json; charset=utf-8",
                        success: function(data) {
                            if (data.result) {
                                $confirmDeleteExpenditureDialogModal.modal("hide");

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
    function ExportExcelExpenditure() {
        let form = document.createElement("form");
        let element1 = document.createElement("input");
        let element2 = document.createElement("input");

        form.method = "POST";
        form.action = "/AccountBook/ExportExcelExpenditure";

        element1.name = "__RequestVerificationToken";
        element1.value = $__RequestVerificationToken.val() as string;
        form.appendChild(element1);

        element2.name = "fileName";
        element2.value = "Expenditure";
        form.appendChild(element2);

        document.body.appendChild(form);

        form.submit();
    }

    /**
     * Updates the create-form amount label to match the chosen payment method
     * (e.g. its currency). Both `if` branches set the same thing, so a failure
     * just shows whatever fallback label the server sent.
     */
    function ChangeCreateExpenditureAmountLabel(productName: string) {
        $.ajax({
            url: "/AccountBook/GetExpenditureAmountLabel" + "?productName=" + encodeURIComponent(productName),
            type: "POST",
            headers: { "RequestVerificationToken": $__RequestVerificationToken.val() as string },
            dataType: "json",
            data: null as any,
            contentType: "application/json; charset=utf-8",
            success: function(data) {
                if (data.result) {
                    $labelCreateExpenditureAmount.text(data.label);
                } else {
                    $labelCreateExpenditureAmount.text(data.label);
                }
            },
            error: function() {
                toastr.error(failedToLoadAmountLabelMessage);
            }
        });
    }

    /** Same as `ChangeCreateExpenditureAmountLabel` for the edit form. */
    function ChangeEditExpenditureAmountLabel(productName: string) {
        $.ajax({
            url: "/AccountBook/GetExpenditureAmountLabel" + "?productName=" + encodeURIComponent(productName),
            type: "POST",
            headers: { "RequestVerificationToken": $__RequestVerificationToken.val() as string },
            dataType: "json",
            data: null as any,
            contentType: "application/json; charset=utf-8",
            success: function(data) {
                if (data.result) {
                    $labelEditExpenditureAmount.text(data.label);
                } else {
                    $labelEditExpenditureAmount.text(data.label);
                }
            },
            error: function() {
                toastr.error(failedToLoadAmountLabelMessage);
            }
        });
    }

    // --- Button / form / select wiring --------------------------------
    $btnEditExpenditureGridRow.off("click").on("click", function() {
        EditExpenditureGridRow($(this).attr("data-errorMessageSelectGridRow"));
    });

    $btnConfirmDeleteExpenditure.off("click").on("click", function() {
        ConfirmDeleteExpenditure($(this).attr("data-errorMessageSelectGridRow"));
    });

    $btnExportExcelExpenditure.off("click").on("click", function() {
        ExportExcelExpenditure();
    });

    $btnDeleteExpenditure.off("click").on("click", function() {
        DeleteExpenditure($(this).attr("data-errorMessageSelectGridRow"));
    });

    $formCreateExpenditure.off("submit").on("submit", function() {
        return CreateExpenditure();
    });

    $formEditExpenditure.off("submit").on("submit", function() {
        return UpdateExpenditure();
    });

    $createExpenditureMainClass.off("change").on("change", function(event) {
        CreateFormShowExpenditureSubClassBySelectedExpenditureMainClass(
            event.currentTarget as HTMLSelectElement
        );
    });

    $createExpenditureSubClass.off("change").on("change", function(event) {
        CreateFormShowExpenditureDivCreateExpenditureMyDepositAssetBySelectedExpenditureSubClass(
            event.currentTarget as HTMLSelectElement
        );
    });

    $editExpenditureMainClass.off("change").on("change", function(event) {
        EditFormShowExpenditureSubClassBySelectedExpenditureMainClass(
            event.currentTarget as HTMLSelectElement
        );
    });

    $editExpenditureSubClass.off("change").on("change", function(event) {
        EditFormShowExpenditureDivCreateExpenditureMyDepositAssetBySelectedExpenditureSubClass(
            event.currentTarget as HTMLSelectElement
        );
    });

    $createExpenditurePaymentMethod.off("change").on("change", function(event) {
        ChangeCreateExpenditureAmountLabel(
            (event.currentTarget as HTMLSelectElement).value
        );
    });

    $editExpenditurePaymentMethod.off("change").on("change", function(event) {
        ChangeEditExpenditureAmountLabel(
            (event.currentTarget as HTMLSelectElement).value
        );
    });

    // On load, set the create-form amount label for whichever method is preselected.
    $(function() {
        ChangeCreateExpenditureAmountLabel($createExpenditurePaymentMethod.find("option:selected").val() as string);
    });
})();
