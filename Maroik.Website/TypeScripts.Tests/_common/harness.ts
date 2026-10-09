/*
 * Test harness for the compiled custom client scripts.
 *
 * Each `*.test.ts` calls `loadSite(area, feature, page, fixtureHtml, opts)`, which:
 *   1. resets the jsdom document body to `fixtureHtml`,
 *   2. puts a real jQuery 3.6.0 on `window` (matching the runtime) plus no-op stubs
 *      for every vendored plugin the scripts touch (tabs / datepicker / summernote /
 *      modal / tooltip / validation), and stubs for the `toastr`, `MvcGrid`,
 *      `FullCalendar`, `moment` globals,
 *   3. captures `$.ajax` calls (and lets the test drive their `success` callbacks, or replay
 *      a call through jQuery's real ajax pipeline against a canned HTTP reply),
 *   4. runs the compiled `wwwroot/{area}/custom/{feature}/{page}/js/site.js` in the
 *      window scope (the file is the IIFE-wrapped port, so it self-executes).
 *
 * Tests then dispatch DOM events / call globals and assert on the DOM, the captured
 * ajax calls, toastr, submitted forms, etc.
 */
import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { vi, type Mock } from "vitest";
import jqueryImport from "jquery";

/** The website's wwwroot, where the compiled scripts live. */
const WWWROOT = resolve(__dirname, "../../wwwroot");

/** What Views/Shared/_Layout.cshtml renders on every page that the scripts read. */
const LAYOUT_CHROME = `<input id="_LocalizerTemporaryError" type="hidden" value="L_TemporaryError" />`;

/** The options object a script passed to `$.ajax`. */
export interface AjaxCall {
    url: string;
    type?: string;
    method?: string;
    data: unknown;
    headers?: Record<string, unknown>;
    success?: (data: unknown) => void;
    error?: (...a: unknown[]) => void;
    complete?: (...a: unknown[]) => void;
    async?: boolean;
    xhrFields?: Record<string, unknown>;
    /** the settings object itself, as jQuery takes it (respondOverHttp replays it through the real `$.ajax`) */
    settings: JQuery.AjaxSettings;
}

/**
 * The jsdom window a page runs in: the globals the harness installs (`$`, `toastr`, `MvcGrid`, …) are
 * `unknown` to a test, except `escapeHtml`, which a test may call.
 */
export type PageWindow = Window & typeof globalThis;

/** A callback a page handed to a widget; a test calls it with whatever the widget would pass. */
type WidgetCallback = (this: unknown, ...args: unknown[]) => unknown;

/** The FullCalendar options callbacks a test drives directly (see {@link SiteHandle.calendarOptions}). */
export interface CapturedCalendarOptions {
    select?: (arg: { start: Date; end: Date }) => void;
    eventClick?: (arg: object) => unknown;
}

/** An event object a page passed to `calendar.addEvent(...)`. */
export type AddedCalendarEvent = Record<string, unknown> & { extendedProps: Record<string, unknown> };

/** The FullCalendar stand-in a page constructs: every method the pages call is a spy. */
export interface FakeCalendar {
    render: Mock;
    addEvent: Mock<(event: AddedCalendarEvent) => void>;
    removeAllEvents: Mock;
    unselect: Mock;
    /** Not stubbed by default: a test installs it when the flow under test reads the calendar's events. */
    getEvents?: () => object[];
}

/** The MvcGrid stand-in a page constructs. */
export interface FakeMvcGrid {
    url: URL;
    reload: Mock;
}

/** The datepicker callbacks a test calls directly (a picker sets only some of them). */
export interface CapturedDatepickerOptions {
    beforeShow?: WidgetCallback;
    onChangeMonthYear?: WidgetCallback;
    onSelect?: WidgetCallback;
}

/** The Chart.js 2 config the Dashboard passes to `new Chart(ctx, cfg)` — the parts a test reads. */
export interface CapturedChartConfig {
    type: string;
    data: { labels: string[]; datasets: { data: number[]; backgroundColor: string[] }[] };
    options: { tooltips: { callbacks: { label(item: { index: number }, data: CapturedChartConfig["data"]): string } } };
}

/** The summernote options a test drives: the image-upload callback. */
export interface CapturedSummernoteOptions {
    callbacks?: { onImageUpload?: (files: File[]) => void };
}

