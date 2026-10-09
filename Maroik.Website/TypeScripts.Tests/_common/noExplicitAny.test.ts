// Guards the "no `any`" rule for the client scripts and this test tree: every value is typed (or
// `unknown` and narrowed), so `strict` type-checking actually covers the code. tsc has no option that
// forbids an explicit `any`, hence this scan. It strips comments and string / template literals first,
// so prose such as "any AJAX error" in a comment does not count — only `any` written as code does.
import { mkdtempSync, readdirSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join, relative } from "node:path";
import { fileURLToPath } from "node:url";

const websiteRoot = join(dirname(fileURLToPath(import.meta.url)), "..", "..");

function tsFilesUnder(dir: string): string[] {
    return readdirSync(dir, { withFileTypes: true }).flatMap((entry) => {
        const path = join(dir, entry.name);
        if (entry.isDirectory()) {
            return tsFilesUnder(path);
        }
        return entry.name.endsWith(".ts") ? [path] : [];
    });
}

/**
 * Blanks out comments and string / template literal text, keeping line breaks so line numbers stay right.
 * The `${…}` expressions inside a template literal are code and are kept.
 */
function codeOnly(source: string): string {
    const blank = (text: string) => text.replace(/[^\n]/g, " ");
    // Brace depth at which each open `${` began, so its closing `}` is told apart from code braces.
    const templateExpressionDepths: number[] = [];
    let braces = 0;
    let out = "";
    let i = 0;

    /** Consumes template text from `i` up to the closing backtick or the next `${`. */
    function templateText() {
        let j = i;
        while (j < source.length && source[j] !== "`" && !(source[j] === "$" && source[j + 1] === "{")) {
            j += source[j] === "\\" ? 2 : 1;
        }
        out += blank(source.slice(i, j));
        if (source[j] === "`") {
            out += " ";
            i = j + 1;
        } else {
            out += "  ";
            i = j + 2;
            templateExpressionDepths.push(braces);
        }
    }

    while (i < source.length) {
        const c = source[i];
        const next = source[i + 1];
        if (c === "/" && next === "/") {
            while (i < source.length && source[i] !== "\n") {
                i++;
            }
        } else if (c === "/" && next === "*") {
            const end = source.indexOf("*/", i + 2);
            const stop = end === -1 ? source.length : end + 2;
            out += blank(source.slice(i, stop));
            i = stop;
        } else if (c === "/" && /(^|[(,=:[!&|?{};])\s*$/.test(source.slice(Math.max(0, i - 40), i))) {
            // A regex literal (where an expression starts): up to the closing "/" outside a [...] class.
            let j = i + 1;
            let inClass = false;
            while (j < source.length && (source[j] !== "/" || inClass)) {
                if (source[j] === "[") inClass = true;
                else if (source[j] === "]") inClass = false;
                j += source[j] === "\\" ? 2 : 1;
            }
            out += blank(source.slice(i, j + 1));
            i = j + 1;
        } else if (c === '"' || c === "'") {
            let j = i + 1;
            while (j < source.length && source[j] !== c) {
                j += source[j] === "\\" ? 2 : 1;
            }
            out += blank(source.slice(i, j + 1));
            i = j + 1;
        } else if (c === "`") {
            out += " ";
            i++;
            templateText();
        } else if (c === "}" && templateExpressionDepths.at(-1) === braces) {
            templateExpressionDepths.pop();
            out += " ";
            i++;
            templateText();
        } else {
            braces += c === "{" ? 1 : c === "}" ? -1 : 0;
            out += c;
            i++;
        }
    }
    return out;
}

function explicitAnyLines(file: string): string[] {
    const anyWord = new RegExp("(?<![\\w$.])any(?![\\w$])");
    return codeOnly(readFileSync(file, "utf8"))
        .split("\n")
        .flatMap((line, index) => (anyWord.test(line) ? [`${relative(websiteRoot, file)}:${index + 1}`] : []));
}

/** Lines that assert a type (`x as T`, `as const` aside) or non-null (`x!`) instead of checking it. */
function assertionLines(file: string): string[] {
    const assertion = new RegExp("\\bas\\s+(?!const\\b)[\\w$({\\[]|(?<=[\\w$)\\]])!(?!=)");
    return codeOnly(readFileSync(file, "utf8"))
        .split("\n")
        .flatMap((line, index) => (assertion.test(line) ? [`${relative(websiteRoot, file)}:${index + 1}`] : []));
}

describe("no explicit any", () => {
    it("codeOnly blanks comments and literals but keeps code", () => {
        const sample = 'let a: any = 1; // any\nconst s = "any"; /* any */ const t = `any`;';
        expect(codeOnly(sample)).toContain("let a: any = 1;");
        expect(codeOnly(sample).split("\n")[1]).not.toContain("any");
    });

    it("codeOnly skips a regex literal, quotes inside it included", () => {
        const sample = 'const q = text.replace(/"/g, "&quot;"); const words = "is posted as multipart";';
        expect(codeOnly(sample)).toContain("const q = text.replace(");
        expect(codeOnly(sample)).not.toContain("as multipart");
    });

    it("codeOnly keeps the code inside a template literal's ${} expressions", () => {
        const sample = "const html = `any ${list.map((t: any) => `<b>${t}</b>`).join(\"\")} any`;";
        expect(codeOnly(sample)).toContain("list.map((t: any) =>");
        expect(codeOnly(sample).match(/\bany\b/g)).toHaveLength(1);
    });

    it.each(["TypeScripts", "TypeScripts.Tests"])("%s/**/*.ts declares nothing as any", (folder) => {
        const offenders = tsFilesUnder(join(websiteRoot, folder)).flatMap(explicitAnyLines);
        expect(offenders).toEqual([]);
    });
});

describe("no type assertions", () => {
    it("assertionLines finds `as T` and `x!` but not `as const`, `!x` or `!==`", () => {
        const dir = mkdtempSync(join(tmpdir(), "maroik-assertions-"));
        try {
            const file = join(dir, "probe.ts");
            writeFileSync(file, ["const a = b as string;", "const c = d!.e;", "const f = [1] as const;", "if (!g && h !== i) {}"].join("\n"));
            expect(assertionLines(file).map((line) => line.split(":").at(-1))).toEqual(["1", "2"]);
        } finally {
            rmSync(dir, { recursive: true, force: true });
        }
    });

    // Every value the scripts read is checked (window.check / fieldValue / required / byId, …) instead, and the
    // tests read what a script left behind through the harness's present / instanceOfType / successOf, …
    it.each(["TypeScripts", "TypeScripts.Tests"])("%s/**/*.ts neither asserts a type nor asserts non-null", (folder) => {
        expect(tsFilesUnder(join(websiteRoot, folder)).flatMap(assertionLines)).toEqual([]);
    });
});
