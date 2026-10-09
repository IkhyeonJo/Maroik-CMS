# TypeScripts/ — custom client scripts

## Prerequisites — Node.js (one-time setup, written for a non-JS dev)

**You can skip all of this if you only build through Docker Compose** — the Dockerfile
`tsbuild` stage compiles the scripts itself (see **Build integration**). The compiled
`wwwroot/**/site.js` is build output and is **not** committed, so you need the steps
below to **run the site on the host** (`dotnet run`, Rider/VS "Fast Mode" container
debugging), to **edit a `.ts` file and see its `.js`**, or to **run the client-script tests**.

**Why it isn't automatic:** `tsc` (the TypeScript compiler) is not a system-wide
tool here. `tsc`, Vitest, jsdom and every `@types/*` package are listed in
`Maroik.Website/package.json` and get downloaded into a local `node_modules/`
folder by `npm`. `npm` is bundled with Node.js. No Node ⇒ nothing to run ⇒ a
fresh checkout's `.ts` files can't be compiled.

### 1. Install Node.js 22

The build uses Node 22 everywhere else (`Maroik.Website/Dockerfile` →
`FROM node:22-alpine`),
so match it. `package.json` `"engines"` requires `node >=22.22.2 <23` and
`Maroik.Website/.npmrc` sets `engine-strict=true`, so **any other version stops
`npm ci` immediately** with `npm error code EBADENGINE` — nothing gets installed,
just switch Node and re-run.

* **Windows / macOS:** download the **LTS** build from <https://nodejs.org> (the
  left, green "…LTS" button — **not** "Current", which is Node 23/24 and will be
  rejected). Run the installer, then **open a brand-new terminal window** so that
  `node` is picked up on `PATH`.
