const localizer = {
    PrevText: $('#localizerPrevText').val(),
    NextText: $('#localizerNextText').val(),
    January: $('#localizerJanuary').val(),
    February: $('#localizerFebruary').val(),
    March: $('#localizerMarch').val(),
    April: $('#localizerApril').val(),
    May: $('#localizerMay').val(),
    June: $('#localizerJune').val(),
    July: $('#localizerJuly').val(),
    August: $('#localizerAugust').val(),
    September: $('#localizerSeptember').val(),
    October: $('#localizerOctober').val(),
    November: $('#localizerNovember').val(),
    December: $('#localizerDecember').val(),
    Jan: $('#localizerJan').val(),
    Feb: $('#localizerFeb').val(),
    Mar: $('#localizerMar').val(),
    Apr: $('#localizerApr').val(),
    Jun: $('#localizerJun').val(),
    Jul: $('#localizerJul').val(),
    Aug: $('#localizerAug').val(),
    Sep: $('#localizerSep').val(),
    Oct: $('#localizerOct').val(),
    Nov: $('#localizerNov').val(),
    Dec: $('#localizerDec').val(),
    Sunday: $('#localizerSunday').val(),
    Monday: $('#localizerMonday').val(),
    Tuesday: $('#localizerTuesday').val(),
    Wednesday: $('#localizerWednesday').val(),
    Thursday: $('#localizerThursday').val(),
    Friday: $('#localizerFriday').val(),
    Saturday: $('#localizerSaturday').val(),
    Sun: $('#localizerSun').val(),
    Mon: $('#localizerMon').val(),
    Tue: $('#localizerTue').val(),
    Wed: $('#localizerWed').val(),
    Thu: $('#localizerThu').val(),
    Fri: $('#localizerFri').val(),
    Sat: $('#localizerSat').val(),
    Su: $('#localizerSu').val(),
    Mo: $('#localizerMo').val(),
    Tu: $('#localizerTu').val(),
    We: $('#localizerWe').val(),
    Th: $('#localizerTh').val(),
    Fr: $('#localizerFr').val(),
    Sa: $('#localizerSa').val(),
    YearSuffix: $('#localizerYearSuffix').val(),
    NoMaturityDate: $('#localizerNoMaturityDate').val(),
    Today: $('#localizerToday').val(),
    MaturityDateError: $('#localizerMaturityDateError').val(),
};

$.datepicker.setDefaults({
    dateFormat: 'yy-mm-dd',
    prevText: localizer.prevText,
    nextText: localizer.nextText,
    monthNames: [localizer.January, localizer.February, localizer.March, localizer.April, localizer.May, localizer.June, localizer.July, localizer.August, localizer.September, localizer.October, localizer.November, localizer.December],
    monthNamesShort: [localizer.Jan, localizer.Feb, localizer.Mar, localizer.Apr, localizer.May, localizer.Jun, localizer.Jul, localizer.Aug, localizer.Sep, localizer.Oct, localizer.Nov, localizer.Dec],
    dayNames: [localizer.Sunday, localizer.Monday, localizer.Tuesday, localizer.Wednesday, localizer.Thursday, localizer.Friday, localizer.Saturday],
    dayNamesShort: [localizer.Sun, localizer.Mon, localizer.Tue, localizer.Wed, localizer.Thu, localizer.Fri, localizer.Sat],
    dayNamesMin: [localizer.Su, localizer.Mo, localizer.Tu, localizer.We, localizer.Th, localizer.Fr, localizer.Sa],
    showMonthAfterYear: true,
    yearSuffix: localizer.YearSuffix
});

