import { describe, it, expect } from "vitest";
import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { loadSite, hidden, antiForgery } from "@tests/_common/harness";

// wwwroot/user/custom/Dashboard/UserIndex/js/site.js — 10 donut charts + period/unit controls.
const CANVAS_IDS = [
    "regularIncomeYear", "irregularIncomeYear", "regularSavingsYear", "nonConsumerSpendingYear",
    "consumerSpendingYear", "regularIncomeYearMonth", "irregularIncomeYearMonth", "regularSavingsYearMonth",
    "nonConsumerSpendingYearMonth", "consumerSpendingYearMonth",
];

/** The dashboard DOM: the chart canvases and the period / unit controls. */
function fixture(): string {
    return (
        antiForgery +
        `<select id="year"><option value="2024" selected>2024</option></select>
     <select id="month"><option value="5" selected>5</option></select>
     <select id="monetaryUnit"><option value="KRW" selected>KRW</option></select>` +
        CANVAS_IDS.map((id) => `<canvas id="${id}"></canvas>`).join("") +
        // the Number($('#…').val()) inputs — absent ones just become NaN, which is fine
        ["regularIncomeLaborIncomeYear", "regularIncomeBusinessIncomeYear"].map((id) => hidden(id, "10")).join("")
    );
}

describe("Dashboard/UserIndex", () => {
    it("loads and builds one Chart per donut canvas without throwing", () => {
        // jsdom canvas.getContext returns null; the Chart global is stubbed. This asserts
        // the 10-chart $(function) block survives that.
        let h!: ReturnType<typeof loadSite>;
        expect(() => {
            h = loadSite("user", "Dashboard", "UserIndex", fixture());
        }).not.toThrow();
        expect(h.charts).toHaveLength(CANVAS_IDS.length);
    });

    it("changing the year navigates to /Dashboard/UserIndex with year & month", () => {
        const h = loadSite("user", "Dashboard", "UserIndex", fixture());
        h.$("#year").trigger("change");
        expect(h.navigations.at(-1)).toBe("/Dashboard/UserIndex?year=2024&month=5");
    });

    it("changing the monetary unit posts to /Dashboard/UserUpdateDefaultMonetary", () => {
        const h = loadSite("user", "Dashboard", "UserIndex", fixture());
        h.$("#monetaryUnit").trigger("change");
        const call = h.lastAjax();
        expect(call.url).toBe("/Dashboard/UserUpdateDefaultMonetary");
        expect(JSON.parse(String(call.data))).toEqual({ DefaultMonetaryUnit: "KRW" });
    });
});

// ---- chart data and tooltips ------------------------------------------------------------------
// Every `$("#id")` the compiled script reads is discovered from the script itself, so the fixture
// stays in step with it: value inputs get 1500, percentage inputs 12.5, localizers their own id.
function fullFixture(): string {
    const source = readFileSync(resolve(__dirname, "../../../../../../wwwroot/user/custom/Dashboard/UserIndex/js/site.js"), "utf8");
    const ids = [...new Set([...source.matchAll(/\$\("#(\w+)"\)/g)].map((m) => m[1]))];
    const base = fixture().replace(/<input[^>]*type="hidden"[^>]*id="regularIncome\w+Year"[^>]*>/g, "");
    const skip = new Set([...base.matchAll(/id="(\w+)"/g)].map((m) => m[1]));
    return (
        base +
        ids.filter((id) => !skip.has(id)).map((id) =>
            hidden(id, id.startsWith("percentageOf") ? "12.5" : id.startsWith("localizer") ? id : "1500")).join("")
    );
}

describe("Dashboard/UserIndex — donut charts", () => {
    it("each of the ten charts is a doughnut fed from its hidden inputs (one value per label)", () => {
        const h = loadSite("user", "Dashboard", "UserIndex", fullFixture());
        expect(h.charts).toHaveLength(10);
        for (const { cfg } of h.charts as { cfg: any }[]) {
            expect(cfg.type).toBe("doughnut");
            const [dataset] = cfg.data.datasets;
            expect(dataset.data.length).toBe(cfg.data.labels.length);
            expect(dataset.data.length).toBe(dataset.backgroundColor.length);
            expect(dataset.data.every((v: number) => v === 1500)).toBe(true);
        }
    });

    it("every tooltip reads 'Label: 1,500 (12.50%)' for each slice, using the server-supplied percentage", () => {
        const h = loadSite("user", "Dashboard", "UserIndex", fullFixture());
        let checked = 0;
        for (const { cfg } of h.charts as { cfg: any }[]) {
            const label = cfg.options.tooltips.callbacks.label;
            cfg.data.labels.forEach((name: string, index: number) => {
                expect(label({ index }, cfg.data)).toBe(`${name}: ${(1500).toLocaleString()} (12.50%)`);
                checked++;
            });
        }
        expect(checked).toBeGreaterThan(40);
    });
});

describe("Dashboard/UserIndex — period and currency controls", () => {
    it("changing the month reloads the dashboard for the chosen year and month", () => {
        const h = loadSite("user", "Dashboard", "UserIndex", fixture());
        h.$("#month").trigger("change");
        expect(h.navigations.at(-1)).toBe("/Dashboard/UserIndex?year=2024&month=5");
    });

    it("changing the currency reloads the dashboard whether or not the save succeeded", () => {
        const h = loadSite("user", "Dashboard", "UserIndex", fixture());
        h.$("#monetaryUnit").trigger("change");
        expect(h.lastAjax().headers).toEqual({ RequestVerificationToken: "tok" });
        h.respond(0, { result: true });
        h.$("#monetaryUnit").trigger("change");
        h.respond(0, { result: false });
        expect(h.navigations).toEqual(["/Dashboard/UserIndex?year=2024&month=5", "/Dashboard/UserIndex?year=2024&month=5"]);
    });
});
