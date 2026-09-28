/**
 * Shared layout script for the **anonymous** (public / signed-out) area.
 *
 * Loaded once on every public page by `Views/Shared/_Layout.cshtml`. Same three
 * cross-cutting concerns as the admin / user layout scripts, plus the top-bar
 * "Login" link:
 *
 *   1. Session-expiry recovery — any unhandled AJAX error redirects to the
 *      anonymous dashboard.
 *   2. Culture switching — the language links in the top navigation bar.
 *   3. Double-submit / navigation guard — submit buttons are disabled while a
 *      POST is in flight and re-enabled when the last one finishes.
 *
 * Like every script under `TypeScripts/`, the whole file body is wrapped in an
 * IIFE and has no `import` / `export`. Every jQuery handler is namespaced
 * (`.<name>._Layout`) and bound with `.off(...).on(...)` so re-running the
 * script can never stack duplicates.
 */
(function() {
    // Global safety net: any unhandled AJAX failure sends the visitor to the
    // public dashboard rather than leaving them on a page whose data never loaded.
    $(document).off("ajaxError._Layout").on("ajaxError._Layout", function() {
        window.location.href = "/Dashboard/AnonymousIndex";
    });

    /**
     * Persists the UI-language choice on the server and reloads the current page
     * in it: POST the culture code, then navigate to the hidden `#returnUri` for
     * a full localized re-render.
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
            headers: { "RequestVerificationToken": $("input[name=\"__RequestVerificationToken\"]").val() as string },
            dataType: "json",
            data: paramValue,
            contentType: "application/json; charset=utf-8",
            success: function() {
                window.location.href = $("#returnUri").val() as string;
            }
        });
    }

    // Top-bar language links.
    $("#aChangeCultureEnUS").off("click").on("click", function() {
        ChangeCulture("en-US");
    });

    $("#aChangeCultureKoKR").off("click").on("click", function() {
        ChangeCulture("ko-KR");
    });

    // Top-bar "Login" link (public layout only).
    $("#btnMainTopBarLogin").off("click").on("click", function() {
        location.href = "/Account/Login";
    });

    // --- Double-submit / in-flight guard ----------------------------------
    // Number of AJAX POSTs currently running. While it is greater than zero every
    // submit control on the page is disabled, so a double click can't fire a
    // duplicate request. A counter (not a boolean) because POSTs can overlap.
    let inFlightPostCount = 0;

    // `ajaxSend` fires just before every request; guard POSTs only (GETs are
    // safe to repeat).
    $(document).off("ajaxSend._Layout").on("ajaxSend._Layout", function(_event, _xhr, settings: JQuery.AjaxSettings) {
        if ((settings.type || "GET").toUpperCase() !== "POST") return;
        inFlightPostCount++;
        $("button[type=\"submit\"], input[type=\"submit\"]").prop("disabled", true);
    });

    // `ajaxComplete` fires after each request (success or error); re-enable the
    // submit controls once nothing is in flight.
    $(document).off("ajaxComplete._Layout").on("ajaxComplete._Layout", function(_event, _xhr, settings: JQuery.AjaxSettings) {
        if ((settings.type || "GET").toUpperCase() !== "POST") return;
        inFlightPostCount = Math.max(0, inFlightPostCount - 1);
        if (inFlightPostCount === 0) {
            $("button[type=\"submit\"], input[type=\"submit\"]").prop("disabled", false);
        }
    });

    // Classic (non-AJAX) form posts: disable that form's submit buttons on submit.
    $(document).off("submit._Layout", "form").on("submit._Layout", "form", function() {
        $(this).find("button[type=\"submit\"], input[type=\"submit\"]").prop("disabled", true);
    });

    // Returning via the browser Back/Forward cache (bfcache) can restore the page
    // with buttons still disabled and the overlay showing — reset on `pageshow`.
    $(window).off("pageshow._Layout").on("pageshow._Layout", function() {
        inFlightPostCount = 0;
        $("button[type=\"submit\"], input[type=\"submit\"]").prop("disabled", false);
        $("#loading").hide();
    });
})();