$(function () {
    $("#createFixedIncomeTabs").tabs();
    $("#editFixedIncomeTabs").tabs();
    $("#createFixedIncomeMaturityDate").datepicker({
        showButtonPanel: true,
        beforeShow: function (input) {
            setTimeout(function () {
                let buttonPane = $(input)
                    .datepicker("widget")
                    .find(".ui-datepicker-buttonpane");

                $("<button>", {
                    text: localizer.NoMaturityDate,
                    click: function () {
                        $.datepicker._clearDate(input);
                        $("#createFixedIncomeMaturityDate").datepicker("setDate", new Date('9999', '11', '31'));
                    }
                }).appendTo(buttonPane).addClass("ui-datepicker-clear ui-state-default ui-priority-primary ui-corner-all");

                $("<button>", {
                    text: localizer.Today,
                    click: function () {
                        $.datepicker._clearDate(input);
                        $("#createFixedIncomeMaturityDate").datepicker("setDate", new Date());
                    }
                }).appendTo(buttonPane).addClass("ui-datepicker-clear ui-state-default ui-priority-primary ui-corner-all");

            }, 1);
        },
        onChangeMonthYear: function (year, month, instance) {
            setTimeout(function () {
                let buttonPane = $(instance)
                    .datepicker("widget")
                    .find(".ui-datepicker-buttonpane");

                $("<button>", {
                    text: localizer.NoMaturityDate,
                    click: function () {
                        $.datepicker._clearDate(instance.input);
                        $("#createFixedIncomeMaturityDate").datepicker("setDate", new Date('9999', '11', '31'));
                    }
                }).appendTo(buttonPane).addClass("ui-datepicker-clear ui-state-default ui-priority-primary ui-corner-all");

                $("<button>", {
                    text: localizer.Today,
                    click: function () {
                        $.datepicker._clearDate(instance.input);
                        $("#createFixedIncomeMaturityDate").datepicker("setDate", new Date());
                    }
                }).appendTo(buttonPane).addClass("ui-datepicker-clear ui-state-default ui-priority-primary ui-corner-all");

            }, 1);
        },
        onSelect: function (date) {
            if (!date) {
                date = '9999-12-31';
            }
            let tempCurrentDate = new Date();
            let month = tempCurrentDate.getMonth() + 1;
            let day = tempCurrentDate.getDate();

            month = month >= 10 ? month : '0' + month;
            day = day >= 10 ? day : '0' + day;

            let currentDate = tempCurrentDate.getFullYear() + '-' + month + '-' + day;

            if (date < currentDate) {
                alert(localizer.MaturityDateError);
                $("#createFixedIncomeMaturityDate").datepicker("setDate", new Date());
            }
        }
    });

    $("#editFixedIncomeMaturityDate").datepicker({
        showButtonPanel: true,
        beforeShow: function (input) {
            setTimeout(function () {
                let buttonPane = $(input)
                    .datepicker("widget")
                    .find(".ui-datepicker-buttonpane");

                $("<button>", {
                    text: localizer.NoMaturityDate,
                    click: function () {
                        $.datepicker._clearDate(input);
                        $("#editFixedIncomeMaturityDate").datepicker("setDate", new Date('9999', '11', '31'));
                    }
                }).appendTo(buttonPane).addClass("ui-datepicker-clear ui-state-default ui-priority-primary ui-corner-all");

                $("<button>", {
                    text: localizer.Today,
                    click: function () {
                        $.datepicker._clearDate(input);
                        $("#editFixedIncomeMaturityDate").datepicker("setDate", new Date());
                    }
                }).appendTo(buttonPane).addClass("ui-datepicker-clear ui-state-default ui-priority-primary ui-corner-all");

            }, 1);
        },
        onChangeMonthYear: function (year, month, instance) {
            setTimeout(function () {
                let buttonPane = $(instance)
                    .datepicker("widget")
                    .find(".ui-datepicker-buttonpane");

                $("<button>", {
                    text: localizer.NoMaturityDate,
                    click: function () {
                        $.datepicker._clearDate(instance.input);
                        $("#editFixedIncomeMaturityDate").datepicker("setDate", new Date('9999', '11', '31'));
                    }
                }).appendTo(buttonPane).addClass("ui-datepicker-clear ui-state-default ui-priority-primary ui-corner-all");

                $("<button>", {
                    text: localizer.Today,
                    click: function () {
                        $.datepicker._clearDate(instance.input);
                        $("#editFixedIncomeMaturityDate").datepicker("setDate", new Date());
                    }
                }).appendTo(buttonPane).addClass("ui-datepicker-clear ui-state-default ui-priority-primary ui-corner-all");

            }, 1);
        },
        onSelect: function (date) {
            if (!date) {
                date = '9999-12-31';
            }
            let tempCurrentDate = new Date();
            let month = tempCurrentDate.getMonth() + 1;
            let day = tempCurrentDate.getDate();

            month = month >= 10 ? month : '0' + month;
            day = day >= 10 ? day : '0' + day;

            let currentDate = tempCurrentDate.getFullYear() + '-' + month + '-' + day;

            if (date < currentDate) {
                alert(localizer.MaturityDateError);
                $("#editFixedIncomeMaturityDate").datepicker("setDate", new Date());
            }
        }
    });

    $("#createFixedIncomeMaturityDate").datepicker("setDate", new Date());
    $("#editFixedIncomeMaturityDate").datepicker("setDate", new Date());

    $('<style type="text/css"> .ui-datepicker-close { display: none; } </style>').appendTo("head");
    $('<style type="text/css"> .ui-datepicker-current { display: none; } </style>').appendTo("head");
});

let selectedRowColor = "table-primary";

document.addEventListener("rowclick", e => {
    let selectedRow = e.detail;
    let selectedRowId = selectedRow.data.Id;

    $('#fixedIncomeGrid > table > tbody  > tr').each(function (index, tr) {
        $(this).removeClass(selectedRowColor);
    });

    $('#fixedIncomeGrid > table > tbody  > tr').each(function (index, tr) {
        if ((new String(selectedRowId).valueOf() === new String($(this).attr('data-id')).valueOf())) {
            $(this).addClass(selectedRowColor);
        }
    });
});

document.addEventListener("reloadstart", e => {
});

document.addEventListener("reloadend", e => {
});

document.addEventListener("reloadfail", e => {
});

document.addEventListener("gridconfigure", e => {
});


$(document).on("dblclick", "#fixedIncomeGrid > table > tbody  > tr", function () {
    $('#fixedIncomeGrid > table > tbody  > tr').each(function (index, tr) {

        if ($(this).attr("class").includes(selectedRowColor)) {
            EditFixedIncomeGridRow();
        }
    });
});
$('#gridSearch').on('input', function () {
    const grid = new MvcGrid(document.querySelector(".mvc-grid"));

    grid.url.searchParams.set("wholeSearch", this.value);

    grid.reload();
});