/** The fields of a plain object; null for anything else. */
function fieldsOf(value: unknown): Map<string, unknown> | null {
    return typeof value === "object" && value !== null ? new Map(Object.entries(value)) : null;
}

/** Whether `value` is absent or a function (a widget callback a test may call). */
function isOptionalCallback(value: unknown): value is WidgetCallback | undefined {
    return value === undefined || typeof value === "function";
}

/** Whether `value` carries the datepicker callbacks a test calls, each absent or a function. */
function isDatepickerOptions(value: unknown): value is CapturedDatepickerOptions {
    const fields = fieldsOf(value);
    return fields !== null && ["beforeShow", "onChangeMonthYear", "onSelect"].every((name) => isOptionalCallback(fields.get(name)));
}

/** Whether `value` is summernote options whose `callbacks.onImageUpload`, if any, is a function. */
function isSummernoteOptions(value: unknown): value is CapturedSummernoteOptions {
    const fields = fieldsOf(value);
    if (fields === null) return false;
    const callbacks = fields.get("callbacks");
    if (callbacks === undefined) return true;
    const callbackFields = fieldsOf(callbacks);
    return callbackFields !== null && isOptionalCallback(callbackFields.get("onImageUpload"));
}

/** Whether `value` is FullCalendar options whose `select` / `eventClick`, if any, are functions. */
function isCalendarOptions(value: unknown): value is CapturedCalendarOptions {
    const fields = fieldsOf(value);
    return fields !== null && isOptionalCallback(fields.get("select")) && isOptionalCallback(fields.get("eventClick"));
}

/** Whether `value` is a doughnut config with the data, colours and tooltip callback the Dashboard tests read. */
function isChartConfig(value: unknown): value is CapturedChartConfig {
    const fields = fieldsOf(value);
    const data = fieldsOf(fields?.get("data"));
    const datasets = data?.get("datasets");
    const tooltips = fieldsOf(fieldsOf(fields?.get("options"))?.get("tooltips"));
    const label = fieldsOf(tooltips?.get("callbacks"))?.get("label");
    return typeof fields?.get("type") === "string" && Array.isArray(data?.get("labels")) && Array.isArray(datasets)
        && datasets.every((dataset) => Array.isArray(fieldsOf(dataset)?.get("data"))) && typeof label === "function";
}

/** `value`, which must pass `guard` (the options a script handed to a stubbed widget). */
function captured<T>(value: unknown, guard: (value: unknown) => value is T, what: string): T {
    if (guard(value)) return value;
    throw new Error(`${what} are not of the expected shape`);
}

/** The FullCalendar instance the page constructed. */
export function firstCalendar(h: SiteHandle): FakeCalendar {
    return present(h.calendarInstances[0], "the FullCalendar instance");
}

/** Calls the `select` callback a page gave FullCalendar (a drag-selected date range). */
export function calendarSelect(h: SiteHandle, arg: { start: Date; end: Date }): void {
    present(present(h.calendarOptions[0], "the FullCalendar options").select, "the select callback")(arg);
}

/** Calls the `eventClick` callback a page gave FullCalendar, returning what it returns. */
export function calendarEventClick(h: SiteHandle, arg: object): unknown {
    return present(present(h.calendarOptions[0], "the FullCalendar options").eventClick, "the eventClick callback")(arg);
}

/** What {@link loadSite} returns: the page's window and every stub/capture a test asserts on. */
export interface SiteHandle {
    /** the jQuery bound to the page window */
    $: JQueryStatic;
    /** the jsdom window the script ran in */
    win: PageWindow;
    /** every `{ ctx, cfg }` passed to `new Chart(...)` (Dashboard) */
    charts: { ctx: unknown; cfg: CapturedChartConfig }[];
    /** every options object passed to `new FullCalendar.Calendar(el, opts)` — lets a test call a page's `select` / `eventClick` callback (see the comment on `calendarOptions` in loadSite). */
    calendarOptions: CapturedCalendarOptions[];
    /** every `$.ajax` call, in order */
    ajaxCalls: AjaxCall[];

    /** invoke the `success` of the captured ajax call `indexFromEnd` places before the last (0 = the last) with `data` */
    respond(indexFromEnd: number, data: unknown): void;

