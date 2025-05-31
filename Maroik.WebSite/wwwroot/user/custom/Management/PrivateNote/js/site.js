function base64ToBlob(base64, mime) {
    const byteCharacters = atob(base64);
    const byteNumbers = new Array(byteCharacters.length);
    for (let i = 0; i < byteCharacters.length; i++) {
        byteNumbers[i] = byteCharacters.charCodeAt(i);
    }
    const byteArray = new Uint8Array(byteNumbers);
    return new Blob([byteArray], { type: mime });
}

const localizer = {
    IETFLanguageTag: $('#localizerIETFLanguageTag').val()
};

$('#writeBoardContent').summernote({
    height: 300,
    lang: localizer.IETFLanguageTag,
    callbacks: {
        onImageUpload: function (files) {
            for (let i = 0; i < files.length; i++) {
                WriteUploadImageFile(files[i]);
            }
        }
    }
});

$('#editBoardContent').summernote({
    height: 300,
    lang: localizer.IETFLanguageTag,
    callbacks: {
        onImageUpload: function (files) {
            for (let i = 0; i < files.length; i++) {
                EditUploadImageFile(files[i]);
            }
        }
    }
});

let writeUploadedFile;
let editUploadedFile;

function WriteUploadFile(obj, errorMessage) {
    if (obj.files[0].size > 10485760) {
        alert(errorMessage);
        document.getElementById("writeUploadedFile").value = "";
        return false;
    }
    else {
        writeUploadedFile = obj.files[0];
    }
}

function EditUploadFile(obj, errorMessage) {
    if (obj.files[0].size > 10485760) {
        alert(errorMessage);
        document.getElementById("editUploadedFile").value = "";
        return false;
    }
    else {
        editUploadedFile = obj.files[0];
    }
}

function SearchBoard() {
    window.location.href = "/Management/PrivateNote?searchType=" + $('#searchType').val() + "&searchText=" + $('#btnPrivateNoteSearchText').val();
}


function ShowWriteBoardLoading() {
    if (!$('#formWriteBoard').valid()) {
        $('#loading').hide();
        return false;
    }
    else {
        $('#loading').show();
        return true;
    }
}

function ShowEditBoardLoading() {
    if (!$('#formEditBoard').valid()) {
        $('#loading').hide();
        return false;
    }
    else {
        $('#loading').show();
        return true;
    }
}

function WriteUploadImageFile(file) {

    formData = new FormData();
    formData.append("summernoteImageFile", file);

    $.ajax({
        url: "/Management/UploadImageFile",
        data: formData,
        headers: { "RequestVerificationToken": $('input[name="__RequestVerificationToken"]').val() },
        type: 'POST',
        enctype: 'multipart/form-data',
        processData: false,
        contentType: false,
        dataType: 'json',
        cache: false,
        success: function (data) {
            if (data.result) {
                const imgURL = URL.createObjectURL(base64ToBlob(data.file.fileContents, data.file.contentType));
                const imgNode = document.createElement('img');
                imgNode.src = imgURL;
                imgNode.style.maxWidth = "170px";
                imgNode.style.maxHeight = "209px";
                imgNode.setAttribute('alt', data.filePath);

                $('#writeBoardContent').summernote('insertNode', imgNode);

                imgNode.onload = function () {
                    URL.revokeObjectURL(imgURL);
                };
                imgNode.onerror = function () {
                    URL.revokeObjectURL(imgURL);
                };
            }
            else {
                alert(data.errorMessage);
            }
        }
    });
}

