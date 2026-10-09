/**
 * Script for the **Fixed Expenditure** page
 * (`Views/Notice/FixedExpenditure.cshtml`) — recurring/scheduled expenditure
 * entries. A NonFactors MVC.Grid list with create / edit / delete modals and
 * Excel export, following the standard grid-CRUD pattern (see
 * `AccountBook/Asset`). It combines everything the Fixed Income and Expenditure
 * pages do:
 *
 *   • A localized jQuery-UI datepicker on the "maturity date" fields, with two
 *     injected button-panel buttons: "No maturity date" (`NO_MATURITY_DATE`
 *     sentinel) and "Today".
 *   • Dependent `<select>`s: main class -> allowed subclasses
 *     (`ExpenditureClassPolicy`); deposit month -> valid deposit days 1..maxDay
 *     (`FixedSchedulePolicy`, via `maxDepositDayByMonth`).
 *   • A "deposit asset" block shown only for transfer-type subclasses
 *     (`expenditureDepositAssetSubClasses`).
 *   • The amount field's label follows the chosen payment method.
 *
 * Client checks are UX mirrors; the controller re-validates on save.
 * IIFE-wrapped, no `import` / `export`.
 */
(function() {
    // The runtime-check helpers the _Layout script defines (see TypeScripts/global.d.ts).
    const { check, parseJson, fieldValue, selectValue, optionalFieldValue, attribute } = window;
    // Cached references: tab containers, grid search box, the two maturity-date
    // inputs, the deposit-asset wrapper divs, every field of both modals, the
    // amount labels, and the anti-forgery input.
    const $createFixedExpenditureTabs = $("#createFixedExpenditureTabs");
    const $editFixedExpenditureTabs = $("#editFixedExpenditureTabs");
    const $createFixedExpenditureMaturityDate = $("#createFixedExpenditureMaturityDate");
    const $editFixedExpenditureMaturityDate = $("#editFixedExpenditureMaturityDate");
    const $gridSearch = $("#gridSearch");
    const $createFixedExpenditureSubClass = $("#createFixedExpenditureSubClass");
    const $divCreateFixedExpenditureMyDepositAsset = $("#divCreateFixedExpenditureMyDepositAsset");
    const $createFixedExpenditureDepositDay = $("#createFixedExpenditureDepositDay");
    const $editFixedExpenditureSubClass = $("#editFixedExpenditureSubClass");
    const $divEditFixedExpenditureMyDepositAsset = $("#divEditFixedExpenditureMyDepositAsset");
    const $editFixedExpenditureDepositDay = $("#editFixedExpenditureDepositDay");
    const $formCreateFixedExpenditure = $("#formCreateFixedExpenditure");
    const $editFixedExpenditureDialogModal = $("#editFixedExpenditureDialogModal");
    const $formEditFixedExpenditure = $("#formEditFixedExpenditure");
    const $confirmDeleteFixedExpenditureDialogModal = $("#confirmDeleteFixedExpenditureDialogModal");
    const $__RequestVerificationToken = $("input[name=\"__RequestVerificationToken\"]");
    // Localized toast shown when the amount-label request itself fails (transport error).
    const failedToLoadAmountLabelMessage = fieldValue($("#localizerFailedToLoadAmountLabel"));
    const $createFixedExpenditureDialogModal = $("#createFixedExpenditureDialogModal");
    const $createFixedExpenditurePaymentMethod = $("#createFixedExpenditurePaymentMethod");
    const $editFixedExpenditurePaymentMethod = $("#editFixedExpenditurePaymentMethod");
    const $editFixedExpenditureDepositMonth = $("#editFixedExpenditureDepositMonth");
    const $editFixedExpenditureMainClass = $("#editFixedExpenditureMainClass");
    const $createFixedExpenditureDepositMonth = $("#createFixedExpenditureDepositMonth");
    const $createFixedExpenditureMainClass = $("#createFixedExpenditureMainClass");
    const $btnDeleteFixedExpenditure = $("#btnDeleteFixedExpenditure");
    const $btnExportExcelFixedExpenditure = $("#btnExportExcelFixedExpenditure");
    const $btnConfirmDeleteFixedExpenditure = $("#btnConfirmDeleteFixedExpenditure");
    const $btnEditFixedExpenditureGridRow = $("#btnEditFixedExpenditureGridRow");
    const $labelEditFixedExpenditureAmount = $("#labelEditFixedExpenditureAmount");
    const $labelCreateFixedExpenditureAmount = $("#labelCreateFixedExpenditureAmount");
    const $editFixedExpenditureId = $("#editFixedExpenditureId");
    const $editFixedExpenditureContent = $("#editFixedExpenditureContent");
    const $editFixedExpenditureAmount = $("#editFixedExpenditureAmount");
    const $editFixedExpenditureNote = $("#editFixedExpenditureNote");
    const $editFixedExpenditureMyDepositAsset = $("#editFixedExpenditureMyDepositAsset");
    const $editFixedExpenditureUnpunctuality = $("#editFixedExpenditureUnpunctuality");
    const $createFixedExpenditureContent = $("#createFixedExpenditureContent");
    const $createFixedExpenditureAmount = $("#createFixedExpenditureAmount");
    const $createFixedExpenditureNote = $("#createFixedExpenditureNote");
    const $createFixedExpenditureMyDepositAsset = $("#createFixedExpenditureMyDepositAsset");

    // Server-published rules (FixedExpenditure.cshtml). Authoritative in ExpenditureClassPolicy /
    // FixedSchedulePolicy; mirrored here for form UX only, the server re-validates on save.
    const expenditureSubClassMap = parseJson(optionalFieldValue($("#expenditureSubClassMap")) || "{}", check.record(check.array(check.string)), "#expenditureSubClassMap");
    const expenditureDepositAssetSubClasses = parseJson(optionalFieldValue($("#expenditureDepositAssetSubClasses")) || "[]", check.array(check.string), "#expenditureDepositAssetSubClasses");
    const maxDepositDayByMonth = parseJson(optionalFieldValue($("#maxDepositDayByMonth")) || "{}", check.record(check.number), "#maxDepositDayByMonth");
    // Far-future stand-in for "no maturity date", published by the server (FixedSchedulePolicy)
    // as a yyyy-MM-dd string so the client and server never drift on the value.
    const noMaturityIso = fieldValue($("#noMaturityDate"));
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
        const maxDay = maxDepositDayByMonth[String(monthValue)];
        const values = [];
        for (let day = 1; day <= maxDay; day++) {
            values.push(String(day));
        }
        return values;
    }

    // The "deposit asset" field is only relevant for transfer-type subclasses.
    function ToggleCreateFixedExpenditureMyDepositAsset(subClassValue: string | null) {
        subClassValue !== null && expenditureDepositAssetSubClasses.indexOf(subClassValue) !== -1
            ? $divCreateFixedExpenditureMyDepositAsset.show()
            : $divCreateFixedExpenditureMyDepositAsset.hide();
    }

    /** Same as `ToggleCreateFixedExpenditureMyDepositAsset` for the edit modal. */
    function ToggleEditFixedExpenditureMyDepositAsset(subClassValue: string | null) {
        subClassValue !== null && expenditureDepositAssetSubClasses.indexOf(subClassValue) !== -1
            ? $divEditFixedExpenditureMyDepositAsset.show()
            : $divEditFixedExpenditureMyDepositAsset.hide();
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
        $createFixedExpenditureTabs.tabs();
        $editFixedExpenditureTabs.tabs();
        // Maturity-date pickers. `minDate: 0` forbids past dates. `beforeShow` /
        // `onChangeMonthYear` (deferred a tick with `setTimeout`) append two
        // custom buttons: "No maturity date" -> the far-future `NO_MATURITY_DATE`
        // sentinel, and "Today". `_clearDate` is an undocumented jQuery-UI
        // internal (typed in global.d.ts) that wipes the field first.
        $createFixedExpenditureMaturityDate.datepicker({
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
                            $createFixedExpenditureMaturityDate.datepicker("setDate", NO_MATURITY_DATE);
                        }
                    }).appendTo(buttonPane).addClass("ui-datepicker-clear ui-state-default ui-priority-primary ui-corner-all");

                    $("<button>", {
                        text: localizer.Today,
                        click: function() {
                            $.datepicker._clearDate(input);
                            $createFixedExpenditureMaturityDate.datepicker("setDate", new Date());
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
                            $createFixedExpenditureMaturityDate.datepicker("setDate", NO_MATURITY_DATE);
                        }
                    }).appendTo(buttonPane).addClass("ui-datepicker-clear ui-state-default ui-priority-primary ui-corner-all");

                    $("<button>", {
                        text: localizer.Today,
                        click: function() {
                            $.datepicker._clearDate(instance.input);
                            $createFixedExpenditureMaturityDate.datepicker("setDate", new Date());
                        }
                    }).appendTo(buttonPane).addClass("ui-datepicker-clear ui-state-default ui-priority-primary ui-corner-all");

                }, 1);
            }
        });

        $editFixedExpenditureMaturityDate.datepicker({
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
                            $editFixedExpenditureMaturityDate.datepicker("setDate", NO_MATURITY_DATE);
                        }
                    }).appendTo(buttonPane).addClass("ui-datepicker-clear ui-state-default ui-priority-primary ui-corner-all");

                    $("<button>", {
                        text: localizer.Today,
                        click: function() {
                            $.datepicker._clearDate(input);
                            $editFixedExpenditureMaturityDate.datepicker("setDate", new Date());
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
                            $editFixedExpenditureMaturityDate.datepicker("setDate", NO_MATURITY_DATE);
                        }
                    }).appendTo(buttonPane).addClass("ui-datepicker-clear ui-state-default ui-priority-primary ui-corner-all");

                    $("<button>", {
                        text: localizer.Today,
                        click: function() {
                            $.datepicker._clearDate(instance.input);
                            $editFixedExpenditureMaturityDate.datepicker("setDate", new Date());
                        }
                    }).appendTo(buttonPane).addClass("ui-datepicker-clear ui-state-default ui-priority-primary ui-corner-all");

                }, 1);
            }
        });

        // Default both pickers to today.
        $createFixedExpenditureMaturityDate.datepicker("setDate", new Date());
        $editFixedExpenditureMaturityDate.datepicker("setDate", new Date());

        // Hide the date picker's built-in "Close" / "Today" buttons via injected CSS.
        $("<style> .ui-datepicker-close { display: none; } </style>").appendTo("head");
        $("<style> .ui-datepicker-current { display: none; } </style>").appendTo("head");
    });

    // Bootstrap table class that marks the currently selected grid row.
    let selectedRowColor = "table-primary";

    // MvcGrid `rowclick`: move the highlight to the row whose `data-id` matches.
    $(document).off("rowclick.FixedExpenditure").on("rowclick.FixedExpenditure", (e: JQuery.TriggeredEvent) => {
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
    $(document).off("reloadstart.FixedExpenditure").on("reloadstart.FixedExpenditure", _ => {
    });

    $(document).off("reloadend.FixedExpenditure").on("reloadend.FixedExpenditure", _ => {
    });

    $(document).off("reloadfail.FixedExpenditure").on("reloadfail.FixedExpenditure", _ => {
    });

    $(document).off("gridconfigure.FixedExpenditure").on("gridconfigure.FixedExpenditure", _ => {
    });

    // Double-click the highlighted row to open its edit modal.
    $(document).off("dblclick.FixedExpenditure", ".clsGridRow").on("dblclick.FixedExpenditure", ".clsGridRow", function() {
        $(".clsGridRow").each(function() {
            if ($(this).attr("class")!.includes(selectedRowColor)) {
                EditFixedExpenditureGridRow();
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
    function CreateFormShowFixedExpenditureSubClassBySelectedFixedExpenditureMainClass(createFixedExpenditureMainClass: HTMLSelectElement) {
        ApplyAllowedOptions($createFixedExpenditureSubClass, expenditureSubClassMap[createFixedExpenditureMainClass.value] || [], null);
        ToggleCreateFixedExpenditureMyDepositAsset(selectValue($createFixedExpenditureSubClass));
    }

    /** Create form subclass change: show/hide the deposit-asset block for it. */
    function CreateFormShowFixedExpenditureDivCreateFixedExpenditureMyDepositAssetBySelectedFixedExpenditureSubClass(createFixedExpenditureSubClass: HTMLSelectElement) {
        ToggleCreateFixedExpenditureMyDepositAsset(createFixedExpenditureSubClass.value);
    }

    /** Create form deposit-month change: re-fill the deposit-day options 1..maxDay for that month. */
    function CreateFormShowFixedExpenditureDepositDayBySelectedFixedExpenditureDepositMonth(createFixedExpenditureDepositMonth: HTMLSelectElement) {
        ApplyAllowedOptions($createFixedExpenditureDepositDay, DepositDayValues(createFixedExpenditureDepositMonth.value), null);
    }

    /** Edit form main-class change: same as the create-form version. */
    function EditFormShowFixedExpenditureSubClassBySelectedFixedExpenditureMainClass(editFixedExpenditureMainClass: HTMLSelectElement) {
        ApplyAllowedOptions($editFixedExpenditureSubClass, expenditureSubClassMap[editFixedExpenditureMainClass.value] || [], null);
        ToggleEditFixedExpenditureMyDepositAsset(selectValue($editFixedExpenditureSubClass));
    }

    /** Edit form subclass change: show/hide the deposit-asset block for it. */
    function EditFormShowFixedExpenditureDivCreateFixedExpenditureMyDepositAssetBySelectedFixedExpenditureSubClass(editFixedExpenditureSubClass: HTMLSelectElement) {
        ToggleEditFixedExpenditureMyDepositAsset(editFixedExpenditureSubClass.value);
    }

    /** Edit form deposit-month change: same as the create-form version. */
    function EditFormShowFixedExpenditureDepositDayBySelectedFixedExpenditureDepositMonth(editFixedExpenditureDepositMonth: HTMLSelectElement) {
        ApplyAllowedOptions($editFixedExpenditureDepositDay, DepositDayValues(editFixedExpenditureDepositMonth.value), null);
    }

    /** Create-fixed-expenditure modal submit: validate, POST the fields as JSON, then close / reload / toast. */
    function CreateFixedExpenditure() {

        if (!$formCreateFixedExpenditure.valid()) {
            return false;
        }

        let mainClass = $createFixedExpenditureMainClass.val();
        let subClass = $createFixedExpenditureSubClass.val();
        let content = $createFixedExpenditureContent.val();
        let amount = $createFixedExpenditureAmount.val();
        let depositMonth = $createFixedExpenditureDepositMonth.val();
        let depositDay = $createFixedExpenditureDepositDay.val();
        let maturityDate = $createFixedExpenditureMaturityDate.val();
        let note = $createFixedExpenditureNote.val();
        let paymentMethod = $createFixedExpenditurePaymentMethod.val();
        let myDepositAsset = $createFixedExpenditureMyDepositAsset.val();

        let paramValue = JSON.stringify({
            MainClass: mainClass,
            SubClass: subClass,
            Content: content,
            Amount: amount,
            DepositMonth: depositMonth,
            DepositDay: depositDay,
            MaturityDate: maturityDate,
            Note: note,
            PaymentMethod: paymentMethod,
            MyDepositAsset: myDepositAsset
        });

        $.ajax({
            url: "/Notice/CreateFixedExpenditure",
            type: "POST",
            headers: { "RequestVerificationToken": fieldValue($__RequestVerificationToken) },
            dataType: "json",
            data: paramValue,
            contentType: "application/json; charset=utf-8",
            success: function(data: ActionReply) {
                if (data.result) {
                    $createFixedExpenditureDialogModal.modal("hide");

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
     * (`IsFixedExpenditureExists`), fills the form, re-filters the subclass and
     * deposit-day selects, toggles the deposit-asset block, and (when the record
     * has no explicit `myDepositAsset`) falls back to the payment method for
     * that select.
     *
     * @param errorMessageSelectGridRow localized "pick a row first" text from a `data-*` attribute.
     */
    function EditFixedExpenditureGridRow(errorMessageSelectGridRow?: string) {

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
            url: "/Notice/IsFixedExpenditureExists" + "?id=" + selectedRowId,
            type: "POST",
            headers: { "RequestVerificationToken": fieldValue($__RequestVerificationToken) },
            dataType: "json",
            contentType: "application/json; charset=utf-8",
            success: function(data: ReadReply<FixedExpenditurePayload>) {
                if (data.result) {

                    $editFixedExpenditureId.val(data.fixedExpenditure.id);
                    $editFixedExpenditureMainClass.val(data.fixedExpenditure.mainClass).trigger("change");

                    ApplyAllowedOptions($editFixedExpenditureSubClass, expenditureSubClassMap[data.fixedExpenditure.mainClass] || [], data.fixedExpenditure.subClass);

                    $editFixedExpenditureContent.val(data.fixedExpenditure.content);
                    $editFixedExpenditureAmount.val(data.fixedExpenditure.amount);

                    $editFixedExpenditureDepositMonth.val(data.fixedExpenditure.depositMonth.toString()).trigger("change");

                    ApplyAllowedOptions($editFixedExpenditureDepositDay, DepositDayValues(data.fixedExpenditure.depositMonth.toString()), data.fixedExpenditure.depositDay.toString());

                    $editFixedExpenditureMaturityDate.val(data.fixedExpenditure.maturityDate);
                    $editFixedExpenditureNote.val(data.fixedExpenditure.note);
                    $editFixedExpenditurePaymentMethod.val(data.fixedExpenditure.paymentMethod).trigger("change");

                    if (data.fixedExpenditure.myDepositAsset) {
                        $editFixedExpenditureMyDepositAsset.val(data.fixedExpenditure.myDepositAsset).trigger("change");
                    } else {
                        $editFixedExpenditureMyDepositAsset.val(data.fixedExpenditure.paymentMethod).trigger("change");
                    }

                    ToggleEditFixedExpenditureMyDepositAsset(data.fixedExpenditure.subClass);

                    $editFixedExpenditureUnpunctuality.prop("checked", data.fixedExpenditure.unpunctuality);

                    $editFixedExpenditureDialogModal.modal({
                        keyboard: false,
                        backdrop: "static"
                    });

                    $editFixedExpenditureDialogModal.modal("toggle");
                    $editFixedExpenditureDialogModal.modal("show");
                } else {
                    toastr.error(data.error);
                }
            }
        });
    }

    /** Edit-fixed-expenditure modal submit: same shape as `CreateFixedExpenditure` plus `ID` and `Unpunctuality`. */
    function UpdateFixedExpenditure() {

        if (!$formEditFixedExpenditure.valid()) {
            return false;
        }

        let id = $editFixedExpenditureId.val();
        let mainClass = $editFixedExpenditureMainClass.val();
        let subClass = $editFixedExpenditureSubClass.val();
        let content = $editFixedExpenditureContent.val();
        let amount = $editFixedExpenditureAmount.val();
        let depositMonth = $editFixedExpenditureDepositMonth.val();
        let depositDay = $editFixedExpenditureDepositDay.val();
        let maturityDate = $editFixedExpenditureMaturityDate.val();
        let note = $editFixedExpenditureNote.val();
        let paymentMethod = $editFixedExpenditurePaymentMethod.val();
        let myDepositAsset = $editFixedExpenditureMyDepositAsset.val();
        let unpunctuality = $editFixedExpenditureUnpunctuality.is(":checked");

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
            PaymentMethod: paymentMethod,
            MyDepositAsset: myDepositAsset,
            Unpunctuality: unpunctuality
        });

        $.ajax({
            url: "/Notice/UpdateFixedExpenditure",
            type: "POST",
            headers: { "RequestVerificationToken": fieldValue($__RequestVerificationToken) },
            dataType: "json",
            data: paramValue,
            contentType: "application/json; charset=utf-8",
            success: function(data: ActionReply) {
                if (data.result) {
                    $editFixedExpenditureDialogModal.modal("hide");

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
    function ConfirmDeleteFixedExpenditure(errorMessageSelectGridRow?: string) {

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

        $confirmDeleteFixedExpenditureDialogModal.modal({
            keyboard: false,
            backdrop: "static"
        });

        $confirmDeleteFixedExpenditureDialogModal.modal("toggle");
        $confirmDeleteFixedExpenditureDialogModal.modal("show");
    }

    /** Confirmed delete: re-check selection, confirm the record exists, then POST `DeleteFixedExpenditure`. */
    function DeleteFixedExpenditure(errorMessageSelectGridRow?: string) {

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
            url: "/Notice/IsFixedExpenditureExists" + "?id=" + selectedRowId,
            type: "POST",
            headers: { "RequestVerificationToken": fieldValue($__RequestVerificationToken) },
            dataType: "json",
            contentType: "application/json; charset=utf-8",
            success: function(data: ReadReply<FixedExpenditurePayload>) {
                if (data.result) {

                    let paramValue = JSON.stringify({
                        Id: data.fixedExpenditure.id
                    });

                    $.ajax({
                        url: "/Notice/DeleteFixedExpenditure",
                        type: "POST",
                        headers: { "RequestVerificationToken": fieldValue($__RequestVerificationToken) },
                        dataType: "json",
                        data: paramValue,
                        contentType: "application/json; charset=utf-8",
                        success: function(data: ActionReply) {
                            if (data.result) {
                                $confirmDeleteFixedExpenditureDialogModal.modal("hide");

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
    function ExportExcelFixedExpenditure() {
        let form = document.createElement("form");
        let element1 = document.createElement("input");
        let element2 = document.createElement("input");

        form.method = "POST";
        form.action = "/Notice/ExportExcelFixedExpenditure";

        element1.name = "__RequestVerificationToken";
        element1.value = fieldValue($__RequestVerificationToken);
        form.appendChild(element1);

        element2.name = "fileName";
        element2.value = "FixedExpenditure";
        form.appendChild(element2);

        document.body.appendChild(form);

        form.submit();
    }

    /**
     * Updates the create-form amount label to match the chosen payment method
     * (its currency). Both `if` branches set the same thing.
     */
    function ChangeCreateFixedExpenditureAmountLabel(productName: string) {
        $.ajax({
            url: "/Notice/GetFixedExpenditureAmountLabel" + "?productName=" + encodeURIComponent(productName),
            type: "POST",
            headers: { "RequestVerificationToken": fieldValue($__RequestVerificationToken) },
            dataType: "json",
            contentType: "application/json; charset=utf-8",
            success: function(data: AmountLabelReply) {
                if (data.result) {
                    $labelCreateFixedExpenditureAmount.text(data.label);
                } else {
                    $labelCreateFixedExpenditureAmount.text(data.label);
                }
            },
            error: function() {
                toastr.error(failedToLoadAmountLabelMessage);
            }
        });
    }

    /** Same as `ChangeCreateFixedExpenditureAmountLabel` for the edit form. */
    function ChangeEditFixedExpenditureAmountLabel(productName: string) {
        $.ajax({
            url: "/Notice/GetFixedExpenditureAmountLabel" + "?productName=" + encodeURIComponent(productName),
            type: "POST",
            headers: { "RequestVerificationToken": fieldValue($__RequestVerificationToken) },
            dataType: "json",
            contentType: "application/json; charset=utf-8",
            success: function(data: AmountLabelReply) {
                if (data.result) {
                    $labelEditFixedExpenditureAmount.text(data.label);
                } else {
                    $labelEditFixedExpenditureAmount.text(data.label);
                }
            },
            error: function() {
                toastr.error(failedToLoadAmountLabelMessage);
            }
        });
    }

    // --- Button / form / select wiring --------------------------------
    $btnEditFixedExpenditureGridRow.off("click").on("click", function() {
        EditFixedExpenditureGridRow($(this).attr("data-errorMessageSelectGridRow"));
    });

    $btnConfirmDeleteFixedExpenditure.off("click").on("click", function() {
        ConfirmDeleteFixedExpenditure($(this).attr("data-errorMessageSelectGridRow"));
    });

    $btnExportExcelFixedExpenditure.off("click").on("click", function() {
        ExportExcelFixedExpenditure();
    });

    $btnDeleteFixedExpenditure.off("click").on("click", function() {
        DeleteFixedExpenditure($(this).attr("data-errorMessageSelectGridRow"));
    });

    $formCreateFixedExpenditure.off("submit").on("submit", function() {
        return CreateFixedExpenditure();
    });

    $formEditFixedExpenditure.off("submit").on("submit", function() {
        return UpdateFixedExpenditure();
    });

    $createFixedExpenditureMainClass.off("change").on("change", function(event) {
        return CreateFormShowFixedExpenditureSubClassBySelectedFixedExpenditureMainClass(event.currentTarget as HTMLSelectElement);
    });

    $createFixedExpenditureSubClass.off("change").on("change", function(event) {
        return CreateFormShowFixedExpenditureDivCreateFixedExpenditureMyDepositAssetBySelectedFixedExpenditureSubClass(event.currentTarget as HTMLSelectElement);
    });

    $createFixedExpenditureDepositMonth.off("change").on("change", function(event) {
        return CreateFormShowFixedExpenditureDepositDayBySelectedFixedExpenditureDepositMonth(event.currentTarget as HTMLSelectElement);
    });

    $editFixedExpenditureMainClass.off("change").on("change", function(event) {
        return EditFormShowFixedExpenditureSubClassBySelectedFixedExpenditureMainClass(event.currentTarget as HTMLSelectElement);
    });

    $editFixedExpenditureSubClass.off("change").on("change", function(event) {
        return EditFormShowFixedExpenditureDivCreateFixedExpenditureMyDepositAssetBySelectedFixedExpenditureSubClass(event.currentTarget as HTMLSelectElement);
    });

    $editFixedExpenditureDepositMonth.off("change").on("change", function(event) {
        return EditFormShowFixedExpenditureDepositDayBySelectedFixedExpenditureDepositMonth(event.currentTarget as HTMLSelectElement);
    });

    $createFixedExpenditurePaymentMethod.off("change").on("change", function(event) {
        ChangeCreateFixedExpenditureAmountLabel((event.currentTarget as HTMLSelectElement).value);
    });

    $editFixedExpenditurePaymentMethod.off("change").on("change", function(event) {
        ChangeEditFixedExpenditureAmountLabel((event.currentTarget as HTMLSelectElement).value);
    });

    // On load, set the create-form amount label for whichever method is preselected.
    $(function() {
        // An account without assets has nothing preselected, and so no label to ask for.
        const preselected = selectValue($createFixedExpenditurePaymentMethod);
        if (preselected !== null) {
            ChangeCreateFixedExpenditureAmountLabel(preselected);
        }
    });
})();
