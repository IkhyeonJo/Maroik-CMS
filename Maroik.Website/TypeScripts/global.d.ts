/*
 * Ambient declarations for globals loaded via <script> in Views/Shared/_Layout.cshtml
 * (and Views/Account/_Layout.cshtml) that have no matching @types package.
 *
 * This file has NO top-level import/export, so it stays a global script and its
 * declarations apply across every TypeScripts/**\/*.ts without an import.
 * (import("x") in a *type* position does not turn the file into a module.)
 */

// NonFactors MVC.Grid 6 client companion — wwwroot/{area}/js/mvc-grid/nonfactors-mvc-grid.js.
// Not published to npm (NuGet-ecosystem vendored JS). Typed loosely to the surface the
// custom scripts actually touch: new MvcGrid(el), .url.searchParams, .reload().
declare class MvcGrid {
    /** Binds the grid behavior to its rendered `.mvc-grid` container. */
    constructor(container: Element | null);

    /** The URL the grid reloads its rows from; the scripts edit its `searchParams` (e.g. the search box). */
    url: URL;

    /** Re-fetches the grid's rows from `url`. */
    reload(): void;

    /** Any other member of the untyped vendored library. */
    [key: string]: unknown;
}

// The `detail` of MvcGrid's `rowclick` CustomEvent: the clicked row's column values by column name.
// jQuery 3 copies the native `detail` onto the jQuery event, but @types/jquery types `detail` as
// UIEvent's number, so a `rowclick` handler narrows `e.detail` to this.
interface MvcGridRowClickDetail {
    /** Column name -> the row's value for that column. */
    data: Record<string, string>;
}

// Shared helpers each role's _Layout script puts on `window` (see "Client scripts" in CLAUDE.md).
interface Window {
    /** Fallback upload limit for a page whose `#maxAttachedFileSizeBytes` hidden field is missing. */
    MaroikDefaultMaxAttachedFileSizeBytes: number;

    /** HTML-escapes a value before it is put into string-built markup. */
    escapeHtml(value: string): string;
}

// FullCalendar 5.5.1 global build — wwwroot/{area}/plugins/fullcalendar/main.js.
// Backed (type-only) by the @fullcalendar/core@5.5.1 dev dependency.
declare const FullCalendar: typeof import("@fullcalendar/core");

// moment 2.30.1 global build — wwwroot/{area}/plugins/moment/moment.min.js.
// Backed (type-only) by the moment@2.30.1 dev dependency (ships its own d.ts).
declare const moment: typeof import("moment");

// Bootstrap 4 modal plugin — wwwroot/{area}/plugins/bootstrap/js/bootstrap.bundle.min.js
// (Popper v1 bundled in). The custom scripts only ever call `$el.modal(...)`, so it's
// typed here rather than via `@types/bootstrap`, which pulls in the deprecated
// `popper.js@1` package for the rest of the Bootstrap 4 surface we never touch.
interface BootstrapModalOptions {
    /** Show a backdrop; "static" keeps the modal open when the backdrop is clicked. */
    backdrop?: boolean | "static";
    /** Close the modal on the Escape key. */
    keyboard?: boolean;
    /** Move focus into the modal when it opens. */
    focus?: boolean;
    /** Show the modal immediately on initialization. */
    show?: boolean;
}

// Augments jQuery's own interface (merged with @types/jquery) with the Bootstrap 4 modal plugin.
interface JQuery<TElement = HTMLElement> {
    /** Runs a modal command (defaults to "toggle" when called with no argument). */
    modal(action?: "show" | "hide" | "toggle" | "handleUpdate" | "dispose"): this;

    /** Initializes (and by default shows) the modal with the given options. */
    modal(options: BootstrapModalOptions): this;
}

// jQuery UI datepicker options as jQuery UI really takes them. @types/jqueryui requires `beforeShow` to
// return DatepickerOptions and gives `onSelect` no `this`; jQuery UI also accepts a `beforeShow` that
// returns nothing (a returned object only overrides settings) and calls `onSelect` with the input as `this`.
interface DatepickerSetupOptions extends Omit<JQueryUI.DatepickerOptions, "beforeShow" | "onSelect"> {
    beforeShow?: (input: HTMLInputElement, inst: unknown) => JQueryUI.DatepickerOptions | void;
    onSelect?: (this: HTMLInputElement, dateText: string, inst: unknown) => void;
}

// Augments jQuery's own interface (merged with @types/jquery and @types/jqueryui).
interface JQuery<TElement = HTMLElement> {
    /** Initializes a datepicker with {@link DatepickerSetupOptions}. */
    datepicker(options: DatepickerSetupOptions): this;
}

// The `inst` object jQuery UI passes to datepicker callbacks — only the member the scripts read.
interface DatepickerInstance {
    /** The input the datepicker is attached to. */
    input: JQuery<HTMLInputElement>;
}

