import { describe, it, expect, beforeEach } from "vitest";
import { loadSite, hidden, antiForgery, present, stubPlugin, successOf, formDataOf, type SiteHandle, type AjaxCall } from "@tests/_common/harness";

// wwwroot/user/custom/FileManager/UserIndex/js/site.js — the File Manager ("My Drive"): the jsTree folder tree, the grid / list of
// items, selection, drag and drop, and the chunked upload with its name-conflict dialog.

/** The page shell, as UserIndex.cshtml renders it (localized texts as "L:<key>" so assertions can see them). */
function fixture(maxFileBytes = 2147483647): string {
    const texts: Record<string, string> = {
        MyDrive: "My Drive", ItemCount: "{0} items", SelectedCount: "{0} selected", EmptyFolder: "Empty", NoResults: "Nothing",
        Name: "Name", Size: "Size", Modified: "Modified", Location: "Location", Actions: "Actions", Download: "Download",
        Preview: "Preview", Rename: "Rename", Delete: "Delete", Conflict: "'{0}' exists", Uploading: "Uploading {0}/{1}: {2}",
        UploadSummary: "{0} up, {1} failed, {2} skipped", Uploaded: "Uploaded", Skipped: "Skipped", FileTooLarge: "Too large",
    };
    return (
        antiForgery +
        hidden("maxFileBytes", String(maxFileBytes)) +
        Object.entries(texts).map(([key, value]) => hidden(`localizer${key}`, value)).join("") +
        `<button id="btnUploadFiles"></button><button id="btnUploadFolder"></button>
         <input type="file" id="fileManagerFileInput" multiple /><input type="file" id="fileManagerFolderInput" multiple />
         <button id="btnNewFolder"></button>
         <div id="fileManagerTree"><ul><li id="7"><a class="jstree-anchor">Docs</a></li></ul></div>
         <div id="fileManagerUsageBar"></div><small id="fileManagerUsage"></small>
         <ol id="fileManagerBreadcrumb"></ol><input type="search" id="fileManagerSearch" />
         <button id="btnGridView"></button><button id="btnListView"></button>
         <div id="fileManagerSelectionBar" class="d-none"><span id="fileManagerSelectionCount"></span>
           <button id="btnDownloadSelected"></button><button id="btnMoveSelected"></button><button id="btnDeleteSelected"></button></div>
         <div id="fileManagerItems"></div><small id="fileManagerItemCount"></small>
         <div id="newFolderModal"><form id="formNewFolder"><input id="newFolderName" /></form></div>
         <div id="renameModal"><form id="formRename"><input type="hidden" id="renameId" /><input id="renameName" /></form></div>
         <div id="moveModal"><select id="moveTarget"></select><button id="btnConfirmMove"></button></div>
         <div id="conflictModal"><p id="conflictMessage"></p><input type="checkbox" id="conflictApplyToAll" />
           <button id="btnConflictOverwrite"></button><button id="btnConflictKeepBoth"></button><button id="btnConflictSkip"></button></div>
         <div id="uploadModal"><p id="uploadProgressText"></p><div id="uploadProgressBar"></div><ul id="uploadResults"></ul>
           <button id="btnUploadClose" disabled></button></div>
         <div id="previewModal"><h5 id="previewTitle"></h5><img id="previewImage" /></div>
         <div id="confirmDeleteModal"><button id="btnConfirmDelete"></button></div>`
    );
}

/** An item as the controller sends it. */
function item(id: number, name: string, over: Record<string, unknown> = {}): Record<string, unknown> {
    return {
        id, parentId: null, name, isFolder: false, size: "1 KB", modified: "2026-05-24 12:00", icon: "fas fa-file text-muted",
        previewable: false, location: "", itemCount: 0, ...over,
    };
}

/** A folder item. */
function folder(id: number, name: string, over: Record<string, unknown> = {}): Record<string, unknown> {
    return item(id, name, { isFolder: true, size: "", icon: "fas fa-folder text-warning", ...over });
}

/** Lets the awaited replies run on. */
async function flush(): Promise<void> {
    for (let i = 0; i < 10; i++) await new Promise((resolve) => setTimeout(resolve, 0));
}

/** The most recent call to `url` (a prefix for a query string). */
function callTo(h: SiteHandle, url: string): AjaxCall {
    return present([...h.ajaxCalls].reverse().find((c) => String(c.url).startsWith(url)), `a call to ${url}`);
}

/** Every call to `url`. */
function callsTo(h: SiteHandle, url: string): AjaxCall[] {
    return h.ajaxCalls.filter((c) => String(c.url).startsWith(url));
}

/** The JSON body of a call. */
function bodyOf(call: AjaxCall): unknown {
    return JSON.parse(String(call.data));
}

/** Answers the latest call to `url` and lets the script continue. */
async function answer(h: SiteHandle, url: string, data: unknown): Promise<void> {
    successOf(callTo(h, url))(data);
    await flush();
}

/** The jsTree calls the page made and a fake tree instance. */
interface TreeSpy { options: unknown[]; instance: { settings: { core: { data: unknown[] } }; refreshed: number; refresh(): void; deselect_all(): void; select_node(): void } }

/** Stubs jsTree and the modal plugin; returns what they were asked. */
function stubWidgets(h: SiteHandle): { tree: TreeSpy; modals: [string, unknown][] } {
    const initialData: unknown[] = [];
    const instance = {
        settings: { core: { data: initialData } },
        refreshed: 0,
        refresh() { this.refreshed++; },
        deselect_all() { return undefined; },
        select_node() { return undefined; },
    };
    const tree: TreeSpy = { options: [], instance };
    stubPlugin(h, "jstree", function(this: JQuery, arg: never) {
        if (arg === true) return instance;
        tree.options.push(arg);
        return this;
    });
    const modals: [string, unknown][] = [];
    stubPlugin(h, "modal", function(this: JQuery, arg: never) {
        modals.push([String(this.attr("id")), arg]);
        return this;
    });
    return { tree, modals };
}

/** Loads the page and answers its first two requests (the drive and the root folder). */
async function load(items: Record<string, unknown>[] = [folder(7, "Docs"), item(8, "a.txt")], storage = "grid"): Promise<{ h: SiteHandle; tree: TreeSpy; modals: [string, unknown][] }> {
    window.localStorage.setItem("maroik.fileManager.view", storage);
    const h = loadSite("user", "FileManager", "UserIndex", fixture());
    const widgets = stubWidgets(h);
    await answer(h, "/FileManager/GetDrive", { result: true, drive: { usage: "1 KB / 10 GB", usedPercent: 5, rootItemCount: 2, folders: [folder(7, "Docs", { location: "My Drive", itemCount: 3 })] } });
    await answer(h, "/FileManager/GetFolder", { result: true, folder: { folderId: null, breadcrumb: [], items } });
    return { h, ...widgets };
}