    /**
     * Replays the captured ajax call `indexFromEnd` places before the last through jQuery's REAL
     * `$.ajax` (response-type inference and converters included) against a fake XHR answering
     * HTTP 200 with `body` as `contentType`. Unlike {@link respond}, this shows which callback
     * (`success` / `error`) a real browser would reach for that reply.
     */
    respondOverHttp(indexFromEnd: number, body: string, contentType: string): void;

    /** last captured ajax call */
    lastAjax(): AjaxCall;

    /** the `toastr` spies */
    toastr: { success: Mock; error: Mock; info: Mock; warning: Mock };
    /** `<form>`s that site.js created + submitted via ExportExcel* */
    submittedForms: HTMLFormElement[];
    /** values passed to `window.location.href = …` */
    navigations: string[];
    /** every `new MvcGrid(...)` stub the script constructed */
    MvcGridInstances: FakeMvcGrid[];
    /** the `$.datepicker` statics (`setDefaults` / `_clearDate`) as spies */
    datepickerStatics: { setDefaults: Mock<(options: Record<string, unknown>) => void>; _clearDate: Mock };
    /** every `{ el, options }` passed to `$(el).datepicker({...})` — lets a test call `options.onSelect` / `beforeShow` */
    datepickerInits: { el: Element | undefined; options: CapturedDatepickerOptions }[];
    /** every `FullCalendar.Calendar` the script constructed (`addEvent` / `removeAllEvents` / `render` are spies) */
    calendarInstances: FakeCalendar[];
    /** every `{ el, options }` passed to `$(el).summernote({...})` — lets a test call `options.callbacks.onImageUpload(...)` */
    summernoteInits: { el: Element | undefined; options: CapturedSummernoteOptions }[];
    /** every other `$(el).summernote("cmd", ...args)` call (insertNode, code setter, …) */
    summernoteCalls: { el: Element | undefined; args: unknown[] }[];
}

/** jQuery's real `$.ajax`, kept before {@link loadSite} replaces it with a capturing stub. */
// (Vitest's jsdom environment has a global window, so the CommonJS build exports jQuery bound to it.)
const realAjax: JQueryStatic["ajax"] = jqueryImport.ajax;

/**
 * Builds a minimal `XMLHttpRequest` stand-in for jQuery's xhr transport that answers HTTP 200 with
 * `body` as `contentType`. Like a browser, it hands back a Blob in `response` when the caller set
 * `responseType = "blob"`, and refuses to read `responseText` for a non-text response type.
 */
function fakeXhrFactory(win: Window & typeof globalThis, body: string, contentType: string): () => XMLHttpRequest {
    return () => {
        // A real XMLHttpRequest whose network side is replaced: own properties shadow the prototype's.
        const xhr = new win.XMLHttpRequest();
        let readyState = 0;
        let status = 0;
        let statusText = "";
        let response: unknown = null;
        const nothing = () => undefined;
        Object.defineProperties(xhr, {
            readyState: { get: () => readyState },
            status: { get: () => status },
            statusText: { get: () => statusText },
            response: { get: () => response },
            responseText: {
                get() {
                    if (xhr.responseType !== "" && xhr.responseType !== "text") {
                        throw new win.DOMException("responseText is only available for a text response", "InvalidStateError");
                    }
                    return body;
                },
            },
            open: { value: nothing }, // nothing to connect
            setRequestHeader: { value: nothing }, // headers are not inspected here
            overrideMimeType: { value: nothing },
            abort: { value: nothing },
            getAllResponseHeaders: { value: () => `content-type: ${contentType}\r\n` },
            send: {
                value() {
                    void Promise.resolve().then(() => {
                        readyState = 4;
                        status = 200;
                        statusText = "OK";
                        response = xhr.responseType === "blob" ? new win.Blob([body], { type: contentType }) : body;
                        xhr.dispatchEvent(new win.ProgressEvent("load"));
                    });
                },
            },
        });
        return xhr;
    };
}

