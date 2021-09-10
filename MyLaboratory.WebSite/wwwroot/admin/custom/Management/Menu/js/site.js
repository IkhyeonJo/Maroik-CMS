// Please see documentation at https://docs.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.
$(function () {
    $("#createMenuTabs").tabs();
    $("#editCategoryTabs").tabs();
    $("#editSubCategoryTabs").tabs();
});

var selectedRowColor = "table-primary";

// Triggered when grid's row is clicked. It's recommended to use event delegation in ajax scenarios.
document.addEventListener("rowclick", e => {
    //e.detail.data - clicked row's data from columns.
    //e.detail.grid - grid's instance.
    //e.detail.originalEvent - original tr click event which triggered the rowclick.

    var selectedRow = e.detail;
    var selectedRowId = selectedRow.data.Id;
    var selectedRowCategoryId = selectedRow.data.CategoryId;

    if (selectedRowCategoryId === "") {
        selectedRowCategoryId = "-1";
    }

    $('#menuGrid > table > tbody  > tr').each(function (index, tr) {
        $(this).removeClass(selectedRowColor);
    }); // remove all selected row color

    $('#menuGrid > table > tbody  > tr').each(function (index, tr) {
        if ((new String(selectedRowId).valueOf() === new String($(this).attr('data-id')).valueOf()) && (new String(selectedRowCategoryId).valueOf() === new String($(this).attr('data-categoryid')).valueOf())) {
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


$(document).on("dblclick", "#menuGrid > table > tbody  > tr", function () {
    //console.log('dblclick-start');
    $('#menuGrid > table > tbody  > tr').each(function (index, tr) {

        if ($(this).attr("class").includes(selectedRowColor)) { // Get SelectedRow by Color
            EditMenuGridRow();
        }
    });
    //console.log('dblclick-end');
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

    var createForm = $('#formCreateCategory');
    var name = createForm.find('input[id="createCategoryName"]').val();
    var displayName = createForm.find('input[id="createCategoryDisplayName"]').val();
    var iconPath = createForm.find('input[id="createCategoryIconPath"]').val();
    var controller = createForm.find('input[id="createCategoryController"]').val();
    var action = createForm.find('input[id="createCategoryAction"]').val();
    var role = createForm.find('select[id="createCategoryRole"]').val();
    var order = createForm.find('input[id="createCategoryOrder"]').val();

    var paramValue = JSON.stringify({
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
        //async: false,
        //cache: false,
        success: function (data) {
            if (data.result) {
                $('#createMenuDialogModal').modal('hide'); // hide Modal

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

function CreateSubCategory() {

    if (!$('#formCreateSubCategory').valid()) {
        return false;
    }

    var createForm = $('#formCreateSubCategory');

    var categoryId = createForm.find('input[id="createSubcategoryCategoryId"]').val();
    var name = createForm.find('input[id="createSubcategoryName"]').val();
    var displayName = createForm.find('input[id="createSubcategoryDisplayName"]').val();
    var iconPath = createForm.find('input[id="createSubcategoryIconPath"]').val();
    var action = createForm.find('input[id="createSubcategoryAction"]').val();
    var role = createForm.find('select[id="createSubcategoryRole"]').val();
    var order = createForm.find('input[id="createSubcategoryOrder"]').val();

    var paramValue = JSON.stringify({
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
        //async: false,
        //cache: false,
        success: function (data) {
            if (data.result) {
                $('#createMenuDialogModal').modal('hide'); // hide Modal

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

function EditMenuGridRow(errorMessageSelectGridRow) {

    var selectedRowId = "-1";
    var selectedRowCategoryId = "-1";

    $('#menuGrid > table > tbody  > tr').each(function (index, tr) {
        if ($(this).attr("class").includes(selectedRowColor)) { // Get SelectedRow by Color
            selectedRowId = $(this).attr("data-id");
            selectedRowCategoryId = $(this).attr("data-categoryid");
        }
    }); // Search Selected Row

    if (selectedRowId === "-1") {
        toastr.error(errorMessageSelectGridRow);
        return false;
    } // check whether selected row exist or not

    if (selectedRowCategoryId === "-1") {

        $.ajax({
            url: '/Management/IsCategoryExists' + '?id=' + selectedRowId,
            type: 'POST',
            headers: { "RequestVerificationToken": $('input[name="__RequestVerificationToken"]').val() },
            dataType: 'json',
            data: null,
            contentType: 'application/json; charset=utf-8',
            //async: false,
            //cache: false,
            success: function (data) {
                if (data.result) {

                    var editCategoryForm = $('#formEditCategory');

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
    } // Category
    else {
        $.ajax({
            url: '/Management/IsSubCategoryExists' + '?id=' + selectedRowId,
            type: 'POST',
            headers: { "RequestVerificationToken": $('input[name="__RequestVerificationToken"]').val() },
            dataType: 'json',
            data: null,
            contentType: 'application/json; charset=utf-8',
            //async: false,
            //cache: false,
            success: function (data) {
                if (data.result) {

                    var editCategoryForm = $('#formEditSubCategory');

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
    } // SubCategory
}

function UpdateCategory() {
    if (!$('#formEditCategory').valid()) {
        return false;
    }

    var createForm = $('#formEditCategory');

    var id = createForm.find('input[id="editCategoryId"]').val();
    var name = createForm.find('input[id="editCategoryName"]').val();
    var displayName = createForm.find('input[id="editCategoryDisplayName"]').val();
    var iconPath = createForm.find('input[id="editCategoryIconPath"]').val();
    var controller = createForm.find('input[id="editCategoryController"]').val();
    var action = createForm.find('input[id="editCategoryAction"]').val();
    var role = createForm.find('select[id="editCategoryRole"]').val();
    var order = createForm.find('input[id="editCategoryOrder"]').val();

    var paramValue = JSON.stringify({
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
        //async: false,
        //cache: false,
        success: function (data) {
            if (data.result) {
                $('#editCategoryDialogModal').modal('hide'); // hide Modal

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

function UpdateSubCategory() {
    if (!$('#formEditSubCategory').valid()) {
        return false;
    }

    var createForm = $('#formEditSubCategory');

    var id = createForm.find('input[id="editSubCategoryId"]').val();
    var categoryId = createForm.find('input[id="editSubCategoryCategoryId"]').val();
    var name = createForm.find('input[id="editSubCategoryName"]').val();
    var displayName = createForm.find('input[id="editSubCategoryDisplayName"]').val();
    var iconPath = createForm.find('input[id="editSubCategoryIconPath"]').val();
    var action = createForm.find('input[id="editSubCategoryAction"]').val();
    var role = createForm.find('select[id="editSubCategoryRole"]').val();
    var order = createForm.find('input[id="editSubCategoryOrder"]').val();

    var paramValue = JSON.stringify({
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
        //async: false,
        //cache: false,
        success: function (data) {
            if (data.result) {
                $('#editSubCategoryDialogModal').modal('hide'); // hide Modal

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

function ConfirmDeleteMenu(errorMessageSelectGridRow) {

    var selectedRowId = "-1";

    $('#menuGrid > table > tbody  > tr').each(function (index, tr) {
        if ($(this).attr("class").includes(selectedRowColor)) { // Get SelectedRow by Color
            selectedRowId = $(this).attr("data-id");
        }
    }); // Search Selected Row

    if (selectedRowId === "-1") {
        toastr.error(errorMessageSelectGridRow);
        return false;
    } // check whether selected row exist or not

    $('#confirmDeleteMenuDialogModal').modal({
        keyboard: false,
        backdrop: "static"
    });

    $('#confirmDeleteMenuDialogModal').modal('toggle');
    $('#confirmDeleteMenuDialogModal').modal('show');
}

function DeleteMenu(errorMessageSelectGridRow) {
    var selectedRowId = "-1";
    var selectedRowCategoryId = "-1";

    $('#menuGrid > table > tbody  > tr').each(function (index, tr) {
        if ($(this).attr("class").includes(selectedRowColor)) { // Get SelectedRow by Color
            selectedRowId = $(this).attr("data-id");
            selectedRowCategoryId = $(this).attr("data-categoryid");
        }
    }); // Search Selected Row

    if (selectedRowId === "-1") {
        toastr.error(errorMessageSelectGridRow);
        return false;
    } // check whether selected row exist or not

    if (selectedRowCategoryId === "-1") {

        $.ajax({
            url: '/Management/IsCategoryExists' + '?id=' + selectedRowId,
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
                        //async: false,
                        //cache: false,
                        success: function (data) {
                            if (data.result) {
                                $('#confirmDeleteMenuDialogModal').modal('hide'); // hide Modal

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
    } // Category
    else {
        $.ajax({
            url: '/Management/IsSubCategoryExists' + '?id=' + selectedRowId,
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
                        //async: false,
                        //cache: false,
                        success: function (data) {
                            if (data.result) {
                                $('#confirmDeleteMenuDialogModal').modal('hide'); // hide Modal

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
    } // SubCategory
}

function ExportExcelMenu() {
    var form = document.createElement("form");
    var element1 = document.createElement("input");
    var element2 = document.createElement("input");

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