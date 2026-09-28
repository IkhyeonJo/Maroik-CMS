/**
 * Script for the **user** Private Note page (`Views/Management/PrivateNote.cshtml`
 * rendered for a signed-in user — their personal notes). Same shape as the Free
 * Forum script (list / write / edit / detail sub-views chosen by a `method`
 * query-string param), pointed at the `/Management/PrivateNote*` endpoints.
 * Unlike the admin version there is no "notice" flag.
 *
 * Features: summernote rich-text body with image upload (each image POSTed,
 * returned as base64, re-inserted as an object URL); attachment upload with a
 * client-side size check; write / edit / delete note, write / delete comment
 * (all AJAX, redirecting on success; `{ result:false, error }` -> `toastr.error`);
 * search, "back to list", inline-image rehydration and attachment download.
 *
 * IIFE-wrapped, no `import` / `export`. Client checks mirror server rules.
 */
(function() {
    // Cached references. Many exist only on one sub-view, so `.length` is checked.
    const $writeBoardContent = $("#writeBoardContent");
    const $editBoardContent = $("#editBoardContent");
    // Authoritative in ServerSetting.MaxAttachedFileSizeBytes (server); mirrored here for form UX
    // only. Falls back to the shared per-role default (_Layout/site.ts) if the hidden field is
    // missing or unparseable.
    const maxFileSize = parseInt($("#maxAttachedFileSizeBytes").val() as string) || (window as any).MaroikDefaultMaxAttachedFileSizeBytes;
    const $searchType = $("#searchType");
    const $btnPrivateNoteSearchText = $("#btnPrivateNoteSearchText");
    const $formWriteBoard = $("#formWriteBoard");
    const $loading = $("#loading");
    const $formEditBoard = $("#formEditBoard");
    const $__RequestVerificationToken = $("input[name=\"__RequestVerificationToken\"]");
    const $writeBoardTitle = $("#writeBoardTitle");
    const $writeBoardLocked = $("#writeBoardLocked");
    const $editBoardTitle = $("#editBoardTitle");
    const $editBoardLocked = $("#editBoardLocked");
    const $confirmDeleteBoardDialogModal = $("#confirmDeleteBoardDialogModal");
    const $writePrivateNoteCommentContent = $("#writePrivateNoteCommentContent");
    const $btnPrivateNoteShowWriteBoardLoading = $("#btnPrivateNoteShowWriteBoardLoading");
    const $btnPrivateNoteModify = $("#btnPrivateNoteModify");
    const $btnPrivateNoteConfirmDeleteBoard = $("#btnPrivateNoteConfirmDeleteBoard");
    const $btnPrivateNoteList = $("#btnPrivateNoteList");
    const $btnPrivateNoteSubmitModify = $("#btnPrivateNoteSubmitModify");
    const $btnPrivateNoteWrite = $("#btnPrivateNoteWrite");
    const $btnPrivateNoteSearchBoard = $("#btnPrivateNoteSearchBoard");
    const $btnPrivateNoteDeleteBoard = $("#btnPrivateNoteDeleteBoard");
    const $formWritePrivateNoteComment = $("#formWritePrivateNoteComment");
    const $writeUploadedFile = $("#writeUploadedFile");
    const $editUploadedFile = $("#editUploadedFile");
    const $detailBoardContent = $("#detailBoardContent");
    const $divEditBoardContent = $("#divEditBoardContent");
    const $aDetailBoardAttachedFile = $("#aDetailBoardAttachedFile");
    const $aEditBoardAttachedFile = $("#aEditBoardAttachedFile");

    /** base64 -> Blob, for turning server-embedded image/attachment payloads into object URLs. */
    function base64ToBlob(base64: string, mime: string) {
        const byteCharacters = atob(base64);
        const byteNumbers = new Array(byteCharacters.length);
        for (let i = 0; i < byteCharacters.length; i++) {
            byteNumbers[i] = byteCharacters.charCodeAt(i);
        }
        const byteArray = new Uint8Array(byteNumbers);
        return new Blob([byteArray], { type: mime });
    }

    /**
     * Frees each rebuilt image's object URL once the image has loaded (or failed to): the browser keeps the Blob alive until then.
     * Applied to the images as they are in the live page — the ones rebuilt in the parsed, detached copy are re-serialized into
     * HTML, which drops any handler set on them.
     */
    function ReleaseObjectUrlsOnLoad(root: Element | undefined | null) {
        if (!root) return;
        root.querySelectorAll<HTMLImageElement>("img[src^=\"blob:\"]").forEach(function(img) {
            const url = img.src;
            img.onload = img.onerror = function() {
                URL.revokeObjectURL(url);
            };
        });
    }

    // Only the summernote UI language is needed; `any` avoids typing a one-off bag.
    const localizer: any = {
        IETFLanguageTag: $("#localizerIETFLanguageTag").val()
    };

    // Rich-text editors for the write / edit bodies; each pasted image is
    // uploaded and re-inserted by the helpers below.
    $writeBoardContent.summernote({
        height: 300,
        lang: localizer.IETFLanguageTag,
        callbacks: {
            onImageUpload: function(files: any) {
                for (let i = 0; i < files.length; i++) {
                    WriteUploadImageFile(files[i]);
                }
            }
        }
    });

    $editBoardContent.summernote({
        height: 300,
        lang: localizer.IETFLanguageTag,
        callbacks: {
            onImageUpload: function(files: any) {
                for (let i = 0; i < files.length; i++) {
                    EditUploadImageFile(files[i]);
                }
            }
        }
    });

    // The attachment chosen in each form, held until submit.
    let writeUploadedFile: any;
    let editUploadedFile: any;

    /** Write-form attachment `change`: reject + clear if over `maxFileSize`, else stash the `File`. */
    function WriteUploadFile(obj: HTMLInputElement, errorMessage?: string) {
        if (!obj.files || obj.files.length === 0) {
            return;
        }
        if (obj.files[0].size > maxFileSize) {
            alert(errorMessage);
            (document.getElementById("writeUploadedFile") as HTMLInputElement).value = "";
            return false;
        } else {
            writeUploadedFile = obj.files[0];
        }
    }

    /** Same as `WriteUploadFile` for the edit form. */
    function EditUploadFile(obj: HTMLInputElement, errorMessage?: string) {
        if (!obj.files || obj.files.length === 0) {
            return;
        }
        if (obj.files[0].size > maxFileSize) {
            alert(errorMessage);
            (document.getElementById("editUploadedFile") as HTMLInputElement).value = "";
            return false;
        } else {
            editUploadedFile = obj.files[0];
        }
    }

    /** Reloads the list filtered by the chosen search type + text. */
    function SearchBoard() {
        window.location.href = "/Management/PrivateNote?searchType=" + encodeURIComponent($searchType.val() as string) + "&searchText=" + encodeURIComponent($btnPrivateNoteSearchText.val() as string);
    }

    /** Write form: show the `#loading` overlay only if the form validates. */
    function ShowWriteBoardLoading() {
        if (!$formWriteBoard.valid()) {
            $loading.hide();
            return false;
        } else {
            $loading.show();
            return true;
        }
    }

    /** Edit form: show the `#loading` overlay only if the form validates. */
    function ShowEditBoardLoading() {
        if (!$formEditBoard.valid()) {
            $loading.hide();
            return false;
        } else {
            $loading.show();
            return true;
        }
    }

    /**
     * summernote `onImageUpload` for the write editor: POST the raw image, get
     * `{ file: { fileContents(base64), contentType }, filePath }` back, insert it
     * as a size-capped `<img>` on an object URL (revoked once decoded). `filePath`
     * goes in `alt` so the server can resolve the stored image on save.
     */
    function WriteUploadImageFile(file: File) {

        let formData = new FormData();
        formData.append("summernoteImageFile", file);

        $.ajax({
            url: "/Management/UploadImageFile",
            data: formData,
            headers: { "RequestVerificationToken": $__RequestVerificationToken.val() as string },
            type: "POST",
            enctype: "multipart/form-data",
            processData: false,
            contentType: false,
            dataType: "json",
            cache: false,
            success: function(data) {
                if (data.result) {
                    const imgURL = URL.createObjectURL(base64ToBlob(data.file.fileContents, data.file.contentType));
                    const imgNode = document.createElement("img");
                    imgNode.src = imgURL;
                    imgNode.style.maxWidth = "170px";
                    imgNode.style.maxHeight = "209px";
                    imgNode.setAttribute("alt", data.filePath);

                    $writeBoardContent.summernote("insertNode", imgNode);

                    imgNode.onload = function() {
                        URL.revokeObjectURL(imgURL);
                    };
                    imgNode.onerror = function() {
                        URL.revokeObjectURL(imgURL);
                    };
                } else {
                    alert(data.errorMessage);
                }
            }
        });
    }

    /** Same as `WriteUploadImageFile` for the edit editor. */
    function EditUploadImageFile(file: File) {

        let formData = new FormData();
        formData.append("summernoteImageFile", file);

        $.ajax({
            url: "/Management/UploadImageFile",
            data: formData,
            headers: { "RequestVerificationToken": $__RequestVerificationToken.val() as string },
            type: "POST",
            enctype: "multipart/form-data",
            processData: false,
            contentType: false,
            dataType: "json",
            cache: false,
            success: function(data) {
                if (data.result) {
                    const imgURL = URL.createObjectURL(base64ToBlob(data.file.fileContents, data.file.contentType));
                    const imgNode = document.createElement("img");
                    imgNode.src = imgURL;
                    imgNode.style.maxWidth = "170px";
                    imgNode.style.maxHeight = "209px";
                    imgNode.setAttribute("alt", data.filePath);

                    $editBoardContent.summernote("insertNode", imgNode);

                    imgNode.onload = function() {
                        URL.revokeObjectURL(imgURL);
                    };
                    imgNode.onerror = function() {
                        URL.revokeObjectURL(imgURL);
                    };
                } else {
                    alert(data.errorMessage);
                }
            }
        });
    }

    /**
     * Submits a new note as multipart `FormData` (title, body HTML, the locked
     * flag, the stashed attachment). On success alerts and returns to the list;
     * on failure toasts and hides the overlay. `as any` casts because
     * `FormData.append` wants `string | Blob`. Returns `false`.
     */
    function WriteBoard() {

        if (!$formWriteBoard.valid()) {
            return false;
        }

        let title = $writeBoardTitle.val();
        let content = $writeBoardContent.val();
        let locked = $writeBoardLocked.is(":checked");

        let formData = new FormData();
        formData.append("Title", title as any);
        formData.append("Content", content as any);
        formData.append("Locked", locked as any);
        formData.append("UploadedFile", writeUploadedFile);

        $.ajax({
            url: "/Management/WritePrivateNoteBoard",
            type: "POST",
            headers: { "RequestVerificationToken": $__RequestVerificationToken.val() as string },
            dataType: "json",
            data: formData,
            contentType: false,
            processData: false,
            success: function(data) {
                if (data.result) {
                    alert(data.message);
                    window.location.href = "/Management/PrivateNote";
                } else {
                    toastr.error(data.error);
                    $loading.hide();
                }
            }
        });

        return false;
    }

    /**
     * Submits an edit to an existing note (same multipart shape plus `ID`); on
     * success returns to that note's detail view at the caller's page.
     */
    function EditBoard(editBoardId?: string, editCurrentPage?: string) {

        if (!$formEditBoard.valid()) {
            return false;
        }

        let title = $editBoardTitle.val();
        let content = $editBoardContent.val();
        let locked = $editBoardLocked.is(":checked");

        let formData = new FormData();
        formData.append("Id", editBoardId as any);
        formData.append("Title", title as any);
        formData.append("Locked", locked as any);
        formData.append("Content", content as any);
        formData.append("UploadedFile", editUploadedFile);

        $.ajax({
            url: "/Management/EditPrivateNoteBoard",
            type: "POST",
            headers: { "RequestVerificationToken": $__RequestVerificationToken.val() as string },
            dataType: "json",
            data: formData,
            contentType: false,
            processData: false,
            success: function(data) {
                if (data.result) {
                    alert(data.message);
                    window.location.href = "/Management/PrivateNote?method=detail" + "&boardId=" + editBoardId + "&page=" + editCurrentPage;
                } else {
                    toastr.error(data.error);
                    $loading.hide();
                }
            }
        });

        return false;
    }

    /** Opens the "delete this note?" confirm modal (static backdrop, no Esc). */
    function ConfirmDeleteBoard() {
        $confirmDeleteBoardDialogModal.modal({
            keyboard: false,
            backdrop: "static"
        });

        $confirmDeleteBoardDialogModal.modal("toggle");
        $confirmDeleteBoardDialogModal.modal("show");
    }

    /**
     * Confirmed note delete: verify it still exists (`IsBoardExists`), then POST
     * `DeleteBoard` with the server's current id. Nested so a stale id can't be used.
     */
    function DeleteBoard(detailBoardId?: string) {

        $.ajax({
            url: "/Management/IsBoardExists" + "?id=" + detailBoardId,
            type: "POST",
            headers: { "RequestVerificationToken": $__RequestVerificationToken.val() as string },
            dataType: "json",
            data: null as any,
            contentType: "application/json; charset=utf-8",
            success: function(data) {
                if (data.result) {

                    let paramValue = JSON.stringify({
                        Id: data.privateNoteBoard.id
                    });

                    $.ajax({
                        url: "/Management/DeleteBoard",
                        type: "POST",
                        headers: { "RequestVerificationToken": $__RequestVerificationToken.val() as string },
                        dataType: "json",
                        data: paramValue,
                        contentType: "application/json; charset=utf-8",
                        success: function(data) {
                            if (data.result) {
                                $confirmDeleteBoardDialogModal.modal("hide");
                                alert(data.message);
                                window.location.href = "/Management/PrivateNote";
                            } else {
                                toastr.error(data.error);
                            }
                        }
                    });
                } else {
                    toastr.error(data.error);
                }
            }
        });
    }

    /**
     * Posts a comment on the detail view (non-empty check is a UX mirror); on
     * success reloads the detail view so the comment appears.
     */
    function WritePrivateNoteComment(detailBoardId?: string, detailCurrentPage?: string, errorMessage?: string) {

        let content = $writePrivateNoteCommentContent.val();

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
            url: "/Management/WritePrivateNoteComment",
            type: "POST",
            headers: { "RequestVerificationToken": $__RequestVerificationToken.val() as string },
            dataType: "json",
            data: paramValue,
            contentType: "application/json; charset=utf-8",
            success: function(data) {
                if (data.result) {
                    window.location.href = "/Management/PrivateNote?method=detail" + "&boardId=" + data.boardId + "&page=" + data.page;
                } else {
                    toastr.error(data.error);
                }
            }
        });

        return false;
    }

    /** Deletes one comment by id, then reloads the detail view. */
    function DeleteComment(commentId?: string, boardId?: string, page?: string) {
        $.ajax({
            url: "/Management/DeleteComment" + "?id=" + commentId,
            type: "POST",
            headers: { "RequestVerificationToken": $__RequestVerificationToken.val() as string },
            dataType: "json",
            data: null as any,
            contentType: "application/json; charset=utf-8",
            success: function(data) {
                if (data.result) {
                    window.location.href = "/Management/PrivateNote?method=detail" + "&boardId=" + boardId + "&page=" + page;
                } else {
                    toastr.error(data.error);
                }
            }
        });
    }

    // --- Button / form wiring -----------------------------------------
    // Every handler is cleared with `.off` first. Navigation buttons carry their
    // target in a `data-*` attribute.

    $btnPrivateNoteShowWriteBoardLoading.off("click").on("click", function() {
        return ShowWriteBoardLoading();
    });

    $btnPrivateNoteModify.off("click").on("click", function() {
        location.href = $(this).attr("data-link")!;
    });

    $btnPrivateNoteConfirmDeleteBoard.off("click").on("click", function() {
        ConfirmDeleteBoard();
    });

    $btnPrivateNoteList.off("click").on("click", function() {
        location.href = $(this).attr("data-link")!;
    });

    $btnPrivateNoteSubmitModify.off("click").on("click", function() {
        return ShowEditBoardLoading();
    });

    $btnPrivateNoteWrite.off("click").on("click", function() {
        location.href = "/Management/PrivateNote?method=write";
    });

    $btnPrivateNoteSearchBoard.off("click").on("click", function() {
        SearchBoard();
    });

    $btnPrivateNoteDeleteBoard.off("click").on("click", function() {
        DeleteBoard($(this).attr("data-boardId"));
    });

    // Enter (keyCode 13) in the search box runs the search.
    $btnPrivateNoteSearchText.off("keydown").on("keydown", function(event) {
        if (event.key === "Enter") {
            SearchBoard();
        }
    });

    // Per-comment delete links (a NodeList, hence the class selector).
    $(".aPrivateNoteDeleteComment").off("click").on("click", function(event) {
        event.preventDefault();
        DeleteComment($(this).attr("data-commentId"), $(this).attr("data-detailBoardId"), $(this).attr("data-detailCurrentPage"));
    });

    $formWritePrivateNoteComment.off("submit").on("submit", function() {
        return WritePrivateNoteComment($(this).attr("data-detailBoardId"), $(this).attr("data-detailCurrentPage"), $(this).attr("data-errorMessage"));
    });

    $formWriteBoard.off("submit").on("submit", function() {
        return WriteBoard();
    });

    $formEditBoard.off("submit").on("submit", function() {
        return EditBoard($(this).attr("data-editBoardId"), $(this).attr("data-editCurrentPage"));
    });

    // Attachment inputs: validate + stash on selection.
    $writeUploadedFile.off("change").on("change", function(event) {
        return WriteUploadFile(event.currentTarget as HTMLInputElement, $(event.currentTarget).attr("data-errorMessage"));
    });

    $editUploadedFile.off("change").on("change", function(event) {
        return EditUploadFile(event.currentTarget as HTMLInputElement, $(event.currentTarget).attr("data-errorMessage"));
    });

    // --- On ready: rehydrate embedded images + wire attachment download ---
    $(function() {
        // Detail view: body rendered hidden with base64 `data-file` images; swap
        // each to an object URL in a detached document, then reveal.
        if ($detailBoardContent.length > 0 && $detailBoardContent.is(":hidden")) {
            const parser = new DOMParser();
            const htmlDoc = parser.parseFromString($detailBoardContent.html(), "text/html");

            const imgTags = htmlDoc.querySelectorAll<HTMLImageElement>("img[data-file]");

            imgTags.forEach(function(imgTag) {
                const base64Data = imgTag.getAttribute("data-file");
                const contentType = imgTag.getAttribute("data-contenttype");

                if (base64Data && contentType) {
                    const blob = base64ToBlob(base64Data, contentType);
                    imgTag.src = URL.createObjectURL(blob);
                    imgTag.removeAttribute("data-file");
                    imgTag.removeAttribute("data-contenttype");
                }
            });

            const updatedHtml = htmlDoc.body.innerHTML;
            $detailBoardContent.html(updatedHtml);
            ReleaseObjectUrlsOnLoad($detailBoardContent[0]);
            $detailBoardContent.show();
        } else if ($editBoardContent.length > 0 && $divEditBoardContent.is(":hidden")) {
            // Edit view: same rehydration, but the HTML lives inside summernote.
            const parser = new DOMParser();
            const htmlDoc = parser.parseFromString($editBoardContent.summernote("code"), "text/html");

            const imgTags = htmlDoc.querySelectorAll<HTMLImageElement>("img[data-file]");

            imgTags.forEach(function(imgTag) {
                const base64Data = imgTag.getAttribute("data-file");
                const contentType = imgTag.getAttribute("data-contenttype");

                if (base64Data && contentType) {
                    const blob = base64ToBlob(base64Data, contentType);
                    imgTag.src = URL.createObjectURL(blob);
                    imgTag.removeAttribute("data-file");
                    imgTag.removeAttribute("data-contenttype");
                }
            });

            const updatedHtml = htmlDoc.body.innerHTML;
            $editBoardContent.summernote("code", updatedHtml);
            ReleaseObjectUrlsOnLoad($editBoardContent.next(".note-editor")[0]);
            $divEditBoardContent.show();
        }

        // Attachment download link (detail or edit view): base64 -> Blob -> a
        // throwaway `<a download>` that is clicked and cleaned up.
        if ($aDetailBoardAttachedFile.length > 0) {
            $aDetailBoardAttachedFile.off("click").on("click", function(event) {
                event.preventDefault();
                let base64Data = $(this).attr("data-file");
                let contentType = $(this).attr("data-contenttype");
                let name = $(this).attr("data-name");

                if (base64Data && contentType) {
                    let blob = base64ToBlob(base64Data, contentType);
                    let url = URL.createObjectURL(blob);
                    let a = document.createElement("a");
                    try {
                        a.href = url;
                        a.download = name!;
                        a.click();
                    } finally {
                        setTimeout(() => URL.revokeObjectURL(url), 100);
                        a.remove();
                    }
                }
            });
        } else if ($aEditBoardAttachedFile.length > 0) {
            $aEditBoardAttachedFile.off("click").on("click", function(event) {
                event.preventDefault();
                let base64Data = $(this).attr("data-file");
                let contentType = $(this).attr("data-contenttype");
                let name = $(this).attr("data-name");

                if (base64Data && contentType) {
                    let blob = base64ToBlob(base64Data, contentType);
                    let url = URL.createObjectURL(blob);
                    let a = document.createElement("a");
                    try {
                        a.href = url;
                        a.download = name!;
                        a.click();
                    } finally {
                        setTimeout(() => URL.revokeObjectURL(url), 100);
                        a.remove();
                    }
                }
            });
        }
    });
})();
