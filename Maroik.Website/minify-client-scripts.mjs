// Minifies the custom client scripts in place: every wwwroot/{area}/custom/**/js/site.js that tsc emitted
// from a TypeScripts/{area}/custom/**/js/site.ts is overwritten with its terser output.
//
// Runs as the second half of `npm run build` (the release build), so the minified files are what every
// release consumer gets: the Dockerfile tsbuild stage (the production image), a Release .csproj build, and
// `npm test` (pretest runs the build), so the Vitest suite and the CI E2E suite exercise the minified
// scripts. `npm run build:debug` / `npm run watch` do not run it: their site.js stays readable and keeps
// its source map for Chrome DevTools.
//
// Each site.js is a plain global <script> wrapped in an IIFE (tsconfig.json: moduleDetection "legacy"), so
// top-level names are left alone (mangle.toplevel false) and only the IIFE's locals are renamed. Strings are
// always printed with double quotes: tests find `$("#id")` reads in the compiled script with that pattern.
import { readFileSync, readdirSync, statSync, writeFileSync, existsSync } from "node:fs";
import { dirname, join, relative } from "node:path";
import { fileURLToPath } from "node:url";
import { minify } from "terser";

const root = dirname(fileURLToPath(import.meta.url));
const sourceRoot = join(root, "TypeScripts");
const outputRoot = join(root, "wwwroot");

/** Every TypeScripts/{area}/custom/**\/site.ts, as paths relative to TypeScripts/. */
function siteSources(dir = sourceRoot) {
    return readdirSync(dir).flatMap((name) => {
        const path = join(dir, name);
        if (statSync(path).isDirectory()) return siteSources(path);
        const rel = relative(sourceRoot, path).replaceAll("\\", "/");
        return name === "site.ts" && rel.split("/")[1] === "custom" ? [rel] : [];
    });
}

const sources = siteSources();
if (sources.length === 0) {
    console.error(`minify: no site.ts found under ${sourceRoot}`);
    process.exit(1);
}

let before = 0;
let after = 0;
for (const source of sources) {
    const file = join(outputRoot, source.replace(/\.ts$/, ".js"));
    if (!existsSync(file)) {
        console.error(`minify: ${relative(root, file)} is missing; run tsc first (npm run build does)`);
        process.exit(1);
    }
    const code = readFileSync(file, "utf8");
    const result = await minify(code, {
        ecma: 2020,
        module: false,
        compress: true,
        mangle: { toplevel: false },
        format: { comments: false, quote_style: 2 },
    });
    if (typeof result.code !== "string" || result.code.length === 0) {
        console.error(`minify: terser returned no output for ${relative(root, file)}`);
        process.exit(1);
    }
    writeFileSync(file, result.code + "\n");
    before += Buffer.byteLength(code);
    after += Buffer.byteLength(result.code) + 1;
}

console.log(`minify: ${sources.length} site.js minified, ${before} -> ${after} bytes`);
