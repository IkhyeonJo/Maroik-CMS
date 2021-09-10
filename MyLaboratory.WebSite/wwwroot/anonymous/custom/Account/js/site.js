// Please see documentation at https://docs.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.
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
    
    var paramValue = JSON.stringify({
        Culture: culture
    });

    $.ajax({
        url: '/Account/CultureManagement',
        type: 'POST',
        headers: { "RequestVerificationToken": $('input[name="__RequestVerificationToken"]').val() },
        dataType: 'json',
        data: paramValue,
        contentType: 'application/json; charset=utf-8',
        //async: false,
        //cache: false,
        success: function (data) {
            if (data.result) {
                window.location.href = "/Account/Login/";
            }
            else {
                window.location.href = "/Account/Login/";
            }
        }
    });
}