/** Gives an input the files the user picked. */
function pick(h: SiteHandle, inputId: string, files: File[]): void {
    Object.defineProperty(present(h.win.document.getElementById(inputId)), "files", { value: files, configurable: true });
    h.$(`#${inputId}`).trigger("change");
}

/** A file of `size` bytes (its declared size may be larger than its content). */
function file(name: string, size = 3, relativePath = ""): File {
    const f = new File([new Uint8Array(Math.min(size, 3))], name);
    if (size > 3) Object.defineProperty(f, "size", { value: size });
    if (relativePath !== "") Object.defineProperty(f, "webkitRelativePath", { value: relativePath });
    return f;
}

beforeEach(() => window.localStorage.clear());

describe("FileManager/UserIndex — loading", () => {
    it("asks for the drive and the root folder", async () => {
        const h = loadSite("user", "FileManager", "UserIndex", fixture());
        stubWidgets(h);

        expect(callsTo(h, "/FileManager/GetDrive")).toHaveLength(1);
        expect(bodyOf(callTo(h, "/FileManager/GetFolder"))).toEqual({ FolderId: null });
        expect(callTo(h, "/FileManager/GetFolder").headers).toEqual({ RequestVerificationToken: "tok" });
    });

    it("builds the folder tree (names escaped: jsTree renders HTML) and the usage bar from the server's values", async () => {
        const h = loadSite("user", "FileManager", "UserIndex", fixture());
        const { tree } = stubWidgets(h);

        await answer(h, "/FileManager/GetDrive", { result: true, drive: { usage: "10 GB / 10 GB", usedPercent: 100, rootItemCount: 1, folders: [folder(7, "<b>x</b>", { itemCount: 2 }), folder(9, "Sub", { parentId: 7 })] } });

        const options = present(tree.options[0]);
        expect(options).toMatchObject({ core: { data: [
            { id: "root", parent: "#", text: "My Drive (1)" },
            { id: "7", parent: "root", text: "&lt;b&gt;x&lt;/b&gt; (2)" },
            { id: "9", parent: "7", text: "Sub (0)" },
        ] }, plugins: ["dnd"] });
        expect(h.$("#fileManagerUsage").text()).toBe("10 GB / 10 GB");
        expect(present(h.win.document.getElementById("fileManagerUsageBar")).style.width).toBe("100%");
        expect(h.$("#fileManagerUsageBar").hasClass("bg-danger")).toBe(true);
    });

    it("the tree allows a folder move into a folder or the root but nothing beside the root, and never drags the root", async () => {
        const { tree } = await load();
        const options = present(tree.options[0]);
        if (typeof options !== "object" || options === null || !("core" in options) || !("dnd" in options)) throw new Error("no options");
        const core = options.core;
        const dnd = options.dnd;
        if (typeof core !== "object" || core === null || !("check_callback" in core) || typeof core.check_callback !== "function") throw new Error("no callback");
        if (typeof dnd !== "object" || dnd === null || !("is_draggable" in dnd) || typeof dnd.is_draggable !== "function") throw new Error("no is_draggable");

        expect(core.check_callback("move_node", { id: "7" }, { id: "#" })).toBe(false);
        expect(core.check_callback("move_node", { id: "7" }, { id: "root" })).toBe(true);
        expect(core.check_callback("create_node", { id: "7" }, { id: "#" })).toBe(true);
        expect(dnd.is_draggable([{ id: "root" }])).toBe(false);
        expect(dnd.is_draggable([{ id: "7" }])).toBe(true);
    });

    it("a later drive refresh feeds the tree its new nodes and refreshes it", async () => {
        const { h, tree } = await load();
        h.$("#fileManagerSearch").val("x").trigger("input");
        h.$("#fileManagerSearch").val("").trigger("input");
        h.$("#btnConfirmDelete").trigger("click");
        await answer(h, "/FileManager/Delete", { result: true, message: "gone" });
        await answer(h, "/FileManager/GetDrive", { result: true, drive: { usage: "", usedPercent: 0, rootItemCount: 0, folders: [] } });

        expect(tree.instance.refreshed).toBe(1);
        expect(tree.instance.settings.core.data).toEqual([{ id: "root", parent: "#", text: "My Drive (0)", state: { opened: true } }]);
    });

    it("draws the root folder as tiles, its item count and a one-step breadcrumb", async () => {
        const { h } = await load([folder(7, "Docs"), item(8, "<script>a</script>.txt")]);

        expect(h.$(".file-manager-tile").map((_, el) => h.$(el).attr("data-id")).get()).toEqual(["7", "8"]);
        expect(h.$(".file-manager-tile-name").eq(1).text()).toBe("<script>a</script>.txt");
        expect(h.$("#fileManagerItems script")).toHaveLength(0);
        expect(h.$("#fileManagerItemCount").text()).toBe("2 items");
        expect(h.$("#fileManagerBreadcrumb li.active").text()).toBe("My Drive");
    });

    it("an empty folder says so", async () => {
        const { h } = await load([]);

        expect(h.$("#fileManagerItems").text()).toBe("Empty");
    });

    it("a refused drive or folder shows the server's reason; a reply of another shape shows the generic error", async () => {
        const h = loadSite("user", "FileManager", "UserIndex", fixture());
        stubWidgets(h);

        await answer(h, "/FileManager/GetDrive", { result: false, error: "no drive" });
        await answer(h, "/FileManager/GetFolder", { result: true, folder: { folderId: null } });

        expect(h.toastr.error).toHaveBeenCalledWith("no drive");
        expect(h.toastr.error).toHaveBeenCalledWith("L_TemporaryError");
    });
});

