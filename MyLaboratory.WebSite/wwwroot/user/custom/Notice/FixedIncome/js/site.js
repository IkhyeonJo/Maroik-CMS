// Please see documentation at https://docs.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

var selectedRowColor = "table-primary";

// Triggered when grid's row is clicked. It's recommended to use event delegation in ajax scenarios.
document.addEventListener("rowclick", e => {
    //e.detail.data - clicked row's data from columns.
    //e.detail.grid - grid's instance.
    //e.detail.originalEvent - original tr click event which triggered the rowclick.

    var selectedRow = e.detail;
    var selectedRowId = selectedRow.data.Id;

    $('#fixedIncomeGrid > table > tbody  > tr').each(function (index, tr) {
        $(this).removeClass(selectedRowColor);
    }); // remove all selected row color

    $('#fixedIncomeGrid > table > tbody  > tr').each(function (index, tr) {
        if ((new String(selectedRowId).valueOf() === new String($(this).attr('data-id')).valueOf())) {
            $(this).addClass(selectedRowColor);
        }
    }); // set selected row color class
});

// Triggered before grid starts loading. It's recommended to use event delegation in ajax scenarios.
document.addEventListener("reloadstart", e => {
    //e.detail.grid - grid's instance.
});

// Triggered after grid stop loading. It's recommended to use event delegation in ajax scenarios.
document.addEventListener("reloadend", e => {
    //e.detail.grid - grid's instance.
});

// Triggered after grid reload fails. It's recommended to use event delegation in ajax scenarios.
document.addEventListener("reloadfail", e => {
    //e.detail.reason - failed response promise reason.
    //e.detail.grid - grid's instance.

    // Preventing default stops failed promise from bubbling out.
});

// Triggered after grid configuration changes. It's recommended to use event delegation in ajax scenarios.
document.addEventListener("gridconfigure", e => {
    //e.detail.grid - grid's instance.
});


$(document).on("dblclick", "#fixedIncomeGrid > table > tbody  > tr", function () {
    //console.log('dblclick-start');
    $('#fixedIncomeGrid > table > tbody  > tr').each(function (index, tr) {

        if ($(this).attr("class").includes(selectedRowColor)) { // Get SelectedRow by Color
            EditFixedIncomeGridRow();
        }
    });
    //console.log('dblclick-end');
});
$('#gridSearch').on('input', function () {
    const grid = new MvcGrid(document.querySelector(".mvc-grid"));

    grid.url.searchParams.set("wholeSearch", this.value);

    grid.reload();
});

