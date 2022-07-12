// Please see documentation at https://docs.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.
//$(function () {
//    $("#editCategoryTabs").tabs();
//    $("#editSubCategoryTabs").tabs();
//});

function UploadMLModelData(noFileAttachedErrorMessage, fileSizeLimitationErrorMessage, fileTypeErrorMessage) {
    var files = document.getElementById('MLModelDataFiles').files[0];
    if (files === undefined || files == null) {
        alert(noFileAttachedErrorMessage);
        window.location.href = "/Develop/MachineLearning";
    }
    else {
        if (files.size > 10485760) {
            alert(fileSizeLimitationErrorMessage);
            window.location.href = "/Develop/MachineLearning";
        }
        else if (files.size > 0 && files.size <= 10485760) {
            if (files.type.toUpperCase() === "application/vnd.ms-excel".toUpperCase()) {
                var form = $('#formUploadMLModelData')[0]; // FormData 객체 생성
                var formData = new FormData(form);
                $.ajax({
                    url: "/Develop/CreateMachineLearning",
                    data: formData,
                    type: 'POST',
                    enctype: 'multipart/form-data',
                    processData: false,
                    contentType: false,
                    dataType: 'json',
                    cache: false,
                    success: function (data) {
                        if (data.result) {
                            window.location.href = "/Develop/MachineLearning";
                        }
                        else {
                            alert(data.errorMessage);
                            window.location.href = "/Develop/MachineLearning";
                        }
                    }
                });

            }
            else {
                alert(fileTypeErrorMessage)
                window.location.href = "/Develop/MachineLearning";
            }
        }
        else {
            alert(fileSizeLimitationErrorMessage);
            window.location.href = "/Develop/MachineLearning";
        }
    }
}