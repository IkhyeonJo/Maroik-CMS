$(function () {
    $("#createMenuTabs").tabs();
    $("#editCategoryTabs").tabs();
    $("#editSubCategoryTabs").tabs();
});

let selectedRowColor = "table-primary";

document.addEventListener("rowclick", e => {
    let selectedRow = e.detail;
    let selectedRowId = selectedRow.data.Id;
    let selectedRowCategoryId = selectedRow.data.CategoryId;

    if (selectedRowCategoryId === "") {
        selectedRowCategoryId = "-1";
    }

    $('#menuGrid > table > tbody  > tr').each(function (index, tr) {
        $(this).removeClass(selectedRowColor);
    });

    $('#menuGrid > table > tbody  > tr').each(function (index, tr) {
        if ((new String(selectedRowId).valueOf() === new String($(this).attr('data-id')).valueOf()) && (new String(selectedRowCategoryId).valueOf() === new String($(this).attr('data-categoryid')).valueOf())) {
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

$(document).on("dblclick", "#menuGrid > table > tbody  > tr", function () {
    $('#menuGrid > table > tbody  > tr').each(function (index, tr) {
        if ($(this).attr("class").includes(selectedRowColor)) {
            EditMenuGridRow();
        }
    });
});
$('#gridSearch').on('input', function () {
    const grid = new MvcGrid(document.querySelector(".mvc-grid"));

    grid.url.searchParams.set("wholeSearch", this.value);

    grid.reload();
});

function CreateCategory() {

    if (!$('#formCreateCategory').valid()) {
        return false;
    }

    let createForm = $('#formCreateCategory');
    let name = createForm.find('input[id="createCategoryName"]').val();
    let displayName = createForm.find('input[id="createCategoryDisplayName"]').val();
    let iconPath = createForm.find('input[id="createCategoryIconPath"]').val();
    let controller = createForm.find('input[id="createCategoryController"]').val();
    let action = createForm.find('input[id="createCategoryAction"]').val();
    let role = createForm.find('select[id="createCategoryRole"]').val();
    let order = createForm.find('input[id="createCategoryOrder"]').val();

    let paramValue = JSON.stringify({
        Name: name,
        DisplayName: displayName,
        IconPath: iconPath,
        Controller: controller,
        Action: action,
        Role: role,
        Order: order
    });

    $.ajax({
        url: '/Management/CreateCategory',
        type: 'POST',
        headers: { "RequestVerificationToken": $('input[name="__RequestVerificationToken"]').val() },
        dataType: 'json',
        data: paramValue,
        contentType: 'application/json; charset=utf-8',
        success: function (data) {
            if (data.result) {
                $('#createMenuDialogModal').modal('hide');

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

function CreateSubCategory() {

    if (!$('#formCreateSubCategory').valid()) {
        return false;
    }

    let createForm = $('#formCreateSubCategory');

    let categoryId = createForm.find('input[id="createSubcategoryCategoryId"]').val();
    let name = createForm.find('input[id="createSubcategoryName"]').val();
    let displayName = createForm.find('input[id="createSubcategoryDisplayName"]').val();
    let iconPath = createForm.find('input[id="createSubcategoryIconPath"]').val();
    let action = createForm.find('input[id="createSubcategoryAction"]').val();
    let role = createForm.find('select[id="createSubcategoryRole"]').val();
    let order = createForm.find('input[id="createSubcategoryOrder"]').val();

    let paramValue = JSON.stringify({
        CategoryId: categoryId,
        Name: name,
        DisplayName: displayName,
        IconPath: iconPath,
        Action: action,
        Role: role,
        Order: order
    });

    $.ajax({
        url: '/Management/CreateSubCategory',
        type: 'POST',
        headers: { "RequestVerificationToken": $('input[name="__RequestVerificationToken"]').val() },
        dataType: 'json',
        data: paramValue,
        contentType: 'application/json; charset=utf-8',
        success: function (data) {
            if (data.result) {
                $('#createMenuDialogModal').modal('hide');

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

function EditMenuGridRow(errorMessageSelectGridRow) {

    let selectedRowId = "-1";
    let selectedRowCategoryId = "-1";

    $('#menuGrid > table > tbody  > tr').each(function (index, tr) {
        if ($(this).attr("class").includes(selectedRowColor)) {
            selectedRowId = $(this).attr("data-id");
            selectedRowCategoryId = $(this).attr("data-categoryid");
        }
    });

    if (selectedRowId === "-1") {
        toastr.error(errorMessageSelectGridRow);
        return false;
    }

    if (selectedRowCategoryId === "-1") {

        $.ajax({
            url: '/Management/IsCategoryExists' + '?id=' + selectedRowId,
            type: 'POST',
            headers: { "RequestVerificationToken": $('input[name="__RequestVerificationToken"]').val() },
            dataType: 'json',
            data: null,
            contentType: 'application/json; charset=utf-8',
            success: function (data) {
                if (data.result) {

                    let editCategoryForm = $('#formEditCategory');

                    editCategoryForm.find('input[id="editCategoryId"]').val(data.category.id);
                    editCategoryForm.find('input[id="editCategoryName"]').val(data.category.name);
                    editCategoryForm.find('input[id="editCategoryDisplayName"]').val(data.category.displayName);
                    editCategoryForm.find('input[id="editCategoryIconPath"]').val(data.category.iconPath);
                    editCategoryForm.find('input[id="editCategoryController"]').val(data.category.controller);
                    editCategoryForm.find('input[id="editCategoryAction"]').val(data.category.action);
                    editCategoryForm.find('select[id="editCategoryRole"]').val(data.category.role).change();
                    editCategoryForm.find('input[id="editCategoryOrder"]').val(data.category.order);

                    $('#editCategoryDialogModal').modal({
                        keyboard: false,
                        backdrop: "static"
                    });

                    $('#editCategoryDialogModal').modal('toggle');
                    $('#editCategoryDialogModal').modal('show');
                }
                else {
                    toastr.error(data.error);
                }
            }
        });
    }
    else {
        $.ajax({
            url: '/Management/IsSubCategoryExists' + '?id=' + selectedRowId,
            type: 'POST',
            headers: { "RequestVerificationToken": $('input[name="__RequestVerificationToken"]').val() },
            dataType: 'json',
            data: null,
            contentType: 'application/json; charset=utf-8',
            success: function (data) {
                if (data.result) {

                    let editCategoryForm = $('#formEditSubCategory');

                    editCategoryForm.find('input[id="editSubCategoryId"]').val(data.subCategory.id);
                    editCategoryForm.find('input[id="editSubCategoryCategoryId"]').val(data.subCategory.categoryId);
                    editCategoryForm.find('input[id="editSubCategoryName"]').val(data.subCategory.name);
                    editCategoryForm.find('input[id="editSubCategoryDisplayName"]').val(data.subCategory.displayName);
                    editCategoryForm.find('input[id="editSubCategoryIconPath"]').val(data.subCategory.iconPath);
                    editCategoryForm.find('input[id="editSubCategoryAction"]').val(data.subCategory.action);
                    editCategoryForm.find('select[id="editSubCategoryRole"]').val(data.subCategory.role).change();
                    editCategoryForm.find('input[id="editSubCategoryOrder"]').val(data.subCategory.order);

                    $('#editSubCategoryDialogModal').modal({
                        keyboard: false,
                        backdrop: "static"
                    });

                    $('#editSubCategoryDialogModal').modal('toggle');
                    $('#editSubCategoryDialogModal').modal('show');
                }
                else {
                    toastr.error(data.error);
                }
            }
        });
    }
}

function UpdateCategory() {
    if (!$('#formEditCategory').valid()) {
        return false;
    }

    let createForm = $('#formEditCategory');

    let id = createForm.find('input[id="editCategoryId"]').val();
    let name = createForm.find('input[id="editCategoryName"]').val();
    let displayName = createForm.find('input[id="editCategoryDisplayName"]').val();
    let iconPath = createForm.find('input[id="editCategoryIconPath"]').val();
    let controller = createForm.find('input[id="editCategoryController"]').val();
    let action = createForm.find('input[id="editCategoryAction"]').val();
    let role = createForm.find('select[id="editCategoryRole"]').val();
    let order = createForm.find('input[id="editCategoryOrder"]').val();

    let paramValue = JSON.stringify({
        Id: id,
        Name: name,
        DisplayName: displayName,
        IconPath: iconPath,
        Controller: controller,
        Action: action,
        Role: role,
        Order: order
    });

    $.ajax({
        url: '/Management/UpdateCategory',
        type: 'POST',
        headers: { "RequestVerificationToken": $('input[name="__RequestVerificationToken"]').val() },
        dataType: 'json',
        data: paramValue,
        contentType: 'application/json; charset=utf-8',
        success: function (data) {
            if (data.result) {
                $('#editCategoryDialogModal').modal('hide');

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

function UpdateSubCategory() {
    if (!$('#formEditSubCategory').valid()) {
        return false;
    }

    let createForm = $('#formEditSubCategory');

    let id = createForm.find('input[id="editSubCategoryId"]').val();
    let categoryId = createForm.find('input[id="editSubCategoryCategoryId"]').val();
    let name = createForm.find('input[id="editSubCategoryName"]').val();
    let displayName = createForm.find('input[id="editSubCategoryDisplayName"]').val();
    let iconPath = createForm.find('input[id="editSubCategoryIconPath"]').val();
    let action = createForm.find('input[id="editSubCategoryAction"]').val();
    let role = createForm.find('select[id="editSubCategoryRole"]').val();
    let order = createForm.find('input[id="editSubCategoryOrder"]').val();

    let paramValue = JSON.stringify({
        Id: id,
        CategoryId: categoryId,
        Name: name,
        DisplayName: displayName,
        IconPath: iconPath,
        Action: action,
        Role: role,
        Order: order
    });

    $.ajax({
        url: '/Management/UpdateSubCategory',
        type: 'POST',
        headers: { "RequestVerificationToken": $('input[name="__RequestVerificationToken"]').val() },
        dataType: 'json',
        data: paramValue,
        contentType: 'application/json; charset=utf-8',
        success: function (data) {
            if (data.result) {
                $('#editSubCategoryDialogModal').modal('hide');

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

function ConfirmDeleteMenu(errorMessageSelectGridRow) {

    let selectedRowId = "-1";

    $('#menuGrid > table > tbody  > tr').each(function (index, tr) {
        if ($(this).attr("class").includes(selectedRowColor)) {
            selectedRowId = $(this).attr("data-id");
        }
    });

    if (selectedRowId === "-1") {
        toastr.error(errorMessageSelectGridRow);
        return false;
    }

    $('#confirmDeleteMenuDialogModal').modal({
        keyboard: false,
        backdrop: "static"
    });

    $('#confirmDeleteMenuDialogModal').modal('toggle');
    $('#confirmDeleteMenuDialogModal').modal('show');
}

function DeleteMenu(errorMessageSelectGridRow) {
    let selectedRowId = "-1";
    let selectedRowCategoryId = "-1";

    $('#menuGrid > table > tbody  > tr').each(function (index, tr) {
        if ($(this).attr("class").includes(selectedRowColor)) {
            selectedRowId = $(this).attr("data-id");
            selectedRowCategoryId = $(this).attr("data-categoryid");
        }
    });

    if (selectedRowId === "-1") {
        toastr.error(errorMessageSelectGridRow);
        return false;
    }

    if (selectedRowCategoryId === "-1") {

        $.ajax({
            url: '/Management/IsCategoryExists' + '?id=' + selectedRowId,
            type: 'POST',
            headers: { "RequestVerificationToken": $('input[name="__RequestVerificationToken"]').val() },
            dataType: 'json',
            data: null,
            contentType: 'application/json; charset=utf-8',
            success: function (data) {
                if (data.result) {

                    let paramValue = JSON.stringify({
                        Id: data.category.id,
                        Name: data.category.name,
                        DisplayName: data.category.displayName,
                        IconPath: data.category.iconPath,
                        Controller: data.category.controller,
                        Action: data.category.action,
                        Role: data.category.role,
                        Order: data.category.order
                    });

                    $.ajax({
                        url: '/Management/DeleteCategory',
                        type: 'POST',
                        headers: { "RequestVerificationToken": $('input[name="__RequestVerificationToken"]').val() },
                        dataType: 'json',
                        data: paramValue,
                        contentType: 'application/json; charset=utf-8',
                        success: function (data) {
                            if (data.result) {
                                $('#confirmDeleteMenuDialogModal').modal('hide');

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
    else {
        $.ajax({
            url: '/Management/IsSubCategoryExists' + '?id=' + selectedRowId,
            type: 'POST',
            headers: { "RequestVerificationToken": $('input[name="__RequestVerificationToken"]').val() },
            dataType: 'json',
            data: null,
            contentType: 'application/json; charset=utf-8',
            success: function (data) {
                if (data.result) {

                    let paramValue = JSON.stringify({
                        Id: data.subCategory.id,
                        CategoryId: data.subCategory.categoryId,
                        Name: data.subCategory.name,
                        DisplayName: data.subCategory.displayName,
                        IconPath: data.subCategory.iconPath,
                        Action: data.subCategory.action,
                        Role: data.subCategory.role,
                        Order: data.subCategory.order
                    });

                    $.ajax({
                        url: '/Management/DeleteSubCategory',
                        type: 'POST',
                        headers: { "RequestVerificationToken": $('input[name="__RequestVerificationToken"]').val() },
                        dataType: 'json',
                        data: paramValue,
                        contentType: 'application/json; charset=utf-8',
                        success: function (data) {
                            if (data.result) {
                                $('#confirmDeleteMenuDialogModal').modal('hide');

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
}

function ExportExcelMenu() {
    let form = document.createElement("form");
    let element1 = document.createElement("input");
    let element2 = document.createElement("input");

    form.method = "POST";
    form.action = "/Management/ExportExcelMenu";

    element1.name = "__RequestVerificationToken";
    element1.value = $('input[name="__RequestVerificationToken"]').val();
    form.appendChild(element1);

    element2.name = "fileName";
    element2.value = "Menu";
    form.appendChild(element2);

    document.body.appendChild(form);

    form.submit();
}

$('#btnEditMenuGridRow').off('click').on('click', function () {
    EditMenuGridRow($(this).attr('data-errorMessageSelectGridRow'));
});

$('#btnConfirmDeleteMenu').off('click').on('click', function () {
    ConfirmDeleteMenu($(this).attr('data-errorMessageSelectGridRow'));
});

$('#btnExportExcelMenu').off('click').on('click', function () {
    ExportExcelMenu();
});

$('#btnDeleteMenu').off('click').on('click', function () {
    DeleteMenu($(this).attr('data-errorMessageSelectGridRow'));
});

$('#formCreateCategory').off('submit').on('submit', function () {
    return CreateCategory();
});

$('#formCreateSubCategory').off('submit').on('submit', function () {
    return CreateSubCategory();
});

$('#formEditCategory').off('submit').on('submit', function () {
    return UpdateCategory();
});

$('#formEditSubCategory').off('submit').on('submit', function () {
    return UpdateSubCategory();
});