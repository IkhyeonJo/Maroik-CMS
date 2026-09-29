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