function CreateFormShowFixedIncomeSubClassBySelectedFixedIncomeMainClass(createFixedIncomeMainClass) {

    if (createFixedIncomeMainClass.value === "RegularIncome") {
        $('#createFixedIncomeSubClass option[value=LaborIncome]').prop('disabled', true).prop('hidden', true);
        $('#createFixedIncomeSubClass option[value=BusinessIncome]').prop('disabled', true).prop('hidden', true);
        $('#createFixedIncomeSubClass option[value=PensionIncome]').prop('disabled', true).prop('hidden', true);
        $('#createFixedIncomeSubClass option[value=FinancialIncome]').prop('disabled', true).prop('hidden', true);
        $('#createFixedIncomeSubClass option[value=RentalIncome]').prop('disabled', true).prop('hidden', true);
        $('#createFixedIncomeSubClass option[value=OtherIncome]').prop('disabled', true).prop('hidden', true);

        $("#createFixedIncomeSubClass option:selected").removeAttr("selected");

        $('#createFixedIncomeSubClass option[value=LaborIncome]').prop('disabled', false).prop('hidden', false).attr('selected', 'selected');
        $('#createFixedIncomeSubClass option[value=BusinessIncome]').prop('disabled', false).prop('hidden', false);
        $('#createFixedIncomeSubClass option[value=PensionIncome]').prop('disabled', false).prop('hidden', false);
        $('#createFixedIncomeSubClass option[value=FinancialIncome]').prop('disabled', false).prop('hidden', false);
        $('#createFixedIncomeSubClass option[value=RentalIncome]').prop('disabled', false).prop('hidden', false);
        $('#createFixedIncomeSubClass option[value=OtherIncome]').prop('disabled', false).prop('hidden', false);

        $("#createFixedIncomeSubClass").val("LaborIncome").change();
    }
    else if (createFixedIncomeMainClass.value === "IrregularIncome") {
        $('#createFixedIncomeSubClass option[value=LaborIncome]').prop('disabled', true).prop('hidden', true);
        $('#createFixedIncomeSubClass option[value=BusinessIncome]').prop('disabled', true).prop('hidden', true);
        $('#createFixedIncomeSubClass option[value=PensionIncome]').prop('disabled', true).prop('hidden', true);
        $('#createFixedIncomeSubClass option[value=FinancialIncome]').prop('disabled', true).prop('hidden', true);
        $('#createFixedIncomeSubClass option[value=RentalIncome]').prop('disabled', true).prop('hidden', true);
        $('#createFixedIncomeSubClass option[value=OtherIncome]').prop('disabled', true).prop('hidden', true);

        $("#createFixedIncomeSubClass option:selected").removeAttr("selected");

        $('#createFixedIncomeSubClass option[value=LaborIncome]').prop('disabled', false).prop('hidden', false).attr('selected', 'selected');
        $('#createFixedIncomeSubClass option[value=OtherIncome]').prop('disabled', false).prop('hidden', false);

        $("#createFixedIncomeSubClass").val("LaborIncome").change();
    }
}

function CreateFormShowFixedIncomeDepositDayBySelectedFixedIncomeDepositMonth(createFixedIncomeDepositMonth) {

    if (createFixedIncomeDepositMonth.value === "1" ||
        createFixedIncomeDepositMonth.value === "3" ||
        createFixedIncomeDepositMonth.value === "5" ||
        createFixedIncomeDepositMonth.value === "7" ||
        createFixedIncomeDepositMonth.value === "8" ||
        createFixedIncomeDepositMonth.value === "10" ||
        createFixedIncomeDepositMonth.value === "12"
    ) {
        $('#createFixedIncomeDepositDay option[value=29]').prop('disabled', true).prop('hidden', true);
        $('#createFixedIncomeDepositDay option[value=30]').prop('disabled', true).prop('hidden', true);
        $('#createFixedIncomeDepositDay option[value=31]').prop('disabled', true).prop('hidden', true);

        $("#createFixedIncomeDepositDay option:selected").removeAttr("selected");

        $('#createFixedIncomeDepositDay option[value=1]').attr('selected', 'selected');
        $('#createFixedIncomeDepositDay option[value=29]').prop('disabled', false).prop('hidden', false);
        $('#createFixedIncomeDepositDay option[value=30]').prop('disabled', false).prop('hidden', false);
        $('#createFixedIncomeDepositDay option[value=31]').prop('disabled', false).prop('hidden', false);

        $("#createFixedIncomeDepositDay").val("1").change();
    }
    else if (createFixedIncomeDepositMonth.value === "2") {
        $('#createFixedIncomeDepositDay option[value=29]').prop('disabled', true).prop('hidden', true);
        $('#createFixedIncomeDepositDay option[value=30]').prop('disabled', true).prop('hidden', true);
        $('#createFixedIncomeDepositDay option[value=31]').prop('disabled', true).prop('hidden', true);

        $("#createFixedIncomeDepositDay option:selected").removeAttr("selected");

        $('#createFixedIncomeDepositDay option[value=1]').attr('selected', 'selected');
        $('#createFixedIncomeDepositDay option[value=29]').prop('disabled', false).prop('hidden', false);

        $("#createFixedIncomeDepositDay").val("1").change();
    }
    else if (createFixedIncomeDepositMonth.value === "4" ||
        createFixedIncomeDepositMonth.value === "6" ||
        createFixedIncomeDepositMonth.value === "9" ||
        createFixedIncomeDepositMonth.value === "11"
    ) {
        $('#createFixedIncomeDepositDay option[value=29]').prop('disabled', true).prop('hidden', true);
        $('#createFixedIncomeDepositDay option[value=30]').prop('disabled', true).prop('hidden', true);
        $('#createFixedIncomeDepositDay option[value=31]').prop('disabled', true).prop('hidden', true);

        $("#createFixedIncomeDepositDay option:selected").removeAttr("selected");

        $('#createFixedIncomeDepositDay option[value=1]').attr('selected', 'selected');
        $('#createFixedIncomeDepositDay option[value=29]').prop('disabled', false).prop('hidden', false);
        $('#createFixedIncomeDepositDay option[value=30]').prop('disabled', false).prop('hidden', false);

        $("#createFixedIncomeDepositDay").val("1").change();
    }
}