describe("FileManager/UserIndex — views and selection", () => {
    it("the list view shows a row per item with the server's size / date and only the actions each item allows; the choice is remembered", async () => {
        const { h } = await load([folder(7, "Docs"), item(8, "a.png", { previewable: true }), item(9, "b.zip")]);

        h.$("#btnListView").trigger("click");

        expect(window.localStorage.getItem("maroik.fileManager.view")).toBe("list");
        const rows = h.$(".file-manager-list tbody tr");
        expect(rows).toHaveLength(3);
        const actionsOf = (i: number) => rows.eq(i).find(".file-manager-action").map((_, el) => h.$(el).attr("data-action")).get();
        expect(actionsOf(0)).toEqual(["rename", "delete"]);
        expect(actionsOf(1)).toEqual(["download", "preview", "rename", "delete"]);
        expect(actionsOf(2)).toEqual(["download", "rename", "delete"]);
        expect(rows.eq(1).text()).toContain("1 KB");

        const again = await load([item(8, "a.txt")], "list");
        expect(again.h.$(".file-manager-list")).toHaveLength(1);
    });

    it("click selects one tile, Ctrl-click toggles, Shift-click selects a range; the selection bar counts them", async () => {
        const { h } = await load([item(1, "a"), item(2, "b"), item(3, "c"), item(4, "d")]);
        const tile = (id: number) => h.$(`.file-manager-tile[data-id='${id}']`);

        tile(1).trigger("click");
        tile(3).trigger(h.$.Event("click", { ctrlKey: true }));
        expect(h.$(".file-manager-selected").map((_, el) => h.$(el).attr("data-id")).get()).toEqual(["1", "3"]);
        expect(h.$("#fileManagerSelectionCount").text()).toBe("2 selected");
        expect(h.$("#fileManagerSelectionBar").hasClass("d-none")).toBe(false);

        tile(3).trigger(h.$.Event("click", { ctrlKey: true }));
        tile(4).trigger(h.$.Event("click", { shiftKey: true }));
        expect(h.$(".file-manager-selected").map((_, el) => h.$(el).attr("data-id")).get()).toEqual(["3", "4"]);

        // A Shift-click replaces the selection with the range from the last plain / Ctrl click (item 3), as Explorer does.
        tile(1).trigger(h.$.Event("click", { shiftKey: true }));
        expect(h.$(".file-manager-selected").map((_, el) => h.$(el).attr("data-id")).get()).toEqual(["1", "2", "3"]);
        tile(4).trigger(h.$.Event("click", { shiftKey: true, ctrlKey: true }));
        expect(h.$(".file-manager-selected")).toHaveLength(4);

        tile(2).trigger("click");
        tile(2).trigger(h.$.Event("click", { metaKey: true }));
        expect(h.$("#fileManagerSelectionBar").hasClass("d-none")).toBe(true);
    });

    it("list checkboxes select rows, and the header checkbox selects or clears them all", async () => {
        const { h } = await load([item(1, "a"), item(2, "b")], "list");

        h.$(".file-manager-check").eq(1).prop("checked", true).trigger("change");
        expect(h.$("#fileManagerSelectionCount").text()).toBe("1 selected");
        h.$(".file-manager-check").eq(1).prop("checked", false).trigger("change");
        expect(h.$("#fileManagerSelectionBar").hasClass("d-none")).toBe(true);

        h.$(".file-manager-check-all").prop("checked", true).trigger("change");
        expect(h.$("#fileManagerSelectionCount").text()).toBe("2 selected");
        expect(h.$(".file-manager-check-all").prop("checked")).toBe(true);
        h.$(".file-manager-check-all").prop("checked", false).trigger("change");
        expect(h.$("#fileManagerSelectionBar").hasClass("d-none")).toBe(true);
    });

    it("a double click opens a folder, previews an image and downloads any other file", async () => {
        const { h } = await load([folder(7, "Docs"), item(8, "a.png", { previewable: true }), item(9, "b.zip")]);

        h.$(".file-manager-tile[data-id='8']").trigger("dblclick");
        h.$(".file-manager-tile[data-id='9']").trigger("dblclick");
        h.$(".file-manager-tile[data-id='7']").trigger("dblclick");

        expect(callTo(h, "/FileManager/Preview").url).toBe("/FileManager/Preview?id=8");
        expect(callTo(h, "/FileManager/Download").url).toBe("/FileManager/Download?id=9");
        expect(bodyOf(callTo(h, "/FileManager/GetFolder"))).toEqual({ FolderId: 7 });
    });
});

describe("FileManager/UserIndex — navigation and search", () => {
    it("opening a folder draws its breadcrumb; a breadcrumb step opens its folder, the first one the root", async () => {
        const { h } = await load();
        h.$(".file-manager-tile[data-id='7']").trigger("dblclick");
        await answer(h, "/FileManager/GetFolder", { result: true, folder: { folderId: 9, breadcrumb: [{ id: 7, name: "Docs" }, { id: 9, name: "Sub" }], items: [] } });

        const crumbs = h.$("#fileManagerBreadcrumb li");
        expect(crumbs.map((_, el) => h.$(el).text()).get()).toEqual(["My Drive", "Docs", "Sub"]);

        h.$(".file-manager-crumb[data-folder-id='7']").trigger("click");
        expect(bodyOf(callTo(h, "/FileManager/GetFolder"))).toEqual({ FolderId: 7 });
        h.$(".file-manager-crumb[data-folder-id='']").trigger("click");
        expect(bodyOf(callTo(h, "/FileManager/GetFolder"))).toEqual({ FolderId: null });
    });

    it("selecting a tree node opens its folder (the root node opens the root)", async () => {
        const { h } = await load();

        h.$("#fileManagerTree").trigger("select_node.jstree", [{ node: { id: "7" } }]);
        expect(bodyOf(callTo(h, "/FileManager/GetFolder"))).toEqual({ FolderId: 7 });
        h.$("#fileManagerTree").trigger("select_node.jstree", [{ node: { id: "root" } }]);
        expect(bodyOf(callTo(h, "/FileManager/GetFolder"))).toEqual({ FolderId: null });
    });

    it("typing searches the whole drive and lists the results with their locations; clearing goes back to the folder", async () => {
        const { h } = await load([item(8, "a.txt")], "list");

        h.$("#fileManagerSearch").val(" rep ").trigger("input");
        expect(bodyOf(callTo(h, "/FileManager/Search"))).toEqual({ Query: "rep" });
        await answer(h, "/FileManager/Search", { result: true, items: [item(5, "report.txt", { location: "My Drive / Docs" })] });
        expect(h.$(".file-manager-list thead").text()).toContain("Location");
        expect(h.$(".file-manager-list tbody").text()).toContain("My Drive / Docs");

        h.$("#fileManagerSearch").val("").trigger("input");
        expect(bodyOf(callTo(h, "/FileManager/GetFolder"))).toEqual({ FolderId: null });
    });

    it("search results show their location on the tiles too, and 'no results' when nothing matches; a refused search is toasted", async () => {
        const { h } = await load();
        h.$("#fileManagerSearch").val("x").trigger("input");
        await answer(h, "/FileManager/Search", { result: true, items: [item(5, "x.txt", { location: "My Drive" })] });
        expect(h.$(".file-manager-location").text()).toBe("My Drive");

        h.$("#fileManagerSearch").val("y").trigger("input");
        await answer(h, "/FileManager/Search", { result: true, items: [] });
        expect(h.$("#fileManagerItems").text()).toBe("Nothing");

        h.$("#fileManagerSearch").val("z").trigger("input");
        await answer(h, "/FileManager/Search", { result: false, error: "bad" });
        expect(h.toastr.error).toHaveBeenCalledWith("bad");
    });
});

