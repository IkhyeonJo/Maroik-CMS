/*
 * Every page script reads the server-published constants (limits, class maps, time-of-day lists ...) from hidden inputs and
 * falls back to a safe default when one is absent. This shared check loads a script with every such hidden input removed
 * (the antiforgery token and the localized texts stay) and asserts it still initializes — the fallback arms of those reads.
 */
import { describe, it, expect, vi } from "vitest";
import { loadSite } from "@tests/_common/harness";

/**
 * Removes every `<input type="hidden" id="...">` except the localizer texts and the inputs the scripts REQUIRE rather than default:
 * `noMaturityDate` and the server-rendered event data (`calendarEventOutputViewModels`, `otherCalendarEventOutputViewModels`).
 */
export function withoutServerConstants(html: string): string {
    return html.replace(/<input type="hidden" id="(?!localizer|noMaturityDate|calendarEventOutputViewModels|otherCalendarEventOutputViewModels)[^"]*"[^>]*\/?>/g, "");
}

/** Registers a test that the page script still initializes with its server-published constants removed. */
export function describeMissingServerConstants(area: "admin" | "anonymous" | "user", feature: string, page: string, fixture: () => string): void {
    describe(`${area}/${feature}/${page} — without the server-published constants`, () => {
        it("still initialises, falling back to its built-in defaults", () => {
            const html = withoutServerConstants(fixture());
            expect(html).not.toBe(fixture()); // the fixture really did carry some
            // an exception inside a jQuery ready-callback does not fail loadSite: jQuery logs it ("jQuery.Deferred exception") and moves on
            const warn = vi.spyOn(console, "warn").mockImplementation(() => undefined);
            try {
                expect(() => loadSite(area, feature, page, html)).not.toThrow();
                expect(warn.mock.calls.filter((c: unknown[]) => String(c[0]).includes("jQuery.Deferred exception"))).toEqual([]);
            } finally {
                warn.mockRestore();
            }
        });
    });
}
