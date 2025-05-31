$(function () {
    $("#createAssetTabs").tabs();
    $("#editAssetTabs").tabs();
});

let selectedRowColor = "table-primary";

document.addEventListener("rowclick", e => {
    let selectedRow = e.detail;
    let selectedRowProductName = selectedRow.data.ProductName;

    $('#assetGrid > table > tbody  > tr').each(function (index, tr) {
        $(this).removeClass(selectedRowColor);
    });

    $('#assetGrid > table > tbody  > tr').each(function (index, tr) {
        if ((new String(selectedRowProductName).valueOf() === new String($(this).attr('data-productName')).valueOf())) {
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

$(document).on("dblclick", "#assetGrid > table > tbody  > tr", function () {
    $('#assetGrid > table > tbody  > tr').each(function (index, tr) {

        if ($(this).attr("class").includes(selectedRowColor)) {
            EditAssetGridRow();
        }
    });
});

$('#gridSearch').on('input', function () {
    const grid = new MvcGrid(document.querySelector(".mvc-grid"));

    grid.url.searchParams.set("wholeSearch", this.value);

    grid.reload();
});

function CreateAsset() {

    if (!$('#formCreateAsset').valid()) {
        return false;
    }

    let createForm = $('#formCreateAsset');

    let productName = createForm.find('input[id="createAssetProductName"]').val();
    let item = createForm.find('select[id="createAssetItem"]').val();
    let amount = createForm.find('input[id="createAssetAmount"]').val();
    let monetaryUnit = createForm.find('input[id="createAssetMonetaryUnit"]').val();
    let note = createForm.find('input[id="createAssetNote"]').val();

    let paramValue = JSON.stringify({
        ProductName: productName,
        Item: item,
        Amount: amount,
        MonetaryUnit: monetaryUnit,
        Note: note
    });

    $.ajax({
        url: '/AccountBook/CreateAsset',
        type: 'POST',
        headers: { "RequestVerificationToken": $('input[name="__RequestVerificationToken"]').val() },
        dataType: 'json',
        data: paramValue,
        contentType: 'application/json; charset=utf-8',
        success: function (data) {
            if (data.result) {
                $('#createAssetDialogModal').modal('hide');

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

function EditAssetGridRow(errorMessageSelectGridRow) {

    let selectedRowProductName = "";

    $('#assetGrid > table > tbody  > tr').each(function (index, tr) {
        if ($(this).attr("class").includes(selectedRowColor)) {
            selectedRowProductName = $(this).attr("data-productName");
        }
    });

    if (selectedRowProductName === "") {
        toastr.error(errorMessageSelectGridRow);
        return false;
    }

    $.ajax({
        url: '/AccountBook/IsAssetExists' + '?productName=' + selectedRowProductName,
        type: 'POST',
        headers: { "RequestVerificationToken": $('input[name="__RequestVerificationToken"]').val() },
        dataType: 'json',
        data: null,
        contentType: 'application/json; charset=utf-8',
        success: function (data) {
            if (data.result) {

                let formEditAsset = $('#formEditAsset');

                formEditAsset.find('input[id="editAssetOriginalProductName"]').val(data.asset.productName);
                formEditAsset.find('input[id="editAssetProductName"]').val(data.asset.productName);
                formEditAsset.find('select[id="editAssetItem"]').val(data.asset.item).change();
                formEditAsset.find('input[id="editAssetAmount"]').val(data.asset.amount);
                formEditAsset.find('input[id="editAssetMonetaryUnit"]').val(data.asset.monetaryUnit);
                formEditAsset.find('input[id="editAssetNote"]').val(data.asset.note);
                formEditAsset.find('input[id="editAssetDeleted"]').prop("checked", data.asset.deleted);

                $('#editAssetDialogModal').modal({
                    keyboard: false,
                    backdrop: "static"
                });

                $('#editAssetDialogModal').modal('toggle');
                $('#editAssetDialogModal').modal('show');
            }
            else {
                toastr.error(data.error);
            }
        }
    });
}

function UpdateAsset() {

    if (!$('#formEditAsset').valid()) {
        return false;
    }

    let createForm = $('#formEditAsset');

    let productName = createForm.find('input[id="editAssetProductName"]').val();
    let item = createForm.find('select[id="editAssetItem"]').val();
    let amount = createForm.find('input[id="editAssetAmount"]').val();
    let monetaryUnit = createForm.find('input[id="editAssetMonetaryUnit"]').val();
    let note = createForm.find('input[id="editAssetNote"]').val();
    let deleted = createForm.find('input[id="editAssetDeleted"]').is(":checked");
    let originalProductName = createForm.find('input[id="editAssetOriginalProductName"]').val();

    let paramValue = JSON.stringify({
        ProductName: productName,
        Item: item,
        Amount: amount,
        MonetaryUnit: monetaryUnit,
        Note: note,
        Deleted: deleted,
        OriginalProductName: originalProductName
    });

    $.ajax({
        url: '/AccountBook/UpdateAsset',
        type: 'POST',
        headers: { "RequestVerificationToken": $('input[name="__RequestVerificationToken"]').val() },
        dataType: 'json',
        data: paramValue,
        contentType: 'application/json; charset=utf-8',
        success: function (data) {
            if (data.result) {
                $('#editAssetDialogModal').modal('hide');

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

function ConfirmDeleteAsset(errorMessageSelectGridRow) {

    let selectedRowProductName = "";

    $('#assetGrid > table > tbody  > tr').each(function (index, tr) {
        if ($(this).attr("class").includes(selectedRowColor)) {
            selectedRowProductName = $(this).attr("data-productName");
        }
    });

    if (selectedRowProductName === "") {
        toastr.error(errorMessageSelectGridRow);
        return false;
    }

    $('#confirmDeleteAssetDialogModal').modal({
        keyboard: false,
        backdrop: "static"
    });

    $('#confirmDeleteAssetDialogModal').modal('toggle');
    $('#confirmDeleteAssetDialogModal').modal('show');
}

function DeleteAsset(errorMessageSelectGridRow) {

    let selectedRowProductName = "";

    $('#assetGrid > table > tbody  > tr').each(function (index, tr) {
        if ($(this).attr("class").includes(selectedRowColor)) {
            selectedRowProductName = $(this).attr("data-productName");
        }
    });

    if (selectedRowProductName === "") {
        toastr.error(errorMessageSelectGridRow);
        return false;
    }

    $.ajax({
        url: '/AccountBook/IsAssetExists' + '?productName=' + selectedRowProductName,
        type: 'POST',
        headers: { "RequestVerificationToken": $('input[name="__RequestVerificationToken"]').val() },
        dataType: 'json',
        data: null,
        contentType: 'application/json; charset=utf-8',
        success: function (data) {
            if (data.result) {

                let paramValue = JSON.stringify({
                    ProductName: data.asset.productName
                });

                $.ajax({
                    url: '/AccountBook/DeleteAsset',
                    type: 'POST',
                    headers: { "RequestVerificationToken": $('input[name="__RequestVerificationToken"]').val() },
                    dataType: 'json',
                    data: paramValue,
                    contentType: 'application/json; charset=utf-8',
                    success: function (data) {
                        if (data.result) {
                            $('#confirmDeleteAssetDialogModal').modal('hide');

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

function ExportExcelAsset() {
    let form = document.createElement("form");
    let element1 = document.createElement("input");
    let element2 = document.createElement("input");

    form.method = "POST";
    form.action = "/AccountBook/ExportExcelAsset";

    element1.name = "__RequestVerificationToken";
    element1.value = $('input[name="__RequestVerificationToken"]').val();
    form.appendChild(element1);

    element2.name = "fileName";
    element2.value = "Asset";
    form.appendChild(element2);

    document.body.appendChild(form);

    form.submit();
}

$('#btnEditAssetGridRow').off('click').on('click', function () {
    EditAssetGridRow($(this).attr("data-errorMessageSelectGridRow"));
});

$('#btnConfirmDeleteAsset').off('click').on('click', function () {
    ConfirmDeleteAsset($(this).attr("data-errorMessageSelectGridRow"));
});

$('#btnExportExcelAsset').off('click').on('click', function () {
    ExportExcelAsset();
});

$('#btnDeleteAsset').off('click').on('click', function () {
    DeleteAsset($(this).attr("data-errorMessageSelectGridRow"));
});

$('#formCreateAsset').off('submit').on('submit', function () {
    return CreateAsset();
});

$('#formEditAsset').off('submit').on('submit', function () {
    return UpdateAsset();
});