// Augments @types/jqueryui's `$.datepicker` with the one undocumented internal the scripts call.
declare namespace JQueryUI {
    interface Datepicker {
        /** jQuery UI internal: empties the target's date (used by the custom "No maturity date" / "Today" buttons). */
        _clearDate(target: Element | JQuery): void;
    }
}

// FormData turns every non-Blob value into a string (WebIDL USVString conversion), and the board, note and
// calendar forms rely on that: they append `.val()` results, checkbox booleans, ids and an attachment
// that may be missing (`undefined` is sent as the text "undefined", which the server binds as no file).
interface FormData {
    append(name: string, value: string | number | boolean | string[] | Blob | null | undefined): void;
}

// FullCalendar types the calendar scripts annotate with (type-only, from the same dev dependency).
type FullCalendarCalendar = import("@fullcalendar/core").Calendar;
type FullCalendarEventApi = import("@fullcalendar/core").EventApi;
type FullCalendarEventClickArg = import("@fullcalendar/core").EventClickArg;
type FullCalendarDateSelectArg = import("@fullcalendar/core").DateSelectArg;

// ── Calendar payloads from CalendarController ───────────────────────────────────────────────────────
// Two JSON spellings reach the scripts: `JsonSerialization.ToClientJson` strings (events, reminders —
// PascalCase, parsed with JSON.parse) and MVC `Json(...)` replies (camelCase).

/** One reminder in `serializedCalendarReminders` (CalendarReminderDto). Exactly one `*BeforeEvent` is set. */
interface CalendarReminderJson {
    Method: string;
    MinutesBeforeEvent: number | null;
    HoursBeforeEvent: number | null;
    DaysBeforeEvent: number | null;
    WeeksBeforeEvent: number | null;
    /** Time of day ("HH:mm:ss") of an all-day event's reminder; null for a timed event. */
    TimesBeforeEvent: string | null;
}

/** One event of `calendarEvents` or a page's `*CalendarEventOutputViewModels` (CalendarEventOutputViewModel). */
interface CalendarEventJson {
    /** FullCalendar takes event ids as strings (it converts any other value), so the scripts pass `String(Id)`. */
    Id: number;
    CalendarId: number;
    Title: string;
    AllDay: boolean;
    StartDate: string;
    EndDate: string;
    HtmlColorCode: string;
    DisplayStartDate: string;
    DisplayEndDate: string;
    DisplayStartDateTimeZone: string;
    DisplayEndDateTimeZone: string;
    /** "My" or "Other". */
    CalendarType: string;
}

/** A calendar in the `calendars` / `tempOtherCalendars` replies (CalendarResponse). */
interface CalendarSummary {
    id: number;
    name: string;
    htmlColorCode: string;
}

/** A row of the `setCalendarShareds` reply (CalendarSharedSummaryResponse). */
interface CalendarSharedSummary {
    id: number;
    name: string;
    user: boolean;
    guest: boolean;
}

/** A row of the `browseCalendarsOfInterests` reply (CalendarBrowseSummaryResponse). */
interface CalendarBrowseSummary {
    id: number;
    name: string;
    checked: boolean;
}

// ── Calendar request bodies the calendar scripts build ─────────────────────────────────────────────

/** What jQuery's `.val()` returns; form values are posted as read. */
type FormFieldValue = string | number | string[] | undefined;

/** The `GetCalendarEvents` request: the ids of the checked calendars. */
interface CalendarEventsRequest {
    Calendars: { Id: number }[];
}

/** A reminder row as an event form posts it in `SerializedCalendarReminders`. Exactly one `*BeforeEvent` is set. */
interface CalendarReminderFormValue {
    Method: FormFieldValue;
    MinutesBeforeEvent: FormFieldValue | null;
    HoursBeforeEvent: FormFieldValue | null;
    DaysBeforeEvent: FormFieldValue | null;
    WeeksBeforeEvent: FormFieldValue | null;
    TimesBeforeEvent: FormFieldValue | null;
}

/** One calendar's row of the `UpdateCalendarShared` request (ids parsed from the row's element id). */
interface CalendarSharedFormValue {
    CalendarId: string;
    User: boolean;
    Anonymous: boolean;
}

/** One checked calendar of the `UpdateOtherCalendar` request. */
interface OtherCalendarFormValue {
    CalendarId: string;
}

/** The `CreateCalendar` / `UpdateCalendar` request: the calendar form's values (`Id` only on update). */
interface CalendarFormRequest {
    Calendars: {
        Id?: FormFieldValue;
        Name: FormFieldValue;
        Description: FormFieldValue;
        HtmlColorCode: FormFieldValue;
        TimeZoneIanaId: FormFieldValue;
    }[];
}
