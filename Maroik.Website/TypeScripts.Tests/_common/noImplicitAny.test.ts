// Guards the other half of the "no `any`" rule: values the libraries hand over. `TypeScripts/global.d.ts`
// (for the scripts) and `noImplicitAnyLibs.d.ts` (for this tree) redeclare the calls whose typings answer
// `any` so they answer `unknown` or the real type. Nothing at runtime notices if those overloads stop being
// the ones TypeScript picks (say a typings upgrade reorders them), so this compiles a probe that uses each
// value without stating a type, with the project's own `tsc`, and expects every use to be refused.
import { execFileSync } from "node:child_process";
import { mkdtempSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const websiteRoot = join(dirname(fileURLToPath(import.meta.url)), "..", "..");
const tsc = join(websiteRoot, "node_modules", "typescript", "bin", "tsc");

/** One untyped use per line; each must be a compile error. */
const PROBE = [
    `$.ajax({ url: "/x", success: function(data) { data.result; } });`,
    `JSON.parse("{}").result;`,
    `$("#x").data("id").length;`,
    `const checked: string = $("#x").prop("checked");`,
].join("\n");

/** Compiles `probe` together with `declarations` and returns tsc's diagnostics, one per line. */
function compileProbe(declarations: string, types: string[]): string[] {
    const dir = mkdtempSync(join(tmpdir(), "maroik-no-implicit-any-"));
    try {
        const probe = join(dir, "probe.ts");
        writeFileSync(probe, PROBE);
        try {
            execFileSync(process.execPath, [tsc, "--ignoreConfig", "--noEmit", "--strict", "--skipLibCheck", "--target", "ES2020",
                "--lib", "ES2020,DOM", "--types", types.join(","), "--typeRoots", join(websiteRoot, "node_modules", "@types"),
                declarations, probe], { cwd: websiteRoot, encoding: "utf8", stdio: "pipe" });
            return [];
        } catch (error) {
            return String((error as { stdout?: string }).stdout).split("\n").filter((line) => line.includes("probe.ts("));
        }
    } finally {
        rmSync(dir, { recursive: true, force: true });
    }
}

/** The probe lines (1-based) that tsc refused. */
function refusedLines(diagnostics: string[]): number[] {
    return [...new Set(diagnostics.map((line) => Number(/probe\.ts\((\d+),/.exec(line)![1])))].sort((a, b) => a - b);
}

describe("no implicit any from the libraries", () => {
    it("the scripts' global.d.ts makes every untyped use of a library value a compile error", () => {
        const diagnostics = compileProbe(join(websiteRoot, "TypeScripts", "global.d.ts"),
            ["jquery", "jqueryui", "jquery.validation", "toastr", "summernote", "chart.js"]);
        expect(refusedLines(diagnostics)).toEqual([1, 2, 3, 4]);
    }, 60000);

    it("the test tree's noImplicitAnyLibs.d.ts does the same for the calls the tests make", () => {
        const diagnostics = compileProbe(join(websiteRoot, "TypeScripts.Tests", "_common", "noImplicitAnyLibs.d.ts"), ["jquery"]);
        // `$.ajax` is not redeclared for the tests: they call the scripts' `success` callbacks themselves.
        expect(refusedLines(diagnostics)).toEqual([2, 3, 4]);
    }, 60000);
});
