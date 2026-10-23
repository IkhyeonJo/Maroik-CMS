/**
 * Script for the **File Manager** page (`Views/FileManager/UserIndex.cshtml`) — the user's drive ("My Drive").
 *
 *   • Left: the folder tree (jsTree; folders only, each with its item count), Upload / New folder, the storage usage bar.
 *     Clicking a folder opens it; a folder can be dragged onto another folder (or the root) to move it.
 *   • Right: the breadcrumb, a drive-wide name search, the grid / list toggle (remembered in localStorage), the items, the item
 *     count and — when items are selected — the "N selected · Download · Move · Delete" bar. List rows carry Download / Preview /
 *     Rename / Delete buttons; grid tiles are selected with a click, Ctrl-click and Shift-click, and opened with a double click.
 *   • Drag and drop: items dragged onto a folder (tile, row or tree node) move there; files or a whole folder dropped from the PC
 *     upload into the open folder, or into the tree folder they are dropped on.
 *   • Uploads go file by file in chunks: the batch's total is checked against the quota first, the folders of a folder upload are
 *     created (merging into existing ones), a name conflict asks Overwrite / Keep both / Skip (with "Apply to all"), and a failed
 *     file is skipped while the rest continue; a success / failure list closes the batch.
 *
 * Every rule is the server's: the replies arrive with sizes, local times, icons, usage and the preview flag already decided, and
 * the server re-checks every name, size and quota. The 2 GB check before an upload starts is a UX mirror of
 * FileManagerUploadPolicy.MaxFileBytes (read from #maxFileBytes). IIFE-wrapped, no `import` / `export`.
 */
