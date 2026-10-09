/**
 * Script for the admin **Account** management page
 * (`Views/Management/Account.cshtml`). A NonFactors MVC.Grid list of user
 * accounts with create / edit / delete modals and Excel export.
 *
 * Same shape as every grid-backed CRUD page in the app (see
 * `AccountBook/Asset` for the fullest commentary):
 *
 *   • MvcGrid raises native `CustomEvent`s on `document` (`rowclick`,
 *     `reloadstart/end/fail`, `gridconfigure`) — handled with `e: any` / `e.detail`.
 *   • The selected row is tracked only by the `selectedRowColor` CSS class on
 *     one `.clsGridRow`; the identity key here is the account e-mail
 *     (`data-email`).
 *   • Each mutation validates its form, POSTs JSON, then on success hides the
 *     modal, reloads the grid and toasts; `{ result:false, error }` becomes a
 *     `toastr.error`. Every rule is re-enforced server-side.
 *
 * IIFE-wrapped, no `import` / `export`.
 */
(function() {
    // The runtime-check helpers the _Layout script defines (see TypeScripts/global.d.ts).
    const { fieldValue, attribute } = window;
    // Cached references: the jQuery-UI tab containers, the grid search box, and
    // every field of the creation / edit modals plus the anti-forgery input.
    const $createAccountTabs = $("#createAccountTabs");
    const $editAccountTabs = $("#editAccountTabs");
    const $gridSearch = $("#gridSearch");
    const $formCreateAccount = $("#formCreateAccount");
    const $createAccountEmail = $("#createAccountEmail");
    const $createAccountPassword = $("#createAccountPassword");
    const $createAccountNickname = $("#createAccountNickname");
    const $createAccountRole = $("#createAccountRole");
    const $createAccountTimeZone = $("#createAccountTimeZone");
    const $__RequestVerificationToken = $("input[name=\"__RequestVerificationToken\"]");
    const $createAccountDialogModal = $("#createAccountDialogModal");
    const $editAccountEmail = $("#editAccountEmail");
    const $editAccountNickname = $("#editAccountNickname");
    const $editAccountRole = $("#editAccountRole");
    const $editAccountTimeZone = $("#editAccountTimeZone");
    const $editAccountLocked = $("#editAccountLocked");
    const $editAccountEmailConfirmed = $("#editAccountEmailConfirmed");
    const $editAccountAgreedServiceTerms = $("#editAccountAgreedServiceTerms");
    const $editMessage = $("#editMessage");
    const $editAccountDeleted = $("#editAccountDeleted");
    const $formEditAccount = $("#formEditAccount");
    const $editAccountPassword = $("#editAccountPassword");
    const $btnEditAccountGridRow = $("#btnEditAccountGridRow");
    const $btnConfirmDeleteAccount = $("#btnConfirmDeleteAccount");
    const $btnExportExcelAccount = $("#btnExportExcelAccount");
    const $btnDeleteAccount = $("#btnDeleteAccount");
    const $editAccountDialogModal = $("#editAccountDialogModal");
    const $confirmDeleteAccountDialogModal = $("#confirmDeleteAccountDialogModal");
    // Bootstrap table class that marks the currently selected grid row.
    const selectedRowColor = "table-primary";

    // Initialize the jQuery-UI tab widgets inside the two modals.
    $(function() {
        $createAccountTabs.tabs();
        $editAccountTabs.tabs();
    });

    // MvcGrid `rowclick` (native CustomEvent): move the selection highlight to
    // the row whose `data-email` matches the clicked record.
    $(document).off("rowclick.Account").on("rowclick.Account", (e: JQuery.TriggeredEvent) => {
        let selectedRow = e.detail as unknown as MvcGridRowClickDetail;
        let selectedRowEmail = selectedRow.data.Email;

        let $clsGridRow = $(".clsGridRow");

        $clsGridRow.each(function() {
            $(this).removeClass(selectedRowColor);
        });

        $clsGridRow.each(function() {
            if ((String(selectedRowEmail).valueOf() === String($(this).attr("data-email")).valueOf())) {
                $(this).addClass(selectedRowColor);
            }
        });
    });

    // MvcGrid lifecycle events — intentional no-ops, bound only as explicit hook points.
    $(document).off("reloadstart.Account").on("reloadstart.Account", _ => {
    });

    $(document).off("reloadend.Account").on("reloadend.Account", _ => {
    });

    $(document).off("reloadfail.Account").on("reloadfail.Account", _ => {
    });

    $(document).off("gridconfigure.Account").on("gridconfigure.Account", _ => {
    });

    // Double-click the highlighted row to open its edit modal (delegated because
    // rows are re-rendered on every grid reload).
    $(document).off("dblclick.Account", ".clsGridRow").on("dblclick.Account", ".clsGridRow", function() {
        $(".clsGridRow").each(function() {
            if ($(this).attr("class")!.includes(selectedRowColor)) {
                EditAccountGridRow();
            }
        });
    });

    // Free-text search: push the term into the grid query string and reload.
    $gridSearch.off("input").on("input", function(event) {
        const grid = new MvcGrid(document.querySelector(".mvc-grid"));
        grid.url.searchParams.set("wholeSearch", (event.currentTarget as HTMLInputElement).value);
        grid.reload();
    });

    /** Create-account modal submit: validate, POST the fields as JSON, then close / reload / toast. */
    function CreateAccount() {

        if (!$formCreateAccount.valid()) {
            return false;
        }

        let email = $createAccountEmail.val();
        let password = $createAccountPassword.val();
        let nickname = $createAccountNickname.val();
        let role = $createAccountRole.val();
        let timeZoneIanaId = $createAccountTimeZone.val();

        let paramValue = JSON.stringify({
            Email: email,
            Password: password,
            Nickname: nickname,
            Role: role,
            TimeZoneIanaId: timeZoneIanaId
        });

        $.ajax({
            url: "/Management/CreateAccount",
            type: "POST",
            headers: { "RequestVerificationToken": fieldValue($__RequestVerificationToken) },
            dataType: "json",
            data: paramValue,
            contentType: "application/json; charset=utf-8",
            success: function(data: ActionReply) {
                if (data.result) {
                    $createAccountDialogModal.modal("hide");

                    const grid = new MvcGrid(document.querySelector(".mvc-grid"));
                    grid.reload();

                    toastr.success(data.message);
                } else {
                    toastr.error(data.error);
                }
            }
        });

        return false;
    }

    /**
     * Opens the edit modal for the selected account. Reads the selection from
     * the row CSS class, aborts with a toast if none is selected, fetches the
     * record (`IsAccountExists`) and fills the form. The e-mail is the identity
     * key; the boolean flags (locked / confirmed / agreed / deleted) map to
     * checkboxes.
     *
     * @param errorMessageSelectGridRow localized "pick a row first" text from a `data-*` attribute.
     */
    function EditAccountGridRow(errorMessageSelectGridRow?: string) {

        let selectedRowEmail = "";

        $(".clsGridRow").each(function() {
            if ($(this).attr("class")!.includes(selectedRowColor)) {
                selectedRowEmail = $(this).attr("data-email")!;
            }
        });

        if (selectedRowEmail === "") {
            toastr.error(errorMessageSelectGridRow!);
            return false;
        }

        $.ajax({
            url: "/Management/IsAccountExists" + "?email=" + encodeURIComponent(selectedRowEmail),
            type: "POST",
            headers: { "RequestVerificationToken": fieldValue($__RequestVerificationToken) },
            dataType: "json",
            contentType: "application/json; charset=utf-8",
            success: function(data: ReadReply<AccountPayload>) {
                if (data.result) {

                    $editAccountEmail.val(data.account.email);
                    $editAccountNickname.val(data.account.nickname);
                    // `.trigger("change")` so select-dependent UI (role / timezone) re-syncs.
                    $editAccountRole.val(data.account.role).trigger("change");
                    $editAccountTimeZone.val(data.account.timeZoneIanaId).trigger("change");
                    $editAccountLocked.prop("checked", data.account.locked);
                    $editAccountEmailConfirmed.prop("checked", data.account.emailConfirmed);
                    $editAccountAgreedServiceTerms.prop("checked", data.account.agreedServiceTerms);
                    $editMessage.val(data.account.message);
                    $editAccountDeleted.prop("checked", data.account.deleted);

                    // Static backdrop + no keyboard dismiss; `toggle` then `show`
                    // forces it open even mid-transition.
                    $editAccountDialogModal.modal({
                        keyboard: false,
                        backdrop: "static"
                    });

                    $editAccountDialogModal.modal("toggle");
                    $editAccountDialogModal.modal("show");
                } else {
                    toastr.error(data.error);
                }
            }
        });
    }

    /** Edit-account modal submit: validate, POST every field + flag as JSON, then close / reload / toast. */
    function UpdateAccount() {
        if (!$formEditAccount.valid()) {
            return false;
        }

        let email = $editAccountEmail.val();
        let password = $editAccountPassword.val();
        let role = $editAccountRole.val();
        let timeZoneIanaId = $editAccountTimeZone.val();
        let locked = $editAccountLocked.is(":checked");
        let emailConfirmed = $editAccountEmailConfirmed.is(":checked");
        let agreedServiceTerms = $editAccountAgreedServiceTerms.is(":checked");
        let message = $editMessage.val();
        let deleted = $editAccountDeleted.is(":checked");

        let paramValue = JSON.stringify({
            Email: email,
            Password: password,
            Role: role,
            TimeZoneIanaId: timeZoneIanaId,
            Locked: locked,
            EmailConfirmed: emailConfirmed,
            AgreedServiceTerms: agreedServiceTerms,
            Message: message,
            Deleted: deleted
        });

        $.ajax({
            url: "/Management/UpdateAccount",
            type: "POST",
            headers: { "RequestVerificationToken": fieldValue($__RequestVerificationToken) },
            dataType: "json",
            data: paramValue,
            contentType: "application/json; charset=utf-8",
            success: function(data: ActionReply) {
                if (data.result) {
                    $editAccountDialogModal.modal("hide");

                    const grid = new MvcGrid(document.querySelector(".mvc-grid"));
                    grid.reload();

                    toastr.success(data.message);
                } else {
                    toastr.error(data.error);
                }
            }
        });
        return false;
    }

    /** "Delete" button: require a selected row, then just open the confirmation modal. */
    function ConfirmDeleteAccount(errorMessageSelectGridRow?: string) {

        let selectedRowEmail = "";

        $(".clsGridRow").each(function() {
            if ($(this).attr("class")!.includes(selectedRowColor)) {
                selectedRowEmail = $(this).attr("data-email")!;
            }
        });

        if (selectedRowEmail === "") {
            toastr.error(errorMessageSelectGridRow!);
            return false;
        }

        $confirmDeleteAccountDialogModal.modal({
            keyboard: false,
            backdrop: "static"
        });

        $confirmDeleteAccountDialogModal.modal("toggle");
        $confirmDeleteAccountDialogModal.modal("show");
    }

    /** Confirmed delete: re-check selection, confirm the record exists, then POST `DeleteAccount`. */
    function DeleteAccount(errorMessageSelectGridRow?: string) {

        let selectedRowEmail = "";

        $(".clsGridRow").each(function() {
            if ($(this).attr("class")!.includes(selectedRowColor)) {
                selectedRowEmail = $(this).attr("data-email")!;
            }
        });

        if (selectedRowEmail === "") {
            toastr.error(errorMessageSelectGridRow!);
            return false;
        }

        $.ajax({
            url: "/Management/IsAccountExists" + "?email=" + encodeURIComponent(selectedRowEmail),
            type: "POST",
            headers: { "RequestVerificationToken": fieldValue($__RequestVerificationToken) },
            dataType: "json",
            contentType: "application/json; charset=utf-8",
            success: function(data: ReadReply<AccountPayload>) {
                if (data.result) {

                    let paramValue = JSON.stringify({
                        Email: data.account.email
                    });

                    $.ajax({
                        url: "/Management/DeleteAccount",
                        type: "POST",
                        headers: { "RequestVerificationToken": fieldValue($__RequestVerificationToken) },
                        dataType: "json",
                        data: paramValue,
                        contentType: "application/json; charset=utf-8",
                        success: function(data: ActionReply) {
                            if (data.result) {
                                $confirmDeleteAccountDialogModal.modal("hide");

                                const grid = new MvcGrid(document.querySelector(".mvc-grid"));
                                grid.reload();

                                toastr.success(data.message);
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
     * "Export to Excel": the response is a binary file `$.ajax` cannot save, so
     * submit a throwaway `<form>` as a real POST and let the browser download it.
     * The anti-forgery token rides along as a hidden input.
     */
    function ExportExcelAccount() {
        let form = document.createElement("form");
        let element1 = document.createElement("input");
        let element2 = document.createElement("input");

        form.method = "POST";
        form.action = "/Management/ExportExcelAccount";

        element1.name = "__RequestVerificationToken";
        element1.value = fieldValue($__RequestVerificationToken);
        form.appendChild(element1);

        element2.name = "fileName";
        element2.value = "Account";
        form.appendChild(element2);

        document.body.appendChild(form);

        form.submit();
    }

    // --- Button wiring ---------------------------------------------------
    $btnEditAccountGridRow.off("click").on("click", function() {
        EditAccountGridRow($(this).attr("data-errorMessageSelectGridRow"));
    });

    $btnConfirmDeleteAccount.off("click").on("click", function() {
        ConfirmDeleteAccount($(this).attr("data-errorMessageSelectGridRow"));
    });

    $btnExportExcelAccount.off("click").on("click", function() {
        ExportExcelAccount();
    });

    $btnDeleteAccount.off("click").on("click", function() {
        DeleteAccount($(this).attr("data-errorMessageSelectGridRow"));
    });

    $formCreateAccount.off("submit").on("submit", function() {
        return CreateAccount();
    });

    $formEditAccount.off("submit").on("submit", function() {
        return UpdateAccount();
    });
})();
