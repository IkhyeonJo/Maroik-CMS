/*
 * Shared behavior suite for the three `_Layout` scripts (admin / user / anonymous): session-expiry
 * recovery, culture switching, the double-submit guard and the bfcache reset. The admin and user
 * scripts additionally publish `escapeHtml` and the default upload-size fallback.
 */
import { describe, it, expect } from "vitest";
import { loadSite, antiForgery, hidden, hiddenByStyle, type SiteHandle } from "@tests/_common/harness";

const fixture = () =>
    antiForgery +
    hidden("returnUri", "/back-to-here") +
    `<a id="aChangeCultureEnUS"></a><a id="aChangeCultureKoKR"></a>
   <div id="loading"></div>
   <form id="f1"><button type="submit" id="s1">a</button><input type="submit" id="s1b" /></form>
   <form id="f2"><button type="submit" id="s2">b</button></form>`;

export function describeLayoutScript(area: "admin" | "user" | "anonymous"): void {
    const load = () => loadSite(area, "_Layout", "", fixture());
    const disabled = (h: SiteHandle, id: string) => (h.win.document.getElementById(id) as HTMLButtonElement).disabled;
    const send = (h: SiteHandle, type?: string) => h.$(h.win.document).trigger("ajaxSend", [{}, type === undefined ? {} : { type }]);
    const complete = (h: SiteHandle, type?: string) => h.$(h.win.document).trigger("ajaxComplete", [{}, type === undefined ? {} : { type }]);

    describe(`${area}/_Layout — shared layout behaviour`, () => {
        it("an unhandled AJAX error sends the visitor to the public dashboard (session expiry recovery)", () => {
            const h = load();
            h.$(h.win.document).trigger("ajaxError");
            expect(h.navigations.at(-1)).toBe("/Dashboard/AnonymousIndex");
        });

        it.each([["EnUS", "en-US"], ["KoKR", "ko-KR"]])("the %s language link posts its culture with the anti-forgery header and then returns to the page", (id, culture) => {
            const h = load();
            h.$(`#aChangeCulture${id}`).trigger("click");
            const call = h.lastAjax();
            expect(call.url).toBe("/Dashboard/CultureManagement");
            expect(call.headers).toEqual({ RequestVerificationToken: "tok" });
            expect(JSON.parse(String(call.data))).toEqual({ Culture: culture });
            call.success!({});
            expect(h.navigations.at(-1)).toBe("/back-to-here");
        });

        if (area !== "anonymous") {
            it("publishes escapeHtml, which neutralises every HTML-significant character", () => {
                const h = load();
                expect(h.win.escapeHtml(`<div class="test" data-value='a&b'>text</div>`))
                    .toBe("&lt;div class=&quot;test&quot; data-value=&#39;a&amp;b&#39;&gt;text&lt;/div&gt;");
                expect(h.win.escapeHtml("plain")).toBe("plain");
            });

            it("publishes the 10 MB default for the client-side upload-size check", () => {
                expect(load().win.MaroikDefaultMaxAttachedFileSizeBytes).toBe(10485760);
            });
        }

        it("a POST disables the submit controls; a GET does not", () => {
            const h = load();
            send(h, "GET");
            send(h);
            expect(disabled(h, "s1")).toBe(false);
            send(h, "post");
            complete(h); // a completed request with no type is a GET: it does not release the POST's lock
            expect(disabled(h, "s1")).toBe(true);
            expect(disabled(h, "s1b")).toBe(true);
            expect(disabled(h, "s2")).toBe(true);
        });

        it("overlapping POSTs keep the controls disabled until the last one completes", () => {
            const h = load();
            send(h, "POST");
            send(h, "POST");
            complete(h, "POST");
            expect(disabled(h, "s1")).toBe(true);
            complete(h, "POST");
            expect(disabled(h, "s1")).toBe(false);
        });

        it("a completing GET never re-enables the controls, and a stray completion cannot drive the counter negative", () => {
            const h = load();
            send(h, "POST");
            complete(h, "GET");
            expect(disabled(h, "s1")).toBe(true);
            complete(h, "POST");
            complete(h, "POST"); // clamped at 0
            send(h, "POST");
            expect(disabled(h, "s1")).toBe(true);
            complete(h, "POST");
            expect(disabled(h, "s1")).toBe(false);
        });

        it("a classic form submit disables only that form's own submit buttons", () => {
            const h = load();
            h.$("#f1").trigger("submit");
            expect(disabled(h, "s1")).toBe(true);
            expect(disabled(h, "s1b")).toBe(true);
            expect(disabled(h, "s2")).toBe(false);
        });

        it("returning through the back/forward cache re-enables the controls, hides the overlay and resets the counter", () => {
            const h = load();
            h.$("#loading").show();
            send(h, "POST");
            send(h, "POST");
            h.$(h.win as unknown as Element).trigger("pageshow");
            expect(disabled(h, "s1")).toBe(false);
            expect(hiddenByStyle(h.win.document.getElementById("loading"))).toBe(true);

            send(h, "POST");
            complete(h, "POST");
            expect(disabled(h, "s1")).toBe(false); // would still be disabled if the counter had not been reset
        });
    });
}