function EditUploadImageFile(file) {

    formData = new FormData();
    formData.append("summernoteImageFile", file);

    $.ajax({
        url: "/Management/UploadImageFile",
        data: formData,
        headers: { "RequestVerificationToken": $('input[name="__RequestVerificationToken"]').val() },
        type: 'POST',
        enctype: 'multipart/form-data',
        processData: false,
        contentType: false,
        dataType: 'json',
        cache: false,
        success: function (data) {
            if (data.result) {
                const imgURL = URL.createObjectURL(base64ToBlob(data.file.fileContents, data.file.contentType));
                const imgNode = document.createElement('img');
                imgNode.src = imgURL;
                imgNode.style.maxWidth = "170px";
                imgNode.style.maxHeight = "209px";
                imgNode.setAttribute('alt', data.filePath);

                $('#editBoardContent').summernote('insertNode', imgNode);

                imgNode.onload = function () {
                    URL.revokeObjectURL(imgURL);
                };
                imgNode.onerror = function () {
                    URL.revokeObjectURL(imgURL);
                };
            }
            else {
                alert(data.errorMessage);
            }
        }
    });
}

function WriteBoard() {

    if (!$('#formWriteBoard').valid()) {
        return false;
    }

    let createForm = $('#formWriteBoard');

    let title = createForm.find('input[id="writeBoardTitle"]').val();
    let content = createForm.find('textarea[id="writeBoardContent"]').val();
    let locked = createForm.find('input[id="writeBoardLocked"]').is(":checked");

    let formData = new FormData();
    formData.append('Title', title);
    formData.append('Content', content);
    formData.append('Locked', locked);
    formData.append('UploadedFile', writeUploadedFile);

    $.ajax({
        url: '/Management/WritePrivateNoteBoard',
        type: 'POST',
        headers: { "RequestVerificationToken": $('input[name="__RequestVerificationToken"]').val() },
        dataType: 'json',
        data: formData,
        contentType: false,
        processData: false,
        success: function (data) {
            if (data.result) {
                alert(data.message);
                window.location.href = "/Management/PrivateNote";
            }
            else {
                toastr.error(data.error);
                $('#loading').hide();
            }
        }
    });

    return false;
}

function EditBoard(editBoardId, editCurrentPage) {

    if (!$('#formEditBoard').valid()) {
        return false;
    }

    let createForm = $('#formEditBoard');

    let title = createForm.find('input[id="editBoardTitle"]').val();
    let content = createForm.find('textarea[id="editBoardContent"]').val();
    let locked = createForm.find('input[id="editBoardLocked"]').is(":checked");

    let formData = new FormData();
    formData.append('Id', editBoardId);
    formData.append('Title', title);
    formData.append('Locked', locked);
    formData.append('Content', content);
    formData.append('UploadedFile', editUploadedFile);


    $.ajax({
        url: '/Management/EditPrivateNoteBoard',
        type: 'POST',
        headers: { "RequestVerificationToken": $('input[name="__RequestVerificationToken"]').val() },
        dataType: 'json',
        data: formData,
        contentType: false,
        processData: false,
        success: function (data) {
            if (data.result) {
                alert(data.message);
                window.location.href = "/Management/PrivateNote?method=detail" + "&boardId=" + editBoardId + "&page=" + editCurrentPage;
            }
            else {
                toastr.error(data.error);
                $('#loading').hide();
            }
        }
    });

    return false;
}

function ConfirmDeleteBoard() {
    $('#confirmDeleteBoardDialogModal').modal({
        keyboard: false,
        backdrop: "static"
    });

    $('#confirmDeleteBoardDialogModal').modal('toggle');
    $('#confirmDeleteBoardDialogModal').modal('show');
}