function CreateFormShowFixedIncomeSubClassBySelectedFixedIncomeMainClass(createFixedIncomeMainClass) {

    if (createFixedIncomeMainClass.value == "RegularIncome") {
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
    else if (createFixedIncomeMainClass.value == "IrregularIncome") {
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

    if (createFixedIncomeDepositMonth.value == "1" ||
        createFixedIncomeDepositMonth.value == "3" ||
        createFixedIncomeDepositMonth.value == "5" ||
        createFixedIncomeDepositMonth.value == "7" ||
        createFixedIncomeDepositMonth.value == "8" ||
        createFixedIncomeDepositMonth.value == "10" ||
        createFixedIncomeDepositMonth.value == "12"
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
    else if (createFixedIncomeDepositMonth.value == "2") {
        $('#createFixedIncomeDepositDay option[value=29]').prop('disabled', true).prop('hidden', true);
        $('#createFixedIncomeDepositDay option[value=30]').prop('disabled', true).prop('hidden', true);
        $('#createFixedIncomeDepositDay option[value=31]').prop('disabled', true).prop('hidden', true);

        $("#createFixedIncomeDepositDay option:selected").removeAttr("selected");

        $('#createFixedIncomeDepositDay option[value=1]').attr('selected', 'selected');
        $('#createFixedIncomeDepositDay option[value=29]').prop('disabled', false).prop('hidden', false);

        $("#createFixedIncomeDepositDay").val("1").change();
    }
    else if (createFixedIncomeDepositMonth.value == "4" ||
        createFixedIncomeDepositMonth.value == "6" ||
        createFixedIncomeDepositMonth.value == "9" ||
        createFixedIncomeDepositMonth.value == "11"
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

    if (editFixedIncomeMainClass.value == "RegularIncome") {
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
    else if (editFixedIncomeMainClass.value == "IrregularIncome") {
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

    if (editFixedIncomeDepositMonth.value == "1" ||
        editFixedIncomeDepositMonth.value == "3" ||
        editFixedIncomeDepositMonth.value == "5" ||
        editFixedIncomeDepositMonth.value == "7" ||
        editFixedIncomeDepositMonth.value == "8" ||
        editFixedIncomeDepositMonth.value == "10" ||
        editFixedIncomeDepositMonth.value == "12"
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
    else if (editFixedIncomeDepositMonth.value == "2") {
        $('#editFixedIncomeDepositDay option[value=29]').prop('disabled', true).prop('hidden', true);
        $('#editFixedIncomeDepositDay option[value=30]').prop('disabled', true).prop('hidden', true);
        $('#editFixedIncomeDepositDay option[value=31]').prop('disabled', true).prop('hidden', true);

        $("#editFixedIncomeDepositDay option:selected").removeAttr("selected");

        $('#editFixedIncomeDepositDay option[value=1]').attr('selected', 'selected');
        $('#editFixedIncomeDepositDay option[value=29]').prop('disabled', false).prop('hidden', false);

        $("#editFixedIncomeDepositDay").val("1").change();
    }
    else if (editFixedIncomeDepositMonth.value == "4" ||
        editFixedIncomeDepositMonth.value == "6" ||
        editFixedIncomeDepositMonth.value == "9" ||
        editFixedIncomeDepositMonth.value == "11"
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

    var createForm = $('#formCreateFixedIncome');

    var mainClass = createForm.find('select[id="createFixedIncomeMainClass"]').val();
    var subClass = createForm.find('select[id="createFixedIncomeSubClass"]').val();
    var contents = createForm.find('input[id="createFixedIncomeContents"]').val();
    var amount = createForm.find('input[id="createFixedIncomeAmount"]').val();
    var depositMonth = createForm.find('select[id="createFixedIncomeDepositMonth"]').val();
    var depositDay = createForm.find('select[id="createFixedIncomeDepositDay"]').val();
    var maturityDate = createForm.find('input[id="createFixedIncomeMaturityDate"]').val();
    var note = createForm.find('input[id="createFixedIncomeNote"]').val();
    var depositMyAssetProductName = createForm.find('select[id="createFixedIncomeDepositMyAssetProductName"]').val();

    var paramValue = JSON.stringify({
        MainClass: mainClass,
        SubClass: subClass,
        Contents: contents,
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
        //async: false,
        //cache: false,
        success: function (data) {
            if (data.result) {
                $('#createFixedIncomeDialogModal').modal('hide'); // hide Modal

                const grid = new MvcGrid(document.querySelector(".mvc-grid")); //reload grid
                grid.reload(); //reload grid

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

    var selectedRowId = "";

    $('#fixedIncomeGrid > table > tbody  > tr').each(function (index, tr) {
        if ($(this).attr("class").includes(selectedRowColor)) { // Get SelectedRow by Color
            selectedRowId = $(this).attr("data-id");
        }
    }); // Search Selected Row

    if (selectedRowId === "") {
        toastr.error(errorMessageSelectGridRow);
        return false;
    } // check whether selected row exist or not

    $.ajax({
        url: '/Notice/IsFixedIncomeExists' + '?id=' + selectedRowId,
        type: 'POST',
        headers: { "RequestVerificationToken": $('input[name="__RequestVerificationToken"]').val() },
        dataType: 'json',
        data: null,
        contentType: 'application/json; charset=utf-8',
        //async: false,
        //cache: false,
        success: function (data) {
            if (data.result) {

                var formEditFixedIncome = $('#formEditFixedIncome');

                formEditFixedIncome.find('input[id="editFixedIncomeId"]').val(data.fixedIncome.id);
                formEditFixedIncome.find('select[id="editFixedIncomeMainClass"]').val(data.fixedIncome.mainClass).change();

                if (data.fixedIncome.mainClass == "RegularIncome") {
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
                else if (data.fixedIncome.mainClass == "IrregularIncome") {
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

                formEditFixedIncome.find('input[id="editFixedIncomeContents"]').val(data.fixedIncome.contents);
                formEditFixedIncome.find('input[id="editFixedIncomeAmount"]').val(data.fixedIncome.amount);

                formEditFixedIncome.find('select[id="editFixedIncomeDepositMonth"]').val(data.fixedIncome.depositMonth.toString()).change();

                if (data.fixedIncome.depositMonth.toString() == "1" ||
                    data.fixedIncome.depositMonth.toString() == "3" ||
                    data.fixedIncome.depositMonth.toString() == "5" ||
                    data.fixedIncome.depositMonth.toString() == "7" ||
                    data.fixedIncome.depositMonth.toString() == "8" ||
                    data.fixedIncome.depositMonth.toString() == "10" ||
                    data.fixedIncome.depositMonth.toString() == "12"
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
                else if (data.fixedIncome.depositMonth.toString() == "2") {
                    $('#editFixedIncomeDepositDay option[value=29]').prop('disabled', true).prop('hidden', true);
                    $('#editFixedIncomeDepositDay option[value=30]').prop('disabled', true).prop('hidden', true);
                    $('#editFixedIncomeDepositDay option[value=31]').prop('disabled', true).prop('hidden', true);

                    $("#editFixedIncomeDepositDay option:selected").removeAttr("selected");

                    $('#editFixedIncomeDepositDay option[value=29]').prop('disabled', false).prop('hidden', false);

                    formEditFixedIncome.find('select[id="editFixedIncomeDepositDay"]').val(data.fixedIncome.depositDay.toString()).change();
                }
                else if (data.fixedIncome.depositMonth.toString() == "4" ||
                    data.fixedIncome.depositMonth.toString() == "6" ||
                    data.fixedIncome.depositMonth.toString() == "9" ||
                    data.fixedIncome.depositMonth.toString() == "11"
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

    var createForm = $('#formEditFixedIncome');

    var id = createForm.find('input[id="editFixedIncomeId"]').val();
    var mainClass = createForm.find('select[id="editFixedIncomeMainClass"]').val();
    var subClass = createForm.find('select[id="editFixedIncomeSubClass"]').val();
    var contents = createForm.find('input[id="editFixedIncomeContents"]').val();
    var amount = createForm.find('input[id="editFixedIncomeAmount"]').val();
    var depositMonth = createForm.find('select[id="editFixedIncomeDepositMonth"]').val();
    var depositDay = createForm.find('select[id="editFixedIncomeDepositDay"]').val();
    var maturityDate = createForm.find('input[id="editFixedIncomeMaturityDate"]').val();
    var note = createForm.find('input[id="editFixedIncomeNote"]').val();
    var depositMyAssetProductName = createForm.find('select[id="editFixedIncomeDepositMyAssetProductName"]').val();

    var paramValue = JSON.stringify({
        Id: id,
        MainClass: mainClass,
        SubClass: subClass,
        Contents: contents,
        Amount: amount,
        DepositMonth: depositMonth,
        DepositDay: depositDay,
        MaturityDate: maturityDate,
        Note: note,
        DepositMyAssetProductName: depositMyAssetProductName
    });

    $.ajax({
        url: '/Notice/UpdateFixedIncome',
        type: 'POST',
        headers: { "RequestVerificationToken": $('input[name="__RequestVerificationToken"]').val() },
        dataType: 'json',
        data: paramValue,
        contentType: 'application/json; charset=utf-8',
        //async: false,
        //cache: false,
        success: function (data) {
            if (data.result) {
                $('#editFixedIncomeDialogModal').modal('hide'); // hide Modal

                const grid = new MvcGrid(document.querySelector(".mvc-grid")); //reload grid
                grid.reload(); //reload grid

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

    var selectedRowId = "";

    $('#fixedIncomeGrid > table > tbody  > tr').each(function (index, tr) {
        if ($(this).attr("class").includes(selectedRowColor)) { // Get SelectedRow by Color
            selectedRowId = $(this).attr("data-id");
        }
    }); // Search Selected Row

    if (selectedRowId === "") {
        toastr.error(errorMessageSelectGridRow);
        return false;
    } // check whether selected row exist or not

    $('#confirmDeleteFixedIncomeDialogModal').modal({
        keyboard: false,
        backdrop: "static"
    });

    $('#confirmDeleteFixedIncomeDialogModal').modal('toggle');
    $('#confirmDeleteFixedIncomeDialogModal').modal('show');
}

function DeleteFixedIncome(errorMessageSelectGridRow) {

    var selectedRowId = "";

    $('#fixedIncomeGrid > table > tbody  > tr').each(function (index, tr) {
        if ($(this).attr("class").includes(selectedRowColor)) { // Get SelectedRow by Color
            selectedRowId = $(this).attr("data-id");
        }
    }); // Search Selected Row

    if (selectedRowId === "") {
        toastr.error(errorMessageSelectGridRow);
        return false;
    } // check whether selected row exist or not

    $.ajax({
        url: '/Notice/IsFixedIncomeExists' + '?id=' + selectedRowId,
        type: 'POST',
        headers: { "RequestVerificationToken": $('input[name="__RequestVerificationToken"]').val() },
        dataType: 'json',
        data: null,
        contentType: 'application/json; charset=utf-8',
        //async: false,
        //cache: false,
        success: function (data) {
            if (data.result) {

                var paramValue = JSON.stringify({
                    Id: data.fixedIncome.id
                });

                $.ajax({
                    url: '/Notice/DeleteFixedIncome',
                    type: 'POST',
                    headers: { "RequestVerificationToken": $('input[name="__RequestVerificationToken"]').val() },
                    dataType: 'json',
                    data: paramValue,
                    contentType: 'application/json; charset=utf-8',
                    //async: false,
                    //cache: false,
                    success: function (data) {
                        if (data.result) {
                            $('#confirmDeleteFixedIncomeDialogModal').modal('hide'); // hide Modal

                            const grid = new MvcGrid(document.querySelector(".mvc-grid")); //reload grid
                            grid.reload(); //reload grid

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
    var form = document.createElement("form");
    var element1 = document.createElement("input");
    var element2 = document.createElement("input");

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