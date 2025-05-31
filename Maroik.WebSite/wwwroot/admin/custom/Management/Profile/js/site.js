function UpdateProfileAvatar(noFileAttachedErrorMessage, fileSizeLimitationErrorMessage, fileTypeErrorMessage) {
    let files = document.getElementById('ProfileAvatarFiles').files[0];
    if (files === undefined || files === null) {
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
                let form = $('#formUpdateProfileAvatar')[0];
                let formData = new FormData(form);
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

    let createForm = $('#formUpdateProfilePassword');
    let password = createForm.find('input[id="Password"]').val();
    let newPassword = createForm.find('input[id="NewPassword"]').val();

    let paramValue = JSON.stringify({
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

$('#formUpdateProfilePassword').off('submit').on('submit', function () {
    return UpdateProfilePassword();
});

$('#ProfileAvatarFiles').off('change').on('change', function () {
    return UpdateProfileAvatar($(this).attr('data-noFileAttachedErrorMessage'), $(this).attr('data-fileSizeLimitationErrorMessage'), $(this).attr('data-fileTypeErrorMessage'));
});