function EditFormShowFixedIncomeSubClassBySelectedFixedIncomeMainClass(editFixedIncomeMainClass) {

    if (editFixedIncomeMainClass.value === "RegularIncome") {
        $('#editFixedIncomeSubClass option[value=LaborIncome]').prop('disabled', true).prop('hidden', true);
        $('#editFixedIncomeSubClass option[value=BusinessIncome]').prop('disabled', true).prop('hidden', true);
        $('#editFixedIncomeSubClass option[value=PensionIncome]').prop('disabled', true).prop('hidden', true);
        $('#editFixedIncomeSubClass option[value=FinancialIncome]').prop('disabled', true).prop('hidden', true);
        $('#editFixedIncomeSubClass option[value=RentalIncome]').prop('disabled', true).prop('hidden', true);
        $('#editFixedIncomeSubClass option[value=OtherIncome]').prop('disabled', true).prop('hidden', true);

        $("#editFixedIncomeSubClass option:selected").removeAttr("selected");

        $('#editFixedIncomeSubClass option[value=LaborIncome]').prop('disabled', false).prop('hidden', false).attr('selected', 'selected');
        $('#editFixedIncomeSubClass option[value=BusinessIncome]').prop('disabled', false).prop('hidden', false);
        $('#editFixedIncomeSubClass option[value=PensionIncome]').prop('disabled', false).prop('hidden', false);
        $('#editFixedIncomeSubClass option[value=FinancialIncome]').prop('disabled', false).prop('hidden', false);
        $('#editFixedIncomeSubClass option[value=RentalIncome]').prop('disabled', false).prop('hidden', false);
        $('#editFixedIncomeSubClass option[value=OtherIncome]').prop('disabled', false).prop('hidden', false);

        $("#editFixedIncomeSubClass").val("LaborIncome").change();
    }
    else if (editFixedIncomeMainClass.value === "IrregularIncome") {
        $('#editFixedIncomeSubClass option[value=LaborIncome]').prop('disabled', true).prop('hidden', true);
        $('#editFixedIncomeSubClass option[value=BusinessIncome]').prop('disabled', true).prop('hidden', true);
        $('#editFixedIncomeSubClass option[value=PensionIncome]').prop('disabled', true).prop('hidden', true);
        $('#editFixedIncomeSubClass option[value=FinancialIncome]').prop('disabled', true).prop('hidden', true);
        $('#editFixedIncomeSubClass option[value=RentalIncome]').prop('disabled', true).prop('hidden', true);
        $('#editFixedIncomeSubClass option[value=OtherIncome]').prop('disabled', true).prop('hidden', true);

        $("#editFixedIncomeSubClass option:selected").removeAttr("selected");

        $('#editFixedIncomeSubClass option[value=LaborIncome]').prop('disabled', false).prop('hidden', false).attr('selected', 'selected');
        $('#editFixedIncomeSubClass option[value=OtherIncome]').prop('disabled', false).prop('hidden', false);

        $("#editFixedIncomeSubClass").val("LaborIncome").change();
    }
}