describe("FileManager/UserIndex — folder writes", () => {
    it("New folder posts the name into the open folder; success closes the modal, toasts and refreshes", async () => {
        const { h, modals } = await load();
        h.$("#btnNewFolder").trigger("click");
        expect(modals).toContainEqual(["newFolderModal", "show"]);
        h.$("#newFolderName").val("Design");
        h.$("#formNewFolder").trigger("submit");

        expect(bodyOf(callTo(h, "/FileManager/CreateFolder"))).toEqual({ ParentId: null, Name: "Design", Merge: false });
        await answer(h, "/FileManager/CreateFolder", { result: true, message: "made", folderId: 12 });
        expect(modals).toContainEqual(["newFolderModal", "hide"]);
        expect(h.toastr.success).toHaveBeenCalledWith("made");
        expect(callsTo(h, "/FileManager/GetDrive")).toHaveLength(2);
    });

    it("a refused folder name is toasted and the modal stays; an invalid form posts nothing", async () => {
        const { h, modals } = await load();
        h.$("#formNewFolder").trigger("submit");
        await answer(h, "/FileManager/CreateFolder", { result: false, error: "taken" });
        expect(h.toastr.error).toHaveBeenCalledWith("taken");
        expect(modals).not.toContainEqual(["newFolderModal", "hide"]);

        stubPlugin(h, "valid", () => false);
        const before = h.ajaxCalls.length;
        h.$("#formNewFolder").trigger("submit");
        h.$("#formRename").trigger("submit");
        expect(h.ajaxCalls).toHaveLength(before);
    });

    it("Rename fills the modal with the item and posts the new name", async () => {
        const { h, modals } = await load([item(8, "old.txt")], "list");
        h.$(".file-manager-action[data-action='rename']").trigger("click");
        expect(h.$("#renameName").val()).toBe("old.txt");
        expect(modals).toContainEqual(["renameModal", "show"]);

        h.$("#renameName").val("new.txt");
        h.$("#formRename").trigger("submit");
        expect(bodyOf(callTo(h, "/FileManager/Rename"))).toEqual({ Id: 8, Name: "new.txt" });
        await answer(h, "/FileManager/Rename", { result: true, message: "renamed" });
        expect(modals).toContainEqual(["renameModal", "hide"]);

        h.$("#formRename").trigger("submit");
        await answer(h, "/FileManager/Rename", { result: false, error: "no" });
        expect(h.toastr.error).toHaveBeenCalledWith("no");
    });

    it("Delete asks first, then posts the ids; a refusal is toasted", async () => {
        const { h, modals } = await load([item(8, "a.txt"), item(9, "b.txt")], "list");
        h.$(".file-manager-action[data-action='delete']").eq(1).trigger("click");
        expect(modals).toContainEqual(["confirmDeleteModal", "show"]);

        h.$("#btnConfirmDelete").trigger("click");
        expect(bodyOf(callTo(h, "/FileManager/Delete"))).toEqual({ Ids: [9] });
        await answer(h, "/FileManager/Delete", { result: false, error: "nope" });
        expect(h.toastr.error).toHaveBeenCalledWith("nope");
    });

    it("the selection bar's Delete and Move act on every selected item; Move offers the root and the other folders by location", async () => {
        const { h, modals } = await load([folder(7, "Docs"), item(8, "a.txt")]);
        h.$(".file-manager-tile[data-id='7']").trigger("click");
        h.$(".file-manager-tile[data-id='8']").trigger(h.$.Event("click", { ctrlKey: true }));

        h.$("#btnMoveSelected").trigger("click");
        expect(h.$("#moveTarget option").map((_, el) => h.$(el).text()).get()).toEqual(["My Drive"]);
        expect(modals).toContainEqual(["moveModal", "show"]);
        h.$("#btnConfirmMove").trigger("click");
        expect(bodyOf(callTo(h, "/FileManager/Move"))).toEqual({ Ids: [7, 8], TargetFolderId: null });
        await answer(h, "/FileManager/Move", { result: true, message: "moved" });
        expect(h.toastr.success).toHaveBeenCalledWith("moved");

        h.$(".file-manager-tile[data-id='8']").trigger("click");
        h.$("#btnMoveSelected").trigger("click");
        expect(h.$("#moveTarget option").map((_, el) => h.$(el).text()).get()).toEqual(["My Drive", "My Drive / Docs"]);
        h.$("#moveTarget").val("7");
        h.$("#btnConfirmMove").trigger("click");
        expect(bodyOf(callTo(h, "/FileManager/Move"))).toEqual({ Ids: [8], TargetFolderId: 7 });
        await answer(h, "/FileManager/Move", { result: false, error: "cycle" });
        expect(h.toastr.error).toHaveBeenCalledWith("cycle");

        h.$("#btnDeleteSelected").trigger("click");
        h.$("#btnConfirmDelete").trigger("click");
        expect(bodyOf(callTo(h, "/FileManager/Delete"))).toEqual({ Ids: [8] });
    });

    it("dragging a folder in the tree moves it (onto the root: to the root)", async () => {
        const { h } = await load();

        h.$("#fileManagerTree").trigger("move_node.jstree", [{ node: { id: "9" }, parent: "root" }]);
        expect(bodyOf(callTo(h, "/FileManager/Move"))).toEqual({ Ids: [9], TargetFolderId: null });
        h.$("#fileManagerTree").trigger("move_node.jstree", [{ node: { id: "9" }, parent: "7" }]);
        expect(bodyOf(callTo(h, "/FileManager/Move"))).toEqual({ Ids: [9], TargetFolderId: 7 });
    });
});