/** Resets the document to `fixtureHtml`, installs the stubs, runs the compiled page script and returns a {@link SiteHandle}. */
export function loadSite(
    area: "admin" | "anonymous" | "user",
    feature: string,
    page: string,
    fixtureHtml: string,
    opts: { autoAjaxResult?: boolean; /** what `$(el).summernote("code")` returns */ summernoteCode?: string } = {},
): SiteHandle {
    const win: PageWindow = window;

    // Views/Shared/_Layout.cshtml's hidden inputs ride along with every page it lays out (not the account pages).
    win.document.body.innerHTML = (feature === "Account" ? "" : LAYOUT_CHROME) + fixtureHtml;

    const $ = jqueryImport;
    Object.assign(win, { $, jQuery: $ });

    // --- vendored jQuery plugins the scripts call: chainable no-ops -------------
    // Always (re)assigned so a mutation from one test (e.g. `$.fn.valid = () => false`)
    // does not leak into the next loadSite() in the same file.
    const chainable = function(this: JQuery) {
        return this;
    };
    Object.assign($.fn, { tabs: chainable, modal: chainable, tooltip: chainable });
    // Captured for SiteHandle.datepickerInits; `$(el).datepicker("getDate")` answers null.
    const datepickerInits: SiteHandle["datepickerInits"] = [];
    Object.assign($.fn, {
        datepicker: function(this: JQuery, arg?: unknown) {
            if (arg && typeof arg === "object") datepickerInits.push({ el: this[0], options: captured(arg, isDatepickerOptions, "the datepicker options") });
            return arg === "getDate" ? null : this;
        },
        valid: () => true,
    });
    // Captured for SiteHandle.summernoteInits / summernoteCalls; `summernote("code")` returns opts.summernoteCode.
    const summernoteInits: SiteHandle["summernoteInits"] = [];
    const summernoteCalls: SiteHandle["summernoteCalls"] = [];
    Object.assign($.fn, {
        summernote: function(this: JQuery, ...args: unknown[]) {
            if (args.length === 0 || typeof args[0] === "object") {
                summernoteInits.push({ el: this[0], options: captured(args[0] ?? {}, isSummernoteOptions, "the summernote options") });
                return this;
            }
            if (args[0] === "code" && args.length === 1) return opts.summernoteCode ?? "";
            summernoteCalls.push({ el: this[0], args });
            return this;
        },
    });

    // --- static jQuery helpers -------------------------------------------------
    const datepickerStatics: SiteHandle["datepickerStatics"] = { setDefaults: vi.fn(), _clearDate: vi.fn() };
    Object.assign($, { datepicker: datepickerStatics });

    // --- $.ajax capture ------------------------------------------------------
    const ajaxCalls: AjaxCall[] = [];
    interface FakeJqXhr {
        done(): FakeJqXhr;
        fail(): FakeJqXhr;
        always(): FakeJqXhr;
    }
    Object.assign($, {
        // What the page passes: the fields a test reads, and jQuery's settings type for the replay.
        ajax: vi.fn((options: Omit<AjaxCall, "settings"> & JQuery.AjaxSettings) => {
            ajaxCalls.push({ ...options, settings: options });
            if (opts.autoAjaxResult && typeof options.success === "function") {
                options.success({ result: true, message: "ok", calendars: [], tempOtherCalendars: [] });
            }
            const d: FakeJqXhr = { done: () => d, fail: () => d, always: () => d };
            return d;
        }),
    });

    // --- globals -----------------------------------------------------------
    const toastr = { success: vi.fn(), error: vi.fn(), info: vi.fn(), warning: vi.fn() };
    Object.assign(win, { toastr });

    // Stub MvcGrid: records each instance, with a fixed URL and a spy `reload`.
    const MvcGridInstances: FakeMvcGrid[] = [];
    Object.assign(win, { MvcGrid: class {
        url = new URL("http://localhost/grid");
        reload = vi.fn();

        constructor(_container: Element | null) {
            MvcGridInstances.push(this);
        }
    } });

    // Every options object passed to `new FullCalendar.Calendar(el, opts)`, so a test can invoke a
    // page's `select` / `eventClick` callback directly (`h.calendarOptions[0].select(arg)`) to drive
    // flows that only run from inside those callbacks — the render/interaction loop itself is not
    // simulated, only the options object FullCalendar would have called back into.
    const calendarOptions: CapturedCalendarOptions[] = [];
    const calendarInstances: FakeCalendar[] = [];
    Object.assign(win, { FullCalendar: {
        Calendar: class {
            constructor(_el: unknown, opts: unknown) {
                calendarOptions.push(captured(opts, isCalendarOptions, "the FullCalendar options"));
                calendarInstances.push(this);
            }

            render = vi.fn();
            addEvent = vi.fn<(event: AddedCalendarEvent) => void>();
            removeAllEvents = vi.fn();
            unselect = vi.fn();
        },
    } });

    // Minimal moment stub: every date formats as "2024-01-01".
    interface FakeMoment {
        format(): string;
        add(): FakeMoment;
        subtract(): FakeMoment;
    }
    function moment(_d?: unknown): FakeMoment {
        return {
            format: () => "2024-01-01",
            add: () => moment(),
            subtract: () => moment(),
        };
    }
    moment.utc = (_d?: unknown) => moment();
    Object.assign(win, { moment });

    // Chart.js 2.x — Dashboard builds ~10 of these in its $(function).
    const charts: SiteHandle["charts"] = [];
    Object.assign(win, { Chart: class {
        constructor(ctx: unknown, cfg: unknown) {
            charts.push({ ctx, cfg: captured(cfg, isChartConfig, "the Chart config") });
        }
    } });

    // --- browser bits jsdom lacks / complains about --------------------------
    // jsdom has no canvas: getContext answers null where a browser hands Chart.js a 2D context.
    Object.defineProperty(win.HTMLCanvasElement.prototype, "getContext", {
        configurable: true,
        value: function(this: HTMLCanvasElement) {
            return { canvas: this };
        },
    });
    win.alert = vi.fn();
    win.confirm = vi.fn(() => true);
    if (!win.URL.createObjectURL) win.URL.createObjectURL = () => "blob:stub";
    if (!win.URL.revokeObjectURL) win.URL.revokeObjectURL = () => undefined;

    // window.location stub: records every navigation (href set, assign, replace) instead of navigating.
    const navigations: string[] = [];
    let hrefValue = "http://localhost/";
    const locationMock = {
        get href() {
            return hrefValue;
        },
        set href(v: string) {
            hrefValue = String(v);
            navigations.push(hrefValue);
        },
        assign: vi.fn((v: string) => navigations.push(String(v))),
        replace: vi.fn((v: string) => navigations.push(String(v))),
        reload: vi.fn(),
        toString() {
            return hrefValue;
        },
    };
    try {
        Object.defineProperty(win, "location", { configurable: true, get: () => locationMock });
    } catch {
        /* older jsdom locks window.location; navigation assertions just won't fire */
    }

    // ExportExcel* helpers build a <form> and call form.submit(); jsdom's submit is a
    // navigating no-op, so capture instead. Stays patched for the life of the jsdom
    // (one per test file); a later loadSite() just rebinds `submittedForms`.
    const submittedForms: HTMLFormElement[] = [];
    win.HTMLFormElement.prototype.submit = function(this: HTMLFormElement) {
        submittedForms.push(this);
    };

    // --- run the compiled script -------------------------------------------
    // jQuery defers `$(fn)` via window.setTimeout when the document is already
    // "complete" (which it is in jsdom). Fake timers around the eval so those
    // ready-callbacks — where most Calendar/Forum wiring lives — run synchronously
    // before the test asserts.
    const file = resolve(WWWROOT, area, "custom", feature, page, "js", "site.js");
    const code = readFileSync(file, "utf8");
    // The view renders every localized text the script reads (pinned by viewContract.test.ts); a fixture lists
    // only those its test looks at, so the rest are filled in here as "L_<Key>".
    for (const match of code.matchAll(/\$\("#(localizer(\w+))"\)/g)) {
        const id = present(match[1]);
        const key = present(match[2]);
        if (win.document.getElementById(id) === null) {
            win.document.body.insertAdjacentHTML("beforeend", `<input type="hidden" id="${id}" value="L_${key}" />`);
        }
    }
    vi.useFakeTimers();
    try {
        // A real page load runs the role's _Layout script first (Views/Shared/_Layout.cshtml): it defines the
        // window helpers every page script uses (escapeHtml, check, onReply, fieldValue, …). The account pages
        // have a layout of their own without it.
        if (feature !== "_Layout" && feature !== "Account") {
            win.eval(readFileSync(resolve(WWWROOT, area, "custom", "_Layout", "js", "site.js"), "utf8"));
        }
        win.eval(code);
        vi.runOnlyPendingTimers();
    } finally {
        vi.useRealTimers();
    }

    return {
        $,
        win,
        charts,
        calendarOptions,
        ajaxCalls,
        respond(indexFromEnd, data) {
            const call = ajaxCalls[ajaxCalls.length - 1 - indexFromEnd];
            call?.success?.(data);
        },
        respondOverHttp(indexFromEnd, body, contentType) {
            const call = ajaxCalls[ajaxCalls.length - 1 - indexFromEnd];
            const settings: JQuery.AjaxSettings = { ...present(call, "the ajax call to replay").settings, xhr: fakeXhrFactory(win, body, contentType) };
            void realAjax(settings);
        },
        lastAjax: () => lastOf(ajaxCalls),
        toastr,
        submittedForms,
        navigations,
        MvcGridInstances,
        summernoteInits,
        summernoteCalls,
        calendarInstances,
        datepickerStatics,
        datepickerInits,
    };
}