function EditFormShowFixedIncomeDepositDayBySelectedFixedIncomeDepositMonth(editFixedIncomeDepositMonth) {

    if (editFixedIncomeDepositMonth.value === "1" ||
        editFixedIncomeDepositMonth.value === "3" ||
        editFixedIncomeDepositMonth.value === "5" ||
        editFixedIncomeDepositMonth.value === "7" ||
        editFixedIncomeDepositMonth.value === "8" ||
        editFixedIncomeDepositMonth.value === "10" ||
        editFixedIncomeDepositMonth.value === "12"
    ) {
        $('#editFixedIncomeDepositDay option[value=29]').prop('disabled', true).prop('hidden', true);
        $('#editFixedIncomeDepositDay option[value=30]').prop('disabled', true).prop('hidden', true);
        $('#editFixedIncomeDepositDay option[value=31]').prop('disabled', true).prop('hidden', true);

        $("#editFixedIncomeDepositDay option:selected").removeAttr("selected");

        $('#editFixedIncomeDepositDay option[value=1]').attr('selected', 'selected');
        $('#editFixedIncomeDepositDay option[value=29]').prop('disabled', false).prop('hidden', false);
        $('#editFixedIncomeDepositDay option[value=30]').prop('disabled', false).prop('hidden', false);
        $('#editFixedIncomeDepositDay option[value=31]').prop('disabled', false).prop('hidden', false);

        $("#editFixedIncomeDepositDay").val("1").change();
    }
    else if (editFixedIncomeDepositMonth.value === "2") {
        $('#editFixedIncomeDepositDay option[value=29]').prop('disabled', true).prop('hidden', true);
        $('#editFixedIncomeDepositDay option[value=30]').prop('disabled', true).prop('hidden', true);
        $('#editFixedIncomeDepositDay option[value=31]').prop('disabled', true).prop('hidden', true);

        $("#editFixedIncomeDepositDay option:selected").removeAttr("selected");

        $('#editFixedIncomeDepositDay option[value=1]').attr('selected', 'selected');
        $('#editFixedIncomeDepositDay option[value=29]').prop('disabled', false).prop('hidden', false);

        $("#editFixedIncomeDepositDay").val("1").change();
    }
    else if (editFixedIncomeDepositMonth.value === "4" ||
        editFixedIncomeDepositMonth.value === "6" ||
        editFixedIncomeDepositMonth.value === "9" ||
        editFixedIncomeDepositMonth.value === "11"
    ) {
        $('#editFixedIncomeDepositDay option[value=29]').prop('disabled', true).prop('hidden', true);
        $('#editFixedIncomeDepositDay option[value=30]').prop('disabled', true).prop('hidden', true);
        $('#editFixedIncomeDepositDay option[value=31]').prop('disabled', true).prop('hidden', true);

        $("#editFixedIncomeDepositDay option:selected").removeAttr("selected");

        $('#editFixedIncomeDepositDay option[value=1]').attr('selected', 'selected');
        $('#editFixedIncomeDepositDay option[value=29]').prop('disabled', false).prop('hidden', false);
        $('#editFixedIncomeDepositDay option[value=30]').prop('disabled', false).prop('hidden', false);

        $("#editFixedIncomeDepositDay").val("1").change();
    }
}

function CreateFixedIncome() {

    if (!$('#formCreateFixedIncome').valid()) {
        return false;
    }

    let createForm = $('#formCreateFixedIncome');

    let mainClass = createForm.find('select[id="createFixedIncomeMainClass"]').val();
    let subClass = createForm.find('select[id="createFixedIncomeSubClass"]').val();
    let content = createForm.find('input[id="createFixedIncomeContent"]').val();
    let amount = createForm.find('input[id="createFixedIncomeAmount"]').val();
    let depositMonth = createForm.find('select[id="createFixedIncomeDepositMonth"]').val();
    let depositDay = createForm.find('select[id="createFixedIncomeDepositDay"]').val();
    let maturityDate = createForm.find('input[id="createFixedIncomeMaturityDate"]').val();
    let note = createForm.find('input[id="createFixedIncomeNote"]').val();
    let depositMyAssetProductName = createForm.find('select[id="createFixedIncomeDepositMyAssetProductName"]').val();

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
        url: '/Notice/CreateFixedIncome',
        type: 'POST',
        headers: { "RequestVerificationToken": $('input[name="__RequestVerificationToken"]').val() },
        dataType: 'json',
        data: paramValue,
        contentType: 'application/json; charset=utf-8',
        success: function (data) {
            if (data.result) {
                $('#createFixedIncomeDialogModal').modal('hide');

                const grid = new MvcGrid(document.querySelector(".mvc-grid"));
                grid.reload();

                toastr.success(data.message);
            }
            else {
                toastr.error(data.error);
            }
        }
    });

    return false;
}