function DeleteBoard(detailBoardId) {

    $.ajax({
        url: '/Management/IsBoardExists' + '?id=' + detailBoardId,
        type: 'POST',
        headers: { "RequestVerificationToken": $('input[name="__RequestVerificationToken"]').val() },
        dataType: 'json',
        data: null,
        contentType: 'application/json; charset=utf-8',
        success: function (data) {
            if (data.result) {

                let paramValue = JSON.stringify({
                    Id: data.privateNoteBoard.id
                });

                $.ajax({
                    url: '/Management/DeleteBoard',
                    type: 'POST',
                    headers: { "RequestVerificationToken": $('input[name="__RequestVerificationToken"]').val() },
                    dataType: 'json',
                    data: paramValue,
                    contentType: 'application/json; charset=utf-8',
                    success: function (data) {
                        if (data.result) {
                            $('#confirmDeleteBoardDialogModal').modal('hide');
                            alert(data.message);
                            window.location.href = "/Management/PrivateNote";
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

function WritePrivateNoteComment(detailBoardId, detailCurrentPage, errorMessage) {

    let createForm = $('#formWritePrivateNoteComment');
    let content = createForm.find('input[id="writePrivateNoteCommentContent"]').val();

    if (!content) {
        alert(errorMessage);
        return false;
    }

    let paramValue = JSON.stringify({
        BoardId: detailBoardId,
        Content: content,
        DetailCurrentPage: detailCurrentPage
    });

    $.ajax({
        url: '/Management/WritePrivateNoteComment',
        type: 'POST',
        headers: { "RequestVerificationToken": $('input[name="__RequestVerificationToken"]').val() },
        dataType: 'json',
        data: paramValue,
        contentType: 'application/json; charset=utf-8',
        success: function (data) {
            if (data.result) {
                window.location.href = "/Management/PrivateNote?method=detail" + "&boardId=" + data.boardId + "&page=" + data.page;
            }
            else {
                toastr.error(data.error);
            }
        }
    });

    return false;
}
function DeleteComment(commentId, boardId, page) {
    $.ajax({
        url: '/Management/DeleteComment' + '?id=' + commentId,
        type: 'POST',
        headers: { "RequestVerificationToken": $('input[name="__RequestVerificationToken"]').val() },
        dataType: 'json',
        data: null,
        contentType: 'application/json; charset=utf-8',
        success: function (data) {
            if (data.result) {
                window.location.href = "/Management/PrivateNote?method=detail" + "&boardId=" + boardId + "&page=" + page;
            }
            else {
                toastr.error(data.error);
            }
        }
    });
}

$('#btnPrivateNoteShowWriteBoardLoading').off('click').on('click', function () {
    return ShowWriteBoardLoading();
});

$('#btnPrivateNoteModify').off('click').on('click', function () {
    location.href = $(this).attr("data-link");
});

$('#btnPrivateNoteConfirmDeleteBoard').off('click').on('click', function () {
    ConfirmDeleteBoard();
});

$('#btnPrivateNoteList').off('click').on('click', function () {
    location.href = $(this).attr("data-link");
});

$('#btnPrivateNoteSubmitModify').off('click').on('click', function () {
    return ShowEditBoardLoading();
});

$('#btnPrivateNoteWrite').off('click').on('click', function () {
    location.href = "/Management/PrivateNote?method=write";
});

$('#btnPrivateNoteSearchBoard').off('click').on('click', function () {
    SearchBoard();
});

$('#btnPrivateNoteDeleteBoard').off('click').on('click', function () {
    DeleteBoard($(this).attr("data-boardId"));
});

$('#btnPrivateNoteSearchText').off('keypress').on('keypress', function (event) {
    if (event.keyCode === 13 || event.which === 13) {
        SearchBoard();
    }
});

$('.aPrivateNoteDeleteComment').off('click').on('click', function (event) {
    event.preventDefault();
    DeleteComment($(this).attr("data-commentId"), $(this).attr("data-detailBoardId"), $(this).attr("data-detailCurrentPage"));
});

$('#formWritePrivateNoteComment').off('submit').on('submit', function () {
    return WritePrivateNoteComment($(this).attr("data-detailBoardId"), $(this).attr("data-detailCurrentPage"), $(this).attr("data-errorMessage"));
});

$('#formWriteBoard').off('submit').on('submit', function () {
    return WriteBoard();
});

$('#formEditBoard').off('submit').on('submit', function () {
    return EditBoard($(this).attr('data-editBoardId'), $(this).attr('data-editCurrentPage'));
});

$('#writeUploadedFile').off('change').on('change', function () {
    return WriteUploadFile(this, $(this).attr('data-errorMessage'));
});

$('#editUploadedFile').off('change').on('change', function () {
    return EditUploadFile(this, $(this).attr('data-errorMessage'));
});

$(function () {
    if ($('#detailBoardContent').length > 0 && $('#detailBoardContent').is(":hidden")) {
        const parser = new DOMParser();
        const htmlDoc = parser.parseFromString($('#detailBoardContent').html(), 'text/html');

        const imgTags = htmlDoc.querySelectorAll('img[data-file]');

        imgTags.forEach(function (imgTag) {
            const base64Data = imgTag.getAttribute('data-file');
            const contentType = imgTag.getAttribute('data-contenttype');

            if (base64Data && contentType) {
                const blob = base64ToBlob(base64Data, contentType);
                const imgURL = URL.createObjectURL(blob);

                imgTag.src = imgURL;

                imgTag.onload = function () {
                    URL.revokeObjectURL(imgURL);
                };
                imgTag.onerror = function () {
                    URL.revokeObjectURL(imgURL);
                };

                imgTag.removeAttribute('data-file');
                imgTag.removeAttribute('data-contenttype');
            }
        });

        const updatedHtml = htmlDoc.body.innerHTML;
        $('#detailBoardContent').html(updatedHtml);
        $('#detailBoardContent').show();
    } else if ($('#editBoardContent').length > 0 && $('#divEditBoardContent').is(":hidden")) {
        const parser = new DOMParser();
        const htmlDoc = parser.parseFromString($('#editBoardContent').summernote('code'), 'text/html');

        const imgTags = htmlDoc.querySelectorAll('img[data-file]');

        imgTags.forEach(function (imgTag) {
            const base64Data = imgTag.getAttribute('data-file');
            const contentType = imgTag.getAttribute('data-contenttype');

            if (base64Data && contentType) {
                const blob = base64ToBlob(base64Data, contentType);
                const imgURL = URL.createObjectURL(blob);

                imgTag.src = imgURL;

                imgTag.onload = function () {
                    URL.revokeObjectURL(imgURL);
                };
                imgTag.onerror = function () {
                    URL.revokeObjectURL(imgURL);
                };

                imgTag.removeAttribute('data-file');
                imgTag.removeAttribute('data-contenttype');
            }
        });

        const updatedHtml = htmlDoc.body.innerHTML;
        $('#editBoardContent').summernote('code', updatedHtml);
        $('#divEditBoardContent').show();
    }

    if ($('#aDetailBoardAttachedFile').length > 0) {
        $('#aDetailBoardAttachedFile').off('click').on('click', function (event) {
            event.preventDefault();
            let base64Data = $(this).attr('data-file');
            let contentType = $(this).attr('data-contenttype');
            let name = $(this).attr('data-name')

            if (base64Data && contentType) {
                let blob = base64ToBlob(base64Data, contentType);
                let url = URL.createObjectURL(blob);
                let a = document.createElement('a');
                try {
                    a.href = url;
                    a.download = name;
                    a.click();
                } finally {
                    setTimeout(() => URL.revokeObjectURL(url), 100);
                    a.remove();
                }
            }
        });
    } else if ($('#aEditBoardAttachedFile').length > 0) {
        $('#aEditBoardAttachedFile').off('click').on('click', function (event) {
            event.preventDefault();
            let base64Data = $(this).attr('data-file');
            let contentType = $(this).attr('data-contenttype');
            let name = $(this).attr('data-name')

            if (base64Data && contentType) {
                let blob = base64ToBlob(base64Data, contentType);
                let url = URL.createObjectURL(blob);
                let a = document.createElement('a');
                try {
                    a.href = url;
                    a.download = name;
                    a.click();
                } finally {
                    setTimeout(() => URL.revokeObjectURL(url), 100);
                    a.remove();
                }
            }
        });
    }
});