(function() {
    // The runtime-check helpers the _Layout script defines (see TypeScripts/global.d.ts).
    const { check, replies, onReply, onReplyText, fieldValue, conform, escapeHtml } = window;

    // The replies this page reads, mirroring the controller's Json(...) results.
    const itemCheck = check.object({
        id: check.number, parentId: check.nullable(check.number), name: check.string, isFolder: check.boolean, size: check.string,
        modified: check.string, icon: check.string, previewable: check.boolean, location: check.string, itemCount: check.number,
    });
    type Item = Checked<typeof itemCheck>;
    const driveReply = replies.read({
        drive: check.object({ usage: check.string, usedPercent: check.number, rootItemCount: check.number, folders: check.array(itemCheck) }),
    });
    const folderReply = replies.read({
        folder: check.object({
            folderId: check.nullable(check.number),
            breadcrumb: check.array(check.object({ id: check.number, name: check.string })),
            items: check.array(itemCheck),
        }),
    });
    const searchReply = replies.read({ items: check.array(itemCheck) });
    const filesReply = replies.read({ files: check.array(check.object({ id: check.number, name: check.string })) });
    const doneReply = replies.read({});
    const folderCreatedReply = replies.write({ folderId: check.number });
    const beginReply = replies.read({ conflict: check.boolean, uploadId: check.string, name: check.string, chunkBytes: check.number });
    const chunkReply = replies.read({ length: check.number });
    const selectNodeEvent = check.object({ node: check.object({ id: check.string }) });
    const moveNodeEvent = check.object({ node: check.object({ id: check.string }), parent: check.string });

    // Cached references.
    const $tree = $("#fileManagerTree");
    const $items = $("#fileManagerItems");
    const $breadcrumb = $("#fileManagerBreadcrumb");
    const $search = $("#fileManagerSearch");
    const $itemCount = $("#fileManagerItemCount");
    const $usage = $("#fileManagerUsage");
    const $usageBar = $("#fileManagerUsageBar");
    const $selectionBar = $("#fileManagerSelectionBar");
    const $selectionCount = $("#fileManagerSelectionCount");
    const $btnGridView = $("#btnGridView");
    const $btnListView = $("#btnListView");
    const $fileInput = $("#fileManagerFileInput");
    const $folderInput = $("#fileManagerFolderInput");
    const $newFolderModal = $("#newFolderModal");
    const $formNewFolder = $("#formNewFolder");
    const $newFolderName = $("#newFolderName");
    const $renameModal = $("#renameModal");
    const $formRename = $("#formRename");
    const $renameId = $("#renameId");
    const $renameName = $("#renameName");
    const $moveModal = $("#moveModal");
    const $moveTarget = $("#moveTarget");
    const $conflictModal = $("#conflictModal");
    const $conflictMessage = $("#conflictMessage");
    const $conflictApplyToAll = $("#conflictApplyToAll");
    const $uploadModal = $("#uploadModal");
    const $uploadProgressText = $("#uploadProgressText");
    const $uploadProgressBar = $("#uploadProgressBar");
    const $uploadResults = $("#uploadResults");
    const $btnUploadClose = $("#btnUploadClose");
    const $previewModal = $("#previewModal");
    const $previewTitle = $("#previewTitle");
    const $previewImage = $("#previewImage");
    const $confirmDeleteModal = $("#confirmDeleteModal");
    const $__RequestVerificationToken = $("input[name=\"__RequestVerificationToken\"]");

    // Authoritative in FileManagerUploadPolicy.MaxFileBytes (server); mirrored here for form UX only.
    const maxFileBytes = Number(fieldValue($("#maxFileBytes")));
    // Localized texts.
    const text = {
        myDrive: fieldValue($("#localizerMyDrive")),
        itemCount: fieldValue($("#localizerItemCount")),
        selectedCount: fieldValue($("#localizerSelectedCount")),
        emptyFolder: fieldValue($("#localizerEmptyFolder")),
        noResults: fieldValue($("#localizerNoResults")),
        name: fieldValue($("#localizerName")),
        size: fieldValue($("#localizerSize")),
        modified: fieldValue($("#localizerModified")),
        location: fieldValue($("#localizerLocation")),
        actions: fieldValue($("#localizerActions")),
        download: fieldValue($("#localizerDownload")),
        preview: fieldValue($("#localizerPreview")),
        rename: fieldValue($("#localizerRename")),
        delete: fieldValue($("#localizerDelete")),
        conflict: fieldValue($("#localizerConflict")),
        uploading: fieldValue($("#localizerUploading")),
        uploadSummary: fieldValue($("#localizerUploadSummary")),
        uploaded: fieldValue($("#localizerUploaded")),
        skipped: fieldValue($("#localizerSkipped")),
        fileTooLarge: fieldValue($("#localizerFileTooLarge")),
    };

    /** Replaces {0}, {1}, … in a localized template. */
    function format(template: string, ...values: (string | number)[]): string {
        return values.reduce<string>((result, value, index) => result.split(`{${index}}`).join(String(value)), template);
    }

    // -- State ------------------------------------------------------------------------

    /** The open folder (null = the root). */
    let currentFolderId: number | null = null;
    /** The items on screen (the open folder's, or the search results). */
    let shownItems: Item[] = [];
    /** True while search results are shown. */
    let searching = false;
    /** The selected item ids. */
    const selected = new Set<number>();
    /** The index of the last clicked item (for Shift-click). */
    let anchorIndex = -1;
    /** Every folder of the drive (the Move list). */
    let driveFolders: Item[] = [];
    /** The items a Move / Delete acts on. */
    let pendingIds: number[] = [];

    /** The remembered view; localStorage is a per-viewer convenience and may be unavailable. */
    const viewKey = "maroik.fileManager.view";
    let view: "grid" | "list" = "grid";
    try {
        view = window.localStorage.getItem(viewKey) === "list" ? "list" : "grid";
    } catch {
        // No storage (private window, blocked site data): the grid view is shown.
        view = "grid";
    }

    // -- Requests ---------------------------------------------------------------------

    /**
     * POSTs `body` as JSON and resolves with the reply once it passes `reply`; a reply that does not shows the generic error
     * (onReply) and resolves with null, so a batch can go on.
     */
    function post<T>(url: string, body: unknown, reply: Check<T>): Promise<T | null> {
        return new Promise((resolve) => {
            $.ajax({
                url: url,
                type: "POST",
                headers: { "RequestVerificationToken": fieldValue($__RequestVerificationToken) },
                dataType: "json",
                data: JSON.stringify(body),
                contentType: "application/json; charset=utf-8",
                success: function(data: unknown) {
                    onReply(reply, resolve)(data);
                    if (!reply.is(data)) resolve(null);
                }
            });
        });
    }

    /** POSTs a multipart form (an upload chunk); resolves like {@link post}. */
    function postForm<T>(url: string, form: FormData, reply: Check<T>): Promise<T | null> {
        return new Promise((resolve) => {
            $.ajax({
                url: url,
                type: "POST",
                headers: { "RequestVerificationToken": fieldValue($__RequestVerificationToken) },
                dataType: "json",
                data: form,
                processData: false,
                contentType: false,
                success: function(data: unknown) {
                    onReply(reply, resolve)(data);
                    if (!reply.is(data)) resolve(null);
                }
            });
        });
    }

    /** POSTs to a file action (Download / Preview) and resolves with the Blob, or null after toasting a JSON refusal. */
    function postForBlob(url: string): Promise<Blob | null> {
        return new Promise((resolve) => {
            $.ajax({
                url: url,
                type: "POST",
                headers: { "RequestVerificationToken": fieldValue($__RequestVerificationToken) },
                xhrFields: { responseType: "blob" },
                // Without a declared dataType jQuery infers "json" from a refusal's Content-Type and fails to parse the Blob.
                dataType: "binary",
                success: onReply(check.instance(Blob), function(data) {
                    if (data.type.indexOf("application/json") === 0) {
                        data.text().then(onReplyText(replies.failed, function(refusal) {
                            toastr.error(refusal.error);
                        }));
                        resolve(null);
                        return;
                    }
                    resolve(data);
                })
            });
        });
    }

    // -- Drive and folders ------------------------------------------------------------------

    /** The tree nodes of the drive: the root ("My Drive") and every folder, each labelled with its item count (escaped: jsTree renders HTML). */
    function treeNodes(rootItemCount: number, folders: Item[]): JsTreeNodeData[] {
        return [
            { id: "root", parent: "#", text: escapeHtml(`${text.myDrive} (${rootItemCount})`), state: { opened: true } },
            ...folders.map((f) => ({
                id: String(f.id),
                parent: f.parentId === null ? "root" : String(f.parentId),
                text: escapeHtml(`${f.name} (${f.itemCount})`),
            })),
        ];
    }

    /** Whether the tree was built. */
    let treeBuilt = false;

    /** Fetches the drive and redraws the tree and the usage bar. */
    async function LoadDrive() {
        const data = await post("/FileManager/GetDrive", {}, driveReply);
        if (data === null) return;
        if (!data.result) {
            toastr.error(data.error);
            return;
        }
        driveFolders = data.drive.folders;
        $usage.text(data.drive.usage);
        $usageBar.css("width", `${data.drive.usedPercent}%`).attr("aria-valuenow", data.drive.usedPercent);
        $usageBar.toggleClass("bg-danger", data.drive.usedPercent >= 100);
        const nodes = treeNodes(data.drive.rootItemCount, data.drive.folders);
        if (!treeBuilt) {
            treeBuilt = true;
            $tree.jstree({
                core: {
                    data: nodes,
                    // Only a move of a folder into a folder or the root (never beside the root).
                    check_callback: (operation, _node, parent) => operation !== "move_node" || parent.id !== "#",
                    multiple: false,
                    themes: { name: "default-dark", dots: true },
                },
                plugins: ["dnd"],
                dnd: { is_draggable: (dragged) => dragged.every((n) => n.id !== "root") },
            });
            return;
        }
        const instance = $tree.jstree(true);
        instance.settings.core.data = nodes;
        instance.refresh();
    }

    /** The folder id of a tree node id. */
    function folderIdOf(nodeId: string): number | null {
        return nodeId === "root" ? null : Number(nodeId);
    }

    /** Opens a folder: its breadcrumb and items. */
    async function OpenFolder(folderId: number | null) {
        const data = await post("/FileManager/GetFolder", { FolderId: folderId }, folderReply);
        if (data === null) return;
        if (!data.result) {
            toastr.error(data.error);
            return;
        }
        currentFolderId = data.folder.folderId;
        searching = false;
        $search.val("");
        RenderBreadcrumb(data.folder.breadcrumb);
        ShowItems(data.folder.items);
    }

    /** Reloads the drive and the open folder (or the search results) after a change. */
    async function Refresh() {
        await LoadDrive();
        const query = String($search.val() ?? "").trim();
        if (searching && query !== "") {
            await Search(query);
        } else {
            await OpenFolder(currentFolderId);
        }
    }

    /** Draws the breadcrumb: "My Drive" and each folder down to the open one; every step but the last opens its folder. */
    function RenderBreadcrumb(crumbs: { id: number; name: string }[]) {
        $breadcrumb.empty();
        const steps: { id: number | null; name: string }[] = [{ id: null, name: text.myDrive }, ...crumbs];
        steps.forEach((step, index) => {
            const $li = $("<li>").addClass("breadcrumb-item");
            if (index === steps.length - 1) {
                $li.addClass("active").text(step.name);
            } else {
                $("<a>").attr("href", "#").addClass("file-manager-crumb").attr("data-folder-id", step.id === null ? "" : String(step.id))
                    .text(step.name).appendTo($li);
            }
            $breadcrumb.append($li);
        });
    }

    /** Runs a drive-wide name search and shows the results with their locations. */
    async function Search(query: string) {
        const data = await post("/FileManager/Search", { Query: query }, searchReply);
        if (data === null) return;
        if (!data.result) {
            toastr.error(data.error);
            return;
        }
        searching = true;
        ShowItems(data.items);
    }

    // -- Items ----------------------------------------------------------------------------

    /** Replaces the shown items and clears the selection. */
    function ShowItems(items: Item[]) {
        shownItems = items;
        selected.clear();
        anchorIndex = -1;
        RenderItems();
    }

    /** Draws the shown items in the current view, the item count and the selection bar. */
    function RenderItems() {
        $items.empty();
        $btnGridView.toggleClass("active", view === "grid");
        $btnListView.toggleClass("active", view === "list");
        $itemCount.text(format(text.itemCount, shownItems.length));
        if (shownItems.length === 0) {
            $("<p>").addClass("text-muted text-center my-5").text(searching ? text.noResults : text.emptyFolder).appendTo($items);
        } else if (view === "grid") {
            RenderGrid();
        } else {
            RenderList();
        }
        RenderSelection();
    }

    /** The grid view: one tile per item (icon, name, size and date). */
    function RenderGrid() {
        const $row = $("<div>").addClass("row");
        for (const item of shownItems) {
            const $tile = $("<div>").addClass("file-manager-tile file-manager-item").attr("data-id", String(item.id)).attr("draggable", "true");
            $("<i>").addClass(`${item.icon} file-manager-tile-icon`).appendTo($tile);
            $("<div>").addClass("file-manager-tile-name").text(item.name).appendTo($tile);
            $("<small>").addClass("text-muted d-block").text(item.isFolder ? item.modified : `${item.size} · ${item.modified}`).appendTo($tile);
            if (item.location !== "") {
                $("<small>").addClass("text-muted d-block file-manager-location").text(item.location).appendTo($tile);
            }
            $("<div>").addClass("col-6 col-sm-4 col-md-3 col-xl-2 mb-2").append($tile).appendTo($row);
        }
        $items.append($row);
    }

    /** One action button of a list row. */
    function actionButton(action: string, icon: string, title: string): JQuery {
        return $("<button>").attr("type", "button").attr("title", title).attr("data-action", action)
            .addClass("btn btn-sm btn-outline-secondary mr-1 file-manager-action").append($("<i>").addClass(icon));
    }

    /** The list view: a table with a checkbox, name, (location,) size, modified date and the action buttons. */
    function RenderList() {
        const $table = $("<table>").addClass("table table-hover table-sm file-manager-list");
        const $head = $("<tr>");
        $("<th>").append($("<input>").attr("type", "checkbox").addClass("file-manager-check-all")).appendTo($head);
        $("<th>").text(text.name).appendTo($head);
        if (searching) $("<th>").text(text.location).appendTo($head);
        $("<th>").text(text.size).appendTo($head);
        $("<th>").text(text.modified).appendTo($head);
        $("<th>").text(text.actions).appendTo($head);
        $("<thead>").append($head).appendTo($table);
        const $body = $("<tbody>");
        for (const item of shownItems) {
            const $row = $("<tr>").addClass("file-manager-item").attr("data-id", String(item.id)).attr("draggable", "true");
            $("<td>").append($("<input>").attr("type", "checkbox").addClass("file-manager-check")).appendTo($row);
            $("<td>").addClass("file-manager-name").append($("<i>").addClass(`${item.icon} mr-2`), $("<span>").text(item.name)).appendTo($row);
            if (searching) $("<td>").text(item.location).appendTo($row);
            $("<td>").text(item.size).appendTo($row);
            $("<td>").text(item.modified).appendTo($row);
            const $actions = $("<td>").addClass("text-nowrap");
            if (!item.isFolder) $actions.append(actionButton("download", "fas fa-download", text.download));
            if (item.previewable) $actions.append(actionButton("preview", "fas fa-eye", text.preview));
            $actions.append(actionButton("rename", "fas fa-pen", text.rename), actionButton("delete", "fas fa-trash", text.delete));
            $row.append($actions);
            $body.append($row);
        }
        $table.append($body);
        $items.append($table);
    }

    /** Marks the selected items and shows or hides the selection bar. */
    function RenderSelection() {
        $items.find(".file-manager-item").each(function() {
            const isSelected = selected.has(Number($(this).attr("data-id")));
            $(this).toggleClass("file-manager-selected", isSelected);
            $(this).find(".file-manager-check").prop("checked", isSelected);
        });
        $items.find(".file-manager-check-all").prop("checked", shownItems.length > 0 && selected.size === shownItems.length);
        $selectionBar.toggleClass("d-none", selected.size === 0);
        $selectionCount.text(format(text.selectedCount, selected.size));
    }

    /** The shown item with this id. */
    function itemById(id: number): Item | undefined {
        return shownItems.find((i) => i.id === id);
    }

    /** The id of the item element an event came from. */
    function idOf(element: Element): number {
        return Number($(element).closest(".file-manager-item").attr("data-id"));
    }

    /** Selects with a click: alone, Ctrl / ⌘ toggles one, Shift extends from the last click. */
    function SelectByClick(id: number, ctrl: boolean, shift: boolean) {
        const index = shownItems.findIndex((i) => i.id === id);
        if (shift && anchorIndex >= 0) {
            const [from, to] = anchorIndex < index ? [anchorIndex, index] : [index, anchorIndex];
            if (!ctrl) selected.clear();
            shownItems.slice(from, to + 1).forEach((i) => selected.add(i.id));
        } else if (ctrl) {
            if (selected.has(id)) selected.delete(id); else selected.add(id);
            anchorIndex = index;
        } else {
            selected.clear();
            selected.add(id);
            anchorIndex = index;
        }
        RenderSelection();
    }

    /** Opens an item: a folder is opened, a previewable image previewed, any other file downloaded. */
    function OpenItem(item: Item) {
        if (item.isFolder) {
            void OpenFolder(item.id);
        } else if (item.previewable) {
            void Preview(item);
        } else {
            void DownloadOne(item.id, item.name);
        }
    }

    // -- Item actions -----------------------------------------------------------------------

    /** Saves a Blob under a name through a temporary link. */
    function save(blob: Blob, name: string) {
        const url = URL.createObjectURL(blob);
        const a = document.createElement("a");
        try {
            a.href = url;
            a.download = name;
            a.click();
        } finally {
            // Revoke after a tick so the download has started; drop the <a>.
            setTimeout(() => URL.revokeObjectURL(url), 100);
            a.remove();
        }
    }

    /** Downloads one file. */
    async function DownloadOne(id: number, name: string) {
        const blob = await postForBlob(`/FileManager/Download?id=${id}`);
        if (blob !== null) save(blob, name);
    }

    /** Downloads the selected items one file at a time (a folder's files included; not a ZIP, F8). */
    async function DownloadSelected(ids: number[]) {
        const data = await post("/FileManager/ListDownloadFiles", { Ids: ids }, filesReply);
        if (data === null) return;
        if (!data.result) {
            toastr.error(data.error);
            return;
        }
        for (const file of data.files) {
            await DownloadOne(file.id, file.name);
        }
    }

    /** The object URL of the image in the preview, released when the preview closes. */
    let previewUrl: string | null = null;

    /** Shows an image in the preview modal (the server decides it is one, F9). */
    async function Preview(item: Item) {
        const blob = await postForBlob(`/FileManager/Preview?id=${item.id}`);
        if (blob === null) return;
        if (previewUrl !== null) URL.revokeObjectURL(previewUrl);
        previewUrl = URL.createObjectURL(blob);
        $previewTitle.text(item.name);
        $previewImage.attr("src", previewUrl).attr("alt", item.name);
        $previewModal.modal("show");
    }

    /** Opens the rename modal for an item. */
    function OpenRename(item: Item) {
        $renameId.val(String(item.id));
        $renameName.val(item.name);
        $renameModal.modal("show");
    }

    /** Opens the delete confirmation for some items. */
    function ConfirmDelete(ids: number[]) {
        pendingIds = ids;
        $confirmDeleteModal.modal("show");
    }

    /** Opens the Move modal for some items: the root and every folder (with its location) to choose from. */
    function OpenMove(ids: number[]) {
        pendingIds = ids;
        $moveTarget.empty();
        $("<option>").val("").text(text.myDrive).appendTo($moveTarget);
        for (const folder of driveFolders) {
            if (ids.includes(folder.id)) continue;
            $("<option>").val(String(folder.id)).text(`${folder.location} / ${folder.name}`).appendTo($moveTarget);
        }
        $moveModal.modal("show");
    }

    /** Moves items into a folder (null = the root), then refreshes. */
    async function Move(ids: number[], targetFolderId: number | null) {
        const data = await post("/FileManager/Move", { Ids: ids, TargetFolderId: targetFolderId }, replies.action);
        if (data === null) return;
        if (data.result) {
            toastr.success(data.message);
        } else {
            toastr.error(data.error);
        }
        await Refresh();
    }

    // -- Uploads --------------------------------------------------------------------------

    /** One file of an upload batch and the folders (from the batch's root) it goes in. */
    interface UploadEntry { dir: string[]; file: File; }

    /** The choice a name conflict was answered with. */
    type ConflictChoice = "Overwrite" | "KeepBoth" | "Skip";

    /** Asks how to resolve a name conflict; resolves with the choice and whether it applies to the rest of the batch. */
    function AskConflict(name: string): Promise<{ choice: ConflictChoice; applyToAll: boolean }> {
        return new Promise((resolve) => {
            $conflictMessage.text(format(text.conflict, name));
            $conflictApplyToAll.prop("checked", false);
            const answer = (choice: ConflictChoice) => () => {
                $conflictModal.find("button").off("click.conflict");
                $conflictModal.modal("hide");
                resolve({ choice: choice, applyToAll: $conflictApplyToAll.is(":checked") });
            };
            $("#btnConflictOverwrite").off("click.conflict").on("click.conflict", answer("Overwrite"));
            $("#btnConflictKeepBoth").off("click.conflict").on("click.conflict", answer("KeepBoth"));
            $("#btnConflictSkip").off("click.conflict").on("click.conflict", answer("Skip"));
            $conflictModal.modal("show");
        });
    }

    /** Adds a line to the upload result list. */
    function report(name: string, outcome: string, failed: boolean) {
        $("<li>").addClass(failed ? "text-danger" : "text-success").append($("<strong>").text(name), document.createTextNode(` — ${outcome}`))
            .appendTo($uploadResults);
    }

    /** The outcome of one file: stored (no error), skipped, or the reason it failed; and the conflict answer it was given. */
    interface FileOutcome { error: string | null; skipped: boolean; choice: ConflictChoice | null; applyToAll: boolean; }

    /**
     * Uploads one file: starts it (with the batch's remembered conflict answer, if any), asks on a name conflict, sends its chunks
     * and completes it. A chunk that fails aborts the upload.
     */
    async function SendFile(entry: UploadEntry, parentId: number | null, remembered: ConflictChoice | null): Promise<FileOutcome> {
        let choice: ConflictChoice | null = remembered;
        let applyToAll = false;
        const begin = (conflict: string) =>
            post("/FileManager/BeginUpload", { ParentId: parentId, Name: entry.file.name, Size: entry.file.size, Conflict: conflict }, beginReply);
        const fail = (error: string): FileOutcome => ({ error: error, skipped: false, choice: choice, applyToAll: applyToAll });

        let started = await begin(choice === "Overwrite" || choice === "KeepBoth" ? choice : "Ask");
        if (started !== null && started.result && started.conflict) {
            if (choice !== "Skip") {
                const answer = await AskConflict(entry.file.name);
                choice = answer.choice;
                applyToAll = answer.applyToAll;
            }
            if (choice === "Skip") return { error: null, skipped: true, choice: choice, applyToAll: applyToAll };
            started = await begin(choice ?? "Ask");
        }
        if (started === null) return fail("");
        if (!started.result) return fail(started.error);
        if (started.conflict) return fail(format(text.conflict, entry.file.name));

        for (let offset = 0; offset < entry.file.size; offset += started.chunkBytes) {
            const form = new FormData();
            form.append("UploadId", started.uploadId);
            form.append("Offset", String(offset));
            form.append("Chunk", entry.file.slice(offset, offset + started.chunkBytes), "chunk");
            const chunk = await postForm("/FileManager/UploadChunk", form, chunkReply);
            if (chunk === null || !chunk.result) {
                await post("/FileManager/AbortUpload", { UploadId: started.uploadId }, doneReply);
                return fail(chunk !== null && !chunk.result ? chunk.error : "");
            }
            $uploadProgressBar.css("width", `${Math.round(chunk.length * 100 / Math.max(entry.file.size, 1))}%`);
        }

        const done = await post("/FileManager/CompleteUpload", { UploadId: started.uploadId, Overwrite: choice === "Overwrite" }, replies.action);
        if (done === null) return fail("");
        return done.result ? { error: null, skipped: false, choice: choice, applyToAll: applyToAll } : fail(done.error);
    }

    /**
     * Uploads a batch into `targetFolderId`: the quota is checked for the whole batch first, then the folders are created (merged
     * into existing ones) and each file is sent; a failure skips only that file. Ends with the result list and a refresh.
     */
    async function Upload(entries: UploadEntry[], emptyDirs: string[][], targetFolderId: number | null) {
        if (entries.length === 0 && emptyDirs.length === 0) return;
        const quota = await post("/FileManager/CheckQuota", { TotalBytes: entries.reduce((sum, e) => sum + e.file.size, 0) }, doneReply);
        if (quota === null) return;
        if (!quota.result) {
            toastr.error(quota.error);
            return;
        }

        $uploadResults.empty();
        $uploadProgressBar.css("width", "0%");
        $btnUploadClose.prop("disabled", true);
        $uploadModal.modal("show");

        // The folders of a folder upload, created from the top down (an existing folder of the same name is reused, F2-C).
        const folderIds = new Map<string, number>();
        async function ensureFolder(dir: string[]): Promise<number | null | undefined> {
            let parent = targetFolderId;
            for (const [index, name] of dir.entries()) {
                const key = dir.slice(0, index + 1).join("/");
                const known = folderIds.get(key);
                if (known !== undefined) {
                    parent = known;
                    continue;
                }
                const created = await post("/FileManager/CreateFolder", { ParentId: parent, Name: name, Merge: true }, folderCreatedReply);
                if (created === null || !created.result) {
                    report(key, created !== null && !created.result ? created.error : "", true);
                    return undefined;
                }
                folderIds.set(key, created.folderId);
                parent = created.folderId;
            }
            return parent;
        }
        for (const dir of emptyDirs) await ensureFolder(dir);

        let uploaded = 0;
        let failed = 0;
        let skipped = 0;
        let rememberedChoice: ConflictChoice | null = null;
        for (const [index, entry] of entries.entries()) {
            $uploadProgressText.text(format(text.uploading, index + 1, entries.length, entry.file.name));
            $uploadProgressBar.css("width", "0%");
            // Authoritative in FileManagerUploadPolicy.MaxFileBytes (server); mirrored here for form UX only.
            if (entry.file.size > maxFileBytes) {
                report(entry.file.name, text.fileTooLarge, true);
                failed++;
                continue;
            }
            const parentId = await ensureFolder(entry.dir);
            if (parentId === undefined) {
                failed++;
                continue;
            }
            const outcome = await SendFile(entry, parentId, rememberedChoice);
            if (outcome.applyToAll && outcome.choice !== null) rememberedChoice = outcome.choice;
            count(entry, outcome);
        }

        /** Tallies one file's outcome and lists it. */
        function count(entry: UploadEntry, outcome: FileOutcome) {
            if (outcome.skipped) {
                skipped++;
                report(entry.file.name, text.skipped, false);
            } else if (outcome.error === null) {
                uploaded++;
                report(entry.file.name, text.uploaded, false);
            } else {
                failed++;
                report(entry.file.name, outcome.error, true);
            }
        }

        $uploadProgressText.text(format(text.uploadSummary, uploaded, failed, skipped));
        $uploadProgressBar.css("width", "100%");
        $btnUploadClose.prop("disabled", false);
        await Refresh();
    }

    /** The upload entries of files picked with the file or folder picker (a folder's files carry their relative path). */
    function entriesOf(files: FileList | null): UploadEntry[] {
        return Array.from(files ?? []).map((file) => {
            const parts = (file.webkitRelativePath || file.name).split("/");
            return { dir: parts.slice(0, -1), file: file };
        });
    }

    /** Whether a dropped entry is a file. */
    function isFileEntry(entry: FileSystemEntry): entry is FileSystemFileEntry {
        return entry.isFile;
    }

    /** Whether a dropped entry is a folder. */
    function isDirectoryEntry(entry: FileSystemEntry): entry is FileSystemDirectoryEntry {
        return entry.isDirectory;
    }

    /** Reads every file and folder under a dropped entry (a dropped folder keeps its structure, empty folders included). */
    async function readEntry(entry: FileSystemEntry, dir: string[], files: UploadEntry[], emptyDirs: string[][]): Promise<void> {
        if (isFileEntry(entry)) {
            const file = await new Promise<File>((resolve, reject) => entry.file(resolve, reject));
            files.push({ dir: dir, file: file });
        }
        if (isDirectoryEntry(entry)) await readDirectory(entry, dir, files, emptyDirs);
    }

    /** Reads every entry of a dropped folder (readEntries answers in batches until an empty one). */
    async function readDirectory(directory: FileSystemDirectoryEntry, dir: string[], files: UploadEntry[], emptyDirs: string[][]): Promise<void> {
        const reader = directory.createReader();
        const children: FileSystemEntry[] = [];
        for (; ;) {
            const batch = await new Promise<FileSystemEntry[]>((resolve, reject) => reader.readEntries(resolve, reject));
            if (batch.length === 0) break;
            children.push(...batch);
        }
        const path = [...dir, directory.name];
        if (children.length === 0) emptyDirs.push(path);
        for (const child of children) await readEntry(child, path, files, emptyDirs);
    }

    /** Uploads what was dropped from the PC into a folder. */
    async function UploadDropped(transfer: DataTransfer, targetFolderId: number | null) {
        const files: UploadEntry[] = [];
        const emptyDirs: string[][] = [];
        const roots = Array.from(transfer.items).map((i) => i.webkitGetAsEntry()).filter((r): r is FileSystemEntry => r !== null);
        if (roots.length > 0) {
            for (const root of roots) await readEntry(root, [], files, emptyDirs);
        } else {
            files.push(...entriesOf(transfer.files));
        }
        await Upload(files, emptyDirs, targetFolderId);
    }

    // -- Drag and drop ------------------------------------------------------------------------

    /** The data type that carries dragged item ids inside the page. */
    const itemsType = "application/x-maroik-file-manager-items";

    /** Whether a value is a DataTransfer (checked by shape: the drag events' own type differs between browsers). */
    function isTransfer(value: unknown): value is DataTransfer {
        return typeof value === "object" && value !== null && "getData" in value && "types" in value;
    }

    /** The DataTransfer of a drag event, or null. */
    function transferOf(event: JQuery.TriggeredEvent): DataTransfer | null {
        const original: unknown = event.originalEvent;
        const transfer = typeof original === "object" && original !== null && "dataTransfer" in original ? original.dataTransfer : null;
        return isTransfer(transfer) ? transfer : null;
    }

    /** The ids a dragged item carries: the selection when it is part of it, else itself. */
    function dragIds(id: number): number[] {
        return selected.has(id) ? [...selected] : [id];
    }

    /** The ids an internal drag carries, or null for anything else (a drop from the PC). */
    function droppedIds(transfer: DataTransfer): number[] | null {
        const raw = transfer.getData(itemsType);
        if (raw === "") return null;
        return conform(JSON.parse(raw), check.array(check.number), "the dragged items");
    }

    /** Handles a drop on a folder: items move there, PC files upload there. */
    function DropOn(transfer: DataTransfer | null | undefined, folderId: number | null) {
        if (!transfer) return;
        const ids = droppedIds(transfer);
        if (ids !== null) {
            const toMove = ids.filter((id) => id !== folderId);
            if (toMove.length > 0) void Move(toMove, folderId);
        } else if (transfer.types.includes("Files")) {
            void UploadDropped(transfer, folderId);
        }
    }

    // -- Wiring -------------------------------------------------------------------------------

    $tree.on("select_node.jstree", function(_event, data: unknown) {
        void OpenFolder(folderIdOf(conform(data, selectNodeEvent, "the selected tree node").node.id));
    });
    $tree.on("move_node.jstree", function(_event, data: unknown) {
        const moved = conform(data, moveNodeEvent, "the moved tree node");
        void Move([Number(moved.node.id)], folderIdOf(moved.parent));
    });
    $tree.on("dragover", ".jstree-anchor", function(event) {
        event.preventDefault();
        $(this).addClass("file-manager-drop-target");
    });
    $tree.on("dragleave drop", ".jstree-anchor", function() {
        $(this).removeClass("file-manager-drop-target");
    });
    $tree.on("drop", ".jstree-anchor", function(event) {
        event.preventDefault();
        DropOn(transferOf(event), folderIdOf(String($(this).closest("li").attr("id"))));
    });

    $breadcrumb.on("click", ".file-manager-crumb", function(event) {
        event.preventDefault();
        const id = String($(this).attr("data-folder-id") ?? "");
        void OpenFolder(id === "" ? null : Number(id));
    });

    $search.on("input", function() {
        const query = String($search.val() ?? "").trim();
        if (query === "") {
            void OpenFolder(currentFolderId);
        } else {
            void Search(query);
        }
    });

    function SetView(next: "grid" | "list") {
        view = next;
        try {
            window.localStorage.setItem(viewKey, next);
        } catch {
            // No storage: the choice lasts until the page is left.
            view = next;
        }
        RenderItems();
    }
    $btnGridView.on("click", () => SetView("grid"));
    $btnListView.on("click", () => SetView("list"));

    $items.on("click", ".file-manager-tile", function(event) {
        SelectByClick(idOf(this), event.ctrlKey || event.metaKey, event.shiftKey);
    });
    $items.on("dblclick", ".file-manager-item", function() {
        const item = itemById(idOf(this));
        if (item) OpenItem(item);
    });
    $items.on("change", ".file-manager-check", function() {
        const id = idOf(this);
        if ($(this).is(":checked")) selected.add(id); else selected.delete(id);
        RenderSelection();
    });
    $items.on("change", ".file-manager-check-all", function() {
        selected.clear();
        if ($(this).is(":checked")) shownItems.forEach((i) => selected.add(i.id));
        RenderSelection();
    });
    $items.on("click", ".file-manager-action", function(event) {
        event.stopPropagation();
        const item = itemById(idOf(this));
        if (!item) return;
        switch ($(this).attr("data-action")) {
            case "download": void DownloadOne(item.id, item.name); break;
            case "preview": void Preview(item); break;
            case "rename": OpenRename(item); break;
            case "delete": ConfirmDelete([item.id]); break;
        }
    });

    $items.on("dragstart", ".file-manager-item", function(event) {
        const transfer = transferOf(event);
        transfer?.setData(itemsType, JSON.stringify(dragIds(idOf(this))));
    });
    $items.on("dragover", function(event) {
        event.preventDefault();
        $items.addClass("file-manager-drop-target");
    });
    $items.on("dragleave drop", function() {
        $items.removeClass("file-manager-drop-target");
    });
    $items.on("drop", function(event) {
        event.preventDefault();
        const transfer = transferOf(event);
        // Dropped on a folder (tile or row): into that folder; anywhere else: into the open folder.
        const folder = itemById(Number($(event.target).closest(".file-manager-item").attr("data-id")));
        DropOn(transfer, folder && folder.isFolder ? folder.id : currentFolderId);
    });

    $("#btnUploadFiles").on("click", () => $fileInput.trigger("click"));
    $("#btnUploadFolder").on("click", () => $folderInput.trigger("click"));
    $fileInput.on("change", function() {
        const input = conform(this, check.instance(HTMLInputElement), "the file input");
        const entries = entriesOf(input.files);
        input.value = "";
        void Upload(entries, [], currentFolderId);
    });
    $folderInput.on("change", function() {
        const input = conform(this, check.instance(HTMLInputElement), "the folder input");
        const entries = entriesOf(input.files);
        input.value = "";
        void Upload(entries, [], currentFolderId);
    });

    $("#btnNewFolder").on("click", function() {
        $newFolderName.val("");
        $newFolderModal.modal("show");
    });
    $formNewFolder.on("submit", function() {
        if (!$formNewFolder.valid()) return false;
        void post("/FileManager/CreateFolder", { ParentId: currentFolderId, Name: fieldValue($newFolderName), Merge: false }, folderCreatedReply).then(async (data) => {
            if (data === null) return;
            if (!data.result) {
                toastr.error(data.error);
                return;
            }
            $newFolderModal.modal("hide");
            toastr.success(data.message);
            await Refresh();
        });
        return false;
    });
    $formRename.on("submit", function() {
        if (!$formRename.valid()) return false;
        void post("/FileManager/Rename", { Id: Number(fieldValue($renameId)), Name: fieldValue($renameName) }, replies.action).then(async (data) => {
            if (data === null) return;
            if (!data.result) {
                toastr.error(data.error);
                return;
            }
            $renameModal.modal("hide");
            toastr.success(data.message);
            await Refresh();
        });
        return false;
    });
    $("#btnConfirmMove").on("click", function() {
        const target = fieldValue($moveTarget);
        $moveModal.modal("hide");
        void Move(pendingIds, target === "" ? null : Number(target));
    });
    $("#btnConfirmDelete").on("click", function() {
        $confirmDeleteModal.modal("hide");
        void post("/FileManager/Delete", { Ids: pendingIds }, replies.action).then(async (data) => {
            if (data === null) return;
            if (data.result) {
                toastr.success(data.message);
            } else {
                toastr.error(data.error);
            }
            await Refresh();
        });
    });
    $("#btnDownloadSelected").on("click", () => void DownloadSelected([...selected]));
    $("#btnMoveSelected").on("click", () => OpenMove([...selected]));
    $("#btnDeleteSelected").on("click", () => ConfirmDelete([...selected]));
    $previewModal.on("hidden.bs.modal", function() {
        if (previewUrl !== null) URL.revokeObjectURL(previewUrl);
        previewUrl = null;
        $previewImage.attr("src", "");
    });

    void LoadDrive();
    void OpenFolder(null);
})();