function EditFixedIncomeGridRow(errorMessageSelectGridRow) {

    let selectedRowId = "";

    $('#fixedIncomeGrid > table > tbody  > tr').each(function (index, tr) {
        if ($(this).attr("class").includes(selectedRowColor)) {
            selectedRowId = $(this).attr("data-id");
        }
    });

    if (selectedRowId === "") {
        toastr.error(errorMessageSelectGridRow);
        return false;
    }

    $.ajax({
        url: '/Notice/IsFixedIncomeExists' + '?id=' + selectedRowId,
        type: 'POST',
        headers: { "RequestVerificationToken": $('input[name="__RequestVerificationToken"]').val() },
        dataType: 'json',
        data: null,
        contentType: 'application/json; charset=utf-8',
        success: function (data) {
            if (data.result) {

                let formEditFixedIncome = $('#formEditFixedIncome');

                formEditFixedIncome.find('input[id="editFixedIncomeId"]').val(data.fixedIncome.id);
                formEditFixedIncome.find('select[id="editFixedIncomeMainClass"]').val(data.fixedIncome.mainClass).change();

                if (data.fixedIncome.mainClass === "RegularIncome") {
                    $('#editFixedIncomeSubClass option[value=LaborIncome]').prop('disabled', true).prop('hidden', true);
                    $('#editFixedIncomeSubClass option[value=BusinessIncome]').prop('disabled', true).prop('hidden', true);
                    $('#editFixedIncomeSubClass option[value=PensionIncome]').prop('disabled', true).prop('hidden', true);
                    $('#editFixedIncomeSubClass option[value=FinancialIncome]').prop('disabled', true).prop('hidden', true);
                    $('#editFixedIncomeSubClass option[value=RentalIncome]').prop('disabled', true).prop('hidden', true);
                    $('#editFixedIncomeSubClass option[value=OtherIncome]').prop('disabled', true).prop('hidden', true);

                    $("#editFixedIncomeSubClass option:selected").removeAttr("selected");

                    $('#editFixedIncomeSubClass option[value=LaborIncome]').prop('disabled', false).prop('hidden', false);
                    $('#editFixedIncomeSubClass option[value=BusinessIncome]').prop('disabled', false).prop('hidden', false);
                    $('#editFixedIncomeSubClass option[value=PensionIncome]').prop('disabled', false).prop('hidden', false);
                    $('#editFixedIncomeSubClass option[value=FinancialIncome]').prop('disabled', false).prop('hidden', false);
                    $('#editFixedIncomeSubClass option[value=RentalIncome]').prop('disabled', false).prop('hidden', false);
                    $('#editFixedIncomeSubClass option[value=OtherIncome]').prop('disabled', false).prop('hidden', false);

                    formEditFixedIncome.find('select[id="editFixedIncomeSubClass"]').val(data.fixedIncome.subClass).change();
                }
                else if (data.fixedIncome.mainClass === "IrregularIncome") {
                    $('#editFixedIncomeSubClass option[value=LaborIncome]').prop('disabled', true).prop('hidden', true);
                    $('#editFixedIncomeSubClass option[value=BusinessIncome]').prop('disabled', true).prop('hidden', true);
                    $('#editFixedIncomeSubClass option[value=PensionIncome]').prop('disabled', true).prop('hidden', true);
                    $('#editFixedIncomeSubClass option[value=FinancialIncome]').prop('disabled', true).prop('hidden', true);
                    $('#editFixedIncomeSubClass option[value=RentalIncome]').prop('disabled', true).prop('hidden', true);
                    $('#editFixedIncomeSubClass option[value=OtherIncome]').prop('disabled', true).prop('hidden', true);

                    $("#editFixedIncomeSubClass option:selected").removeAttr("selected");

                    $('#editFixedIncomeSubClass option[value=LaborIncome]').prop('disabled', false).prop('hidden', false);
                    $('#editFixedIncomeSubClass option[value=OtherIncome]').prop('disabled', false).prop('hidden', false);

                    formEditFixedIncome.find('select[id="editFixedIncomeSubClass"]').val(data.fixedIncome.subClass).change();
                }

                formEditFixedIncome.find('input[id="editFixedIncomeContent"]').val(data.fixedIncome.content);
                formEditFixedIncome.find('input[id="editFixedIncomeAmount"]').val(data.fixedIncome.amount);

                formEditFixedIncome.find('select[id="editFixedIncomeDepositMonth"]').val(data.fixedIncome.depositMonth.toString()).change();

                if (data.fixedIncome.depositMonth.toString() === "1" ||
                    data.fixedIncome.depositMonth.toString() === "3" ||
                    data.fixedIncome.depositMonth.toString() === "5" ||
                    data.fixedIncome.depositMonth.toString() === "7" ||
                    data.fixedIncome.depositMonth.toString() === "8" ||
                    data.fixedIncome.depositMonth.toString() === "10" ||
                    data.fixedIncome.depositMonth.toString() === "12"
                ) {
                    $('#editFixedIncomeDepositDay option[value=29]').prop('disabled', true).prop('hidden', true);
                    $('#editFixedIncomeDepositDay option[value=30]').prop('disabled', true).prop('hidden', true);
                    $('#editFixedIncomeDepositDay option[value=31]').prop('disabled', true).prop('hidden', true);

                    $("#editFixedIncomeDepositDay option:selected").removeAttr("selected");

                    $('#editFixedIncomeDepositDay option[value=29]').prop('disabled', false).prop('hidden', false);
                    $('#editFixedIncomeDepositDay option[value=30]').prop('disabled', false).prop('hidden', false);
                    $('#editFixedIncomeDepositDay option[value=31]').prop('disabled', false).prop('hidden', false);

                    formEditFixedIncome.find('select[id="editFixedIncomeDepositDay"]').val(data.fixedIncome.depositDay.toString()).change();
                }
                else if (data.fixedIncome.depositMonth.toString() === "2") {
                    $('#editFixedIncomeDepositDay option[value=29]').prop('disabled', true).prop('hidden', true);
                    $('#editFixedIncomeDepositDay option[value=30]').prop('disabled', true).prop('hidden', true);
                    $('#editFixedIncomeDepositDay option[value=31]').prop('disabled', true).prop('hidden', true);

                    $("#editFixedIncomeDepositDay option:selected").removeAttr("selected");

                    $('#editFixedIncomeDepositDay option[value=29]').prop('disabled', false).prop('hidden', false);

                    formEditFixedIncome.find('select[id="editFixedIncomeDepositDay"]').val(data.fixedIncome.depositDay.toString()).change();
                }
                else if (data.fixedIncome.depositMonth.toString() === "4" ||
                    data.fixedIncome.depositMonth.toString() === "6" ||
                    data.fixedIncome.depositMonth.toString() === "9" ||
                    data.fixedIncome.depositMonth.toString() === "11"
                ) {
                    $('#editFixedIncomeDepositDay option[value=29]').prop('disabled', true).prop('hidden', true);
                    $('#editFixedIncomeDepositDay option[value=30]').prop('disabled', true).prop('hidden', true);
                    $('#editFixedIncomeDepositDay option[value=31]').prop('disabled', true).prop('hidden', true);

                    $("#editFixedIncomeDepositDay option:selected").removeAttr("selected");

                    $('#editFixedIncomeDepositDay option[value=29]').prop('disabled', false).prop('hidden', false);
                    $('#editFixedIncomeDepositDay option[value=30]').prop('disabled', false).prop('hidden', false);

                    formEditFixedIncome.find('select[id="editFixedIncomeDepositDay"]').val(data.fixedIncome.depositDay.toString()).change();
                }

                formEditFixedIncome.find('input[id="editFixedIncomeMaturityDate"]').val(data.fixedIncome.maturityDate);
                formEditFixedIncome.find('input[id="editFixedIncomeNote"]').val(data.fixedIncome.note);
                formEditFixedIncome.find('select[id="editFixedIncomeDepositMyAssetProductName"]').val(data.fixedIncome.depositMyAssetProductName).change();
                formEditFixedIncome.find('input[id="editFixedIncomeUnpunctuality"]').prop("checked", data.fixedIncome.unpunctuality);

                $('#editFixedIncomeDialogModal').modal({
                    keyboard: false,
                    backdrop: "static"
                });

                $('#editFixedIncomeDialogModal').modal('toggle');
                $('#editFixedIncomeDialogModal').modal('show');
            }
            else {
                toastr.error(data.error);
            }
        }
    });
}

