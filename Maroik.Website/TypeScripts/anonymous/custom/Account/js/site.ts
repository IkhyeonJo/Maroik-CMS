/**
 * Script for the anonymous (signed-out) account pages — login, register, resend
 * confirmation, forgot-password and reset-password — all rendered by
 * `Views/Account/*` under the slim `_Layout` used before authentication.
 *
 * It does two things:
 *
 *   1. Shows the full-screen `#loading` overlay when a form is submitted, but
 *      only if jquery-validation says the form is valid (otherwise the user
 *      stays on the page to fix errors, so the overlay must not appear).
 *   2. Culture switching, identical to the main layout script.
 *
 * The forms themselves post normally (no AJAX) except where noted; these
 * handlers only manage the overlay. Wrapped in an IIFE with no `import` /
 * `export` so it stays a plain global `<script>`; handlers use
 * `.off(...).on(...)` so re-running the file cannot double-bind.
 */
(function() {
    /**
     * The text of a field the account layout always renders. The account pages do not load the
     * `_Layout` script, so this is their own copy of its `window.fieldValue`: a missing field is a
     * mismatch between the view and this script, and throws instead of sending `undefined` on.
     */
    function fieldValue($field: JQuery): string {
        const value = $field.val();
        if (typeof value !== "string") throw new Error("A required field is missing from the page");
        return value;
    }
    // One helper per form. Each returns a boolean purely as documentation of
    // "did we let it through" — the click handlers below ignore the value and
    // the browser performs the real submit. If the form is invalid, hide the
    // overlay and bail; if valid, show it for the duration of the post.

    /** Register form: show the loading overlay only when the form validates. */
    function ShowRegisterLoading() {
        if (!$("#registerForm").valid()) {
            $("#loading").hide();
            return false;
        } else {
            $("#loading").show();
            return true;
        }
    }

    /** Forgot-password form: show the loading overlay only when it validates. */
    function ShowForgotPasswordLoading() {
        if (!$("#forgotPasswordForm").valid()) {
            $("#loading").hide();
            return false;
        } else {
            $("#loading").show();
            return true;
        }
    }

    /** Reset-password form: show the loading overlay only when it validates. */
    function ShowResetPasswordLoading() {
        if (!$("#resetPasswordForm").valid()) {
            $("#loading").hide();
            return false;
        } else {
            $("#loading").show();
            return true;
        }
    }

    /** Login form: show the loading overlay only when it validates. */
    function ShowLoginLoading() {
        if (!$("#loginForm").valid()) {
            $("#loading").hide();
            return false;
        } else {
            $("#loading").show();
            return true;
        }
    }

    /**
     * Persists the UI-language choice on the server and reloads the current page
     * in it. Same contract as the main layout script: POST the culture code,
     * then navigate to the hidden `#returnUri` for a full localized re-render.
     *
     * @param culture BCP-47 tag the server understands: `'en-US'` or `'ko-KR'`.
     */
    function ChangeCulture(culture: string): void {

        let paramValue = JSON.stringify({
            Culture: culture
        });

        $.ajax({
            url: "/Dashboard/CultureManagement",
            type: "POST",
            // Anti-forgery token from MVC's hidden input; `.val()` is a union, so
            // narrow it to `string` for the request header.
            headers: { "RequestVerificationToken": fieldValue($("input[name=\"__RequestVerificationToken\"]")) },
            dataType: "json",
            data: paramValue,
            contentType: "application/json; charset=utf-8",
            success: function() {
                window.location.href = fieldValue($("#returnUri"));
            }
        });
    }

    // --- Button wiring -----------------------------------------------------
    // Each submit/action button flips the overlay through its helper just before
    // the form posts. `.off('click')` guards against a duplicate binding.

    $("#btnRequestNewPassword").off("click").on("click", function() {
        ShowForgotPasswordLoading();
    });

    $("#aChangeCultureEnUS").off("click").on("click", function() {
        ChangeCulture("en-US");
    });

    $("#aChangeCultureKoKR").off("click").on("click", function() {
        ChangeCulture("ko-KR");
    });

    $("#btnSignIn").off("click").on("click", function() {
        ShowLoginLoading();
    });

    $("#btnRegister").off("click").on("click", function() {
        ShowRegisterLoading();
    });

    // "Resend confirmation e-mail" reuses the register form, hence the same helper.
    $("#btnResend").off("click").on("click", function() {
        ShowRegisterLoading();
    });

    $("#btnChangePassword").off("click").on("click", function() {
        ShowResetPasswordLoading();
    });
})();