/** Replaces (or adds) a jQuery plugin method on the page's `$.fn` — e.g. `stubPlugin(h, "valid", () => false)`. */
export function stubPlugin(h: SiteHandle, name: string, impl: (this: JQuery, ...args: never[]) => unknown): void {
    Object.assign(h.$.fn, { [name]: impl });
}

/**
 * A FullCalendar event's extendedProps as the calendar scripts set them (every field present), with `over`
 * merged in: a test names only the fields it looks at.
 */
export function eventProps(over: Record<string, unknown> = {}): Record<string, unknown> {
    return {
        calendarId: 1, displayStartDate: "2024-01-01", displayEndDate: "2024-01-02",
        displayStartDateTimeZone: "UTC", displayEndDateTimeZone: "UTC", calendarType: "My", ...over,
    };
}

// --- Checked reads for tests -------------------------------------------------------------------------
// A test reads what the script left behind (a captured ajax call, a fixture element, a mock's arguments)
// through these instead of type assertions: a value that is not there, or not of the expected type, throws
// and so fails the test with a message naming it.

/** `value`, which the test expects to be there. */
export function present<T>(value: T | null | undefined, what = "an expected value"): T {
    if (value === null || value === undefined) throw new Error(`${what} is missing`);
    return value;
}

/** The last of `items`, which must not be empty. */
export function lastOf<T>(items: readonly T[]): T {
    return present(items[items.length - 1], "the last item");
}

