// The page scripts read every `#localizer…` text as required (window.fieldValue throws when one is missing), and
// loadSite fills in the ones a fixture leaves out. This pins the other half of that contract: every localizer a
// script reads is rendered by its view, Views/{Feature}/{Page}.cshtml.
import { existsSync, readFileSync, readdirSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const websiteRoot = join(dirname(fileURLToPath(import.meta.url)), "..", "..");

/** Every page script, as [area, feature, page]. */
function pageScripts(): [string, string, string][] {
    const scripts: [string, string, string][] = [];
    for (const area of ["admin", "anonymous", "user"]) {
        const custom = join(websiteRoot, "TypeScripts", area, "custom");
        for (const feature of readdirSync(custom)) {
            for (const page of readdirSync(join(custom, feature))) {
                if (existsSync(join(custom, feature, page, "js", "site.ts"))) scripts.push([area, feature, page]);
            }
        }
    }
    return scripts;
}

describe("view contract", () => {
    it.each(pageScripts())("%s/%s/%s reads only localizers its view renders", (area, feature, page) => {
        const script = readFileSync(join(websiteRoot, "TypeScripts", area, "custom", feature, page, "js", "site.ts"), "utf8");
        const ids = [...script.matchAll(/\$\("#(localizer\w+)"\)/g)].map((match) => match[1]);
        if (ids.length === 0) return;
        const view = readFileSync(join(websiteRoot, "Views", feature, `${page}.cshtml`), "utf8");
        expect(ids.filter((id) => !view.includes(`"${id}"`))).toEqual([]);
    });
});
