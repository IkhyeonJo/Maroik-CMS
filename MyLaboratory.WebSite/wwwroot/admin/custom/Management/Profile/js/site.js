// Please see documentation at https://docs.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.
function UpdateProfileAvatar(noFileAttachedErrorMessage, fileSizeLimitationErrorMessage, fileTypeErrorMessage) {
    var files = document.getElementById('ProfileAvatarFiles').files[0];
    if (files === undefined || files == null) {
        alert(noFileAttachedErrorMessage);
        window.location.href = "/Management/Profile";
    }
    else {
        if (files.size > 10485760) {
            alert(fileSizeLimitationErrorMessage);
            window.location.href = "/Management/Profile";
        }
        else if (files.size > 0 && files.size <= 10485760) {
            if (files.type.toUpperCase() === "image/jpeg".toUpperCase() || files.type.toUpperCase() === "image/png".toUpperCase()) {
                var form = $('#formUpdateProfileAvatar')[0]; // FormData 객체 생성
                var formData = new FormData(form);
                $.ajax({
                    url: "/Management/UpdateProfileAvatar",
                    data: formData,
                    type: 'POST',
                    enctype: 'multipart/form-data',
                    processData: false,
                    contentType: false,
                    dataType: 'json',
                    cache: false,
                    success: function (data) {
                        if (data.result) {
                            window.location.href = "/Management/Profile";
                        }
                        else {
                            alert(data.errorMessage);
                            window.location.href = "/Management/Profile";
                        }
                    }
                });

            }
            else {
                alert(fileTypeErrorMessage)
                window.location.href = "/Management/Profile";
            }
        }
        else {
            alert(fileSizeLimitationErrorMessage);
            window.location.href = "/Management/Profile";
        }
    }
}

function UpdateProfilePassword() {

    if (!$('#formUpdateProfilePassword').valid()) {
        return false;
    }

    var createForm = $('#formUpdateProfilePassword');
    var password = createForm.find('input[id="Password"]').val();
    var newPassword = createForm.find('input[id="NewPassword"]').val();

    var paramValue = JSON.stringify({
        Password: password,
        NewPassword: newPassword
    });

    $.ajax({
        url: '/Management/UpdateProfilePassword',
        type: 'POST',
        headers: { "RequestVerificationToken": $('input[name="__RequestVerificationToken"]').val() },
        dataType: 'json',
        data: paramValue,
        contentType: 'application/json; charset=utf-8',
        //async: false,
        //cache: false,
        success: function (data) {
            if (data.result) {
                toastr.success(data.message);
            }
            else {
                toastr.error(data.error);
            }
        }
    });
    return false;
}