/**
 * Script for the user **Profile** page (`Views/Management/Profile.cshtml`).
 *
 * Two independent forms on one page:
 *
 *   1. Change password — `#formUpdateProfilePassword`. Validated client-side,
 *      then POSTed as JSON; on success an `alert` is shown and, once it is confirmed, the user
 *      is sent to the sign-in page (every session of the account is signed out).
 *      Failures are reported with a toastr message and the user stays on the page.
 *   2. Change avatar — `#formUpdateProfileAvatar`. Fires automatically when a
 *      file is picked (no submit button). The chosen file is checked against the
 *      size limit and the allowed image types *before* upload, purely for fast
 *      feedback — `ImageUploadPolicy` on the server re-validates every upload.
 *      On completion the page is reloaded so the new avatar shows everywhere.
 *
 * Wrapped in an IIFE, no `import` / `export`; handlers use `.off(...).on(...)`.
 */
(function() {
    // The runtime-check helpers the _Layout script defines (see TypeScripts/global.d.ts).
    const { check, parseJson, fieldValue, optionalFieldValue } = window;
    // Authoritative in ServerSetting.MaxAttachedFileSizeBytes (server); mirrored here for form UX
    // only. Falls back to the shared per-role default (_Layout/site.ts) if the hidden field is
    // missing or unparseable.
    const maxFileSize = parseInt(optionalFieldValue($("#maxAttachedFileSizeBytes")) ?? "") || window.MaroikDefaultMaxAttachedFileSizeBytes;
    // Accepted avatar image MIME types, published by Profile.cshtml (ImageUploadPolicy). The
    // server re-validates by extension; this is the client mirror.
    const allowedImageContentTypes = parseJson(optionalFieldValue($("#allowedImageContentTypes")) || "[]", check.array(check.string), "#allowedImageContentTypes").map(function(t: string) {
        return t.toLowerCase();
    });
    // Cached form / field references (jQuery objects) and the anti-forgery input.
    const $formUpdateProfileAvatar = $("#formUpdateProfileAvatar");
    const $formUpdateProfilePassword = $("#formUpdateProfilePassword");
    const $password = $("#Password");
    const $newPassword = $("#NewPassword");
    const $__RequestVerificationToken = $("input[name=\"__RequestVerificationToken\"]");
    const $profileAvatarFiles = $("#ProfileAvatarFiles");

    /**
     * Validates the freshly chosen avatar file and, if it passes, uploads the
     * whole `#formUpdateProfileAvatar` as multipart form data. Every exit path
     * navigates back to `/Management/Profile` (either to show the new avatar or
     * to surface an error via `alert`).
     *
     * All three parameters are localized error strings passed in from the
     * `data-*` attributes of the file input; they are optional only because
     * `.attr()` returns `string | undefined`.
     *
     * @param noFileAttachedErrorMessage    shown when nothing was selected.
     * @param fileSizeLimitationErrorMessage shown when the file is empty or over `maxFileSize`.
     * @param fileTypeErrorMessage          shown when the MIME type is not in `allowedImageContentTypes`.
     */
    function UpdateProfileAvatar(noFileAttachedErrorMessage?: string, fileSizeLimitationErrorMessage?: string, fileTypeErrorMessage?: string) {
        // A file input always has `.files`; `[0]` is undefined when nothing was chosen,
        // which the check right below handles.
        let files: File | undefined = (document.getElementById("ProfileAvatarFiles") as HTMLInputElement).files![0];
        if (files === undefined || files === null) {
            alert(noFileAttachedErrorMessage);
            window.location.href = "/Management/Profile";
        } else {
            if (files.size > maxFileSize) {
                alert(fileSizeLimitationErrorMessage);
                window.location.href = "/Management/Profile";
            } else if (files.size > 0 && files.size <= maxFileSize) {
                // Size is fine — now check the MIME type against the allow-list.
                if (allowedImageContentTypes.indexOf((files.type || "").toLowerCase()) !== -1) {
                    // Send the entire form (file + anti-forgery token) as multipart.
                    // processData/contentType `false` let jQuery pass the FormData
                    // through untouched with the correct multipart boundary.
                    let form = $formUpdateProfileAvatar[0] as HTMLFormElement;
                    let formData = new FormData(form);
                    $.ajax({
                        url: "/Management/UpdateProfileAvatar",
                        data: formData,
                        type: "POST",
                        enctype: "multipart/form-data",
                        processData: false,
                        contentType: false,
                        dataType: "json",
                        cache: false,
                        success: function(data: AvatarReply) {
                            if (data.result) {
                                window.location.href = "/Management/Profile";
                            } else {
                                alert(data.errorMessage);
                                window.location.href = "/Management/Profile";
                            }
                        }
                    });

                } else {
                    alert(fileTypeErrorMessage);
                    window.location.href = "/Management/Profile";
                }
            } else {
                // files.size === 0 (or negative) — treat an empty file as a size error.
                alert(fileSizeLimitationErrorMessage);
                window.location.href = "/Management/Profile";
            }
        }
    }

    /**
     * Handles the change-password form submit: bail if client validation fails,
     * otherwise POST `{ Password, NewPassword }` as JSON and report the outcome
     * with a toastr message. Always returns `false` so the form never performs
     * its own navigation — the page stays put.
     */
    function UpdateProfilePassword() {

        if (!$formUpdateProfilePassword.valid()) {
            return false;
        }

        let password = $password.val();
        let newPassword = $newPassword.val();

        let paramValue = JSON.stringify({
            Password: password,
            NewPassword: newPassword
        });

        $.ajax({
            url: "/Management/UpdateProfilePassword",
            type: "POST",
            headers: { "RequestVerificationToken": fieldValue($__RequestVerificationToken) },
            dataType: "json",
            data: paramValue,
            contentType: "application/json; charset=utf-8",
            success: function(data: ActionReply) {
                if (data.result) {
                    // Changing the password signs every session of the account out — this one
                    // included (the server already dropped it). `alert` blocks until the user
                    // confirms, so the message is always read before going to the sign-in page.
                    alert(data.message);
                    window.location.href = "/Account/Login";
                } else {
                    // Server-side rule failure (e.g. wrong current password,
                    // new password rejected by PasswordPolicy).
                    toastr.error(data.error);
                }
            }
        });
        return false;
    }

    // Change-password form: run the handler on submit; its `false` return blocks
    // the native navigation.
    $formUpdateProfilePassword.off("submit").on("submit", function() {
        return UpdateProfilePassword();
    });

    // Avatar upload is triggered by picking a file, not by a submit button. The
    // localized error strings are read from the input's `data-*` attributes.
    $profileAvatarFiles.off("change").on("change", function() {
        return UpdateProfileAvatar($(this).attr("data-noFileAttachedErrorMessage"), $(this).attr("data-fileSizeLimitationErrorMessage"), $(this).attr("data-fileTypeErrorMessage"));
    });
})();
