/**
 * Script for the **admin** Free Forum page (`Views/Forum/FreeForum.cshtml`).
 * One `.cshtml` renders several sub-views by a `method` query-string param
 * (list / write / edit / detail); this script wires whichever controls are on
 * the page.
 *
 * Features:
 *   • Rich-text post body via summernote, including image upload (each pasted
 *     image is POSTed, returned as base64, and re-inserted as an object URL).
 *   • Attachment upload with a client-side size check; the chosen `File` is
 *     stashed and sent with the form as multipart `FormData`.
 *   • Write / edit / delete board, write / delete comment — all AJAX, each
 *     redirecting on success. Server rule failures come back as
 *     `{ result:false, error }` and become `toastr.error`.
 *   • Search, "back to list", inline-image rehydration and attachment download,
 *     shared with the anonymous version of this page.
 *
 * IIFE-wrapped, no `import` / `export`. Client checks are UX mirrors; the
 * controller re-validates everything.
 */
(function() {
    // Cached element references. Many of these exist only on one sub-view, so
    // their `.length` is checked before use.
    const $writeBoardContent = $("#writeBoardContent");
    const $editBoardContent = $("#editBoardContent");
    const $confirmDeleteBoardDialogModal = $("#confirmDeleteBoardDialogModal");
    const $btnFreeForumSearchText = $("#btnFreeForumSearchText");
    const $writeUploadedFile = $("#writeUploadedFile");
    const $editUploadedFile = $("#editUploadedFile");
    const $formWriteBoard = $("#formWriteBoard");
    const $searchType = $("#searchType");
    const $loading = $("#loading");
    const $formEditBoard = $("#formEditBoard");
    const $writeBoardTitle = $("#writeBoardTitle");
    const $writeBoardNoticed = $("#writeBoardNoticed");
    const $writeBoardLocked = $("#writeBoardLocked");
    const $editBoardTitle = $("#editBoardTitle");
    const $editBoardLocked = $("#editBoardLocked");
    const $writeFreeCommentContent = $("#writeFreeCommentContent");
    const $btnFreeForumShowWriteBoardLoading = $("#btnFreeForumShowWriteBoardLoading");
    const $btnFreeForumModify = $("#btnFreeForumModify");
    const $btnFreeForumConfirmDeleteBoard = $("#btnFreeForumConfirmDeleteBoard");
    const $btnFreeForumList = $("#btnFreeForumList");
    const $btnFreeForumSubmitModify = $("#btnFreeForumSubmitModify");
    const $btnFreeForumWrite = $("#btnFreeForumWrite");
    const $btnFreeForumSearchBoard = $("#btnFreeForumSearchBoard");
    const $btnFreeForumDeleteBoard = $("#btnFreeForumDeleteBoard");
    const $formWriteFreeComment = $("#formWriteFreeComment");
    const $detailBoardContent = $("#detailBoardContent");
    const $divEditBoardContent = $("#divEditBoardContent");
    const $aDetailBoardAttachedFile = $("#aDetailBoardAttachedFile");
    const $aEditBoardAttachedFile = $("#aEditBoardAttachedFile");
    const $aFreeForumDeleteComment = $(".aFreeForumDeleteComment");
    const $__RequestVerificationToken = $("input[name=\"__RequestVerificationToken\"]");
    // Authoritative in ServerSetting.MaxAttachedFileSizeBytes (server); mirrored here for form UX
    // only. Falls back to the shared per-role default (_Layout/site.ts) if the hidden field is
    // missing or unparseable.
    const maxFileSize = parseInt($("#maxAttachedFileSizeBytes").val() as string) || (window as any).MaroikDefaultMaxAttachedFileSizeBytes;
    // Editor height (px) of both summernote instances.
    const boardHeight = 300;

    /** base64 -> Blob, for turning server-embedded image payloads into object URLs. */
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

    // Only one localized value is needed here — the summernote UI language.
    // `any` avoids typing a one-off bag of `.val()` strings.
    const localizer: any = {
        IETFLanguageTag: $("#localizerIETFLanguageTag").val()
    };

    // Rich-text editor for the "write" body. `onImageUpload` fires for every
    // image the user drops/pastes; each is uploaded and re-inserted below.
    $writeBoardContent.summernote({
        height: boardHeight,
        lang: localizer.IETFLanguageTag,
        callbacks: {
            onImageUpload: function(files: any) {
                for (let i = 0; i < files.length; i++) {
                    WriteUploadImageFile(files[i]);
                }
            }
        }
    });

    // Rich-text editor for the "edit" body — same wiring.
    $editBoardContent.summernote({
        height: boardHeight,
        lang: localizer.IETFLanguageTag,
        callbacks: {
            onImageUpload: function(files: any) {
                for (let i = 0; i < files.length; i++) {
                    EditUploadImageFile(files[i]);
                }
            }
        }
    });

    // The attachment chosen in each form, held until the form is submitted.
    let writeUploadedFile: any;
    let editUploadedFile: any;

    /**
     * `change` handler for the write-form attachment input: reject the file (and
     * clear the input) if it exceeds `maxFileSize`, otherwise stash it in
     * `writeUploadedFile` for `WriteBoard` to send.
     */
    function WriteUploadFile(obj: HTMLInputElement, errorMessage?: string) {
        if (!obj.files || obj.files.length === 0) {
            return;
        }
        if (obj.files[0].size > maxFileSize) {
            alert(errorMessage);
            $writeUploadedFile.val("");
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
            $editUploadedFile.val("");
            return false;
        } else {
            editUploadedFile = obj.files[0];
        }
    }

    /** Reloads the list filtered by the chosen search type + text. */
    function SearchBoard() {
        window.location.href = "/Forum/FreeForum?searchType=" + encodeURIComponent($searchType.val() as string) + "&searchText=" + encodeURIComponent($btnFreeForumSearchText.val() as string);
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
     * summernote `onImageUpload` for the write editor: POST the raw image file,
     * get `{ file: { fileContents(base64), contentType }, filePath }` back, then
     * insert it into the editor as a size-capped `<img>` backed by an object URL
     * (revoked once the browser has decoded it). `filePath` is stored as `alt`
     * so the server can resolve the real stored image on save.
     */
    function WriteUploadImageFile(file: File) {

        let formData = new FormData();
        formData.append("summernoteImageFile", file);

        $.ajax({
            url: "/Forum/UploadImageFile",
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
            url: "/Forum/UploadImageFile",
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
     * Submits a new post. Validates, then builds multipart `FormData` (title,
     * body HTML, the two flags, and the stashed attachment) and POSTs it. On
     * success alerts and returns to the list; on failure toasts and hides the
     * overlay. The `as any` casts are because `FormData.append` wants
     * `string | Blob` and `.val()` / booleans are wider. Returns `false` so the
     * form never navigates on its own.
     */
    function WriteBoard() {

        if (!$formWriteBoard.valid()) {
            return false;
        }

        let title = $writeBoardTitle.val();
        let content = $writeBoardContent.val();
        let noticed = $writeBoardNoticed.is(":checked");
        let locked = $writeBoardLocked.is(":checked");

        let formData = new FormData();
        formData.append("Title", title as any);
        formData.append("Content", content as any);
        formData.append("Noticed", noticed as any);
        formData.append("Locked", locked as any);
        formData.append("UploadedFile", writeUploadedFile);

        $.ajax({
            url: "/Forum/WriteFreeBoard",
            type: "POST",
            headers: { "RequestVerificationToken": $__RequestVerificationToken.val() as string },
            dataType: "json",
            data: formData,
            contentType: false,
            processData: false,
            success: function(data) {
                if (data.result) {
                    alert(data.message);
                    window.location.href = "/Forum/FreeForum";
                } else {
                    toastr.error(data.error);
                    $loading.hide();
                }
            }
        });

        return false;
    }

    /**
     * Submits an edit to an existing post. Same multipart shape as `WriteBoard`
     * minus the notice flag (pinning is decided at write time only), plus the
     * board `Id`; on success returns to that post's detail view at the caller's page.
     *
     * @param editBoardId    id of the post being edited (from a `data-*` attribute).
     * @param editCurrentPage list page to return to.
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
            url: "/Forum/EditFreeBoard",
            type: "POST",
            headers: { "RequestVerificationToken": $__RequestVerificationToken.val() as string },
            dataType: "json",
            data: formData,
            contentType: false,
            processData: false,
            success: function(data) {
                if (data.result) {
                    alert(data.message);
                    window.location.href = "/Forum/FreeForum?method=detail" + "&boardId=" + editBoardId + "&page=" + editCurrentPage;
                } else {
                    toastr.error(data.error);
                    $loading.hide();
                }
            }
        });

        return false;
    }

    /** Opens the "delete this post?" confirm modal (static backdrop, no Esc). */
    function ConfirmDeleteBoard() {
        $confirmDeleteBoardDialogModal.modal({
            keyboard: false,
            backdrop: "static"
        });

        $confirmDeleteBoardDialogModal.modal("toggle");
        $confirmDeleteBoardDialogModal.modal("show");
    }

    /**
     * Confirmed post delete: verify the post still exists (`IsBoardExists`), then
     * POST `DeleteBoard` with the server's current id. Nested so a stale id in
     * the markup can't be used.
     */
    function DeleteBoard(detailBoardId?: string) {

        $.ajax({
            url: "/Forum/IsBoardExists" + "?id=" + detailBoardId,
            type: "POST",
            headers: { "RequestVerificationToken": $__RequestVerificationToken.val() as string },
            dataType: "json",
            data: null as any,
            contentType: "application/json; charset=utf-8",
            success: function(data) {
                if (data.result) {

                    let paramValue = JSON.stringify({
                        Id: data.freeBoard.id
                    });

                    $.ajax({
                        url: "/Forum/DeleteBoard",
                        type: "POST",
                        headers: { "RequestVerificationToken": $__RequestVerificationToken.val() as string },
                        dataType: "json",
                        data: paramValue,
                        contentType: "application/json; charset=utf-8",
                        success: function(data) {
                            if (data.result) {
                                $confirmDeleteBoardDialogModal.modal("hide");
                                alert(data.message);
                                window.location.href = "/Forum/FreeForum";
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
     * Posts a comment on the detail view. The non-empty check is a UX mirror
     * (the server rejects blank comments too); on success reloads the detail
     * view so the new comment appears.
     */
    function WriteFreeComment(detailBoardId?: string, detailCurrentPage?: string, errorMessage?: string) {

        let content = $writeFreeCommentContent.val();

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
            url: "/Forum/WriteFreeComment",
            type: "POST",
            headers: { "RequestVerificationToken": $__RequestVerificationToken.val() as string },
            dataType: "json",
            data: paramValue,
            contentType: "application/json; charset=utf-8",
            success: function(data) {
                if (data.result) {
                    window.location.href = "/Forum/FreeForum?method=detail" + "&boardId=" + data.boardId + "&page=" + data.page;
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
            url: "/Forum/DeleteComment" + "?id=" + commentId,
            type: "POST",
            headers: { "RequestVerificationToken": $__RequestVerificationToken.val() as string },
            dataType: "json",
            data: null as any,
            contentType: "application/json; charset=utf-8",
            success: function(data) {
                if (data.result) {
                    window.location.href = "/Forum/FreeForum?method=detail" + "&boardId=" + boardId + "&page=" + page;
                } else {
                    toastr.error(data.error);
                }
            }
        });
    }

    // --- Button / form wiring -------------------------------------------
    // Every handler is cleared with `.off` first so a re-run cannot double-bind.
    // Buttons that just navigate carry their target in a `data-*` attribute.

    $btnFreeForumShowWriteBoardLoading.off("click").on("click", function() {
        return ShowWriteBoardLoading();
    });

    $btnFreeForumModify.off("click").on("click", function() {
        location.href = $(this).attr("data-link")!;
    });

    $btnFreeForumConfirmDeleteBoard.off("click").on("click", function() {
        ConfirmDeleteBoard();
    });

    $btnFreeForumList.off("click").on("click", function() {
        location.href = $(this).attr("data-link")!;
    });

    $btnFreeForumSubmitModify.off("click").on("click", function() {
        return ShowEditBoardLoading();
    });

    $btnFreeForumWrite.off("click").on("click", function() {
        location.href = "/Forum/FreeForum?method=write";
    });

    $btnFreeForumSearchBoard.off("click").on("click", function() {
        SearchBoard();
    });

    $btnFreeForumDeleteBoard.off("click").on("click", function() {
        DeleteBoard($(this).attr("data-boardId"));
    });

    // Enter in the search box runs the search.
    $btnFreeForumSearchText.off("keydown").on("keydown", function(event) {
        if (event.key === "Enter") {
            SearchBoard();
        }
    });

    $formWriteFreeComment.off("submit").on("submit", function() {
        return WriteFreeComment($(this).attr("data-detailBoardId"), $(this).attr("data-detailCurrentPage"), $(this).attr("data-errorMessage"));
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

    // Per-comment delete links (a NodeList, hence the class selector).
    $aFreeForumDeleteComment.off("click").on("click", function(event) {
        event.preventDefault();
        DeleteComment($(this).attr("data-commentId"), $(this).attr("data-detailBoardId"), $(this).attr("data-detailCurrentPage"));
    });

    // --- On ready: rehydrate embedded images + wire attachment download ---
    $(function() {
        // Detail view: the body is rendered hidden with base64 `data-file`
        // images; swap each to an object URL in a detached document, then reveal.
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
            // Edit view: same rehydration, but the HTML lives inside summernote
            // (`summernote('code')` gets/sets it) rather than a plain element.
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

        // Attachment download link (detail or edit view): the file is not embedded in the page.
        // It is requested from a POST action that checks the post's visibility again and streams
        // the file; a refusal comes back as JSON `{ result, error }` instead of a file.
        function DownloadAttachedFile(this: HTMLElement, event: JQuery.TriggeredEvent) {
            event.preventDefault();
            let boardId = $(this).attr("data-boardid");
            let name = $(this).attr("data-name");
            if (!boardId) {
                return;
            }

            $.ajax({
                url: "/Forum/DownloadFreeBoardAttachedFile",
                type: "POST",
                headers: { "RequestVerificationToken": $__RequestVerificationToken.val() as string },
                data: { boardId: boardId },
                xhrFields: { responseType: "blob" },
                success: function(data: Blob) {
                    if (data.type.indexOf("application/json") === 0) {
                        data.text().then(function(text) {
                            toastr.error(JSON.parse(text).error);
                        });
                        return;
                    }

                    let url = URL.createObjectURL(data);
                    let a = document.createElement("a");
                    try {
                        a.href = url;
                        a.download = name!;
                        a.click();
                    } finally {
                        // Revoke after a tick so the download has started; drop the <a>.
                        setTimeout(() => URL.revokeObjectURL(url), 100);
                        a.remove();
                    }
                }
            });
        }

        if ($aDetailBoardAttachedFile.length > 0) {
            $aDetailBoardAttachedFile.off("click").on("click", DownloadAttachedFile);
        } else if ($aEditBoardAttachedFile.length > 0) {
            $aEditBoardAttachedFile.off("click").on("click", DownloadAttachedFile);
        }
    });
})();
