# TypeScripts.Tests/ — client-script tests

## Prerequisites — Node.js (written for a non-JS dev)

This suite has **no `.csproj`**, is **not** its own npm package, and is not in
`Maroik.sln`. Vitest, jsdom, jQuery and the `typescript` compiler are npm **devDependencies of
`Maroik.Website/package.json`**, and every command below is
run from **`Maroik.Website/`** (the folder with `package.json` — *not* the
capital- **S** `Maroik.WebSite/` at the repo root). Nothing here runs without
Node.js on the machine.

**Full install walkthrough — with expected output for each step — is in
[`../TypeScripts/README.md`](../TypeScripts/README.md) ("Prerequisites").** The
short version:

1. **Install Node.js 22** (LTS). The build uses Node 22 everywhere (`Maroik.Website/Dockerfile` → `FROM node:22-alpine`).
   `package.json` `"engines"` requires `node >=22.22.2 <23` and
   `Maroik.Website/.npmrc` has `engine-strict=true`, so any other version makes
   the next step stop with `npm error code EBADENGINE` (nothing installed — just
   switch Node and retry). On Windows, install the **LTS** build from
   <https://nodejs.org>, then open a **new terminal**. Check: `node -v` prints
   `v22.22.2` or newer 22.x.

2. **`npm ci`** from `Maroik.Website/` — installs ~105 packages into
   `node_modules/` (git-ignored). This **one** installation covers both `TypeScripts/`
   and this test tree. Success ends with `found 0 vulnerabilities`.

3. **`npm test`** from `Maroik.Website/` — see **Running** below.

`TypeScripts.Tests/tsconfig.json` is standalone for **editor / `tsc --noEmit`
type-checking only** — it emits nothing and needs no separate installation.

If you never run `npm ci`, `dotnet build` still succeeds — with `node_modules/`
absent the MSBuild target that would run the suite is a silent no-op (see **Running → In a build**), so a missing Node
install is quiet, not a build error.

---

Vitest + jsdom tests for the custom client scripts. They run the **compiled**
`wwwroot/{role}/custom/{Feature}/{Page}/js/site.js` (not the `.ts`) inside a jsdom
window against a minimal DOM fixture, so they exercise the exact script that ships.

See `../TypeScripts/README.md` for the migration itself.

---

## Layout — a 1:1 mirror of `wwwroot`

```
TypeScripts.Tests/
  _common/
    harness.ts            loadSite() + fixture helpers + jsdom stubs   (test infra, no source counterpart)
    vitest.setup.ts       runs once before each test file
    *Suite.ts             behaviour shared by several scripts (see "Shared suites")
  {role}/custom/{Feature}/{Page}/js/site.test.ts   ⟷  wwwroot/{role}/custom/{Feature}/{Page}/js/site.js
  tsconfig.json           standalone (the main tsconfig excludes this tree)
```

**Every `site.js` has exactly one `site.test.ts` at the matching path** (22 of each), and the only
files that don't mirror a source path are under `_common/`. Each test file builds its own DOM fixture;
scripts that share behavior (the two board scripts × 3 roles, the grid-CRUD finance pages, the two
calendar scripts, the three layouts, the two profile pages) call a shared suite from `_common/` so the
behavior is asserted once, identically, for each script instead of being copied.

Imports use the **`@tests/*` alias** (`== TypeScripts.Tests/*`), so a helper import
reads the same from any depth. It's declared twice, keep them in sync:

* `../vitest.config.mts` → `resolve.alias` (runtime)
* `./tsconfig.json` → `baseUrl` + `paths: { "@tests/*": ["*"] }` (editor / type-check)

### Shared suites (`_common/*Suite.ts`)

| Suite                | Used by                                                                       | Asserts                                                                                                              |
|----------------------|-------------------------------------------------------------------------------|----------------------------------------------------------------------------------------------------------------------|
| `boardSuite`         | Forum/FreeForum and Management/PrivateNote (user + admin)                     | search, write/edit/delete/comment requests, attachments, image upload, inline-image rehydration, attachment download |
| `gridCrudSuite`      | AccountBook/{Asset,Income,Expenditure}, Notice/{FixedIncome,FixedExpenditure} | row highlight, create/edit/delete round trips, refusals, maturity-date pickers                                       |
| `amountLabelSuite`   | the four finance pages with an amount label                                   | label lookup on the create and edit forms, success / refusal / failure                                               |
| `calendarSuite`      | Calendar/{UserIndex,AdminIndex}                                               | reminder validation, create/edit event, my calendars, popup, editors, downloads                                      |
| `layoutSuite`        | the three `_Layout` scripts                                                   | culture switch, double-submit guard, session-expiry recovery                                                         |
| `profileSuite`       | Management/Profile (user + admin)                                             | avatar upload rules, time zone, password                                                                             |
| `missingConfigSuite` | every script that reads server-published constants                            | still initialises when a constant is absent (and no swallowed ready-callback exception)                              |

---

## The harness (`_common/harness.ts`)

`loadSite(role, feature, page, fixtureHtml, opts?)`:

1. resets `document.body` to `fixtureHtml`,
2. puts a **real jQuery 3.6.0** on `window` (matching the runtime) + chainable no-op
   stubs for every vendored plugin the scripts call — `tabs` / `modal` / `tooltip` /
   `datepicker` / `summernote` / `valid` — and stubs the `toastr`, `MvcGrid`,
   `FullCalendar`, `moment`, `Chart` globals and `$.datepicker`,
