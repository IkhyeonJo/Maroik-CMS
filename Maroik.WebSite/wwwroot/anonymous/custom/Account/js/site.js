$(function () {
    if ($('#_currentCulture').val() === 'ko-KR') {
        $('#TimeZoneIanaId').val('Asia/Seoul');
    }
});

function ShowRegisterLoading() {
    if (!$('#registerForm').valid()) {
        $('#loading').hide();
        return false;
    }
    else {
        $('#loading').show();
        return true;
    }
}

function ShowForgotPasswordLoading() {
    if (!$('#forgotPasswordForm').valid()) {
        $('#loading').hide();
        return false;
    }
    else {
        $('#loading').show();
        return true;
    }
}

function ShowResetPasswordLoading() {
    if (!$('#resetPasswordForm').valid()) {
        $('#loading').hide();
        return false;
    }
    else {
        $('#loading').show();
        return true;
    }
}

function ShowLoginLoading() {
    if (!$('#loginForm').valid()) {
        $('#loading').hide();
        return false;
    }
    else {
        $('#loading').show();
        return true;
    }
}

function ChangeCulture(culture) {
    
    let paramValue = JSON.stringify({
        Culture: culture
    });

    $.ajax({
        url: '/DashBoard/CultureManagement',
        type: 'POST',
        headers: { "RequestVerificationToken": $('input[name="__RequestVerificationToken"]').val() },
        dataType: 'json',
        data: paramValue,
        contentType: 'application/json; charset=utf-8',
        success: function (data) {
            if (data.result) {
                window.location.href = $("#returnUri").val();
            }
            else {
                window.location.href = $("#returnUri").val();
            }
        }
    });
}

$('#btnRequestNewPassword').off('click').on('click', function () {
    ShowForgotPasswordLoading();
});

$('#aChangeCultureEnUS').off('click').on('click', function () {
    ChangeCulture('en-US');
});

$('#aChangeCultureKoKR').off('click').on('click', function () {
    ChangeCulture('ko-KR');
});

$('#btnSignIn').off('click').on('click', function () {
    ShowLoginLoading();
});

$('#btnRegister').off('click').on('click', function () {
    ShowRegisterLoading();
});

$('#btnResend').off('click').on('click', function () {
    ShowRegisterLoading();
});

$('#btnChangePassword').off('click').on('click', function () {
    ShowResetPasswordLoading();
});