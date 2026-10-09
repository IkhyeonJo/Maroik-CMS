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
}

/**
 * The jsdom window a page runs in: the globals the harness installs (`$`, `toastr`, `MvcGrid`, …) are
 * `unknown` to a test, except `escapeHtml`, which a test may call.
 */
export type PageWindow = Window & typeof globalThis & Record<string, unknown> & { escapeHtml(value: string): string };

/** A callback a page handed to a widget; a test calls it with whatever the widget would pass. */
type WidgetCallback = (this: unknown, ...args: unknown[]) => unknown;

/** The FullCalendar options callbacks a test drives directly (see {@link SiteHandle.calendarOptions}). */
export interface CapturedCalendarOptions {
    select(arg: { start: Date; end: Date }): void;
    eventClick(arg: object): unknown;
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
    callbacks: { onImageUpload(files: File[]): void };
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

/** jQuery bound to `win` (the CommonJS build exports a factory when no global window exists). */
function bindJquery(win: Window): JQueryStatic {
    // @types/jquery types the import as jQuery itself; the factory form has no `fn`.
    const mod = jqueryImport as unknown as Partial<Pick<JQueryStatic, "fn">> & ((win: Window) => JQueryStatic);
    const $ = typeof mod === "function" && !mod.fn ? mod(win) : mod;
    return $ as JQueryStatic;
}

/** The members of an `XMLHttpRequest` that jQuery's xhr transport touches. */
interface FakeXhr {
    responseType: XMLHttpRequestResponseType;
    readyState: number;
    status: number;
    statusText: string;
    response: unknown;
    onload: (() => void) | null;
    onerror: (() => void) | null;
    onabort: (() => void) | null;
    ontimeout: (() => void) | null;
    open(): void;
    setRequestHeader(): void;
    overrideMimeType(): void;
    abort(): void;
    getAllResponseHeaders(): string;
    readonly responseText: string;
    send(): void;
}

/** jQuery's real `$.ajax`, kept before {@link loadSite} replaces it with a capturing stub. */
const realAjax: JQueryStatic["ajax"] = bindJquery(window).ajax;

/**
 * Builds a minimal `XMLHttpRequest` stand-in for jQuery's xhr transport that answers HTTP 200 with
 * `body` as `contentType`. Like a browser, it hands back a Blob in `response` when the caller set
 * `responseType = "blob"`, and refuses to read `responseText` for a non-text response type.
 */
function fakeXhrFactory(win: Window & typeof globalThis, body: string, contentType: string): () => XMLHttpRequest {
    return () => {
        const xhr: FakeXhr = {
            responseType: "",
            readyState: 0,
            status: 0,
            statusText: "",
            response: null,
            onload: null,
            onerror: null,
            onabort: null,
            ontimeout: null,
            open() { /* nothing to connect */ },
            setRequestHeader() { /* headers are not inspected here */ },
            overrideMimeType() { /* not used by the scripts */ },
            abort() { /* never aborted */ },
            getAllResponseHeaders: () => `content-type: ${contentType}\r\n`,
            get responseText() {
                if (xhr.responseType !== "" && xhr.responseType !== "text") {
                    throw new win.DOMException("responseText is only available for a text response", "InvalidStateError");
                }
                return body;
            },
            send() {
                void Promise.resolve().then(() => {
                    xhr.readyState = 4;
                    xhr.status = 200;
                    xhr.statusText = "OK";
                    xhr.response = xhr.responseType === "blob" ? new win.Blob([body], { type: contentType }) : body;
                    xhr.onload?.();
                });
            },
        };
        return xhr as unknown as XMLHttpRequest;
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
    const win = window as PageWindow;

    // Views/Shared/_Layout.cshtml's hidden inputs ride along with every page it lays out (not the account pages).
    win.document.body.innerHTML = (feature === "Account" ? "" : LAYOUT_CHROME) + fixtureHtml;

    const $ = bindJquery(win);
    win.$ = win.jQuery = $;

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
            if (arg && typeof arg === "object") datepickerInits.push({ el: this[0], options: arg as CapturedDatepickerOptions });
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
                summernoteInits.push({ el: this[0], options: args[0] as CapturedSummernoteOptions });
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
        ajax: vi.fn((options: AjaxCall) => {
            ajaxCalls.push(options);
            if (opts.autoAjaxResult && typeof options.success === "function") {
                options.success({ result: true, message: "ok", calendars: [], tempOtherCalendars: [] });
            }
            const d: FakeJqXhr = { done: () => d, fail: () => d, always: () => d };
            return d;
        }),
    });

    // --- globals -----------------------------------------------------------
    const toastr = { success: vi.fn(), error: vi.fn(), info: vi.fn(), warning: vi.fn() };
    win.toastr = toastr;

    // Stub MvcGrid: records each instance, with a fixed URL and a spy `reload`.
    const MvcGridInstances: FakeMvcGrid[] = [];
    win.MvcGrid = class {
        url = new URL("http://localhost/grid");
        reload = vi.fn();

        constructor(_container: Element | null) {
            MvcGridInstances.push(this);
        }
    };

    // Every options object passed to `new FullCalendar.Calendar(el, opts)`, so a test can invoke a
    // page's `select` / `eventClick` callback directly (`h.calendarOptions[0].select(arg)`) to drive
    // flows that only run from inside those callbacks — the render/interaction loop itself is not
    // simulated, only the options object FullCalendar would have called back into.
    const calendarOptions: CapturedCalendarOptions[] = [];
    const calendarInstances: FakeCalendar[] = [];
    win.FullCalendar = {
        Calendar: class {
            constructor(_el: unknown, opts: unknown) {
                calendarOptions.push(opts as CapturedCalendarOptions);
                calendarInstances.push(this);
            }

            render = vi.fn();
            addEvent = vi.fn<(event: AddedCalendarEvent) => void>();
            removeAllEvents = vi.fn();
            unselect = vi.fn();
        },
    };

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
    win.Chart = class {
        constructor(ctx: unknown, cfg: unknown) {
            charts.push({ ctx, cfg: cfg as CapturedChartConfig });
        }
    };

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
    for (const [, id, key] of code.matchAll(/\$\("#(localizer(\w+))"\)/g)) {
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
            void realAjax({ ...(call as JQuery.AjaxSettings), xhr: fakeXhrFactory(win, body, contentType) });
        },
        lastAjax: () => ajaxCalls[ajaxCalls.length - 1],
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

/** Minimal hidden-input fixture: `hidden("id", "value")` → `<input id hidden>`. */
export function hidden(id: string, value = ""): string {
    return `<input type="hidden" id="${id}" value="${value}" />`;
}

/**
 * jsdom does no layout, so jQuery `:visible` / `:hidden` are useless. The scripts
 * toggle visibility with `.show()` / `.hide()`, which set `style.display`. Read that.
 */
export function hiddenByStyle(el: Element | null | undefined): boolean {
    return (el as HTMLElement | null | undefined)?.style.display === "none";
}

/** Native `CustomEvent` dispatch — how MvcGrid fires `rowclick`/`reload*` at runtime. */
export function fireNative(target: EventTarget, type: string, detail?: unknown): void {
    target.dispatchEvent(new CustomEvent(type, { detail, bubbles: true }));
}

/** The antiforgery input every page has. */
export const antiForgery = `<input name="__RequestVerificationToken" value="tok" />`;