3. captures `$.ajax` calls (`h.ajaxCalls`, `h.lastAjax()`, `h.respond()`),
4. patches the browser bits jsdom lacks or refuses: `window.location.href`
   (→ `h.navigations`), `HTMLFormElement.submit` (→ `h.submittedForms`), `alert`,
   `confirm`, `URL.createObjectURL`,
5. **fake-timer-flushes** the eval — jQuery defers `$(fn)` via `setTimeout` when the
   document is already `complete` (it is, in jsdom), so the harness wraps the eval in
   `vi.useFakeTimers()` + `vi.runOnlyPendingTimers()` to run those ready-callbacks (where most Calendar/Forum wiring
   lives) synchronously before the test asserts,
6. `window.eval`s the compiled `site.js` (it self-executes — it's the IIFE-wrapped port).

Returned `SiteHandle`: `$`, `win`, `ajaxCalls` / `respond` / `lastAjax`, `toastr`,
`charts`, `submittedForms`, `navigations`, `MvcGridInstances`, plus what the vendored widgets were
asked to do: `calendarOptions` / `calendarInstances` (FullCalendar), `datepickerInits`,
`summernoteInits` / `summernoteCalls` (editor callbacks and `code` / `insertNode` calls;
`opts.summernoteCode` supplies the editor's initial HTML).
Also exported: `hidden(id, value)`, `antiForgery`, `hiddenByStyle(el)`,
`fireNative(target, type, detail)`.

### jsdom gotchas the tests work around

* **no layout** → `:visible` / `:hidden` are always false/true. Use
  `hiddenByStyle(el)` (reads `style.display`), not jQuery pseudo-selectors.
* **MvcGrid events** are native `CustomEvent`s (`el.dispatchEvent`), not
  `$.trigger` — use `fireNative(document, "rowclick", { data: { … } })`.
* mutating `$.fn.*` in a test leaks within the file; the harness re-assigns the
  stubs on every `loadSite`, so call it fresh per test.

---

## What is tested

All 22 scripts, to **100 % of statements and functions** (and ~99.5 % of branches — the few left are arms
the UI cannot reach, e.g. an unknown reminder unit that form validation rejects first). Per script:

* **server-policy UX mirrors** — income/expenditure subclass filtering (`ApplyAllowedOptions`),
  fixed-schedule deposit-day limits (`DepositDayValues`), the calendar reminder lead-time bounds (`Validate*` vs
  `CalendarReminderPolicy`), the size / type limits,
* **form / request wiring** — every create / edit / delete / export request has the right URL, headers and
  JSON / `FormData`; an invalid form makes no request; a refused or failed reply is toasted, never crashes,
* **DOM behavior** — modals, popups, calendar list order, reminder rows rebuilt from stored data, attachments
  and inline images (object URLs are released once an image loads), downloads, date pickers,
* **defaults** — a script still initializes when a server-published constant is missing.

## Measuring coverage

The suite `eval`s the compiled `site.js`, so Vitest's own v8 coverage cannot see it. To measure, instrument the
script inside `loadSite` (e.g. with `istanbul-lib-instrument`) behind an environment variable and merge each
window's `__coverage__`; keep that out of the repository — it is a measuring aid, not part of the suite.

## Adding a test

1. `TypeScripts.Tests/{role}/custom/{Feature}/{Page}/js/site.test.ts` (mirror the source).
2. `import { loadSite, … } from "@tests/_common/harness";`
3. Build a fixture with just the elements the assertion needs (`antiForgery + hidden(…) + \`<html>\``).
4. `const h = loadSite("{role}", "{Feature}", "{Page}", fixture);` then trigger events / drive
   `h.respond(0, { result: true, … })` and assert on `h.ajaxCalls` / `h.navigations` /
   `h.submittedForms` / `h.toastr` / the DOM.

---

## Running

```bash
npm test          # from Maroik.Website/ — runs `npm run build` (pretest) then `vitest run`
npm run test:watch
npx vitest run TypeScripts.Tests/user/custom/AccountBook   # a subset
```

`npm test` recompiles the `.ts` first (the `pretest` hook) because the suite runs
the **compiled** `site.js`, then runs Vitest. A successful run ends with:

```
 Test Files  22 passed (22)
      Tests  783 passed (783)
```

Expect ~30 lines of `Not implemented: HTMLCanvasElement's getContext() ...` to
scroll past on the way — that is **not a failure**, just jsdom reporting it has
no `<canvas>` engine for the Chart.js code to use. Only the
`Test Files … passed` / `Tests … passed` summary matters; a real failure prints
`FAIL` and names the test file.

**In a build:**

* **`dotnet build` / `dotnet test` / Rider / VS** — the `RunClientScriptTests` target (`Maroik.Website.csproj`,
  `AfterTargets="Build"`) runs `npx vitest run` **on by
  default** wherever `npm ci` has been done. It is **incremental** — skipped unless a
  `TypeScripts/**/*.ts`, `TypeScripts.Tests/**/*.ts`, `tsconfig.json`,
  `vitest.config.mts` or `package.json` changed since the last pass (stamp:
  `obj/…clientscripttests.stamp`), so a C#-only edit + build doesn't pay for it.
  One-off fast build: `dotnet build -p:RunClientScriptTests=false`.
* **Docker** — the `Dockerfile` `tsbuild` stage runs the suite only when built with
  `--build-arg RUN_CLIENT_TESTS=true` (keeps Rider/VS "regular mode" docker-compose
  debug from being gated on it). CI passes that arg.

Not a .NET project (no `.csproj`) — not in `Maroik.sln`.
