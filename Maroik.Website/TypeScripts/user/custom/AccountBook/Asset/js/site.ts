/**
 * Script for the **Asset** page of the account book
 * (`Views/AccountBook/Asset.cshtml`). A NonFactors MVC.Grid list of assets with
 * create / edit / delete modals and an "export to Excel" button.
 *
 * The pattern used here recurs on every grid-backed CRUD page (Income,
 * Expenditure, Management/Account, Management/Menu, Notice/Fixed*, …):
 *
 *   • The grid's companion JS dispatches native `CustomEvent`s on `document`
 *     (`rowclick`, `reloadstart/end/fail`, `gridconfigure`) — not jQuery events —
 *     so their handlers take `e: any` and read `e.detail`.
 *   • There is no "selected row" variable. Selection is just a CSS class
 *     (`selectedRowColor`) on one `.clsGridRow`; code that needs the current
 *     selection re-scans the rows for that class.
 *   • Each mutation validates its form, POSTs JSON, then on success hides the
 *     modal, reloads the grid and shows a toastr; server rule failures come back
 *     as `{ result:false, error }` and become `toastr.error`.
 *   • Every rule is also enforced server-side — these checks are UX only.
 *
 * IIFE-wrapped, no `import` / `export`.
 */
(function() {
    // The runtime-check helpers the _Layout script defines (see TypeScripts/global.d.ts).
    const { check, replies, onReply, fieldValue, attribute } = window;

    // The replies this page reads, mirroring the controllers' Json(...) results (see window.replies).
    const assetReply = replies.read({
        asset: check.object({ productName: check.string, item: check.string, amount: check.number, monetaryUnit: check.string, note: check.string, deleted: check.boolean }),
    });
    // Cached references: jQuery-UI tab containers, the grid search box, and every
    // field of the creation/edit modals plus the anti-forgery token input.
    const $createAssetTabs = $("#createAssetTabs");
    const $editAssetTabs = $("#editAssetTabs");
    const $gridSearch = $("#gridSearch");
    const $formCreateAsset = $("#formCreateAsset");
    const $createAssetProductName = $("#createAssetProductName");
    const $createAssetItem = $("#createAssetItem");
    const $createAssetAmount = $("#createAssetAmount");
    const $createAssetMonetaryUnit = $("#createAssetMonetaryUnit");
    const $createAssetNote = $("#createAssetNote");
    const $__RequestVerificationToken = $("input[name=\"__RequestVerificationToken\"]");
    const $createAssetDialogModal = $("#createAssetDialogModal");
    const $editAssetOriginalProductName = $("#editAssetOriginalProductName");
    const $editAssetProductName = $("#editAssetProductName");
    const $editAssetItem = $("#editAssetItem");
    const $editAssetAmount = $("#editAssetAmount");
    const $editAssetMonetaryUnit = $("#editAssetMonetaryUnit");
    const $editAssetNote = $("#editAssetNote");
    const $editAssetDeleted = $("#editAssetDeleted");
    const $editAssetDialogModal = $("#editAssetDialogModal");
    const $formEditAsset = $("#formEditAsset");
    const $confirmDeleteAssetDialogModal = $("#confirmDeleteAssetDialogModal");
    const $btnEditAssetGridRow = $("#btnEditAssetGridRow");
    const $btnConfirmDeleteAsset = $("#btnConfirmDeleteAsset");
    const $btnExportExcelAsset = $("#btnExportExcelAsset");
    const $btnDeleteAsset = $("#btnDeleteAsset");

    // Initialize the jQuery-UI tab widgets inside the two modals once the DOM is ready.
    $(function() {
        $createAssetTabs.tabs();
        $editAssetTabs.tabs();
    });

    // Bootstrap table class that marks the currently selected grid row.
    let selectedRowColor = "table-primary";

    // MvcGrid fires `rowclick` as a native CustomEvent on `document`; `e.detail`
    // is the clicked row and `e.detail.data` its bound record. Move the
    // selection highlight to every row whose ProductName matches (there is only
    // one, but the grid keys rows by `data-productName`, not by index).
    $(document).off("rowclick.Asset").on("rowclick.Asset", (e: JQuery.TriggeredEvent) => {
        let selectedRow = e.detail as unknown as MvcGridRowClickDetail;
        let selectedRowProductName = selectedRow.data.ProductName;
        let $clsGridRow = $(".clsGridRow");

        $clsGridRow.each(function() {
            $(this).removeClass(selectedRowColor);
        });

        $clsGridRow.each(function() {
            // Stringify both sides defensively before comparing the identity key.
            if ((String(selectedRowProductName).valueOf() === String(attribute($(this), "data-productName")).valueOf())) {
                $(this).addClass(selectedRowColor);
            }
        });
    });

    // MvcGrid lifecycle events. Intentionally no-ops here — bound (and cleared
    // first with `.off`) only so the page has an explicit place to hook grid
    // reload progress later without touching the grid library.
    $(document).off("reloadstart.Asset").on("reloadstart.Asset", _ => {
    });

    $(document).off("reloadend.Asset").on("reloadend.Asset", _ => {
    });

    $(document).off("reloadfail.Asset").on("reloadfail.Asset", _ => {
    });

    $(document).off("gridconfigure.Asset").on("gridconfigure.Asset", _ => {
    });

    // Double-clicking the highlighted row opens its edit modal. Delegated from
    // `document` because grid rows are replaced on every reload.
    $(document).off("dblclick.Asset", ".clsGridRow").on("dblclick.Asset", ".clsGridRow", function() {
        $(".clsGridRow").each(function() {
            if (attribute($(this), "class").includes(selectedRowColor)) {
                EditAssetGridRow(attribute($btnEditAssetGridRow, "data-errorMessageSelectGridRow"));
            }
        });
    });

    // Free-text search box: push the term into the grid's query string as
    // `wholeSearch` and reload. A fresh `MvcGrid` wrapper is created each time
    // because the grid element is swapped on reload.
    $gridSearch.off("input").on("input", function() {
        const grid = new MvcGrid(document.querySelector(".mvc-grid"));
        grid.url.searchParams.set("wholeSearch", (this as HTMLInputElement).value);
        grid.reload();
    });

    /**
     * Create-asset modal submit: validate, POST the field values as JSON, and on
     * success close the modal, reload the grid and toast. Returns `false` so the
     * form never navigates.
     */
    function CreateAsset() {

        if (!$formCreateAsset.valid()) {
            return false;
        }

        let productName = $createAssetProductName.val();
        let item = $createAssetItem.val();
        let amount = $createAssetAmount.val();
        let monetaryUnit = $createAssetMonetaryUnit.val();
        let note = $createAssetNote.val();

        let paramValue = JSON.stringify({
            ProductName: productName,
            Item: item,
            Amount: amount,
            MonetaryUnit: monetaryUnit,
            Note: note
        });

        $.ajax({
            url: "/AccountBook/CreateAsset",
            type: "POST",
            headers: { "RequestVerificationToken": fieldValue($__RequestVerificationToken) },
            dataType: "json",
            data: paramValue,
            contentType: "application/json; charset=utf-8",
            success: onReply(replies.action, function(data) {
                if (data.result) {
                    $createAssetDialogModal.modal("hide");

                    const grid = new MvcGrid(document.querySelector(".mvc-grid"));
                    grid.reload();

                    toastr.success(data.message);
                } else {
                    toastr.error(data.error);
                }
            })
        });

        return false;
    }

    /**
     * Opens the edit modal for the currently selected grid row. Finds the
     * selection by CSS class, aborts with a toast if nothing is selected, then
     * asks the server for the record (`IsAssetExists`) and fills the form from
     * the response. `OriginalProductName` is kept as a hidden field so the
     * update knows which row to rename.
     *
     * @param errorMessageSelectGridRow localized "pick a row first" text, from
     *   the button's `data-*` attribute (optional only because `.attr()` is).
     */
    function EditAssetGridRow(errorMessageSelectGridRow: string) {

        let selectedRowProductName = "";

        $(".clsGridRow").each(function() {
            if (attribute($(this), "class").includes(selectedRowColor)) {
                selectedRowProductName = attribute($(this), "data-productName");
            }
        });

        if (selectedRowProductName === "") {
            toastr.error(errorMessageSelectGridRow);
            return false;
        }

        $.ajax({
            url: "/AccountBook/IsAssetExists" + "?productName=" + encodeURIComponent(selectedRowProductName),
            type: "POST",
            headers: { "RequestVerificationToken": fieldValue($__RequestVerificationToken) },
            dataType: "json",
            contentType: "application/json; charset=utf-8",
            success: onReply(assetReply, function(data) {
                if (data.result) {

                    $editAssetOriginalProductName.val(data.asset.productName);
                    $editAssetProductName.val(data.asset.productName);
                    // `.trigger("change")` so any listener on the Item select re-syncs.
                    $editAssetItem.val(data.asset.item).trigger("change");
                    $editAssetAmount.val(data.asset.amount);
                    $editAssetMonetaryUnit.val(data.asset.monetaryUnit);
                    $editAssetNote.val(data.asset.note);
                    $editAssetDeleted.prop("checked", data.asset.deleted);

                    // Static backdrop + no keyboard dismiss; `toggle` then `show`
                    // forces it open even if Bootstrap thinks a transition is running.
                    $editAssetDialogModal.modal({
                        keyboard: false,
                        backdrop: "static"
                    });

                    $editAssetDialogModal.modal("toggle");
                    $editAssetDialogModal.modal("show");
                } else {
                    toastr.error(data.error);
                }
            })
        });
    }

    /**
     * Edit-asset modal submit: validate, POST all fields (plus `Deleted` and the
     * original name) as JSON, then close / reload / toast on success.
     */
    function UpdateAsset() {

        if (!$formEditAsset.valid()) {
            return false;
        }

        let productName = $editAssetProductName.val();
        let item = $editAssetItem.val();
        let amount = $editAssetAmount.val();
        let monetaryUnit = $editAssetMonetaryUnit.val();
        let note = $editAssetNote.val();
        let deleted = $editAssetDeleted.is(":checked");
        let originalProductName = $editAssetOriginalProductName.val();

        let paramValue = JSON.stringify({
            ProductName: productName,
            Item: item,
            Amount: amount,
            MonetaryUnit: monetaryUnit,
            Note: note,
            Deleted: deleted,
            OriginalProductName: originalProductName
        });

        $.ajax({
            url: "/AccountBook/UpdateAsset",
            type: "POST",
            headers: { "RequestVerificationToken": fieldValue($__RequestVerificationToken) },
            dataType: "json",
            data: paramValue,
            contentType: "application/json; charset=utf-8",
            success: onReply(replies.action, function(data) {
                if (data.result) {
                    $editAssetDialogModal.modal("hide");

                    const grid = new MvcGrid(document.querySelector(".mvc-grid"));
                    grid.reload();

                    toastr.success(data.message);
                } else {
                    toastr.error(data.error);
                }
            })
        });
        return false;
    }

    /**
     * "Delete" button: verify a row is selected, then just open the confirmation
     * modal. The actual delete happens in `DeleteAsset` when it is confirmed.
     */
    function ConfirmDeleteAsset(errorMessageSelectGridRow: string) {

        let selectedRowProductName = "";

        $(".clsGridRow").each(function() {
            if (attribute($(this), "class").includes(selectedRowColor)) {
                selectedRowProductName = attribute($(this), "data-productName");
            }
        });

        if (selectedRowProductName === "") {
            toastr.error(errorMessageSelectGridRow);
            return false;
        }

        $confirmDeleteAssetDialogModal.modal({
            keyboard: false,
            backdrop: "static"
        });

        $confirmDeleteAssetDialogModal.modal("toggle");
        $confirmDeleteAssetDialogModal.modal("show");
    }

    /**
     * Confirmed delete: re-check the selection, confirm the record still exists
     * (`IsAssetExists`), then POST `DeleteAsset`. Nested so the delete always
     * uses the server's current record, not stale row markup.
     */
    function DeleteAsset(errorMessageSelectGridRow: string) {

        let selectedRowProductName = "";

        $(".clsGridRow").each(function() {
            if (attribute($(this), "class").includes(selectedRowColor)) {
                selectedRowProductName = attribute($(this), "data-productName");
            }
        });

        if (selectedRowProductName === "") {
            toastr.error(errorMessageSelectGridRow);
            return false;
        }

        $.ajax({
            url: "/AccountBook/IsAssetExists" + "?productName=" + encodeURIComponent(selectedRowProductName),
            type: "POST",
            headers: { "RequestVerificationToken": fieldValue($__RequestVerificationToken) },
            dataType: "json",
            contentType: "application/json; charset=utf-8",
            success: onReply(assetReply, function(data) {
                if (data.result) {

                    let paramValue = JSON.stringify({
                        ProductName: data.asset.productName
                    });

                    $.ajax({
                        url: "/AccountBook/DeleteAsset",
                        type: "POST",
                        headers: { "RequestVerificationToken": fieldValue($__RequestVerificationToken) },
                        dataType: "json",
                        data: paramValue,
                        contentType: "application/json; charset=utf-8",
                        success: onReply(replies.action, function(data) {
                            if (data.result) {
                                $confirmDeleteAssetDialogModal.modal("hide");

                                const grid = new MvcGrid(document.querySelector(".mvc-grid"));
                                grid.reload();

                                toastr.success(data.message);
                            } else {
                                toastr.error(data.error);
                            }
                        })
                    });
                } else {
                    toastr.error(data.error);
                }
            })
        });
    }

    /**
     * "Export to Excel": the response is a binary file, which `$.ajax` cannot
     * save, so build a throwaway `<form>` and submit it as a real POST — the
     * browser then handles the download. The anti-forgery token rides along as a
     * hidden input because there is no MVC form here to carry it.
     */
    function ExportExcelAsset() {
        let form = document.createElement("form");
        let element1 = document.createElement("input");
        let element2 = document.createElement("input");

        form.method = "POST";
        form.action = "/AccountBook/ExportExcelAsset";

        element1.name = "__RequestVerificationToken";
        element1.value = fieldValue($__RequestVerificationToken);
        form.appendChild(element1);

        element2.name = "fileName";
        element2.value = "Asset";
        form.appendChild(element2);

        document.body.appendChild(form);

        form.submit();
    }

    // --- Button wiring ---------------------------------------------------
    // The edit/confirm/delete buttons pass their localized "select a row first"
    // message down from a `data-*` attribute.
    $btnEditAssetGridRow.off("click").on("click", function() {
        EditAssetGridRow(attribute($(this), "data-errorMessageSelectGridRow"));
    });

    $btnConfirmDeleteAsset.off("click").on("click", function() {
        ConfirmDeleteAsset(attribute($(this), "data-errorMessageSelectGridRow"));
    });

    $btnExportExcelAsset.off("click").on("click", function() {
        ExportExcelAsset();
    });

    $btnDeleteAsset.off("click").on("click", function() {
        DeleteAsset(attribute($(this), "data-errorMessageSelectGridRow"));
    });

    $formCreateAsset.off("submit").on("submit", function() {
        return CreateAsset();
    });

    $formEditAsset.off("submit").on("submit", function() {
        return UpdateAsset();
    });
})();