function UpdateFixedIncome() {

    if (!$('#formEditFixedIncome').valid()) {
        return false;
    }

    let createForm = $('#formEditFixedIncome');

    let id = createForm.find('input[id="editFixedIncomeId"]').val();
    let mainClass = createForm.find('select[id="editFixedIncomeMainClass"]').val();
    let subClass = createForm.find('select[id="editFixedIncomeSubClass"]').val();
    let content = createForm.find('input[id="editFixedIncomeContent"]').val();
    let amount = createForm.find('input[id="editFixedIncomeAmount"]').val();
    let depositMonth = createForm.find('select[id="editFixedIncomeDepositMonth"]').val();
    let depositDay = createForm.find('select[id="editFixedIncomeDepositDay"]').val();
    let maturityDate = createForm.find('input[id="editFixedIncomeMaturityDate"]').val();
    let note = createForm.find('input[id="editFixedIncomeNote"]').val();
    let depositMyAssetProductName = createForm.find('select[id="editFixedIncomeDepositMyAssetProductName"]').val();
    let unpunctuality = createForm.find('input[id="editFixedIncomeUnpunctuality"]').is(":checked");

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
        url: '/Notice/UpdateFixedIncome',
        type: 'POST',
        headers: { "RequestVerificationToken": $('input[name="__RequestVerificationToken"]').val() },
        dataType: 'json',
        data: paramValue,
        contentType: 'application/json; charset=utf-8',
        success: function (data) {
            if (data.result) {
                $('#editFixedIncomeDialogModal').modal('hide');

                const grid = new MvcGrid(document.querySelector(".mvc-grid"));
                grid.reload();

                toastr.success(data.message);
            }
            else {
                toastr.error(data.error);
            }
        }
    });

    return false;
}

function ConfirmDeleteFixedIncome(errorMessageSelectGridRow) {

    let selectedRowId = "";

    $('#fixedIncomeGrid > table > tbody  > tr').each(function (index, tr) {
        if ($(this).attr("class").includes(selectedRowColor)) {
            selectedRowId = $(this).attr("data-id");
        }
    });

    if (selectedRowId === "") {
        toastr.error(errorMessageSelectGridRow);
        return false;
    }

    $('#confirmDeleteFixedIncomeDialogModal').modal({
        keyboard: false,
        backdrop: "static"
    });

    $('#confirmDeleteFixedIncomeDialogModal').modal('toggle');
    $('#confirmDeleteFixedIncomeDialogModal').modal('show');
}

