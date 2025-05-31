$(function () {
    $("#createAccountTabs").tabs();
    $("#editAccountTabs").tabs();
});

let selectedRowColor = "table-primary";

document.addEventListener("rowclick", e => {
    let selectedRow = e.detail;
    let selectedRowEmail = selectedRow.data.Email;

    $('#accountGrid > table > tbody  > tr').each(function (index, tr) {
        $(this).removeClass(selectedRowColor);
    });

    $('#accountGrid > table > tbody  > tr').each(function (index, tr) {
        if ((new String(selectedRowEmail).valueOf() === new String($(this).attr('data-email')).valueOf())) {
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

$(document).on("dblclick", "#accountGrid > table > tbody  > tr", function () {
    $('#accountGrid > table > tbody  > tr').each(function (index, tr) {
        if ($(this).attr("class").includes(selectedRowColor)) {
            EditAccountGridRow();
        }
    });
});
$('#gridSearch').on('input', function () {
    const grid = new MvcGrid(document.querySelector(".mvc-grid"));

    grid.url.searchParams.set("wholeSearch", this.value);

    grid.reload();
});

function CreateAccount() {

    if (!$('#formCreateAccount').valid()) {
        return false;
    }

    let createForm = $('#formCreateAccount');

    let email = createForm.find('input[id="createAccountEmail"]').val();
    let password = createForm.find('input[id="createAccountPassword"]').val();
    let nickname = createForm.find('input[id="createAccountNickname"]').val();
    let role = createForm.find('select[id="createAccountRole"]').val();
    let timeZoneIanaId = createForm.find('select[id="createAccountTimeZone"]').val();

    let paramValue = JSON.stringify({
        Email: email,
        Password: password,
        Nickname: nickname,
        Role: role,
        TimeZoneIanaId: timeZoneIanaId
    });

    $.ajax({
        url: '/Management/CreateAccount',
        type: 'POST',
        headers: { "RequestVerificationToken": $('input[name="__RequestVerificationToken"]').val() },
        dataType: 'json',
        data: paramValue,
        contentType: 'application/json; charset=utf-8',
        success: function (data) {
            if (data.result) {
                $('#createAccountDialogModal').modal('hide');

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

function EditAccountGridRow(errorMessageSelectGridRow) {

    let selectedRowEmail = "";

    $('#accountGrid > table > tbody  > tr').each(function (index, tr) {
        if ($(this).attr("class").includes(selectedRowColor)) {
            selectedRowEmail = $(this).attr("data-email");
        }
    });

    if (selectedRowEmail === "") {
        toastr.error(errorMessageSelectGridRow);
        return false;
    }

    $.ajax({
        url: '/Management/IsAccountExists' + '?email=' + selectedRowEmail,
        type: 'POST',
        headers: { "RequestVerificationToken": $('input[name="__RequestVerificationToken"]').val() },
        dataType: 'json',
        data: null,
        contentType: 'application/json; charset=utf-8',
        success: function (data) {
            if (data.result) {

                let formEditAccount = $('#formEditAccount');

                formEditAccount.find('input[id="editAccountEmail"]').val(data.account.email);
                formEditAccount.find('input[id="editAccountNickname"]').val(data.account.nickname);
                formEditAccount.find('select[id="editAccountRole"]').val(data.account.role).change();
                formEditAccount.find('select[id="editAccountTimeZone"]').val(data.account.timeZoneIanaId).change();
                formEditAccount.find('input[id="editAccountLocked"]').prop("checked", data.account.locked);
                formEditAccount.find('input[id="editAccountEmailConfirmed"]').prop("checked", data.account.emailConfirmed);
                formEditAccount.find('input[id="editAccountAgreedServiceTerms"]').prop("checked", data.account.agreedServiceTerms);
                formEditAccount.find('input[id="editMessage"]').val(data.account.message);
                formEditAccount.find('input[id="editAccountDeleted"]').prop("checked", data.account.deleted);

                $('#editAccountDialogModal').modal({
                    keyboard: false,
                    backdrop: "static"
                });

                $('#editAccountDialogModal').modal('toggle');
                $('#editAccountDialogModal').modal('show');
            }
            else {
                toastr.error(data.error);
            }
        }
    });
}

function UpdateAccount() {
    if (!$('#formEditAccount').valid()) {
        return false;
    }

    let createForm = $('#formEditAccount');

    let email = createForm.find('input[id="editAccountEmail"]').val();
    let password = createForm.find('input[id="editAccountPassword"]').val();
    let role = createForm.find('select[id="editAccountRole"]').val();
    let timeZoneIanaId = createForm.find('select[id="editAccountTimeZone"]').val();
    let locked = createForm.find('input[id="editAccountLocked"]').is(":checked");
    let emailConfirmed = createForm.find('input[id="editAccountEmailConfirmed"]').is(":checked");
    let agreedServiceTerms = createForm.find('input[id="editAccountAgreedServiceTerms"]').is(":checked");
    let message = createForm.find('input[id="editMessage"]').val();
    let deleted = createForm.find('input[id="editAccountDeleted"]').is(":checked");

    let paramValue = JSON.stringify({
        Email: email,
        Password: password,
        Role: role,
        TimeZoneIanaId: timeZoneIanaId,
        Locked: locked,
        EmailConfirmed: emailConfirmed,
        AgreedServiceTerms: agreedServiceTerms,
        Message: message,
        Deleted: deleted
    });

    $.ajax({
        url: '/Management/UpdateAccount',
        type: 'POST',
        headers: { "RequestVerificationToken": $('input[name="__RequestVerificationToken"]').val() },
        dataType: 'json',
        data: paramValue,
        contentType: 'application/json; charset=utf-8',
        success: function (data) {
            if (data.result) {
                $('#editAccountDialogModal').modal('hide');

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

function ConfirmDeleteAccount(errorMessageSelectGridRow) {

    let selectedRowEmail = "";

    $('#accountGrid > table > tbody  > tr').each(function (index, tr) {
        if ($(this).attr("class").includes(selectedRowColor)) {
            selectedRowEmail = $(this).attr("data-email");
        }
    });

    if (selectedRowEmail === "") {
        toastr.error(errorMessageSelectGridRow);
        return false;
    }

    $('#confirmDeleteAccountDialogModal').modal({
        keyboard: false,
        backdrop: "static"
    });

    $('#confirmDeleteAccountDialogModal').modal('toggle');
    $('#confirmDeleteAccountDialogModal').modal('show');
}

function DeleteAccount(errorMessageSelectGridRow) {

    let selectedRowEmail = "";

    $('#accountGrid > table > tbody  > tr').each(function (index, tr) {
        if ($(this).attr("class").includes(selectedRowColor)) {
            selectedRowEmail = $(this).attr("data-email");
        }
    });

    if (selectedRowEmail === "") {
        toastr.error(errorMessageSelectGridRow);
        return false;
    }

    $.ajax({
        url: '/Management/IsAccountExists' + '?email=' + selectedRowEmail,
        type: 'POST',
        headers: { "RequestVerificationToken": $('input[name="__RequestVerificationToken"]').val() },
        dataType: 'json',
        data: null,
        contentType: 'application/json; charset=utf-8',
        success: function (data) {
            if (data.result) {

                let paramValue = JSON.stringify({
                    Email: data.account.email
                });

                $.ajax({
                    url: '/Management/DeleteAccount',
                    type: 'POST',
                    headers: { "RequestVerificationToken": $('input[name="__RequestVerificationToken"]').val() },
                    dataType: 'json',
                    data: paramValue,
                    contentType: 'application/json; charset=utf-8',
                    success: function (data) {
                        if (data.result) {
                            $('#confirmDeleteAccountDialogModal').modal('hide');

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

function ExportExcelAccount() {
    let form = document.createElement("form");
    let element1 = document.createElement("input");
    let element2 = document.createElement("input");

    form.method = "POST";
    form.action = "/Management/ExportExcelAccount";

    element1.name = "__RequestVerificationToken";
    element1.value = $('input[name="__RequestVerificationToken"]').val();
    form.appendChild(element1);

    element2.name = "fileName";
    element2.value = "Account";
    form.appendChild(element2);

    document.body.appendChild(form);

    form.submit();
}

$('#btnEditAccountGridRow').off('click').on('click', function () {
    EditAccountGridRow($(this).attr('data-errorMessageSelectGridRow'));
});

$('#btnConfirmDeleteAccount').off('click').on('click', function () {
    ConfirmDeleteAccount($(this).attr('data-errorMessageSelectGridRow'));
});

$('#btnExportExcelAccount').off('click').on('click', function () {
    ExportExcelAccount();
});

$('#btnDeleteAccount').off('click').on('click', function () {
    DeleteAccount($(this).attr('data-errorMessageSelectGridRow'));
});

$('#formCreateAccount').off('submit').on('submit', function () {
    return CreateAccount();
});

$('#formEditAccount').off('submit').on('submit', function () {
    return UpdateAccount();
});