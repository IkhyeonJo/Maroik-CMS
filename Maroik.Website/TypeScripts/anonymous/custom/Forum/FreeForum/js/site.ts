/**
 * Script for the **anonymous** view of the Free Forum
 * (`Views/Forum/FreeForum.cshtml` rendered for signed-out visitors — list,
 * search and read-only post detail).
 *
 * Responsibilities:
 *
 *   1. Convert every server-rendered UTC timestamp on the page to the visitor's
 *      local time (the server emits UTC; the browser knows the timezone).
 *   2. Wire the search box / buttons (navigations with query-string params).
 *   3. Rehydrate inline images in the post body (shipped as base64 in `data-*`
 *      attributes and turned into object URLs here), and download the post's
 *      attachment from its POST endpoint when its link is clicked.
 *
 * IIFE-wrapped, no `import` / `export`. Most of the work runs inside a jQuery
 * `$(function)` ready callback.
 */
(function() {
    // Cached element references. `#free_forum_detail_updated` and the table only
    // exist on the relevant sub-view, so their `.length` is checked before use.
    const $freeForumDetailUpdated = $("#free_forum_detail_updated");
    const $tblFreeForum = $("#tblFreeForum");
    const $searchType = $("#searchType");
    const $btnFreeForumSearchText = $("#btnFreeForumSearchText");
    const $btnFreeForumSearchBoard = $("#btnFreeForumSearchBoard");
    const $btnFreeForumWrite = $("#btnFreeForumWrite");
    const $btnFreeForumList = $("#btnFreeForumList");
    const $formWriteFreeComment = $("#formWriteFreeComment");
    const $detailBoardContent = $("#detailBoardContent");
    const $aDetailBoardAttachedFile = $("#aDetailBoardAttachedFile");
    const $__RequestVerificationToken = $("input[name=\"__RequestVerificationToken\"]");

    /**
     * Decodes a base64 string into a `Blob` of the given MIME type. Used to turn
     * the `data-file` payloads the server embeds into object URLs for `<img>`
     * sources.
     *
     * @param base64 raw base64 (no `data:` prefix).
     * @param mime   MIME type for the resulting Blob.
     */
    function base64ToBlob(base64: string, mime: string) {
        const byteCharacters = atob(base64);
        const byteNumbers = new Array<number>(byteCharacters.length);
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

    $(function() {

        // --- UTC -> local time -------------------------------------------------
        // Post-detail "updated" stamp (present only on the detail sub-view).
        if ($freeForumDetailUpdated.length > 0) {
            let utcDateStr = $freeForumDetailUpdated.text();
            let utcDate = new Date(utcDateStr);
            let localDateStr = utcDate.toLocaleString();
            $freeForumDetailUpdated.text(localDateStr);
        }

        // "Created" column of every row in the list table. The cell sometimes
        // wraps the value in a <span>, so read/write whichever is present.
        $tblFreeForum.find("tbody").children("tr").find(".free_forum_board_created").each(function() {
            let $element = $(this);
            let $span = $element.find("span");
            let utcDateStr = $span.length > 0 ? $span.html() : $element.text();
            let utcDate = new Date(utcDateStr);
            let localDateStr = utcDate.toLocaleString();
            $span.length > 0 ? $span.html(localDateStr) : $element.text(localDateStr);
        });

        // Each comment's "created" stamp on the detail view.
        $(".free_forum_detail_comment_created").each(function() {
            let utcDateStr = $(this).text();
            let utcDate = new Date(utcDateStr);
            let localDateStr = utcDate.toLocaleString();
            $(this).text(localDateStr);
        });

        // --- Search / navigation --------------------------------------------
        /** Reloads the list filtered by the chosen search type + text. */
        function SearchBoard() {
            window.location.href = "/Forum/FreeForum?searchType=" + encodeURIComponent($searchType.val() as string) + "&searchText=" + encodeURIComponent($btnFreeForumSearchText.val() as string);
        }

        $btnFreeForumSearchBoard.off("click").on("click", function() {
            SearchBoard();
        });

        $btnFreeForumWrite.off("click").on("click", function() {
            location.href = "/Forum/FreeForum?method=write";
        });

        // "Back to list" keeps the caller's paging/search state, carried in the
        // button's `data-link` attribute (always present here, hence the `!`).
        $btnFreeForumList.off("click").on("click", function() {
            location.href = $(this).attr("data-link")!;
        });

        // Enter in the search box triggers the search instead of submitting.
        $btnFreeForumSearchText.off("keydown").on("keydown", function(event) {
            if (event.key === "Enter") {
                SearchBoard();
            }
        });

        // In the comment form, swallow Enter so a stray newline can't submit it
        // (returning false from a keydown handler cancels the key).
        $formWriteFreeComment.off("keydown").on("keydown", function(event) {
            return event.key !== "Enter";
        });

        // --- Inline images in the post body --------------------------------
        // The body is rendered hidden with its images as base64 `data-file`
        // attributes; swap each to an object URL, then reveal it. Parsing into a
        // detached document keeps the work off the live DOM until it is ready.
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
        }

        // --- Attachment download -------------------------------------------
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
                // Without a declared dataType jQuery infers "json" from a refusal's Content-Type and fails to
                // parse the Blob (parsererror -> the layout's ajaxError redirect); "binary" hands back the Blob as is.
                dataType: "binary",
                success: function(data: Blob) {
                    if (data.type.indexOf("application/json") === 0) {
                        data.text().then(function(text) {
                            toastr.error((JSON.parse(text) as FailedReply).error);
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
        }
    });
})();