/** A DataTransfer stand-in (jsdom has none). */
function transfer(data: Record<string, string>, files: File[] = []): Record<string, unknown> {
    return {
        getData: (type: string) => data[type] ?? "",
        setData: (type: string, value: string) => { data[type] = value; },
        types: files.length > 0 ? ["Files"] : Object.keys(data),
        files,
        items: [],
    };
}

/** Fires a drag event carrying `dataTransfer` on `target`. */
function drag(h: SiteHandle, target: JQuery, type: string, dataTransfer: Record<string, unknown>): void {
    target.trigger(h.$.Event(type, { originalEvent: { dataTransfer, preventDefault: () => undefined } }));
}

describe("FileManager/UserIndex — drag and drop", () => {
    it("items dragged onto a folder tile move into it (the whole selection when the dragged item is part of it)", async () => {
        const { h } = await load([folder(7, "Docs"), item(8, "a.txt"), item(9, "b.txt")]);
        h.$(".file-manager-tile[data-id='8']").trigger("click");
        h.$(".file-manager-tile[data-id='9']").trigger(h.$.Event("click", { ctrlKey: true }));
        const data: Record<string, string> = {};

        drag(h, h.$(".file-manager-tile[data-id='9']"), "dragstart", transfer(data));
        drag(h, h.$(".file-manager-tile[data-id='7']"), "drop", transfer(data));

        expect(bodyOf(callTo(h, "/FileManager/Move"))).toEqual({ Ids: [8, 9], TargetFolderId: 7 });
    });

    it("an item dragged onto a tree folder moves there; one dropped on itself does nothing", async () => {
        const { h } = await load();
        const data: Record<string, string> = {};
        drag(h, h.$(".file-manager-tile[data-id='8']"), "dragstart", transfer(data));

        drag(h, h.$("#fileManagerTree .jstree-anchor"), "dragover", transfer(data));
        expect(h.$("#fileManagerTree .jstree-anchor").hasClass("file-manager-drop-target")).toBe(true);
        drag(h, h.$("#fileManagerTree .jstree-anchor"), "drop", transfer(data));
        expect(h.$("#fileManagerTree .jstree-anchor").hasClass("file-manager-drop-target")).toBe(false);
        expect(bodyOf(callTo(h, "/FileManager/Move"))).toEqual({ Ids: [8], TargetFolderId: 7 });

        const before = h.ajaxCalls.length;
        drag(h, h.$(".file-manager-tile[data-id='7']"), "drop", transfer({ "application/x-maroik-file-manager-items": "[7]" }));
        drag(h, h.$("#fileManagerItems"), "drop", transfer({}));
        expect(h.ajaxCalls).toHaveLength(before);
    });

    it("PC files dropped on the items upload into the open folder; dropped on a tree folder, into that folder", async () => {
        const { h } = await load();

        drag(h, h.$("#fileManagerItems"), "dragover", transfer({}));
        expect(h.$("#fileManagerItems").hasClass("file-manager-drop-target")).toBe(true);
        drag(h, h.$("#fileManagerItems"), "drop", transfer({}, [file("a.txt")]));
        await flush();
        expect(bodyOf(callTo(h, "/FileManager/CheckQuota"))).toEqual({ TotalBytes: 3 });
        await answer(h, "/FileManager/CheckQuota", { result: true });
        expect(bodyOf(callTo(h, "/FileManager/BeginUpload"))).toEqual({ ParentId: null, Name: "a.txt", Size: 3, Conflict: "Ask" });

        const other = await load();
        drag(other.h, other.h.$("#fileManagerTree .jstree-anchor"), "drop", transfer({}, [file("b.txt")]));
        await flush();
        await answer(other.h, "/FileManager/CheckQuota", { result: true });
        expect(bodyOf(callTo(other.h, "/FileManager/BeginUpload"))).toMatchObject({ ParentId: 7, Name: "b.txt" });
    });
});

/** A dropped file entry (what webkitGetAsEntry answers for a file). */
function fileEntry(f: File): Record<string, unknown> {
    return { isFile: true, isDirectory: false, name: f.name, file: (resolve: (value: File) => void) => resolve(f) };
}

/** A dropped folder entry whose reader answers its children in one batch, then an empty one. */
function folderEntry(name: string, children: Record<string, unknown>[]): Record<string, unknown> {
    return {
        isFile: false, isDirectory: true, name,
        createReader: () => {
            let batches = [children, []];
            return { readEntries: (resolve: (value: unknown[]) => void) => { const [next = [], ...rest] = batches; batches = rest; resolve(next); } };
        },
    };
}

describe("FileManager/UserIndex — dropped folders", () => {
    it("a folder dropped from the PC keeps its structure: its folders (empty ones too) are created and its files uploaded into them", async () => {
        const { h } = await load();
        const dropped = folderEntry("Root", [fileEntry(file("a.txt")), folderEntry("Empty", [])]);
        const dataTransfer = { ...transfer({}, [file("ignored")]), items: [{ webkitGetAsEntry: () => dropped }, { webkitGetAsEntry: () => null }] };

        drag(h, h.$("#fileManagerItems"), "drop", dataTransfer);
        await flush();
        expect(bodyOf(callTo(h, "/FileManager/CheckQuota"))).toEqual({ TotalBytes: 3 });
        await answer(h, "/FileManager/CheckQuota", { result: true });

        expect(bodyOf(callTo(h, "/FileManager/CreateFolder"))).toEqual({ ParentId: null, Name: "Root", Merge: true });
        await answer(h, "/FileManager/CreateFolder", { result: true, message: "", folderId: 40 });
        expect(bodyOf(callTo(h, "/FileManager/CreateFolder"))).toEqual({ ParentId: 40, Name: "Empty", Merge: true });
        await answer(h, "/FileManager/CreateFolder", { result: true, message: "", folderId: 41 });
        expect(bodyOf(callTo(h, "/FileManager/BeginUpload"))).toMatchObject({ ParentId: 40, Name: "a.txt" });
        expect(callsTo(h, "/FileManager/CreateFolder")).toHaveLength(2);
    });

    it("an empty drop starts nothing", async () => {
        const { h } = await load();
        drag(h, h.$("#fileManagerItems"), "drop", { ...transfer({}, [file("x")]), files: [], items: [] });
        await flush();

        expect(callsTo(h, "/FileManager/CheckQuota")).toHaveLength(0);
    });
});