* **Linux, or macOS via [nvm](https://github.com/nvm-sh/nvm):**
  `nvm install 22 && nvm use 22`
  (Windows has a separate tool,
  [nvm-windows](https://github.com/coreybutler/nvm-windows): `nvm install 22.22.2`).
* **Confirm it worked** — both commands must print a version:

  ```console
  $ node -v
  v22.22.2      ← any v22.x, as long as it is 22.22.2 or higher
  $ npm -v
  10.9.8        ← any number is fine
  ```

### 2. Download the toolchain — `npm ci`

Do this from the **`Maroik.Website/`** folder (the one containing `package.json`).

> ⚠️ The repo root also has a `Maroik.WebSite/` with a capital **S** — a
> different, unrelated folder. Make sure you are in **`Maroik.Website/`**.

```console
$ npm ci
```

`npm ci` reads `package-lock.json` and installs those exact package versions into
`Maroik.Website/node_modules/` (~105 packages; the folder is git-ignored). Run it **once after a fresh checkout**, and
again whenever `package.json` or
`package-lock.json` changes. A successful run looks like:

```
added 105 packages, and audited 106 packages in 1s

29 packages are looking for funding
  run `npm fund` for details

found 0 vulnerabilities
```

`N packages are looking for funding` is just an advert for donation links — it is
not a warning, ignore it. `found 0 vulnerabilities` is the line that matters.

### 3. Compile `.ts` → `.js` — `npm run build`

```console
$ npm run build
```

Runs `tsc`, which (re)writes
`wwwroot/{area}/custom/{Feature}/{Page}/js/site.js` for every matching `.ts`. **A clean build prints nothing and just
returns to the prompt.** A type error
prints `path/file.ts(line,col): error TS####: <message>` and exits non-zero — fix
the `.ts` and re-run.

Related: `npm run watch` (stay running, recompile on every save),
`npm run check` (type-check only, writes no files).

### 4. Run the tests — `npm test`

```console
$ npm test
```

This first re-runs `npm run build` (an automatic `pretest` step), then runs the
Vitest + jsdom suite in `TypeScripts.Tests/`. A successful run ends with:

```
 Test Files  22 passed (22)
      Tests  58 passed (58)
```

While it runs you will also see ~30 lines of
`Not implemented: HTMLCanvasElement's getContext() ...` scroll past. **That is
not a failure** — it is jsdom saying it has no `<canvas>` engine, which the
Chart.js code brushes against. Only the `Test Files … passed` / `Tests … passed`
summary at the end counts; a genuine failure prints `FAIL` and names the test.

Run a subset while iterating:
`npx vitest run TypeScripts.Tests/user/custom/AccountBook`.

`TypeScripts.Tests/` is **not** a separate npm package — it shares this same
`package.json` / `node_modules/`, so the one `npm ci` from step 2 already set it
up. Its own **`README.md`** has the details.

### If you skip this

`dotnet build` still succeeds — with no `node_modules/` present the MSBuild target
that would call `tsc` is a silent no-op — but no `wwwroot/**/site.js` is produced, so a
host-run site (`dotnet run`, Fast Mode) serves pages without their custom scripts.
Docker Compose builds are unaffected (see **Build integration**).

---

The 22 handwritten client scripts under `wwwroot/{admin,anonymous,user}/custom/**/js/site.js`
are authored here as TypeScript and compiled back to the exact same paths.

```
Maroik.Website/
  TypeScripts/{area}/custom/{Feature}/{Page}/js/site.ts   ── source (this tree)
                │  tsc  (rootDir: TypeScripts, outDir: wwwroot)
                ▼
  wwwroot/{area}/custom/{Feature}/{Page}/js/site.js        ── generated, git-ignored
```

No other `wwwroot` asset is touched. Vendored libraries (jQuery, AdminLTE, FullCalendar,
Summernote, MvcGrid, …) stay exactly as they are and are **not** compiled or bundled.

---

## Code style

Whitespace / EOL / quotes / semicolons for `*.ts` are pinned in the **repo-root
`.editorconfig`** ("TypeScript / JavaScript" section) — standard keys plus the
`ij_typescript_*` / `ij_javascript_*` keys that are Rider's / WebStorm's code-style
mechanism (Rider reads JS/TS style straight from `.editorconfig`). `TypeScripts/**`
is narrowed there to **whitespace-only** rules so a reformat-on-save can't churn the
byte-faithful port; `TypeScripts.Tests/**` is 2-space; the `tsc` output (`wwwroot/**/custom/**/site.js`) is
`generated_code = true`. `Maroik.sln.DotSettings`
carries the few ReSharper-side JS/TS toggles that aren't editorconfig-backed (e.g. TS interfaces take **no** `I` prefix,
unlike C#).

## Tech stack

| Purpose                 | Tool                                           | Version                 | Notes                                                                                                                      |
|-------------------------|------------------------------------------------|-------------------------|----------------------------------------------------------------------------------------------------------------------------|
| Type-check **and** emit | **TypeScript** (`tsc`)                         | **7.0.2**               | The native (Go) compiler — the whole `TypeScripts/` tree type-checks + emits in well under a second. No Babel, no bundler. |
| Type-check-only gate    | `tsc --noEmit`                                 | —                       | `npm run check`.                                                                                                          |
| Tests                   | **Vitest** + **jsdom** + real **jQuery 3.6.0** | 4.1.11 / 30.0.1 / 3.6.0 | See `../TypeScripts.Tests/`.                                                                                               |

### `@types/*` — pinned to the runtime versions actually loaded by `Views/Shared/_Layout.cshtml`

| Loaded script (`wwwroot/{area}/…`)                  | Runtime version                   | Dev dependency                                                                                                                                                                                                                              |
|-----------------------------------------------------|-----------------------------------|---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `plugins/jquery/jquery.min.js`                      | jQuery 3.6.0                      | `@types/jquery@3.5.34` — the 3.5.x line targets jQuery 3.x; `@types/jquery@4` is for jQuery 4 and would be wrong.                                                                                                                           |
| `plugins/jquery-ui/jquery-ui.min.js`                | jQuery UI 1.12.1                  | `@types/jqueryui@1.12.24`                                                                                                                                                                                                                   |
| `lib/jquery-validation/dist/jquery.validate.min.js` | jquery-validation 1.17.0          | `@types/jquery.validation@1.17.1`                                                                                                                                                                                                           |
| `plugins/bootstrap/js/bootstrap.bundle.min.js`      | Bootstrap 4.6.0                   | none — the scripts only call `$el.modal(...)`, hand-typed in `global.d.ts`. `@types/bootstrap` was dropped: it pulled in the deprecated `popper.js@1` package to type the rest of the Bootstrap 4 surface, which these scripts never touch. |
| `plugins/toastr/toastr.min.js`                      | toastr 2.1.4                      | `@types/toastr@2.1.44`                                                                                                                                                                                                                      |
| `plugins/summernote/summernote-bs4.js`              | summernote 0.8.18                 | `@types/summernote@0.8.10` (latest published)                                                                                                                                                                                               |
| `plugins/chart.js/Chart.min.js`                     | Chart.js 2.9.4                    | `@types/chart.js@2.9.41` — the 2.9.x line targets Chart.js v2; `@types/chart.js@4` is for v3+.                                                                                                                                              |
| `plugins/fullcalendar/main.js`                      | FullCalendar 5.5.1                | `@fullcalendar/core@5.5.1` (installed type-only; global `<script>` build is used at runtime)                                                                                                                                                |
| `plugins/moment/moment.min.js`                      | moment 2.30.1                     | `moment@2.30.1` (ships its own `.d.ts`; installed type-only). Bumped from 2.29.1 for CVE-2022-24785 / CVE-2022-31129; 2.30.1 is the last 2.x and a drop-in for global `<script>` use.                                                       |
| `js/mvc-grid/nonfactors-mvc-grid.js`                | NonFactors.Grid.Mvc6 companion JS | not on npm → hand-written ambient in `global.d.ts`                                                                                                                                                                                          |
| `dist/js/adminlte.js`                               | AdminLTE 3.1.0                    | no types — the custom scripts never call the AdminLTE JS API directly                                                                                                                                                                       |

`global.d.ts` declares the globals with no matching `@types`: `MvcGrid`, `FullCalendar`,
and `moment`, plus a `JQuery.modal(...)` augmentation standing in for `@types/bootstrap`.

---

## How the migration was done (goal: zero behavior change)

Every `site.ts` is the original `site.js` **wrapped in one IIFE**, with the smallest set
of type annotations needed to pass `tsc --strict`. Type syntax erases at compile time, so
the emitted `site.js` is byte-for-byte the original **modulo**:

* a leading `"use strict";` (from `alwaysStrict`),
* the `(function () { … })();` wrapper,
* **comments dropped** (`removeComments: true`) — write freely in the `.ts`; the shipped
  `.js` carries none,
* `tsc`'s own reformatting — it drops blank lines, splits `if (x) return;` onto two lines,
  parentheses single arrow params `(e) =>`, strips the UTF-8 BOM, and adds ASI semicolons.

None of that changes semantics. At migration time every one of the 22 files was proved
equivalent by normalizing the pre-migration original and the compiled output to a
whitespace-/semicolon-/comment-free token stream (minus the wrapper) and diffing — all matched.
Two deliberate follow-up changes have since landed (see "Deliberate post-port changes" below):
the `localizer.prevText` / `.nextText` casing fix, and removal of the dead `data.calendarEvent.calendarId`
line from the two Calendar `select` handlers — so those files' compiled `site.js` no longer
token-matches the pre-migration original, which is intentional. Going forward, `tsc` (types)
and `../TypeScripts.Tests` (behavior) are the guards; a plain `git diff wwwroot/**/site.js`
shows exactly what a `.ts` edit changed in the output.

### Why an IIFE and no `Common/` module

All 22 scripts stay plain global `<script>` files, exactly as today (`moduleDetection: "legacy"`,
no `import`/`export` anywhere). Loading one `tsc` "program" over all 22 would otherwise collide
on the many same-named top-level helpers (`localizer`, `ApplyAllowedOptions`, `base64ToBlob`,
`ChangeCulture`, …). Wrapping each file's body in an IIFE isolates those scopes. This is safe
here because **no Razor view uses an inline `on*=` handler** and **no script references another
script's globals** — verified before starting. Shared helpers therefore stay duplicated
per file (just typed); there is deliberately no shared module.

### The recurring annotations

There is no `any` anywhere in `TypeScripts/` or `TypeScripts.Tests/` (explicit `any` written in code; comments
and strings do not count). `TypeScripts.Tests/_common/noExplicitAny.test.ts` fails on one, since `tsc` has no
option that forbids it. Values a library or the server leaves untyped are given a type here: the shared
ones (server payloads, plugin gaps) live in `global.d.ts`.

| Original pattern                                                                                         | Becomes                                                                              | Reason                                                                                         |
|----------------------------------------------------------------------------------------------------------|--------------------------------------------------------------------------------------|------------------------------------------------------------------------------------------------|
| `function f(x) {` (DOM-interop param)                                                                    | `function f(x: string \| HTMLSelectElement \| …)` / `x?: string`                    | `noImplicitAny`                                                                                |
| `$(sel).val()` used as a string (`location.href =`, `input.value`, ajax `headers`, `parseInt`, `.split`) | `$(sel).val() as string`                                                             | `.val()` is `string \| number \| string[] \| undefined` (`FormFieldValue` in `global.d.ts`)  |
| `$(this).attr('x')` assigned to a `string` / before `.includes()`                                        | `$(this).attr('x')!`                                                                 | `.attr()` is `string \| undefined`; the attribute is always present                           |
| `toastr.error(msg)` where `msg?: string`                                                                 | `toastr.error(msg!)` (inside the guard where it's set)                               | `toastr.error`'s message param is non-optional                                                 |
| `const localizer = { … 44 × $(sel).val() }`                                                              | `const localizer = { … } as Record<string, string>`                                  | every hidden input holds a string (see the casing fix under "Deliberate post-port changes")    |
| `.datepicker({ … beforeShow/onSelect … })` instance call                                                 | no cast; `onSelect: function (this: HTMLInputElement)`                               | `DatepickerSetupOptions` (`global.d.ts`) accepts the void `beforeShow` jQuery UI allows        |
| `$.datepicker._clearDate(x)`                                                                             | as is                                                                                | the undocumented internal is declared on `JQueryUI.Datepicker` in `global.d.ts`                |
| `new FullCalendar.Calendar(document.getElementById('calendar'), {…})`                                    | `… getElementById('calendar')!, {…}` + `FullCalendarEventClickArg` / `…DateSelectArg` | null-guard; FullCalendar's own types, aliased in `global.d.ts`                                 |
| `JSON.parse(…).forEach((item) => …)` / `$.each(response.calendars, …)`                                   | `(item: CalendarEventJson)`, `(_, calendar: CalendarSummary)`, …                     | the calendar payloads, mirrored from the C# view models / DTOs in `global.d.ts`                |
| `formData.append("X", value)` with a `.val()` / boolean / missing file                                   | as is                                                                                | `global.d.ts` adds the `FormData.append` overload for the values it stringifies                |
| `let x;` (no initialiser) / `let arr = [];` reused                                                       | `let x: FormFieldValue;` / `let arr: CalendarReminderFormValue[] = [];`              | `noImplicitAny` on evolving types                                                              |
| `htmlDoc.querySelectorAll('img[data-file]')` then `imgTag.src`                                           | `.querySelectorAll<HTMLImageElement>('img[data-file]')`                              | `Element` has no `.src`                                                                        |

Typing the scripts this way changed the compiled `site.js` in three places, none of them in behaviour:
`data: null` is no longer passed to `$.ajax` (jQuery only tests `data` for truthiness), a calendar event's
`id` is passed as `String(item.Id)` (FullCalendar 5 runs `id` through `String` itself), and the reminder
validators call `isNaN(Number(value))` (global `isNaN` applies the same `ToNumber`).

### Deliberate post-port changes

Two fixes on top of the byte-faithful port. Both are DOM/widget glue, not business rules,
so they stay here and not in `Maroik.Core.*`. Every other `.ts` still compiles to its
pre-migration `site.js` modulo the formatting listed above.

1. **`localizer.prevText` / `.nextText` casing** — the `localizer` object defines
   `PrevText` / `NextText`, but `$.datepicker.setDefaults` read them with a lower-case
   first letter. Corrected to the matching casing in the 6 files that call `setDefaults`
   (`admin/Calendar/AdminIndex`, `user/Calendar/UserIndex`, `user/Notice/FixedIncome`,
   `user/Notice/FixedExpenditure`, `user/AccountBook/Income`, `user/AccountBook/Expenditure`).

2. **Dead `data.calendarEvent.calendarId` line removed** from the FullCalendar `select`
   handler in `admin/Calendar/AdminIndex` and `user/Calendar/UserIndex`. The line
   `$createCalendarEventMyCalendar.val(data.calendarEvent.calendarId)` was copy-pasted
   from the sibling `eventClick` (edit) handler, where `data` is the AJAX `success`
   parameter. In `select` that callback is named `response` (the endpoint,
   `CalendarController.GetCalendars`, returns `{ result: true, calendars }` — no
   `calendarEvent`), and no enclosing scope or global binds `data`, so the line threw
   `ReferenceError` and aborted the rest of the synchronous handler (the modal never
   opened on drag-select). Removed; a brand-new event has no calendar to pre-select and
   the `<select>` is freshly repopulated. The `eventClick` uses of `data.calendarEvent.*`
   are untouched — `data` is a real parameter there. With no free `data` left,
   `declare const data: any;` was dropped from `global.d.ts`.

---

## Commands

Run from `Maroik.Website/`:

```bash
npm ci            # once, after checkout
npm run build     # tsc -p tsconfig.build.json → writes wwwroot/**/site.js  (release: NO source maps)
npm run build:debug  # tsc -p tsconfig.json    → same + wwwroot/**/custom/**/site.js.map  (for Chrome)
npm run watch     # build:debug, --watch
npm run check     # tsc --noEmit  (type-check only, for CI)
npm test          # npm run build + vitest run   (see ../TypeScripts.Tests)
```

## Debugging in Chrome

Building with `tsconfig.json` (`sourceMap` + `inlineSources`) writes `site.js.map` next to
each `wwwroot/{area}/custom/{Feature}/{Page}/js/site.js`, appends a
`//# sourceMappingURL=site.js.map` comment to the `site.js`, and embeds the original `.ts`
text in the map. ASP.NET Core's static-file middleware serves the `.map`, so Chrome
DevTools → **Sources** shows the real `site.ts` — breakpoints, stepping, original
identifiers. Nothing from `TypeScripts/` needs to be served; the source is inside the map.

**Running locally (`dotnet run`):** `npm run watch` in `Maroik.Website/` alongside it, then
hard-reload the page (DevTools open, "Disable cache" on). `npm run build:debug` is the
one-shot equivalent.

**Debugging in the container (`docker-compose.debug.yml`, Rider/VS):** that compose file
passes `--build-arg TS_SOURCEMAPS=true`, so the Dockerfile `tsbuild` stage runs
`npm run build:debug` and the maps are baked into the image and published as normal
`wwwroot` content. Nothing else to do — rebuild the `maroik.website` image after changing
this and the maps are there. A release image build leaves `TS_SOURCEMAPS` at its default `false`.

**Nothing debug-related reaches production.** With `TS_SOURCEMAPS` unset/`false` —
`npm run build`, a Release `dotnet build`, a release Docker build — `tsc` runs `tsconfig.build.json`: no `.map`, no
`sourceMappingURL` comment. On top of that `.dockerignore` keeps host maps out of the build context.
Both `site.js` and `site.js.map` under `Maroik.Website/wwwroot/**/custom/**` are git-ignored.

## Build integration

* **`dotnet build` / `dotnet run`** — `Maroik.Website.csproj` has a `CompileClientTypeScript`
  target (`BeforeTargets="BeforeBuild"`) that runs `npm run build`, **guarded** by
  `Exists('$(MSBuildProjectDirectory)/node_modules/typescript')`. So it compiles on dev
  machines that ran `npm ci`, and is a silent no-op everywhere else (a TS error there fails
  the .NET build — that's intended). Force it with `-p:BuildClientTypeScript=true`.
* **Docker** — `Dockerfile` has a dedicated `node:22-alpine` `tsbuild` stage (`npm ci && npm run build`) whose
  `wwwroot/` output is `COPY --from=tsbuild` over the
  SDK build stage's copy. The .NET SDK image has no Node, so the csproj target stays a no-op.
* **`dotnet publish` with no Node at all** — produces a site without the custom scripts. Don't
  deploy that way; deploy through the Docker image.

`wwwroot/**/custom/**/site.js` (and `.map`), `node_modules/`, `*.tsbuildinfo` and `.vitest/`
are git-ignored in the repo-root `.gitignore`.

### Project-file hygiene

`Maroik.Website.csproj` keeps the Node/TS toolchain out of the .NET project:
`DefaultItemExcludes` drops `node_modules\**` from every glob (so `dotnet publish`
doesn't drag ~75 packages' `package.json` files in via the Web SDK's `**\*.json`
Content rule, and Solution Explorer doesn't choke), `TypeScriptCompileBlocked=true`
stops VS's bundled TypeScript build from double-compiling `TypeScripts/**`, and
`package.json` / `tsconfig.json` / `tsconfig.build.json` / `TypeScripts/**` /
`TypeScripts.Tests/**` are `<Content Remove>`d — still visible in the tree (as `None`),
just not shipped. The compiled `wwwroot/**/site.js` publishes as normal `wwwroot` content,
and so does `wwwroot/**/custom/**/site.js.map` **when present** — it is deliberately not
`<Content Remove>`d, because the container-debug build (`TS_SOURCEMAPS=true`) needs it
published; a release build simply produces no `.map`.

---

## Tests

Live in **`../TypeScripts.Tests/`** with their own **`README.md`**. In short: a Vitest +
jsdom suite, one `*.test.ts` per `site.ts` at the mirrored path, running the *compiled*
`site.js`. `npm test` (from `Maroik.Website/`) builds then runs them; `dotnet build` /
Rider / VS run them too (incrementally, on by default — see `TypeScripts.Tests/README.md`).
