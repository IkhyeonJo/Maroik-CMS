/*
 * Shared suite for the runtime-check toolkit each role's `_Layout` script puts on `window` (`check`, `replies`,
 * `onReply`, `parseJson`, `byId`, `fieldValue`, …). The page scripts read the values the view renders and the
 * replies the controllers send through these helpers instead of type assertions, so every "this is not what the
 * code expects" path lives — and is tested — here: a missing element or attribute throws, a reply that does not
 * pass its check shows the generic "temporary error" toast and logs why.
 */
import { describe, it, expect, vi } from "vitest";
import { loadSite, hidden, type SiteHandle } from "@tests/_common/harness";

/** A few elements to read from (loadSite adds the layout's `_LocalizerTemporaryError`). */
const fixture = () =>
    hidden("text", "hello") +
    `<select id="one"><option value="a">a</option><option value="b" selected>b</option></select>
     <select id="none"><option value="a" disabled>a</option></select>
     <select id="many" multiple><option value="a" selected>a</option><option value="b" selected>b</option></select>
     <canvas id="chart"></canvas>
     <a id="link" data-id="7"></a>`;

/** Registers the toolkit tests for one area's `_Layout` script. */
export function describeToolkit(area: "admin" | "user" | "anonymous"): void {
    const load = (): SiteHandle => loadSite(area, "_Layout", "", fixture());

    describe(`${area}/_Layout — window.check builders`, () => {
        it.each([
            ["string", "x", 1, "value: expected a string, got number"],
            ["number", 1, "1", "value: expected a number, got string"],
            ["boolean", false, null, "value: expected a boolean, got null"],
        ] as const)("check.%s accepts its type and names what it got otherwise", (name, good, bad, message) => {
            const { check } = load().win;
            expect(check[name].is(good)).toBe(true);
            expect(check[name].problem(good, "value")).toBeNull();
            expect(check[name].is(bad)).toBe(false);
            expect(check[name].problem(bad, "value")).toBe(message);
        });

        it("check.literal accepts exactly its value", () => {
            const { check } = load().win;
            expect(check.literal(true).is(true)).toBe(true);
            expect(check.literal(true).problem(false, "reply.result")).toBe("reply.result: expected true, got boolean");
            expect(check.literal("My").problem("Other", "x")).toBe("x: expected \"My\", got string");
        });

        it("check.nullable also accepts null and otherwise defers to its check", () => {
            const { check } = load().win;
            const note = check.nullable(check.string);
            expect(note.is(null)).toBe(true);
            expect(note.is("n")).toBe(true);
            expect(note.problem(3, "note")).toBe("note: expected a string, got number");
        });

        it("check.array checks every item and names the failing index", () => {
            const { check } = load().win;
            const names = check.array(check.string);
            expect(names.is(["a", "b"])).toBe(true);
            expect(names.is([])).toBe(true);
            expect(names.problem({}, "names")).toBe("names: expected an array, got object");
            expect(names.problem(["a", 2], "names")).toBe("names[1]: expected a string, got number");
        });

        it("check.record checks every value of a plain object", () => {
            const { check } = load().win;
            const map = check.record(check.array(check.string));
            expect(map.is({ Labor: ["a"], Other: [] })).toBe(true);
            expect(map.problem([], "map")).toBe("map: expected an object, got an array");
            expect(map.problem(null, "map")).toBe("map: expected an object, got null");
            expect(map.problem({ Labor: "a" }, "map")).toBe("map.Labor: expected an array, got string");
        });

        it("check.object checks the listed fields and ignores the rest", () => {
            const { check } = load().win;
            const asset = check.object({ productName: check.string, amount: check.number });
            expect(asset.is({ productName: "Cash", amount: 1, extra: true })).toBe(true);
            expect(asset.problem("x", "asset")).toBe("asset: expected an object, got string");
            expect(asset.problem({ productName: "Cash" }, "asset")).toBe("asset.amount: expected a number, got undefined");
        });

        it("check.oneOf accepts either shape and names both problems otherwise", () => {
            const { check } = load().win;
            const idOrName = check.oneOf(check.number, check.string);
            expect(idOrName.is(1)).toBe(true);
            expect(idOrName.is("a")).toBe(true);
            expect(idOrName.problem(true, "id")).toBe("id: expected a number, got boolean / id: expected a string, got boolean");
        });

        it("check.jsonText accepts a string whose JSON passes its content check", () => {
            const { check } = load().win;
            const events = check.jsonText(check.array(check.number));
            expect(events.is("[1,2]")).toBe(true);
            expect(events.problem(1, "events")).toBe("events: expected a string, got number");
            expect(events.problem("{", "events")).toBe("events: expected JSON text");
            expect(events.problem("[\"a\"]", "events")).toBe("events(JSON)[0]: expected a number, got string");
        });
    });

    describe(`${area}/_Layout — window.replies`, () => {
        it("replies.failed / replies.action are the refusal and the confirmation-or-refusal", () => {
            const { replies } = load().win;
            expect(replies.failed.is({ result: false, error: "no" })).toBe(true);
            expect(replies.failed.problem({ result: true }, "reply")).toBe("reply.result: expected false, got boolean");
            expect(replies.action.is({ result: true, message: "ok" })).toBe(true);
            expect(replies.action.is({ result: false, error: "no" })).toBe(true);
            expect(replies.action.is({ result: true })).toBe(false);
        });

        it("replies.read / replies.write add a payload to the success shape", () => {
            const { check, replies } = load().win;
            const read = replies.read({ asset: check.object({ amount: check.number }) });
            expect(read.is({ result: true, asset: { amount: 1 } })).toBe(true);
            expect(read.is({ result: false, error: "gone" })).toBe(true);
            expect(read.is({ result: true, asset: {} })).toBe(false);
            const write = replies.write({ calendar: check.object({ id: check.number }) });
            expect(write.is({ result: true, message: "saved", calendar: { id: 1 } })).toBe(true);
            expect(write.is({ result: true, calendar: { id: 1 } })).toBe(false);
        });
    });

    describe(`${area}/_Layout — reading replies`, () => {
        it("onReply hands a reply that passes its check to the handler", () => {
            const h = load();
            const handler = vi.fn();
            h.win.onReply(h.win.replies.action, handler)({ result: true, message: "ok" });
            expect(handler).toHaveBeenCalledWith({ result: true, message: "ok" });
            expect(h.toastr.error).not.toHaveBeenCalled();
        });

        it("onReply shows the generic error and logs why for a reply that does not pass", () => {
            const h = load();
            const handler = vi.fn();
            const log = vi.spyOn(console, "error").mockImplementation(() => undefined);
            try {
                h.win.onReply(h.win.replies.action, handler)({ result: true });
                expect(handler).not.toHaveBeenCalled();
                expect(h.toastr.error).toHaveBeenCalledWith("L_TemporaryError");
                expect(log).toHaveBeenCalledWith(expect.stringContaining("Unexpected reply"));
            } finally {
                log.mockRestore();
            }
        });

        it("onReplyText parses the text first; unparseable text is handled like a mismatch", () => {
            const h = load();
            const handler = vi.fn();
            const log = vi.spyOn(console, "error").mockImplementation(() => undefined);
            try {
                const read = h.win.onReplyText(h.win.replies.failed, handler);
                read("{\"result\":false,\"error\":\"denied\"}");
                expect(handler).toHaveBeenCalledWith({ result: false, error: "denied" });
                read("<html>");
                read("{\"result\":true}");
                expect(handler).toHaveBeenCalledTimes(1);
                expect(h.toastr.error).toHaveBeenCalledTimes(2);
            } finally {
                log.mockRestore();
            }
        });

        it("parseJson returns the parsed value and throws, naming the source, when it is not JSON or does not pass", () => {
            const { check, parseJson } = load().win;
            expect(parseJson("[\"a\"]", check.array(check.string), "#list")).toEqual(["a"]);
            expect(() => parseJson("[", check.array(check.string), "#list")).toThrow("#list is not JSON");
            expect(() => parseJson("[1]", check.array(check.string), "#list")).toThrow("#list[0]: expected a string, got number");
        });

        it("conform returns a value that passes and throws otherwise", () => {
            const { check, conform } = load().win;
            expect(conform({ data: {} }, check.object({ data: check.record(check.string) }), "rowclick detail")).toEqual({ data: {} });
            expect(() => conform(1, check.string, "detail")).toThrow("detail: expected a string, got number");
        });
    });

    describe(`${area}/_Layout — reading the page`, () => {
        it("required returns a present value and throws for null / undefined", () => {
            const { required } = load().win;
            expect(required(0, "index")).toBe(0);
            expect(() => required(null, "the 2D context")).toThrow("the 2D context is missing");
            expect(() => required(undefined, "the index")).toThrow("the index is missing");
        });

        it("byId returns the element of the asked type and throws when it is missing or of another type", () => {
            const { byId, HTMLCanvasElement, HTMLInputElement } = load().win;
            expect(byId("chart", HTMLCanvasElement).id).toBe("chart");
            expect(() => byId("nope", HTMLCanvasElement)).toThrow("#nope is missing from the page");
            expect(() => byId("chart", HTMLInputElement)).toThrow("#chart is not a HTMLInputElement");
        });

        it("elementOf / instanceOf check the first element of a jQuery set and any value", () => {
            const h = load();
            const { elementOf, instanceOf, HTMLCanvasElement, HTMLInputElement, File } = h.win;
            expect(elementOf(h.$("#chart"), HTMLCanvasElement).id).toBe("chart");
            expect(() => elementOf(h.$("#nope"), HTMLCanvasElement)).toThrow("A required element is missing from the page");
            expect(() => elementOf(h.$("#chart"), HTMLInputElement)).toThrow("#chart is not a HTMLInputElement");
            const file = new File(["x"], "x.png");
            expect(instanceOf(file, File, "the dropped file")).toBe(file);
            expect(() => instanceOf("x", File, "the dropped file")).toThrow("the dropped file is not a File");
        });

        it("fieldValue reads a field's text and throws when it is missing or holds several values", () => {
            const h = load();
            expect(h.win.fieldValue(h.$("#text"))).toBe("hello");
            expect(() => h.win.fieldValue(h.$("#nope"))).toThrow("A required element is missing from the page");
            expect(() => h.win.fieldValue(h.$("#many"))).toThrow("#many does not hold a single text value");
        });

        it("selectValue reads a select's chosen value and answers null when nothing is chosen", () => {
            const h = load();
            expect(h.win.selectValue(h.$("#one"))).toBe("b");
            expect(h.win.selectValue(h.$("#none"))).toBeNull();
            expect(() => h.win.selectValue(h.$("#nope"))).toThrow("A required element is missing from the page");
            expect(() => h.win.selectValue(h.$("#many"))).toThrow("#many does not hold a single text value");
        });

        it("optionalFieldValue answers undefined for a field the view left out", () => {
            const h = load();
            expect(h.win.optionalFieldValue(h.$("#text"))).toBe("hello");
            expect(h.win.optionalFieldValue(h.$("#nope"))).toBeUndefined();
            expect(() => h.win.optionalFieldValue(h.$("#many"))).toThrow("#many does not hold a single text value");
        });

        it("attribute reads an attribute and throws when it is missing", () => {
            const h = load();
            expect(h.win.attribute(h.$("#link"), "data-id")).toBe("7");
            expect(() => h.win.attribute(h.$("#link"), "data-name")).toThrow("#link has no data-name attribute");
            expect(() => h.win.attribute(h.$("#nope"), "data-id")).toThrow("A required element is missing from the page");
        });

        it("setMinDate moves a datepicker's earliest selectable date", () => {
            const h = load();
            const picker = { datepicker: vi.fn() };
            const date = new Date(2024, 4, 10);
            h.win.setMinDate(picker, date);
            expect(picker.datepicker).toHaveBeenCalledWith("option", "minDate", date);
        });
    });
}
