/**
 * Shared layout script for the **admin** area.
 *
 * Loaded once on every admin page by `Views/Shared/_Layout.cshtml`. It provides
 * three cross-cutting concerns that every page needs and none should re-implement:
 *
 *   1. Session-expiry recovery — any unhandled AJAX error redirects to the
 *      anonymous dashboard (the request most likely failed because the session
 *      expired and the server redirected it to an HTML page instead of answering JSON).
 *   2. Culture switching — the language links in the top navigation bar.
 *   3. Double-submit / navigation guard — submit buttons are disabled while a
 *      POST is in flight and re-enabled when the last one finishes, so a
 *      double click cannot post a form twice.
 *
 * Like every script under `TypeScripts/`, the whole file body is wrapped in an
 * IIFE and has no `import` / `export`, so it stays a plain global `<script>`.
 * The IIFE keeps its top-level names (`ChangeCulture`, `inFlightPostCount`, …)
 * from colliding with the other per-page scripts compiled in the same `tsc`
 * program. Every jQuery handler is namespaced (`.<name>._Layout`) and bound with
 * `.off(...).on(...)` so re-running the script can never stack duplicates.
 */
(function() {
    // Default fallback for the client-side file-size UX check on every admin upload form, used only
    // when a page's `#maxAttachedFileSizeBytes` hidden field (authoritative: ServerSetting.MaxAttachedFileSizeBytes,
    // rendered per-page from ViewBag by ViewBagPopulatorFilter) is missing or unparseable. Declared
    // once here — loaded before every admin page's own script — instead of duplicated as a literal
    // at each upload form; must match ServerSetting.MaxAttachedFileSizeBytes's own default (10 MB).
    (window as any).MaroikDefaultMaxAttachedFileSizeBytes = 10485760;

    // Shared HTML-escaping helper for pages that interpolate user-controlled text (e.g. a
    // calendar name) into a raw HTML template literal. Declared once here — loaded before every
    // admin page's own script — instead of duplicated per page (see admin/Calendar/AdminIndex).
    (window as any).escapeHtml = function(value: string): string {
        return value.replace(/[&<>"']/g, function(ch: string) {
            switch (ch) {
                case "&":
                    return "&amp;";
                case "<":
                    return "&lt;";
                case ">":
                    return "&gt;";
                case "\"":
                    return "&quot;";
                default:
                    return "&#39;";
            }
        });
    };

    // Global safety net: if any $.ajax call fails and nothing else handles it
    // (typically the session expired, so AuthorizationFilter redirected the call to
    // an HTML page that does not parse as the expected JSON),
    // move the user to the public dashboard instead of leaving them on a page
    // whose data never loaded.
    $(document).off("ajaxError._Layout").on("ajaxError._Layout", function() {
        window.location.href = "/Dashboard/AnonymousIndex";
    });

    /**
     * Persists the user's UI-language choice on the server and reloads the page
     * in that language.
     *
     * Posts the culture code to `CultureManagement`, which stores it in the
     * request-culture cookie so every later request renders in that language. On success
     * the browser is sent to the URL held in the hidden `#returnUri` field —
     * the page the user was on before opening the culture menu — forcing a full
     * server re-render of all localized text.
     *
     * @param culture BCP-47 language tag the server understands: `'en-US'` or `'ko-KR'`.
     */
    function ChangeCulture(culture: string): void {

        let paramValue = JSON.stringify({
            Culture: culture
        });

        $.ajax({
            url: "/Dashboard/CultureManagement",
            type: "POST",
            // ASP.NET anti-forgery token, read from the hidden input MVC renders
            // into every form. `.val()` is typed `string | number | string[] |
            // undefined`, so it is narrowed to `string` for the header value.
            headers: { "RequestVerificationToken": $("input[name=\"__RequestVerificationToken\"]").val() as string },
            dataType: "json",
            data: paramValue,
            contentType: "application/json; charset=utf-8",
            success: function() {
                // Full navigation (not an in-place update) so the server
                // re-renders every localized string on the page.
                window.location.href = $("#returnUri").val() as string;
            }
        });
    }

    // Top-bar language links. `.off('click')` first so a second execution of the
    // script never binds a duplicate handler to the same anchor.
    $("#aChangeCultureEnUS").off("click").on("click", function() {
        ChangeCulture("en-US");
    });

    $("#aChangeCultureKoKR").off("click").on("click", function() {
        ChangeCulture("ko-KR");
    });

    // --- Double-submit / in-flight guard ------------------------------------
    // Number of AJAX POSTs currently running. While it is greater than zero every
    // submit control on the page is disabled, so an impatient double click (or a
    // second form submission) cannot fire a duplicate request. A counter rather
    // than a boolean is used because several POSTs can overlap.
    let inFlightPostCount = 0;

    // jQuery raises `ajaxSend` just before every request leaves the browser.
    // GETs are safe to repeat, so only POSTs are guarded: bump the counter and
    // disable the submit controls.
    $(document).off("ajaxSend._Layout").on("ajaxSend._Layout", function(_event, _xhr, settings: JQuery.AjaxSettings) {
        if ((settings.type || "GET").toUpperCase() !== "POST") return;
        inFlightPostCount++;
        $("button[type=\"submit\"], input[type=\"submit\"]").prop("disabled", true);
    });

    // `ajaxComplete` fires after each request, whether it succeeded or failed.
    // Decrement the counter (clamped at 0 for safety); once nothing is in flight,
    // re-enable the submit controls.
    $(document).off("ajaxComplete._Layout").on("ajaxComplete._Layout", function(_event, _xhr, settings: JQuery.AjaxSettings) {
        if ((settings.type || "GET").toUpperCase() !== "POST") return;
        inFlightPostCount = Math.max(0, inFlightPostCount - 1);
        if (inFlightPostCount === 0) {
            $("button[type=\"submit\"], input[type=\"submit\"]").prop("disabled", false);
        }
    });

    // Classic (non-AJAX) form posts never go through the ajax events above, so
    // guard them directly: the instant a form submits, disable that form's own
    // submit buttons. Delegated from `document` so forms added later are covered.
    $(document).off("submit._Layout", "form").on("submit._Layout", "form", function() {
        $(this).find("button[type=\"submit\"], input[type=\"submit\"]").prop("disabled", true);
    });

    // Returning through the browser Back/Forward cache (bfcache) restores the
    // page exactly as it was left — possibly with buttons still disabled from the
    // navigation that took the user away, and the loading overlay still showing.
    // Reset all of that on `pageshow`.
    $(window).off("pageshow._Layout").on("pageshow._Layout", function() {
        inFlightPostCount = 0;
        $("button[type=\"submit\"], input[type=\"submit\"]").prop("disabled", false);
        $("#loading").hide();
    });
})();