/** `value`, which must be a `type` (an element of the fixture, the FormData a script posted, …). */
export function instanceOfType<T>(value: unknown, type: abstract new (...args: never[]) => T): T {
    if (value instanceof type) return value;
    throw new Error(`expected a ${type.name}, got ${value === null ? "null" : typeof value}`);
}

/** The `success` callback a script passed to `$.ajax`. */
export function successOf(call: AjaxCall | undefined): (data: unknown) => void {
    return present(present(call, "the ajax call").success, "the call's success callback");
}

/** The `complete` callback a script passed to `$.ajax`. */
export function completeOf(call: AjaxCall | undefined): (...a: unknown[]) => void {
    return present(present(call, "the ajax call").complete, "the call's complete callback");
}

/** The `error` callback a script passed to `$.ajax`. */
export function errorOf(call: AjaxCall | undefined): (...a: unknown[]) => void {
    return present(present(call, "the ajax call").error, "the call's error callback");
}

/** The FormData a captured `$.ajax` call posted. */
export function formDataOf(call: AjaxCall | undefined): FormData {
    return instanceOfType(present(call, "the ajax call").data, FormData);
}

/** Minimal hidden-input fixture: `hidden("id", "value")` → `<input id hidden>`. */
export function hidden(id: string, value = ""): string {
    return `<input type="hidden" id="${id}" value="${value}" />`;
}

/**
 * jsdom does no layout, so jQuery `:visible` / `:hidden` are useless. The scripts
 * toggle visibility with `.show()` / `.hide()`, which set `style.display`. Read that.
 */
export function hiddenByStyle(el: Element | null | undefined): boolean {
    return el instanceof HTMLElement && el.style.display === "none";
}

/** Native `CustomEvent` dispatch — how MvcGrid fires `rowclick`/`reload*` at runtime. */
export function fireNative(target: EventTarget, type: string, detail?: unknown): void {
    target.dispatchEvent(new CustomEvent(type, { detail, bubbles: true }));
}

/** The antiforgery input every page has. */
export const antiForgery = `<input name="__RequestVerificationToken" value="tok" />`;
