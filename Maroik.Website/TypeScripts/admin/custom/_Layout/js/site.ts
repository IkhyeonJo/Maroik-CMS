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
    window.MaroikDefaultMaxAttachedFileSizeBytes = 10485760;

    // Shared HTML-escaping helper for pages that interpolate user-controlled text (e.g. a
    // calendar name) into a raw HTML template literal. Declared once here — loaded before every
    // admin page's own script — instead of duplicated per page (see admin/Calendar/AdminIndex).
    window.escapeHtml = function(value: string): string {
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

    // --- Runtime checks (window.check, onReply, byId, fieldValue, …) -------------
    // The page scripts read the values the view renders and the replies the controllers send through
    // these helpers instead of type assertions, so every "this is not what the code expects" path lives
    // here, once: a missing element or attribute throws (the view and the script disagree, which is a
    // bug), and a reply that does not pass its check shows the generic "temporary error" toast and logs
    // why. Typed in global.d.ts; this script loads before every page's own script.

    /** How a value reads in a problem message. */
    function describeValue(value: unknown): string {
        if (value === null) return "null";
        return Array.isArray(value) ? "an array" : typeof value;
    }

    /** A {@link Check} from its problem finder: a value "is" a T when there is no problem. */
    function checkFrom<T>(problem: (value: unknown, path: string) => string | null): Check<T> {
        return {
            problem,
            is: (value: unknown): value is T => problem(value, "value") === null,
        };
    }

    /** The fields of a plain object; null for anything else, arrays included. */
    function fieldsOf(value: unknown): Map<string, unknown> | null {
        if (typeof value !== "object" || value === null || Array.isArray(value)) return null;
        return new Map(Object.entries(value));
    }

    /** A check that `typeof value` is `type`. */
    function typeCheck<T>(type: "string" | "number" | "boolean"): Check<T> {
        return checkFrom<T>((value, path) => typeof value === type ? null : `${path}: expected a ${type}, got ${describeValue(value)}`);
    }

    const check: CheckBuilders = {
        string: typeCheck<string>("string"),
        number: typeCheck<number>("number"),
        boolean: typeCheck<boolean>("boolean"),
        literal<const V extends string | number | boolean>(expected: V): Check<V> {
            return checkFrom<V>((value, path) => value === expected ? null : `${path}: expected ${JSON.stringify(expected)}, got ${describeValue(value)}`);
        },
        nullable<T>(inner: Check<T>): Check<T | null> {
            return checkFrom<T | null>((value, path) => value === null ? null : inner.problem(value, path));
        },
        array<T>(item: Check<T>): Check<T[]> {
            return checkFrom<T[]>((value, path) => {
                if (!Array.isArray(value)) return `${path}: expected an array, got ${describeValue(value)}`;
                for (let index = 0; index < value.length; index++) {
                    const problem = item.problem(value[index], `${path}[${index}]`);
                    if (problem !== null) return problem;
                }
                return null;
            });
        },
        record<T>(entry: Check<T>): Check<Record<string, T>> {
            return checkFrom<Record<string, T>>((value, path) => {
                const fields = fieldsOf(value);
                if (fields === null) return `${path}: expected an object, got ${describeValue(value)}`;
                for (const [name, field] of fields) {
                    const problem = entry.problem(field, `${path}.${name}`);
                    if (problem !== null) return problem;
                }
                return null;
            });
        },
        object<S extends Record<string, Check<unknown>>>(shape: S): Check<CheckedFields<S>> {
            return checkFrom<CheckedFields<S>>((value, path) => {
                const fields = fieldsOf(value);
                if (fields === null) return `${path}: expected an object, got ${describeValue(value)}`;
                for (const name in shape) {
                    const problem = shape[name].problem(fields.get(name), `${path}.${name}`);
                    if (problem !== null) return problem;
                }
                return null;
            });
        },
        oneOf<A, B>(first: Check<A>, second: Check<B>): Check<A | B> {
            return checkFrom<A | B>((value, path) => {
                const firstProblem = first.problem(value, path);
                if (firstProblem === null) return null;
                const secondProblem = second.problem(value, path);
                return secondProblem === null ? null : `${firstProblem} / ${secondProblem}`;
            });
        },
        instance<T>(type: Constructor<T>): Check<T> {
            return checkFrom<T>((value, path) => value instanceof type ? null : `${path}: expected a ${type.name}, got ${describeValue(value)}`);
        },
        jsonText<T>(content: Check<T>): Check<string> {
            return checkFrom<string>((value, path) => {
                if (typeof value !== "string") return `${path}: expected a string, got ${describeValue(value)}`;
                const parsed = parseOrUndefined(value);
                return parsed.ok ? content.problem(parsed.value, `${path}(JSON)`) : `${path}: expected JSON text`;
            });
        },
    };
    window.check = check;

    /** `JSON.parse` that reports unparseable text instead of throwing. */
    function parseOrUndefined(text: string): { ok: true; value: unknown } | { ok: false } {
        try {
            return { ok: true, value: JSON.parse(text) };
        } catch {
            // Not JSON: the caller reports it as a problem of its own.
            return { ok: false };
        }
    }

    const failedReply = check.object({ result: check.literal(false), error: check.string });
    window.replies = {
        failed: failedReply,
        action: check.oneOf(check.object({ result: check.literal(true), message: check.string }), failedReply),
        read<S extends Record<string, Check<unknown>>>(payload: S) {
            return check.oneOf(check.object({ ...payload, result: check.literal(true) }), failedReply);
        },
        write<S extends Record<string, Check<unknown>>>(payload: S) {
            return check.oneOf(check.object({ ...payload, result: check.literal(true), message: check.string }), failedReply);
        },
    };

    /** Logs why a reply was not what the page expected and shows the generic error. */
    function reportUnexpectedReply(problem: string): void {
        console.error(`Unexpected reply: ${problem}`);
        toastr.error(window.fieldValue($("#_LocalizerTemporaryError")));
    }

    window.onReply = function<T>(replyCheck: Check<T>, handler: (reply: T) => void) {
        return function(reply: unknown) {
            if (replyCheck.is(reply)) {
                handler(reply);
                return;
            }
            reportUnexpectedReply(replyCheck.problem(reply, "reply") ?? "");
        };
    };

    window.onReplyText = function<T>(replyCheck: Check<T>, handler: (reply: T) => void) {
        const onParsed = window.onReply(replyCheck, handler);
        return function(text: string) {
            const parsed = parseOrUndefined(text);
            if (parsed.ok) {
                onParsed(parsed.value);
                return;
            }
            reportUnexpectedReply("reply: expected JSON text");
        };
    };

    window.conform = function<T>(value: unknown, valueCheck: Check<T>, what: string): T {
        if (valueCheck.is(value)) return value;
        throw new Error(valueCheck.problem(value, what) ?? what);
    };

    window.parseJson = function<T>(text: string, valueCheck: Check<T>, what: string): T {
        const parsed = parseOrUndefined(text);
        if (!parsed.ok) throw new Error(`${what} is not JSON`);
        return window.conform(parsed.value, valueCheck, what);
    };

    window.required = function<T>(value: T | null | undefined, what: string): T {
        if (value === null || value === undefined) throw new Error(`${what} is missing`);
        return value;
    };

    window.instanceOf = function<T>(value: unknown, type: Constructor<T>, what: string): T {
        if (value instanceof type) return value;
        throw new Error(`${what} is not a ${type.name}`);
    };

    /** The first element of a jQuery set; throws when the set is empty. */
    function firstElement($element: JQuery): HTMLElement {
        const element: HTMLElement | undefined = $element.get(0);
        if (element === undefined) throw new Error("A required element is missing from the page");
        return element;
    }

    window.elementOf = function<T extends Element>($element: JQuery, type: Constructor<T>): T {
        const element = firstElement($element);
        return window.instanceOf(element, type, `#${element.id}`);
    };

    window.byId = function<T extends Element>(id: string, type: Constructor<T>): T {
        const element = document.getElementById(id);
        if (element === null) throw new Error(`#${id} is missing from the page`);
        return window.instanceOf(element, type, `#${id}`);
    };

    window.fieldValue = function($field: JQuery): string {
        const element = firstElement($field);
        const value = $field.val();
        if (typeof value !== "string") throw new Error(`#${element.id} does not hold a single text value`);
        return value;
    };

    window.selectValue = function($select: JQuery): string | null {
        const element = firstElement($select);
        const value = $select.val();
        if (value === null) return null;
        if (typeof value !== "string") throw new Error(`#${element.id} does not hold a single text value`);
        return value;
    };

    window.optionalFieldValue = function($field: JQuery): string | undefined {
        return $field.length === 0 ? undefined : window.fieldValue($field);
    };

    window.attribute = function($element: JQuery, name: string): string {
        const element = firstElement($element);
        const value = $element.attr(name);
        if (value === undefined) throw new Error(`#${element.id} has no ${name} attribute`);
        return value;
    };

    window.setMinDate = function(picker: DatepickerMinDateSetter, date: Date | null): void {
        picker.datepicker("option", "minDate", date);
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
            // into every form.
            headers: { "RequestVerificationToken": window.fieldValue($("input[name=\"__RequestVerificationToken\"]")) },
            dataType: "json",
            data: paramValue,
            contentType: "application/json; charset=utf-8",
            success: function() {
                // Full navigation (not an in-place update) so the server
                // re-renders every localized string on the page.
                window.location.href = window.fieldValue($("#returnUri"));
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
    $(document).off("ajaxSend._Layout").on("ajaxSend._Layout", function(_event, _xhr: JQuery.jqXHR, settings: JQuery.AjaxSettings) {
        if ((settings.type || "GET").toUpperCase() !== "POST") return;
        inFlightPostCount++;
        $("button[type=\"submit\"], input[type=\"submit\"]").prop("disabled", true);
    });

    // `ajaxComplete` fires after each request, whether it succeeded or failed.
    // Decrement the counter (clamped at 0 for safety); once nothing is in flight,
    // re-enable the submit controls.
    $(document).off("ajaxComplete._Layout").on("ajaxComplete._Layout", function(_event, _xhr: JQuery.jqXHR, settings: JQuery.AjaxSettings) {
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