describe("FileManager/UserIndex — upload", () => {
    it("checks the batch against the quota first; a refusal (\"N GB short\") stops it before any file starts", async () => {
        const { h } = await load();

        pick(h, "fileManagerFileInput", [file("a.txt", 3), file("b.txt", 5)]);
        await flush();
        expect(bodyOf(callTo(h, "/FileManager/CheckQuota"))).toEqual({ TotalBytes: 8 });
        await answer(h, "/FileManager/CheckQuota", { result: false, error: "Not enough storage: 0.1 GB short." });

        expect(h.toastr.error).toHaveBeenCalledWith("Not enough storage: 0.1 GB short.");
        expect(callsTo(h, "/FileManager/BeginUpload")).toHaveLength(0);
    });

    it("uploads each file in chunks (upload id, offset, bytes), completes it, lists it and refreshes", async () => {
        const { h, modals } = await load();
        pick(h, "fileManagerFileInput", [file("a.txt", 3)]);
        await flush();
        await answer(h, "/FileManager/CheckQuota", { result: true });
        expect(modals).toContainEqual(["uploadModal", "show"]);
        expect(h.$("#btnUploadClose").prop("disabled")).toBe(true);
        expect(h.$("#uploadProgressText").text()).toBe("Uploading 1/1: a.txt");

        await answer(h, "/FileManager/BeginUpload", { result: true, conflict: false, uploadId: "u-1", name: "a.txt", chunkBytes: 2 });
        let form = formDataOf(callTo(h, "/FileManager/UploadChunk"));
        expect([form.get("UploadId"), form.get("Offset")]).toEqual(["u-1", "0"]);
        expect(present(form.get("Chunk")) instanceof Blob).toBe(true);
        await answer(h, "/FileManager/UploadChunk", { result: true, length: 2 });
        form = formDataOf(callTo(h, "/FileManager/UploadChunk"));
        expect(form.get("Offset")).toBe("2");
        await answer(h, "/FileManager/UploadChunk", { result: true, length: 3 });

        expect(bodyOf(callTo(h, "/FileManager/CompleteUpload"))).toEqual({ UploadId: "u-1", Overwrite: false });
        await answer(h, "/FileManager/CompleteUpload", { result: true, message: "ok" });
        expect(h.$("#uploadResults li").text()).toBe("a.txt — Uploaded");
        expect(h.$("#uploadProgressText").text()).toBe("1 up, 0 failed, 0 skipped");
        expect(h.$("#btnUploadClose").prop("disabled")).toBe(false);
        expect(callsTo(h, "/FileManager/GetDrive")).toHaveLength(2);
    });

    it("a refused or failed file is listed with its reason and the next one still goes; a failed chunk aborts its upload", async () => {
        const { h } = await load();
        pick(h, "fileManagerFileInput", [file("bad.exe"), file("chunky.bin"), file("good.txt")]);
        await flush();
        await answer(h, "/FileManager/CheckQuota", { result: true });

        await answer(h, "/FileManager/BeginUpload", { result: false, error: "Invalid name" });
        await answer(h, "/FileManager/BeginUpload", { result: true, conflict: false, uploadId: "u-2", name: "chunky.bin", chunkBytes: 16 });
        await answer(h, "/FileManager/UploadChunk", { result: false, error: "temporary" });
        expect(bodyOf(callTo(h, "/FileManager/AbortUpload"))).toEqual({ UploadId: "u-2" });
        await answer(h, "/FileManager/AbortUpload", { result: true });
        await answer(h, "/FileManager/BeginUpload", { result: true, conflict: false, uploadId: "u-3", name: "good.txt", chunkBytes: 16 });
        await answer(h, "/FileManager/UploadChunk", { result: true, length: 3 });
        await answer(h, "/FileManager/CompleteUpload", { result: false, error: "infected" });

        expect(h.$("#uploadResults li").map((_, el) => h.$(el).text()).get()).toEqual(["bad.exe — Invalid name", "chunky.bin — temporary", "good.txt — infected"]);
        expect(h.$("#uploadProgressText").text()).toBe("0 up, 3 failed, 0 skipped");
    });

    it("a reply of another shape fails only that file (the generic error is shown) and the batch goes on", async () => {
        const { h } = await load();
        pick(h, "fileManagerFileInput", [file("a.txt"), file("b.txt"), file("c.txt"), file("d.txt")]);
        await flush();
        await answer(h, "/FileManager/CheckQuota", { result: true });

        await answer(h, "/FileManager/BeginUpload", { result: true });
        await answer(h, "/FileManager/BeginUpload", { result: true, conflict: false, uploadId: "u-b", name: "b.txt", chunkBytes: 16 });
        await answer(h, "/FileManager/UploadChunk", { result: true });
        expect(bodyOf(callTo(h, "/FileManager/AbortUpload"))).toEqual({ UploadId: "u-b" });
        await answer(h, "/FileManager/AbortUpload", { result: true });
        await answer(h, "/FileManager/BeginUpload", { result: true, conflict: false, uploadId: "u-c", name: "c.txt", chunkBytes: 16 });
        await answer(h, "/FileManager/UploadChunk", { result: true, length: 3 });
        await answer(h, "/FileManager/CompleteUpload", { result: true });
        await answer(h, "/FileManager/BeginUpload", { result: true, conflict: true, uploadId: "", name: "d.txt", chunkBytes: 16 });
        h.$("#btnConflictKeepBoth").trigger("click");
        await flush();
        await answer(h, "/FileManager/BeginUpload", { result: true, conflict: true, uploadId: "", name: "d.txt", chunkBytes: 16 });

        expect(h.toastr.error).toHaveBeenCalledWith("L_TemporaryError");
        expect(h.$("#uploadResults li").map((_, el) => h.$(el).text()).get()).toEqual(["a.txt — ", "b.txt — ", "c.txt — ", "d.txt — 'd.txt' exists"]);
    });

    it("a quota or folder reply of another shape stops quietly (the generic error is shown)", async () => {
        const { h } = await load();
        pick(h, "fileManagerFileInput", [file("a.txt")]);
        await flush();
        await answer(h, "/FileManager/CheckQuota", { ok: true });
        expect(callsTo(h, "/FileManager/BeginUpload")).toHaveLength(0);

        pick(h, "fileManagerFolderInput", [file("a.txt", 3, "Root/a.txt")]);
        await flush();
        await answer(h, "/FileManager/CheckQuota", { result: true });
        await answer(h, "/FileManager/CreateFolder", { result: true });
        expect(h.$("#uploadResults li").text()).toBe("Root — ");
        expect(h.toastr.error).toHaveBeenCalledWith("L_TemporaryError");
    });

    it("a file over 2 GB is refused before it starts (a UX mirror of the server's limit)", async () => {
        const h = loadSite("user", "FileManager", "UserIndex", fixture(10));
        stubWidgets(h);
        pick(h, "fileManagerFileInput", [file("big.iso", 11)]);
        await flush();
        await answer(h, "/FileManager/CheckQuota", { result: true });

        expect(h.$("#uploadResults li").text()).toBe("big.iso — Too large");
        expect(callsTo(h, "/FileManager/BeginUpload")).toHaveLength(0);
    });

    it("a name conflict asks; \"Keep both\" restarts the file with that choice and \"Apply to all\" answers the next conflicts too", async () => {
        const { h, modals } = await load();
        pick(h, "fileManagerFileInput", [file("a.txt"), file("b.txt")]);
        await flush();
        await answer(h, "/FileManager/CheckQuota", { result: true });

        await answer(h, "/FileManager/BeginUpload", { result: true, conflict: true, uploadId: "", name: "a.txt", chunkBytes: 16 });
        expect(h.$("#conflictMessage").text()).toBe("'a.txt' exists");
        expect(modals).toContainEqual(["conflictModal", "show"]);
        h.$("#conflictApplyToAll").prop("checked", true);
        h.$("#btnConflictKeepBoth").trigger("click");
        await flush();
        expect(bodyOf(callTo(h, "/FileManager/BeginUpload"))).toEqual({ ParentId: null, Name: "a.txt", Size: 3, Conflict: "KeepBoth" });
        await answer(h, "/FileManager/BeginUpload", { result: true, conflict: false, uploadId: "u-a", name: "a (1).txt", chunkBytes: 16 });
        await answer(h, "/FileManager/UploadChunk", { result: true, length: 3 });
        await answer(h, "/FileManager/CompleteUpload", { result: true, message: "ok" });

        expect(bodyOf(callTo(h, "/FileManager/BeginUpload"))).toEqual({ ParentId: null, Name: "b.txt", Size: 3, Conflict: "KeepBoth" });
    });

    it("\"Overwrite\" completes with overwrite on; \"Skip\" (for all) skips this file and every later conflict without asking", async () => {
        const { h, modals } = await load();
        pick(h, "fileManagerFileInput", [file("a.txt"), file("b.txt"), file("c.txt")]);
        await flush();
        await answer(h, "/FileManager/CheckQuota", { result: true });

        await answer(h, "/FileManager/BeginUpload", { result: true, conflict: true, uploadId: "", name: "a.txt", chunkBytes: 16 });
        h.$("#btnConflictOverwrite").trigger("click");
        await flush();
        await answer(h, "/FileManager/BeginUpload", { result: true, conflict: false, uploadId: "u-a", name: "a.txt", chunkBytes: 16 });
        await answer(h, "/FileManager/UploadChunk", { result: true, length: 3 });
        expect(bodyOf(callTo(h, "/FileManager/CompleteUpload"))).toEqual({ UploadId: "u-a", Overwrite: true });
        await answer(h, "/FileManager/CompleteUpload", { result: true, message: "ok" });

        await answer(h, "/FileManager/BeginUpload", { result: true, conflict: true, uploadId: "", name: "b.txt", chunkBytes: 16 });
        h.$("#conflictApplyToAll").prop("checked", true);
        h.$("#btnConflictSkip").trigger("click");
        await flush();
        const asked = modals.filter((m) => m[0] === "conflictModal" && m[1] === "show").length;
        await answer(h, "/FileManager/BeginUpload", { result: true, conflict: true, uploadId: "", name: "c.txt", chunkBytes: 16 });

        expect(modals.filter((m) => m[0] === "conflictModal" && m[1] === "show")).toHaveLength(asked);
        expect(h.$("#uploadResults li").map((_, el) => h.$(el).text()).get()).toEqual(["a.txt — Uploaded", "b.txt — Skipped", "c.txt — Skipped"]);
        expect(h.$("#uploadProgressText").text()).toBe("1 up, 0 failed, 2 skipped");
    });

    it("a folder picked with its structure creates the folders top down (merging) and uploads each file into its own folder", async () => {
        const { h } = await load();
        pick(h, "fileManagerFolderInput", [file("a.txt", 3, "Root/a.txt"), file("b.txt", 3, "Root/Sub/b.txt")]);
        await flush();
        await answer(h, "/FileManager/CheckQuota", { result: true });

        expect(bodyOf(callTo(h, "/FileManager/CreateFolder"))).toEqual({ ParentId: null, Name: "Root", Merge: true });
        await answer(h, "/FileManager/CreateFolder", { result: true, message: "", folderId: 20 });
        expect(bodyOf(callTo(h, "/FileManager/BeginUpload"))).toMatchObject({ ParentId: 20, Name: "a.txt" });
        await answer(h, "/FileManager/BeginUpload", { result: true, conflict: false, uploadId: "u-a", name: "a.txt", chunkBytes: 16 });
        await answer(h, "/FileManager/UploadChunk", { result: true, length: 3 });
        await answer(h, "/FileManager/CompleteUpload", { result: true, message: "ok" });

        expect(bodyOf(callTo(h, "/FileManager/CreateFolder"))).toEqual({ ParentId: 20, Name: "Sub", Merge: true });
        await answer(h, "/FileManager/CreateFolder", { result: true, message: "", folderId: 21 });
        expect(bodyOf(callTo(h, "/FileManager/BeginUpload"))).toMatchObject({ ParentId: 21, Name: "b.txt" });
    });

    it("a folder that cannot be created fails its files, which are listed", async () => {
        const { h } = await load();
        pick(h, "fileManagerFolderInput", [file("a.txt", 3, "Bad:/a.txt")]);
        await flush();
        await answer(h, "/FileManager/CheckQuota", { result: true });
        await answer(h, "/FileManager/CreateFolder", { result: false, error: "Invalid name" });

        expect(h.$("#uploadResults li").text()).toBe("Bad: — Invalid name");
        expect(h.$("#uploadProgressText").text()).toBe("0 up, 1 failed, 0 skipped");
    });

    it("the Upload buttons open the pickers; picking nothing starts nothing", async () => {
        const { h } = await load();
        let opened = 0;
        h.$("#fileManagerFileInput, #fileManagerFolderInput").on("click", () => { opened++; });
        h.$("#btnUploadFiles").trigger("click");
        h.$("#btnUploadFolder").trigger("click");
        expect(opened).toBe(2);

        pick(h, "fileManagerFileInput", []);
        await flush();
        expect(callsTo(h, "/FileManager/CheckQuota")).toHaveLength(0);
    });
});

