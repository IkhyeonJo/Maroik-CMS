/**
 * Script for the **Fixed Income** page (`Views/Notice/FixedIncome.cshtml`) —
 * recurring/scheduled income entries. A NonFactors MVC.Grid list with create /
 * edit / delete modals and Excel export, following the standard grid-CRUD
 * pattern (see `AccountBook/Asset`) plus:
 *
 *   • A localized jQuery-UI datepicker on the "maturity date" fields, with two
 *     extra buttons injected into its button panel: "No maturity date" (sets the
 *     `NO_MATURITY_DATE` sentinel) and "Today".
 *   • Dependent `<select>`s: main class -> allowed subclasses
 *     (`IncomeClassPolicy`); deposit month -> valid deposit days 1..maxDay
 *     (`FixedSchedulePolicy`, via `maxDepositDayByMonth`).
 *   • The amount field's label follows the chosen deposit asset.
 *
 * Client checks are UX mirrors; the controller re-validates on save.
 * IIFE-wrapped, no `import` / `export`.
 */
(function() {
    // Cached references: tab containers, grid search box, the two maturity-date
    // inputs, every field of both modals, the amount labels, and the
    // anti-forgery input. (`$btnEditFixedIncomeGridRow` is this page's "edit" button.)
    const $createFixedIncomeTabs = $("#createFixedIncomeTabs");
    const $editFixedIncomeTabs = $("#editFixedIncomeTabs");
    const $createFixedIncomeMaturityDate = $("#createFixedIncomeMaturityDate");
    const $editFixedIncomeMaturityDate = $("#editFixedIncomeMaturityDate");
    const $gridSearch = $("#gridSearch");
    const $createFixedIncomeSubClass = $("#createFixedIncomeSubClass");
    const $createFixedIncomeDepositDay = $("#createFixedIncomeDepositDay");
    const $editFixedIncomeSubClass = $("#editFixedIncomeSubClass");
    const $editFixedIncomeDepositDay = $("#editFixedIncomeDepositDay");
    const $formCreateFixedIncome = $("#formCreateFixedIncome");
    const $createFixedIncomeMainClass = $("#createFixedIncomeMainClass");
    const $createFixedIncomeContent = $("#createFixedIncomeContent");
    const $createFixedIncomeAmount = $("#createFixedIncomeAmount");
    const $createFixedIncomeDepositMonth = $("#createFixedIncomeDepositMonth");
    const $createFixedIncomeNote = $("#createFixedIncomeNote");
    const $createFixedIncomeDepositMyAssetProductName = $("#createFixedIncomeDepositMyAssetProductName");
    const $__RequestVerificationToken = $("input[name=\"__RequestVerificationToken\"]");
    // Localized toast shown when the amount-label request itself fails (transport error).
    const failedToLoadAmountLabelMessage = $("#localizerFailedToLoadAmountLabel").val() as string;
    const $createFixedIncomeDialogModal = $("#createFixedIncomeDialogModal");
    const $editFixedIncomeId = $("#editFixedIncomeId");
    const $editFixedIncomeMainClass = $("#editFixedIncomeMainClass");
    const $editFixedIncomeContent = $("#editFixedIncomeContent");
    const $editFixedIncomeAmount = $("#editFixedIncomeAmount");
    const $editFixedIncomeDepositMonth = $("#editFixedIncomeDepositMonth");
    const $editFixedIncomeNote = $("#editFixedIncomeNote");
    const $editFixedIncomeDepositMyAssetProductName = $("#editFixedIncomeDepositMyAssetProductName");
    const $editFixedIncomeUnpunctuality = $("#editFixedIncomeUnpunctuality");
    const $editFixedIncomeDialogModal = $("#editFixedIncomeDialogModal");
    const $formEditFixedIncome = $("#formEditFixedIncome");
    const $confirmDeleteFixedIncomeDialogModal = $("#confirmDeleteFixedIncomeDialogModal");
    const $labelCreateFixedIncomeAmount = $("#labelCreateFixedIncomeAmount");
    const $labelEditFixedIncomeAmount = $("#labelEditFixedIncomeAmount");
    const $btnEditFixedIncomeGridRow = $("#btnEditFixedIncomeGridRow");
    const $btnConfirmDeleteFixedIncome = $("#btnConfirmDeleteFixedIncome");
    const $btnExportExcelFixedIncome = $("#btnExportExcelFixedIncome");
    const $btnDeleteFixedIncome = $("#btnDeleteFixedIncome");

    // Server-published rules (FixedIncome.cshtml). Authoritative in IncomeClassPolicy /
    // FixedSchedulePolicy; mirrored here for form UX only, the server re-validates on save.
    const incomeSubClassMap = JSON.parse(($("#incomeSubClassMap").val() as string) || "{}");
    const maxDepositDayByMonth = JSON.parse(($("#maxDepositDayByMonth").val() as string) || "{}");
    // Far-future stand-in for "no maturity date", published by the server (FixedSchedulePolicy)
    // as a yyyy-MM-dd string so the client and server never drift on the value.
    const noMaturityIso = $("#noMaturityDate").val() as string;
    const [nmYear, nmMonth, nmDay] = noMaturityIso.split("-").map(Number);
    const NO_MATURITY_DATE = new Date(nmYear, nmMonth - 1, nmDay);

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

    // The valid deposit-day option values ("1".."maxDay") for the given deposit month.
    function DepositDayValues(monthValue: string) {
        const maxDay = parseInt(maxDepositDayByMonth[String(monthValue)], 10);
        const values = [];
        for (let day = 1; day <= maxDay; day++) {
            values.push(String(day));
        }
        return values;
    }

    // Localized month / day names for the datepicker plus the two extra button captions and the
    // maturity-date error message (asserted `Record<string, string>`: each holds a string, which
    // `.val()` types as a wider union); `PrevText` / `NextText` are read PascalCase to match the
    // keys.
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
        YearSuffix: $("#localizerYearSuffix").val(),
        NoMaturityDate: $("#localizerNoMaturityDate").val(),
        Today: $("#localizerToday").val(),
        MaturityDateError: $("#localizerMaturityDateError").val(),
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
        $createFixedIncomeTabs.tabs();
        $editFixedIncomeTabs.tabs();
        // Maturity-date pickers. `minDate: 0` forbids past dates. `beforeShow`
        // and `onChangeMonthYear` (deferred a tick with `setTimeout` so the
        // widget exists) append two custom buttons to the button panel:
        // "No maturity date" -> sets the far-future `NO_MATURITY_DATE` sentinel,
        // and "Today". `_clearDate` is an undocumented jQuery-UI internal (typed in
        // global.d.ts) that wipes the field first.
        $createFixedIncomeMaturityDate.datepicker({
            showButtonPanel: true,
            minDate: 0,
            beforeShow: function(input: Element) {
                setTimeout(function() {
                    let buttonPane = $(input)
                        .datepicker("widget")
                        .find(".ui-datepicker-buttonpane");

                    $("<button>", {
                        text: localizer.NoMaturityDate,
                        click: function() {
                            $.datepicker._clearDate(input);
                            $createFixedIncomeMaturityDate.datepicker("setDate", NO_MATURITY_DATE);
                        }
                    }).appendTo(buttonPane).addClass("ui-datepicker-clear ui-state-default ui-priority-primary ui-corner-all");

                    $("<button>", {
                        text: localizer.Today,
                        click: function() {
                            $.datepicker._clearDate(input);
                            $createFixedIncomeMaturityDate.datepicker("setDate", new Date());
                        }
                    }).appendTo(buttonPane).addClass("ui-datepicker-clear ui-state-default ui-priority-primary ui-corner-all");

                }, 1);
            },
            onChangeMonthYear: function(_year: number, _month: number, instance: DatepickerInstance) {
                setTimeout(function() {
                    let buttonPane = $(instance)
                        .datepicker("widget")
                        .find(".ui-datepicker-buttonpane");

                    $("<button>", {
                        text: localizer.NoMaturityDate,
                        click: function() {
                            $.datepicker._clearDate(instance.input);
                            $createFixedIncomeMaturityDate.datepicker("setDate", NO_MATURITY_DATE);
                        }
                    }).appendTo(buttonPane).addClass("ui-datepicker-clear ui-state-default ui-priority-primary ui-corner-all");

                    $("<button>", {
                        text: localizer.Today,
                        click: function() {
                            $.datepicker._clearDate(instance.input);
                            $createFixedIncomeMaturityDate.datepicker("setDate", new Date());
                        }
                    }).appendTo(buttonPane).addClass("ui-datepicker-clear ui-state-default ui-priority-primary ui-corner-all");

                }, 1);
            }
        });

        $editFixedIncomeMaturityDate.datepicker({
            showButtonPanel: true,
            minDate: 0,
            beforeShow: function(input: Element) {
                setTimeout(function() {
                    let buttonPane = $(input)
                        .datepicker("widget")
                        .find(".ui-datepicker-buttonpane");

                    $("<button>", {
                        text: localizer.NoMaturityDate,
                        click: function() {
                            $.datepicker._clearDate(input);
                            $editFixedIncomeMaturityDate.datepicker("setDate", NO_MATURITY_DATE);
                        }
                    }).appendTo(buttonPane).addClass("ui-datepicker-clear ui-state-default ui-priority-primary ui-corner-all");

                    $("<button>", {
                        text: localizer.Today,
                        click: function() {
                            $.datepicker._clearDate(input);
                            $editFixedIncomeMaturityDate.datepicker("setDate", new Date());
                        }
                    }).appendTo(buttonPane).addClass("ui-datepicker-clear ui-state-default ui-priority-primary ui-corner-all");

                }, 1);
            },
            onChangeMonthYear: function(_year: number, _month: number, instance: DatepickerInstance) {
                setTimeout(function() {
                    let buttonPane = $(instance)
                        .datepicker("widget")
                        .find(".ui-datepicker-buttonpane");

                    $("<button>", {
                        text: localizer.NoMaturityDate,
                        click: function() {
                            $.datepicker._clearDate(instance.input);
                            $editFixedIncomeMaturityDate.datepicker("setDate", NO_MATURITY_DATE);
                        }
                    }).appendTo(buttonPane).addClass("ui-datepicker-clear ui-state-default ui-priority-primary ui-corner-all");

                    $("<button>", {
                        text: localizer.Today,
                        click: function() {
                            $.datepicker._clearDate(instance.input);
                            $editFixedIncomeMaturityDate.datepicker("setDate", new Date());
                        }
                    }).appendTo(buttonPane).addClass("ui-datepicker-clear ui-state-default ui-priority-primary ui-corner-all");

                }, 1);
            }
        });

        // Default both pickers to today.
        $createFixedIncomeMaturityDate.datepicker("setDate", new Date());
        $editFixedIncomeMaturityDate.datepicker("setDate", new Date());

        // Hide the date picker's built-in "Close" / "Today" buttons via injected CSS.
        $("<style> .ui-datepicker-close { display: none; } </style>").appendTo("head");
        $("<style> .ui-datepicker-current { display: none; } </style>").appendTo("head");
    });

    // Bootstrap table class that marks the currently selected grid row.
    let selectedRowColor = "table-primary";

    // MvcGrid `rowclick`: move the highlight to the row whose `data-id` matches.
    $(document).off("rowclick.FixedIncome").on("rowclick.FixedIncome", (e: JQuery.TriggeredEvent) => {
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
    $(document).off("reloadstart.FixedIncome").on("reloadstart.FixedIncome", _ => {
    });

    $(document).off("reloadend.FixedIncome").on("reloadend.FixedIncome", _ => {
    });

    $(document).off("reloadfail.FixedIncome").on("reloadfail.FixedIncome", _ => {
    });

    $(document).off("gridconfigure.FixedIncome").on("gridconfigure.FixedIncome", _ => {
    });

    // Double-click the highlighted row to open its edit modal.
    $(document).off("dblclick.FixedIncome", ".clsGridRow").on("dblclick.FixedIncome", ".clsGridRow", function() {
        $(".clsGridRow").each(function() {
            if ($(this).attr("class")!.includes(selectedRowColor)) {
                EditFixedIncomeGridRow();
            }
        });
    });

    // Free-text search: push the term into the grid query string and reload.
    $gridSearch.off("input").on("input", function(event) {
        const grid = new MvcGrid(document.querySelector(".mvc-grid"));
        grid.url.searchParams.set("wholeSearch", (event.currentTarget as HTMLInputElement).value);
        grid.reload();
    });

    /** Create form main-class change: narrow the subclass options to those allowed for it. */
    function CreateFormShowFixedIncomeSubClassBySelectedFixedIncomeMainClass(createFixedIncomeMainClass: HTMLSelectElement) {
        ApplyAllowedOptions($createFixedIncomeSubClass, incomeSubClassMap[createFixedIncomeMainClass.value] || [], null);
    }

    /** Create form deposit-month change: re-fill the deposit-day options 1..maxDay for that month. */
    function CreateFormShowFixedIncomeDepositDayBySelectedFixedIncomeDepositMonth(createFixedIncomeDepositMonth: HTMLSelectElement) {
        ApplyAllowedOptions($createFixedIncomeDepositDay, DepositDayValues(createFixedIncomeDepositMonth.value), null);
    }

    /** Edit form main-class change: same as the create-form version. */
    function EditFormShowFixedIncomeSubClassBySelectedFixedIncomeMainClass(editFixedIncomeMainClass: HTMLSelectElement) {
        ApplyAllowedOptions($editFixedIncomeSubClass, incomeSubClassMap[editFixedIncomeMainClass.value] || [], null);
    }

    /** Edit form deposit-month change: same as the create-form version. */
    function EditFormShowFixedIncomeDepositDayBySelectedFixedIncomeDepositMonth(editFixedIncomeDepositMonth: HTMLSelectElement) {
        ApplyAllowedOptions($editFixedIncomeDepositDay, DepositDayValues(editFixedIncomeDepositMonth.value), null);
    }

    /** Create-fixed-income modal submit: validate, POST the fields as JSON, then close / reload / toast. */
    function CreateFixedIncome() {

        if (!$formCreateFixedIncome.valid()) {
            return false;
        }

        let mainClass = $createFixedIncomeMainClass.val();
        let subClass = $createFixedIncomeSubClass.val();
        let content = $createFixedIncomeContent.val();
        let amount = $createFixedIncomeAmount.val();
        let depositMonth = $createFixedIncomeDepositMonth.val();
        let depositDay = $createFixedIncomeDepositDay.val();
        let maturityDate = $createFixedIncomeMaturityDate.val();
        let note = $createFixedIncomeNote.val();
        let depositMyAssetProductName = $createFixedIncomeDepositMyAssetProductName.val();

        let paramValue = JSON.stringify({
            MainClass: mainClass,
            SubClass: subClass,
            Content: content,
            Amount: amount,
            DepositMonth: depositMonth,
            DepositDay: depositDay,
            MaturityDate: maturityDate,
            Note: note,
            DepositMyAssetProductName: depositMyAssetProductName
        });

        $.ajax({
            url: "/Notice/CreateFixedIncome",
            type: "POST",
            headers: { "RequestVerificationToken": $__RequestVerificationToken.val() as string },
            dataType: "json",
            data: paramValue,
            contentType: "application/json; charset=utf-8",
            success: function(data) {
                if (data.result) {
                    $createFixedIncomeDialogModal.modal("hide");

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
     * (`IsFixedIncomeExists`), fills the form, re-filters the subclass and
     * deposit-day selects for the record's main class / deposit month, and
     * sets the "unpunctuality" (always notify) checkbox from the record.
     *
     * @param errorMessageSelectGridRow localized "pick a row first" text from a `data-*` attribute.
     */
    function EditFixedIncomeGridRow(errorMessageSelectGridRow?: string) {

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
            url: "/Notice/IsFixedIncomeExists" + "?id=" + selectedRowId,
            type: "POST",
            headers: { "RequestVerificationToken": $__RequestVerificationToken.val() as string },
            dataType: "json",
            contentType: "application/json; charset=utf-8",
            success: function(data) {
                if (data.result) {

                    $editFixedIncomeId.val(data.fixedIncome.id);
                    $editFixedIncomeMainClass.val(data.fixedIncome.mainClass).trigger("change");

                    ApplyAllowedOptions($editFixedIncomeSubClass, incomeSubClassMap[data.fixedIncome.mainClass] || [], data.fixedIncome.subClass);

                    $editFixedIncomeContent.val(data.fixedIncome.content);
                    $editFixedIncomeAmount.val(data.fixedIncome.amount);

                    $editFixedIncomeDepositMonth.val(data.fixedIncome.depositMonth.toString()).trigger("change");

                    ApplyAllowedOptions($editFixedIncomeDepositDay, DepositDayValues(data.fixedIncome.depositMonth.toString()), data.fixedIncome.depositDay.toString());

                    $editFixedIncomeMaturityDate.val(data.fixedIncome.maturityDate);
                    $editFixedIncomeNote.val(data.fixedIncome.note);
                    $editFixedIncomeDepositMyAssetProductName.val(data.fixedIncome.depositMyAssetProductName).trigger("change");
                    $editFixedIncomeUnpunctuality.prop("checked", data.fixedIncome.unpunctuality);

                    $editFixedIncomeDialogModal.modal({
                        keyboard: false,
                        backdrop: "static"
                    });

                    $editFixedIncomeDialogModal.modal("toggle");
                    $editFixedIncomeDialogModal.modal("show");
                } else {
                    toastr.error(data.error);
                }
            }
        });
    }

    /** Edit-fixed-income modal submit: same shape as `CreateFixedIncome` plus `ID` and `Unpunctuality`. */
    function UpdateFixedIncome() {

        if (!$formEditFixedIncome.valid()) {
            return false;
        }

        let id = $editFixedIncomeId.val();
        let mainClass = $editFixedIncomeMainClass.val();
        let subClass = $editFixedIncomeSubClass.val();
        let content = $editFixedIncomeContent.val();
        let amount = $editFixedIncomeAmount.val();
        let depositMonth = $editFixedIncomeDepositMonth.val();
        let depositDay = $editFixedIncomeDepositDay.val();
        let maturityDate = $editFixedIncomeMaturityDate.val();
        let note = $editFixedIncomeNote.val();
        let depositMyAssetProductName = $editFixedIncomeDepositMyAssetProductName.val();
        let unpunctuality = $editFixedIncomeUnpunctuality.is(":checked");

        let paramValue = JSON.stringify({
            Id: id,
            MainClass: mainClass,
            SubClass: subClass,
            Content: content,
            Amount: amount,
            DepositMonth: depositMonth,
            DepositDay: depositDay,
            MaturityDate: maturityDate,
            Note: note,
            DepositMyAssetProductName: depositMyAssetProductName,
            Unpunctuality: unpunctuality
        });

        $.ajax({
            url: "/Notice/UpdateFixedIncome",
            type: "POST",
            headers: { "RequestVerificationToken": $__RequestVerificationToken.val() as string },
            dataType: "json",
            data: paramValue,
            contentType: "application/json; charset=utf-8",
            success: function(data) {
                if (data.result) {
                    $editFixedIncomeDialogModal.modal("hide");

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
    function ConfirmDeleteFixedIncome(errorMessageSelectGridRow?: string) {

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

        $confirmDeleteFixedIncomeDialogModal.modal({
            keyboard: false,
            backdrop: "static"
        });

        $confirmDeleteFixedIncomeDialogModal.modal("toggle");
        $confirmDeleteFixedIncomeDialogModal.modal("show");
    }

    /** Confirmed delete: re-check selection, confirm the record exists, then POST `DeleteFixedIncome`. */
    function DeleteFixedIncome(errorMessageSelectGridRow?: string) {

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
            url: "/Notice/IsFixedIncomeExists" + "?id=" + selectedRowId,
            type: "POST",
            headers: { "RequestVerificationToken": $__RequestVerificationToken.val() as string },
            dataType: "json",
            contentType: "application/json; charset=utf-8",
            success: function(data) {
                if (data.result) {

                    let paramValue = JSON.stringify({
                        Id: data.fixedIncome.id
                    });

                    $.ajax({
                        url: "/Notice/DeleteFixedIncome",
                        type: "POST",
                        headers: { "RequestVerificationToken": $__RequestVerificationToken.val() as string },
                        dataType: "json",
                        data: paramValue,
                        contentType: "application/json; charset=utf-8",
                        success: function(data) {
                            if (data.result) {
                                $confirmDeleteFixedIncomeDialogModal.modal("hide");

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
    function ExportExcelFixedIncome() {
        let form = document.createElement("form");
        let element1 = document.createElement("input");
        let element2 = document.createElement("input");

        form.method = "POST";
        form.action = "/Notice/ExportExcelFixedIncome";

        element1.name = "__RequestVerificationToken";
        element1.value = $__RequestVerificationToken.val() as string;
        form.appendChild(element1);

        element2.name = "fileName";
        element2.value = "FixedIncome";
        form.appendChild(element2);

        document.body.appendChild(form);

        form.submit();
    }

    /**
     * Updates the create-form amount label to match the chosen deposit asset
     * (its currency). Both `if` branches set the same thing.
     */
    function ChangeCreateFixedIncomeAmountLabel(productName: string) {
        $.ajax({
            url: "/Notice/GetFixedIncomeAmountLabel" + "?productName=" + encodeURIComponent(productName),
            type: "POST",
            headers: { "RequestVerificationToken": $__RequestVerificationToken.val() as string },
            dataType: "json",
            contentType: "application/json; charset=utf-8",
            success: function(data) {
                if (data.result) {
                    $labelCreateFixedIncomeAmount.text(data.label);
                } else {
                    $labelCreateFixedIncomeAmount.text(data.label);
                }
            },
            error: function() {
                toastr.error(failedToLoadAmountLabelMessage);
            }
        });
    }

    /** Same as `ChangeCreateFixedIncomeAmountLabel` for the edit form. */
    function ChangeEditFixedIncomeAmountLabel(productName: string) {
        $.ajax({
            url: "/Notice/GetFixedIncomeAmountLabel" + "?productName=" + encodeURIComponent(productName),
            type: "POST",
            headers: { "RequestVerificationToken": $__RequestVerificationToken.val() as string },
            dataType: "json",
            contentType: "application/json; charset=utf-8",
            success: function(data) {
                if (data.result) {
                    $labelEditFixedIncomeAmount.text(data.label);
                } else {
                    $labelEditFixedIncomeAmount.text(data.label);
                }
            },
            error: function() {
                toastr.error(failedToLoadAmountLabelMessage);
            }
        });
    }

    // --- Button / form / select wiring --------------------------------
    $btnEditFixedIncomeGridRow.off("click").on("click", function() {
        EditFixedIncomeGridRow($(this).attr("data-errorMessageSelectGridRow"));
    });

    $btnConfirmDeleteFixedIncome.off("click").on("click", function() {
        ConfirmDeleteFixedIncome($(this).attr("data-errorMessageSelectGridRow"));
    });

    $btnExportExcelFixedIncome.off("click").on("click", function() {
        ExportExcelFixedIncome();
    });

    $btnDeleteFixedIncome.off("click").on("click", function() {
        DeleteFixedIncome($(this).attr("data-errorMessageSelectGridRow"));
    });

    $formCreateFixedIncome.off("submit").on("submit", function() {
        return CreateFixedIncome();
    });

    $formEditFixedIncome.off("submit").on("submit", function() {
        return UpdateFixedIncome();
    });

    $createFixedIncomeMainClass.off("change").on("change", function(event) {
        return CreateFormShowFixedIncomeSubClassBySelectedFixedIncomeMainClass(event.currentTarget as HTMLSelectElement);
    });

    $createFixedIncomeDepositMonth.off("change").on("change", function(event) {
        return CreateFormShowFixedIncomeDepositDayBySelectedFixedIncomeDepositMonth(event.currentTarget as HTMLSelectElement);
    });

    $editFixedIncomeMainClass.off("change").on("change", function(event) {
        return EditFormShowFixedIncomeSubClassBySelectedFixedIncomeMainClass(event.currentTarget as HTMLSelectElement);
    });

    $editFixedIncomeDepositMonth.off("change").on("change", function(event) {
        return EditFormShowFixedIncomeDepositDayBySelectedFixedIncomeDepositMonth(event.currentTarget as HTMLSelectElement);
    });

    $createFixedIncomeDepositMyAssetProductName.off("change").on("change", function(event) {
        ChangeCreateFixedIncomeAmountLabel((event.currentTarget as HTMLSelectElement).value);
    });

    $editFixedIncomeDepositMyAssetProductName.off("change").on("change", function(event) {
        ChangeEditFixedIncomeAmountLabel((event.currentTarget as HTMLSelectElement).value);
    });

    // On load, set the create-form amount label for whichever asset is preselected.
    $(function() {
        ChangeCreateFixedIncomeAmountLabel($createFixedIncomeDepositMyAssetProductName.find("option:selected").val() as string);
    });
})();
