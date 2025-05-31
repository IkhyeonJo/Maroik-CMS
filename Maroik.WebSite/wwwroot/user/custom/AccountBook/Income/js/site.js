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
    YearSuffix: $('#localizerYearSuffix').val()
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
    $("#createIncomeTabs").tabs();
    $("#editIncomeTabs").tabs();
    $("#createIncomeDate").datepicker({
        showButtonPanel: true,
        beforeShow: function (input) {
            setTimeout(function () {

            }, 1);
        },
        onChangeMonthYear: function (year, month, instance) {
            setTimeout(function () {

            }, 1);
        }
    });

    $("#editIncomeDate").datepicker({
        showButtonPanel: true,
        beforeShow: function (input) {
            setTimeout(function () {

            }, 1);
        },
        onChangeMonthYear: function (year, month, instance) {
            setTimeout(function () {

            }, 1);
        }
    });

    $('<style type="text/css"> .ui-datepicker-close { display: none; } </style>').appendTo("head");
    $('<style type="text/css"> .ui-datepicker-current { display: none; } </style>').appendTo("head");
});

let selectedRowColor = "table-primary";

document.addEventListener("rowclick", e => {
    let selectedRow = e.detail;
    let selectedRowId = selectedRow.data.Id;

    $('#incomeGrid > table > tbody  > tr').each(function (index, tr) {
        $(this).removeClass(selectedRowColor);
    });

    $('#incomeGrid > table > tbody  > tr').each(function (index, tr) {
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


$(document).on("dblclick", "#incomeGrid > table > tbody  > tr", function () {
    $('#incomeGrid > table > tbody  > tr').each(function (index, tr) {

        if ($(this).attr("class").includes(selectedRowColor)) {
            EditIncomeGridRow();
        }
    });
});
$('#gridSearch').on('input', function () {
    const grid = new MvcGrid(document.querySelector(".mvc-grid"));

    grid.url.searchParams.set("wholeSearch", this.value);

    grid.reload();
});

function CreateFormShowIncomeSubClassBySelectedIncomeMainClass(createIncomeMainClass) {

    if (createIncomeMainClass.value === "RegularIncome") {
        $('#createIncomeSubClass option[value=LaborIncome]').prop('disabled', true).prop('hidden', true);
        $('#createIncomeSubClass option[value=BusinessIncome]').prop('disabled', true).prop('hidden', true);
        $('#createIncomeSubClass option[value=PensionIncome]').prop('disabled', true).prop('hidden', true);
        $('#createIncomeSubClass option[value=FinancialIncome]').prop('disabled', true).prop('hidden', true);
        $('#createIncomeSubClass option[value=RentalIncome]').prop('disabled', true).prop('hidden', true);
        $('#createIncomeSubClass option[value=OtherIncome]').prop('disabled', true).prop('hidden', true);

        $("#createIncomeSubClass option:selected").removeAttr("selected");

        $('#createIncomeSubClass option[value=LaborIncome]').prop('disabled', false).prop('hidden', false).attr('selected', 'selected');
        $('#createIncomeSubClass option[value=BusinessIncome]').prop('disabled', false).prop('hidden', false);
        $('#createIncomeSubClass option[value=PensionIncome]').prop('disabled', false).prop('hidden', false);
        $('#createIncomeSubClass option[value=FinancialIncome]').prop('disabled', false).prop('hidden', false);
        $('#createIncomeSubClass option[value=RentalIncome]').prop('disabled', false).prop('hidden', false);
        $('#createIncomeSubClass option[value=OtherIncome]').prop('disabled', false).prop('hidden', false);

        $("#createIncomeSubClass").val("LaborIncome").change();
    }
    else if (createIncomeMainClass.value === "IrregularIncome") {
        $('#createIncomeSubClass option[value=LaborIncome]').prop('disabled', true).prop('hidden', true);
        $('#createIncomeSubClass option[value=BusinessIncome]').prop('disabled', true).prop('hidden', true);
        $('#createIncomeSubClass option[value=PensionIncome]').prop('disabled', true).prop('hidden', true);
        $('#createIncomeSubClass option[value=FinancialIncome]').prop('disabled', true).prop('hidden', true);
        $('#createIncomeSubClass option[value=RentalIncome]').prop('disabled', true).prop('hidden', true);
        $('#createIncomeSubClass option[value=OtherIncome]').prop('disabled', true).prop('hidden', true);

        $("#createIncomeSubClass option:selected").removeAttr("selected");

        $('#createIncomeSubClass option[value=LaborIncome]').prop('disabled', false).prop('hidden', false).attr('selected', 'selected');
        $('#createIncomeSubClass option[value=OtherIncome]').prop('disabled', false).prop('hidden', false);

        $("#createIncomeSubClass").val("LaborIncome").change();
    }
}

function EditFormShowIncomeSubClassBySelectedIncomeMainClass(createIncomeMainClass) {

    if (createIncomeMainClass.value === "RegularIncome") {
        $('#editIncomeSubClass option[value=LaborIncome]').prop('disabled', true).prop('hidden', true);
        $('#editIncomeSubClass option[value=BusinessIncome]').prop('disabled', true).prop('hidden', true);
        $('#editIncomeSubClass option[value=PensionIncome]').prop('disabled', true).prop('hidden', true);
        $('#editIncomeSubClass option[value=FinancialIncome]').prop('disabled', true).prop('hidden', true);
        $('#editIncomeSubClass option[value=RentalIncome]').prop('disabled', true).prop('hidden', true);
        $('#editIncomeSubClass option[value=OtherIncome]').prop('disabled', true).prop('hidden', true);

        $("#editIncomeSubClass option:selected").removeAttr("selected");

        $('#editIncomeSubClass option[value=LaborIncome]').prop('disabled', false).prop('hidden', false).attr('selected', 'selected');
        $('#editIncomeSubClass option[value=BusinessIncome]').prop('disabled', false).prop('hidden', false);
        $('#editIncomeSubClass option[value=PensionIncome]').prop('disabled', false).prop('hidden', false);
        $('#editIncomeSubClass option[value=FinancialIncome]').prop('disabled', false).prop('hidden', false);
        $('#editIncomeSubClass option[value=RentalIncome]').prop('disabled', false).prop('hidden', false);
        $('#editIncomeSubClass option[value=OtherIncome]').prop('disabled', false).prop('hidden', false);

        $("#editIncomeSubClass").val("LaborIncome").change();
    }
    else if (createIncomeMainClass.value === "IrregularIncome") {
        $('#editIncomeSubClass option[value=LaborIncome]').prop('disabled', true).prop('hidden', true);
        $('#editIncomeSubClass option[value=BusinessIncome]').prop('disabled', true).prop('hidden', true);
        $('#editIncomeSubClass option[value=PensionIncome]').prop('disabled', true).prop('hidden', true);
        $('#editIncomeSubClass option[value=FinancialIncome]').prop('disabled', true).prop('hidden', true);
        $('#editIncomeSubClass option[value=RentalIncome]').prop('disabled', true).prop('hidden', true);
        $('#editIncomeSubClass option[value=OtherIncome]').prop('disabled', true).prop('hidden', true);

        $("#editIncomeSubClass option:selected").removeAttr("selected");

        $('#editIncomeSubClass option[value=LaborIncome]').prop('disabled', false).prop('hidden', false).attr('selected', 'selected');
        $('#editIncomeSubClass option[value=OtherIncome]').prop('disabled', false).prop('hidden', false);

        $("#editIncomeSubClass").val("LaborIncome").change();
    }
}

function CreateIncome() {

    if (!$('#formCreateIncome').valid()) {
        return false;
    }

    let createForm = $('#formCreateIncome');

    let mainClass = createForm.find('select[id="createIncomeMainClass"]').val();
    let subClass = createForm.find('select[id="createIncomeSubClass"]').val();
    let content = createForm.find('input[id="createIncomeContent"]').val();
    let amount = createForm.find('input[id="createIncomeAmount"]').val();

    let year = parseInt(createForm.find('input[id="createIncomeDate"]').val().substring(0, 4));
    let month = parseInt(createForm.find('input[id="createIncomeDate"]').val().substring(5, 7));
    let day = parseInt(createForm.find('input[id="createIncomeDate"]').val().substring(8, 10));
    let hour = parseInt(createForm.find('select[id="createIncomeHour"]').val());
    let minute = parseInt(createForm.find('select[id="createIncomeMinute"]').val());
    let second = parseInt(createForm.find('select[id="createIncomeSecond"]').val());

    let created = new Date(year, month - 1, day, hour, minute, second).toISOString();
    let depositMyAssetProductName = createForm.find('select[id="createIncomeDepositMyAssetProductName"]').val();
    let note = createForm.find('input[id="createIncomeNote"]').val();

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
        url: '/AccountBook/CreateIncome',
        type: 'POST',
        headers: { "RequestVerificationToken": $('input[name="__RequestVerificationToken"]').val() },
        dataType: 'json',
        data: paramValue,
        contentType: 'application/json; charset=utf-8',
        success: function (data) {
            if (data.result) {
                $('#createIncomeDialogModal').modal('hide');

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

function EditIncomeGridRow(errorMessageSelectGridRow) {

    let selectedRowId = "";

    $('#incomeGrid > table > tbody  > tr').each(function (index, tr) {
        if ($(this).attr("class").includes(selectedRowColor)) {
            selectedRowId = $(this).attr("data-id");
        }
    });

    if (selectedRowId === "") {
        toastr.error(errorMessageSelectGridRow);
        return false;
    }

    $.ajax({
        url: '/AccountBook/IsIncomeExists' + '?id=' + selectedRowId,
        type: 'POST',
        headers: { "RequestVerificationToken": $('input[name="__RequestVerificationToken"]').val() },
        dataType: 'json',
        data: null,
        contentType: 'application/json; charset=utf-8',
        success: function (data) {
            if (data.result) {

                let formEditIncome = $('#formEditIncome');

                formEditIncome.find('input[id="editIncomeId"]').val(data.income.id);
                formEditIncome.find('select[id="editIncomeMainClass"]').val(data.income.mainClass).change();

                if (data.income.mainClass === "RegularIncome") {
                    $('#editIncomeSubClass option[value=LaborIncome]').prop('disabled', true).prop('hidden', true);
                    $('#editIncomeSubClass option[value=BusinessIncome]').prop('disabled', true).prop('hidden', true);
                    $('#editIncomeSubClass option[value=PensionIncome]').prop('disabled', true).prop('hidden', true);
                    $('#editIncomeSubClass option[value=FinancialIncome]').prop('disabled', true).prop('hidden', true);
                    $('#editIncomeSubClass option[value=RentalIncome]').prop('disabled', true).prop('hidden', true);
                    $('#editIncomeSubClass option[value=OtherIncome]').prop('disabled', true).prop('hidden', true);

                    $("#editIncomeSubClass option:selected").removeAttr("selected");

                    $('#editIncomeSubClass option[value=LaborIncome]').prop('disabled', false).prop('hidden', false);
                    $('#editIncomeSubClass option[value=BusinessIncome]').prop('disabled', false).prop('hidden', false);
                    $('#editIncomeSubClass option[value=PensionIncome]').prop('disabled', false).prop('hidden', false);
                    $('#editIncomeSubClass option[value=FinancialIncome]').prop('disabled', false).prop('hidden', false);
                    $('#editIncomeSubClass option[value=RentalIncome]').prop('disabled', false).prop('hidden', false);
                    $('#editIncomeSubClass option[value=OtherIncome]').prop('disabled', false).prop('hidden', false);

                    formEditIncome.find('select[id="editIncomeSubClass"]').val(data.income.subClass).change();
                }
                else if (data.income.mainClass === "IrregularIncome") {
                    $('#editIncomeSubClass option[value=LaborIncome]').prop('disabled', true).prop('hidden', true);
                    $('#editIncomeSubClass option[value=BusinessIncome]').prop('disabled', true).prop('hidden', true);
                    $('#editIncomeSubClass option[value=PensionIncome]').prop('disabled', true).prop('hidden', true);
                    $('#editIncomeSubClass option[value=FinancialIncome]').prop('disabled', true).prop('hidden', true);
                    $('#editIncomeSubClass option[value=RentalIncome]').prop('disabled', true).prop('hidden', true);
                    $('#editIncomeSubClass option[value=OtherIncome]').prop('disabled', true).prop('hidden', true);

                    $("#editIncomeSubClass option:selected").removeAttr("selected");

                    $('#editIncomeSubClass option[value=LaborIncome]').prop('disabled', false).prop('hidden', false);
                    $('#editIncomeSubClass option[value=OtherIncome]').prop('disabled', false).prop('hidden', false);

                    formEditIncome.find('select[id="editIncomeSubClass"]').val(data.income.subClass).change();
                }

                formEditIncome.find('input[id="editIncomeContent"]').val(data.income.content);
                formEditIncome.find('input[id="editIncomeAmount"]').val(data.income.amount);
                formEditIncome.find('input[id="editIncomeDate"]').val(data.income.created.split('T')[0]);
                formEditIncome.find('select[id="editIncomeHour"]').val(parseInt(data.income.created.split('T')[1].substring(0, 2))).change();
                formEditIncome.find('select[id="editIncomeMinute"]').val(parseInt(data.income.created.split('T')[1].substring(3, 5))).change();
                formEditIncome.find('select[id="editIncomeSecond"]').val(parseInt(data.income.created.split('T')[1].substring(6, 8))).change();
                formEditIncome.find('select[id="editIncomeDepositMyAssetProductName"]').val(data.income.depositMyAssetProductName).change();
                formEditIncome.find('input[id="editIncomeNote"]').val(data.income.note);

                $('#editIncomeDialogModal').modal({
                    keyboard: false,
                    backdrop: "static"
                });

                $('#editIncomeDialogModal').modal('toggle');
                $('#editIncomeDialogModal').modal('show');
            }
            else {
                toastr.error(data.error);
            }
        }
    });
}

function UpdateIncome() {

    if (!$('#formEditIncome').valid()) {
        return false;
    }

    let createForm = $('#formEditIncome');

    let id = createForm.find('input[id="editIncomeId"]').val();
    let mainClass = createForm.find('select[id="editIncomeMainClass"]').val();
    let subClass = createForm.find('select[id="editIncomeSubClass"]').val();
    let content = createForm.find('input[id="editIncomeContent"]').val();
    let amount = createForm.find('input[id="editIncomeAmount"]').val();

    let year = parseInt(createForm.find('input[id="editIncomeDate"]').val().substring(0, 4));
    let month = parseInt(createForm.find('input[id="editIncomeDate"]').val().substring(5, 7));
    let day = parseInt(createForm.find('input[id="editIncomeDate"]').val().substring(8, 10));
    let hour = parseInt(createForm.find('select[id="editIncomeHour"]').val());
    let minute = parseInt(createForm.find('select[id="editIncomeMinute"]').val());
    let second = parseInt(createForm.find('select[id="editIncomeSecond"]').val());

    let created = new Date(year, month - 1, day, hour, minute, second).toISOString();
    let depositMyAssetProductName = createForm.find('select[id="editIncomeDepositMyAssetProductName"]').val();
    let note = createForm.find('input[id="editIncomeNote"]').val();

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
        url: '/AccountBook/UpdateIncome',
        type: 'POST',
        headers: { "RequestVerificationToken": $('input[name="__RequestVerificationToken"]').val() },
        dataType: 'json',
        data: paramValue,
        contentType: 'application/json; charset=utf-8',
        success: function (data) {
            if (data.result) {
                $('#editIncomeDialogModal').modal('hide');

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

function ConfirmDeleteIncome(errorMessageSelectGridRow) {

    let selectedRowId = "";

    $('#incomeGrid > table > tbody  > tr').each(function (index, tr) {
        if ($(this).attr("class").includes(selectedRowColor)) {
            selectedRowId = $(this).attr("data-id");
        }
    });

    if (selectedRowId === "") {
        toastr.error(errorMessageSelectGridRow);
        return false;
    }

    $('#confirmDeleteIncomeDialogModal').modal({
        keyboard: false,
        backdrop: "static"
    });

    $('#confirmDeleteIncomeDialogModal').modal('toggle');
    $('#confirmDeleteIncomeDialogModal').modal('show');
}

function DeleteIncome(errorMessageSelectGridRow) {

    let selectedRowId = "";

    $('#incomeGrid > table > tbody  > tr').each(function (index, tr) {
        if ($(this).attr("class").includes(selectedRowColor)) {
            selectedRowId = $(this).attr("data-id");
        }
    });

    if (selectedRowId === "") {
        toastr.error(errorMessageSelectGridRow);
        return false;
    }

    $.ajax({
        url: '/AccountBook/IsIncomeExists' + '?id=' + selectedRowId,
        type: 'POST',
        headers: { "RequestVerificationToken": $('input[name="__RequestVerificationToken"]').val() },
        dataType: 'json',
        data: null,
        contentType: 'application/json; charset=utf-8',
        success: function (data) {
            if (data.result) {

                let paramValue = JSON.stringify({
                    Id: data.income.id
                });

                $.ajax({
                    url: '/AccountBook/DeleteIncome',
                    type: 'POST',
                    headers: { "RequestVerificationToken": $('input[name="__RequestVerificationToken"]').val() },
                    dataType: 'json',
                    data: paramValue,
                    contentType: 'application/json; charset=utf-8',
                    success: function (data) {
                        if (data.result) {
                            $('#confirmDeleteIncomeDialogModal').modal('hide');

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

function ExportExcelIncome() {
    let form = document.createElement("form");
    let element1 = document.createElement("input");
    let element2 = document.createElement("input");

    form.method = "POST";
    form.action = "/AccountBook/ExportExcelIncome";

    element1.name = "__RequestVerificationToken";
    element1.value = $('input[name="__RequestVerificationToken"]').val();
    form.appendChild(element1);

    element2.name = "fileName";
    element2.value = "Income";
    form.appendChild(element2);

    document.body.appendChild(form);

    form.submit();
}

function ChangeCreateIncomeAmountLabel(productName) {
    $.ajax({
        url: '/AccountBook/GetIncomeAmountLabel' + '?productName=' + productName,
        type: 'POST',
        headers: { "RequestVerificationToken": $('input[name="__RequestVerificationToken"]').val() },
        dataType: 'json',
        data: null,
        contentType: 'application/json; charset=utf-8',
        success: function (data) {
            if (data.result) {
                $('#labelCreateIncomeAmount').text(data.label);
            }
            else {
                $('#labelCreateIncomeAmount').text(data.label);
            }
        }
    });
}

function ChangeEditIncomeAmountLabel(productName) {
    $.ajax({
        url: '/AccountBook/GetIncomeAmountLabel' + '?productName=' + productName,
        type: 'POST',
        headers: { "RequestVerificationToken": $('input[name="__RequestVerificationToken"]').val() },
        dataType: 'json',
        data: null,
        contentType: 'application/json; charset=utf-8',
        success: function (data) {
            if (data.result) {
                $('#labelEditIncomeAmount').text(data.label);
            }
            else {
                $('#labelEditIncomeAmount').text(data.label);
            }
        }
    });
}

$('#btnEditIncomeGridRow').off('click').on('click', function () {
    EditIncomeGridRow($(this).attr("data-errorMessageSelectGridRow"));
});

$('#btnConfirmDeleteIncome').off('click').on('click', function () {
    ConfirmDeleteIncome($(this).attr("data-errorMessageSelectGridRow"));
});

$('#btnExportExcelIncome').off('click').on('click', function () {
    ExportExcelIncome();
});

$('#btnDeleteIncome').off('click').on('click', function () {
    DeleteIncome($(this).attr("data-errorMessageSelectGridRow"));
});

$('#formCreateIncome').off('submit').on('submit', function () {
    return CreateIncome();
});

$('#formEditIncome').off('submit').on('submit', function () {
    return UpdateIncome();
});

$('#createIncomeMainClass').off('change').on('change', function () {
    CreateFormShowIncomeSubClassBySelectedIncomeMainClass(this);
});

$('#editIncomeMainClass').off('change').on('change', function () {
    EditFormShowIncomeSubClassBySelectedIncomeMainClass(this);
});

$('#createIncomeDepositMyAssetProductName').off('change').on('change', function () {
    ChangeCreateIncomeAmountLabel(this.value);
});

$('#editIncomeDepositMyAssetProductName').off('change').on('change', function () {
    ChangeEditIncomeAmountLabel(this.value);
});

$(function () {
    ChangeCreateIncomeAmountLabel($('#createIncomeDepositMyAssetProductName option:selected').val());
})