describe("FileManager/UserIndex — download and preview", () => {
    it("a list row's Download saves the file under its name", async () => {
        const { h } = await load([item(8, "a.txt")], "list");
        h.$(".file-manager-action[data-action='download']").trigger("click");

        const call = callTo(h, "/FileManager/Download");
        expect(call.url).toBe("/FileManager/Download?id=8");
        expect(call.xhrFields).toEqual({ responseType: "blob" });
        successOf(call)(new Blob(["x"], { type: "application/octet-stream" }));
        await flush();
        expect(h.toastr.error).not.toHaveBeenCalled();
    });

    it("a JSON refusal instead of a file is toasted", async () => {
        const { h } = await load([item(8, "a.txt")], "list");
        h.$(".file-manager-action[data-action='download']").trigger("click");
        successOf(callTo(h, "/FileManager/Download"))(new Blob([JSON.stringify({ result: false, error: "gone" })], { type: "application/json" }));
        await flush();

        expect(h.toastr.error).toHaveBeenCalledWith("gone");
    });

    it("the selection bar's Download lists the files (a folder's included) and downloads them one by one", async () => {
        const { h } = await load([folder(7, "Docs"), item(8, "a.txt")]);
        h.$(".file-manager-tile[data-id='7']").trigger("click");
        h.$("#btnDownloadSelected").trigger("click");
        expect(bodyOf(callTo(h, "/FileManager/ListDownloadFiles"))).toEqual({ Ids: [7] });
        await answer(h, "/FileManager/ListDownloadFiles", { result: true, files: [{ id: 30, name: "x.txt" }, { id: 31, name: "y.txt" }] });

        expect(callTo(h, "/FileManager/Download").url).toBe("/FileManager/Download?id=30");
        successOf(callTo(h, "/FileManager/Download"))(new Blob(["x"]));
        await flush();
        expect(callTo(h, "/FileManager/Download").url).toBe("/FileManager/Download?id=31");

        h.$("#btnDownloadSelected").trigger("click");
        await answer(h, "/FileManager/ListDownloadFiles", { result: false, error: "missing" });
        expect(h.toastr.error).toHaveBeenCalledWith("missing");
    });

    it("Preview shows the image in the modal (through a blob URL) and releases it when the modal closes", async () => {
        const { h, modals } = await load([item(8, "a.png", { previewable: true })], "list");
        h.$(".file-manager-action[data-action='preview']").trigger("click");
        successOf(callTo(h, "/FileManager/Preview"))(new Blob(["png"], { type: "image/png" }));
        await flush();

        expect(h.$("#previewTitle").text()).toBe("a.png");
        expect(h.$("#previewImage").attr("src")).toBeTruthy();
        expect(modals).toContainEqual(["previewModal", "show"]);

        h.$(".file-manager-action[data-action='preview']").trigger("click");
        successOf(callTo(h, "/FileManager/Preview"))(new Blob(["png"], { type: "image/png" }));
        await flush();
        h.$("#previewModal").trigger("hidden.bs.modal");
        expect(h.$("#previewImage").attr("src")).toBe("");
        h.$("#previewModal").trigger("hidden.bs.modal");
    });
});

