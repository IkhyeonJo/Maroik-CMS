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
import { vi } from "vitest";
import jqueryImport from "jquery";

/** The website's wwwroot, where the compiled scripts live. */
const WWWROOT = resolve(__dirname, "../../wwwroot");

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
}

/** What {@link loadSite} returns: the page's window and every stub/capture a test asserts on. */
export interface SiteHandle {
    /** the jQuery bound to the page window */
    $: JQueryStatic;
    /** the jsdom window the script ran in */
    win: Window & typeof globalThis & Record<string, any>;
    /** every `{ ctx, cfg }` passed to `new Chart(...)` (Dashboard) */
    charts: unknown[];
    /** every options object passed to `new FullCalendar.Calendar(el, opts)` — lets a test call a page's `select` / `eventClick` callback (see the comment on `calendarOptions` in loadSite). */
    calendarOptions: any[];
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
    toastr: { success: any; error: any; info: any; warning: any };
    /** `<form>`s that site.js created + submitted via ExportExcel* */
    submittedForms: HTMLFormElement[];
    /** values passed to `window.location.href = …` */
    navigations: string[];
    /** every `new MvcGrid(...)` stub the script constructed */
    MvcGridInstances: any[];
    /** every `{ el, options }` passed to `$(el).datepicker({...})` — lets a test call `options.onSelect` / `beforeShow` */
    datepickerInits: { el: Element | undefined; options: any }[];
    /** every `FullCalendar.Calendar` the script constructed (`addEvent` / `removeAllEvents` / `render` are spies) */
    calendarInstances: any[];
    /** every `{ el, options }` passed to `$(el).summernote({...})` — lets a test call `options.callbacks.onImageUpload(...)` */
    summernoteInits: { el: Element | undefined; options: any }[];
    /** every other `$(el).summernote("cmd", ...args)` call (insertNode, code setter, …) */
    summernoteCalls: { el: Element | undefined; args: unknown[] }[];
}

/** jQuery bound to `win` (the CommonJS build exports a factory when no global window exists). */
function bindJquery(win: any): JQueryStatic {
    const mod: any = jqueryImport as any;
    const $ = typeof mod === "function" && !mod.fn ? mod(win) : mod;
    return $ as JQueryStatic;
}

/** jQuery's real `$.ajax`, kept before {@link loadSite} replaces it with a capturing stub. */
const realAjax: JQueryStatic["ajax"] = bindJquery(window).ajax;

/**
 * Builds a minimal `XMLHttpRequest` stand-in for jQuery's xhr transport that answers HTTP 200 with
 * `body` as `contentType`. Like a browser, it hands back a Blob in `response` when the caller set
 * `responseType = "blob"`, and refuses to read `responseText` for a non-text response type.
 */
function fakeXhrFactory(win: any, body: string, contentType: string): () => XMLHttpRequest {
    return () => {
        const xhr: any = {
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
        return xhr as XMLHttpRequest;
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
    const win = window as any;

    win.document.body.innerHTML = fixtureHtml;

    const $ = bindJquery(win);
    win.$ = win.jQuery = $;

    // --- vendored jQuery plugins the scripts call: chainable no-ops -------------
    // Always (re)assigned so a mutation from one test (e.g. `$.fn.valid = () => false`)
    // does not leak into the next loadSite() in the same file.
    const chainable = function(this: any) {
        return this;
    };
    for (const name of ["tabs", "modal", "tooltip"]) {
        ($.fn as any)[name] = chainable;
    }
    // Captured for SiteHandle.datepickerInits; `$(el).datepicker("getDate")` answers null.
    const datepickerInits: SiteHandle["datepickerInits"] = [];
    ($.fn as any).datepicker = function(this: any, arg?: unknown) {
        if (arg && typeof arg === "object") datepickerInits.push({ el: this[0], options: arg });
        return arg === "getDate" ? null : this;
    };
    ($.fn as any).valid = () => true;
    // Captured for SiteHandle.summernoteInits / summernoteCalls; `summernote("code")` returns opts.summernoteCode.
    const summernoteInits: SiteHandle["summernoteInits"] = [];
    const summernoteCalls: SiteHandle["summernoteCalls"] = [];
    ($.fn as any).summernote = function(this: any, ...args: unknown[]) {
        if (args.length === 0 || typeof args[0] === "object") {
            summernoteInits.push({ el: this[0], options: args[0] });
            return this;
        }
        if (args[0] === "code" && args.length === 1) return opts.summernoteCode ?? "";
        summernoteCalls.push({ el: this[0], args });
        return this;
    };

    // --- static jQuery helpers -------------------------------------------------
    ($ as any).datepicker = { setDefaults: vi.fn(), _clearDate: vi.fn() };

    // --- $.ajax capture ------------------------------------------------------
    const ajaxCalls: AjaxCall[] = [];
    ($ as any).ajax = vi.fn((options: AjaxCall) => {
        ajaxCalls.push(options);
        if (opts.autoAjaxResult && typeof options.success === "function") {
            options.success({ result: true, message: "ok", calendars: [], tempOtherCalendars: [] });
        }
        const d: any = { done: () => d, fail: () => d, always: () => d };
        return d;
    });

    // --- globals -----------------------------------------------------------
    const toastr = { success: vi.fn(), error: vi.fn(), info: vi.fn(), warning: vi.fn() };
    win.toastr = toastr;

    // Normally set by each area's _Layout/js/site.ts, loaded before a page's own script in a real
    // page load; loadSite only loads the one-page script under test, so provide the same
    // implementation here (kept byte-identical to _Layout's) for pages that call it unconditionally.
    win.escapeHtml = function(value: string): string {
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

    // Stub MvcGrid: records each instance, with a fixed URL and a spy `reload`.
    const MvcGridInstances: any[] = [];
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
    const calendarOptions: any[] = [];
    const calendarInstances: any[] = [];
    win.FullCalendar = {
        Calendar: class {
            constructor(_el: unknown, opts: unknown) {
                calendarOptions.push(opts);
                calendarInstances.push(this);
            }

            render = vi.fn();
            addEvent = vi.fn();
            removeAllEvents = vi.fn();
            unselect = vi.fn();
        },
    };

    // Minimal moment stub: every date formats as "2024-01-01".
    const moment: any = (_d?: unknown) => ({
        format: () => "2024-01-01",
        add: () => moment(),
    });
    moment.utc = (_d?: unknown) => moment();
    win.moment = moment;

    // Chart.js 2.x — Dashboard builds ~10 of these in its $(function).
    const charts: unknown[] = [];
    win.Chart = class {
        constructor(ctx: unknown, cfg: unknown) {
            charts.push({ ctx, cfg });
        }
    };

    // --- browser bits jsdom lacks / complains about --------------------------
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
    vi.useFakeTimers();
    try {
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
            void realAjax({ ...(call as any), xhr: fakeXhrFactory(win, body, contentType) });
        },
        lastAjax: () => ajaxCalls[ajaxCalls.length - 1],
        toastr,
        submittedForms,
        navigations,
        MvcGridInstances,
        summernoteInits,
        summernoteCalls,
        calendarInstances,
        datepickerInits,
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
    target.dispatchEvent(new (globalThis as any).CustomEvent(type, { detail, bubbles: true }));
}

/** The antiforgery input every page has. */
export const antiForgery = `<input name="__RequestVerificationToken" value="tok" />`;