function DeleteFixedIncome(errorMessageSelectGridRow) {

    let selectedRowId = "";

    $('#fixedIncomeGrid > table > tbody  > tr').each(function (index, tr) {
        if ($(this).attr("class").includes(selectedRowColor)) {
            selectedRowId = $(this).attr("data-id");
        }
    });

    if (selectedRowId === "") {
        toastr.error(errorMessageSelectGridRow);
        return false;
    }

    $.ajax({
        url: '/Notice/IsFixedIncomeExists' + '?id=' + selectedRowId,
        type: 'POST',
        headers: { "RequestVerificationToken": $('input[name="__RequestVerificationToken"]').val() },
        dataType: 'json',
        data: null,
        contentType: 'application/json; charset=utf-8',
        success: function (data) {
            if (data.result) {

                let paramValue = JSON.stringify({
                    Id: data.fixedIncome.id
                });

                $.ajax({
                    url: '/Notice/DeleteFixedIncome',
                    type: 'POST',
                    headers: { "RequestVerificationToken": $('input[name="__RequestVerificationToken"]').val() },
                    dataType: 'json',
                    data: paramValue,
                    contentType: 'application/json; charset=utf-8',
                    success: function (data) {
                        if (data.result) {
                            $('#confirmDeleteFixedIncomeDialogModal').modal('hide');

                            const grid = new MvcGrid(document.querySelector(".mvc-grid"));
                            grid.reload();

                            toastr.success(data.message);
                        }
                        else {
                            toastr.error(data.error);
                        }
                    }
                });
            }
            else {
                toastr.error(data.error);
            }
        }
    });
}

function ExportExcelFixedIncome() {
    let form = document.createElement("form");
    let element1 = document.createElement("input");
    let element2 = document.createElement("input");

    form.method = "POST";
    form.action = "/Notice/ExportExcelFixedIncome";

    element1.name = "__RequestVerificationToken";
    element1.value = $('input[name="__RequestVerificationToken"]').val();
    form.appendChild(element1);

    element2.name = "fileName";
    element2.value = "FixedIncome";
    form.appendChild(element2);

    document.body.appendChild(form);

    form.submit();
}

function ChangeCreateFixedIncomeAmountLabel(productName) {
    $.ajax({
        url: '/Notice/GetFixedIncomeAmountLabel' + '?productName=' + productName,
        type: 'POST',
        headers: { "RequestVerificationToken": $('input[name="__RequestVerificationToken"]').val() },
        dataType: 'json',
        data: null,
        contentType: 'application/json; charset=utf-8',
        success: function (data) {
            if (data.result) {
                $('#labelCreateFixedIncomeAmount').text(data.label);
            }
            else {
                $('#labelCreateFixedIncomeAmount').text(data.label);
            }
        }
    });
}

function ChangeEditFixedIncomeAmountLabel(productName) {
    $.ajax({
        url: '/Notice/GetFixedIncomeAmountLabel' + '?productName=' + productName,
        type: 'POST',
        headers: { "RequestVerificationToken": $('input[name="__RequestVerificationToken"]').val() },
        dataType: 'json',
        data: null,
        contentType: 'application/json; charset=utf-8',
        success: function (data) {
            if (data.result) {
                $('#labelEditFixedIncomeAmount').text(data.label);
            }
            else {
                $('#labelEditFixedIncomeAmount').text(data.label);
            }
        }
    });
}

$('#btnEditFixedExpenditureGridRow').off('click').on('click', function () {
    EditFixedIncomeGridRow($(this).attr('data-errorMessageSelectGridRow'));
});

$('#btnConfirmDeleteFixedIncome').off('click').on('click', function () {
    ConfirmDeleteFixedIncome($(this).attr('data-errorMessageSelectGridRow'));
});

$('#btnExportExcelFixedIncome').off('click').on('click', function () {
    ExportExcelFixedIncome();
});

$('#btnDeleteFixedIncome').off('click').on('click', function () {
    DeleteFixedIncome($(this).attr('data-errorMessageSelectGridRow'));
});

$('#formCreateFixedIncome').off('submit').on('submit', function () {
    return CreateFixedIncome();
});

$('#formEditFixedIncome').off('submit').on('submit', function () {
    return UpdateFixedIncome();
});

$('#createFixedIncomeMainClass').off('change').on('change', function () {
    return CreateFormShowFixedIncomeSubClassBySelectedFixedIncomeMainClass(this);
});

$('#createFixedIncomeDepositMonth').off('change').on('change', function () {
    return CreateFormShowFixedIncomeDepositDayBySelectedFixedIncomeDepositMonth(this);
});

$('#editFixedIncomeMainClass').off('change').on('change', function () {
    return EditFormShowFixedIncomeSubClassBySelectedFixedIncomeMainClass(this);
});

$('#editFixedIncomeDepositMonth').off('change').on('change', function () {
    return EditFormShowFixedIncomeDepositDayBySelectedFixedIncomeDepositMonth(this);
});

$('#createFixedIncomeDepositMyAssetProductName').off('change').on('change', function () {
    ChangeCreateFixedIncomeAmountLabel(this.value);
});

$('#editFixedIncomeDepositMyAssetProductName').off('change').on('change', function () {
    ChangeEditFixedIncomeAmountLabel(this.value);
});

$(function () {
    ChangeCreateFixedIncomeAmountLabel($('#createFixedIncomeDepositMyAssetProductName option:selected').val());
})