describe("FileManager/UserIndex — replies of another shape", () => {
    it("every request's mismatched reply only shows the generic error and changes nothing", async () => {
        const { h, modals } = await load([folder(7, "Docs"), item(8, "a.txt")], "list");
        const bad = { unexpected: true };
        const before = h.$("#fileManagerItems").html();

        h.$("#fileManagerSearch").val("x").trigger("input");
        await answer(h, "/FileManager/Search", bad);
        h.$("#formNewFolder").trigger("submit");
        await answer(h, "/FileManager/CreateFolder", bad);
        h.$("#formRename").trigger("submit");
        await answer(h, "/FileManager/Rename", bad);
        h.$("#btnConfirmDelete").trigger("click");
        await answer(h, "/FileManager/Delete", bad);
        h.$(".file-manager-check").eq(1).prop("checked", true).trigger("change");
        h.$("#btnMoveSelected").trigger("click");
        h.$("#btnConfirmMove").trigger("click");
        await answer(h, "/FileManager/Move", bad);
        h.$("#btnDownloadSelected").trigger("click");
        await answer(h, "/FileManager/ListDownloadFiles", bad);
        h.$(".file-manager-action[data-action='download']").trigger("click");
        successOf(callTo(h, "/FileManager/Download"))("not a blob");
        await flush();

        h.$(".file-manager-check").eq(1).prop("checked", false).trigger("change");
        expect(h.$("#fileManagerItems").html()).toBe(before);

        const reloaded = loadSite("user", "FileManager", "UserIndex", fixture());
        const widgets = stubWidgets(reloaded);
        await answer(reloaded, "/FileManager/GetDrive", bad);

        expect(h.toastr.error).toHaveBeenCalledTimes(7);
        expect(modals.filter((m) => m[1] === "hide").map((m) => m[0])).toEqual(["confirmDeleteModal", "moveModal"]);
        expect(widgets.tree.options).toHaveLength(0);
        expect(reloaded.toastr.error).toHaveBeenCalledWith("L_TemporaryError");
    });
});

describe("FileManager/UserIndex — storage", () => {
    it("works without localStorage (the grid view is shown and a choice lasts for the page)", async () => {
        const original = Object.getOwnPropertyDescriptor(window, "localStorage");
        Object.defineProperty(window, "localStorage", { configurable: true, get() { throw new Error("blocked"); } });
        try {
            const h = loadSite("user", "FileManager", "UserIndex", fixture());
            stubWidgets(h);
            await answer(h, "/FileManager/GetFolder", { result: true, folder: { folderId: null, breadcrumb: [], items: [item(1, "a")] } });
            expect(h.$(".file-manager-tile")).toHaveLength(1);
            h.$("#btnListView").trigger("click");
            expect(h.$(".file-manager-list")).toHaveLength(1);
        } finally {
            if (original) Object.defineProperty(window, "localStorage", original);
        }
    });
});
