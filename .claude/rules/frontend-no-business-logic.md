# Rule: no business logic in `.cshtml` or the client scripts (`TypeScripts/**/site.ts`)

Summary: all business logic goes in `.cs` files under `Maroik.Core.*`
(DDD + Clean Architecture). `.cshtml` views and the custom client scripts keep only
rendering and input handling. This rule always applies — it does not need to be
restated per task.

The custom client scripts are authored as **TypeScript** under
`Maroik.Website/TypeScripts/{admin,anonymous,user}/custom/{Feature}/{Page}/js/site.ts`
and compiled 1:1 by `tsc` to `Maroik.Website/wwwroot/**/custom/**/site.js`.
**Never hand-edit the `wwwroot/**/site.js` — it is generated, git-ignored output.** Edit the `.ts`;
the build (or `npm run build` in `Maroik.Website/`) regenerates the `.js`.
See `Maroik.Website/TypeScripts/README.md`.

## Scope

* every Razor view under `Maroik.Website/Views/**/*.cshtml`
* every client script under `Maroik.Website/TypeScripts/{admin,anonymous,user}/custom/**/site.ts`
  (and, by extension, its generated `wwwroot/**/site.js` — which is off-limits for edits)

## Where logic belongs (Clean Architecture layers)

| Concern | Home |
|---|---|
| Entities, invariants, value objects, **policies / business rules / allowed-value taxonomies** | `Maroik.Core.Domain` |
| Use-case orchestration, transactions, cross-aggregate rules, DTO mapping | `Maroik.Core.Service` |
| Interfaces, DTOs | `Maroik.Core.Contract` |
| Persistence, EF Core, queries | `Maroik.Core.Repository` |
| External systems (SMTP mail, file storage, ClamAV, RabbitMQ…) | `Maroik.Core.Client` |
| HTTP surface: model-bind → call a service → return a result | `Maroik.Website/Controllers` |
| HTML structure + server-injected data | `Maroik.Website/Views/**/*.cshtml` |
| DOM wiring, widget init, AJAX transport, notifications | `Maroik.Website/TypeScripts/**/custom/**/site.ts` |

The dependency rule holds: `Website` → `Service` → `Domain`; `Domain` depends on
nothing. Repositories/Clients are wired through `Contract` interfaces.

## What MUST NOT appear in `.cshtml`

- `@{ ... }` / `@functions` blocks that compute, validate, branch on domain state,
  aggregate, sort/filter collections, or format money/dates by business rule.
  The view receives a ready-to-render view model.
- Inline `<script>` containing anything beyond a one-line call into the page's script.
- Hardcoded business constants (limits, thresholds, class lists, tax/rate tables).
  Expose them from a Domain **policy** and serialize into the page:
  `@Html.Hidden("maxDaysBeforeEvent", CalendarReminderPolicy.MaxDaysBeforeEvent)`
  `@Html.Hidden("incomeSubClassMap", JsonConvert.SerializeObject(IncomeClassPolicy.SubClassesByMainClass))`

## What MUST NOT appear in `site.ts`

- Business rules or magic numbers. Read them from server-injected hidden fields
  that a Domain policy produced. Client checks are **UX mirrors only** — the
  server re-validates every value on every write. Keep the mirror comment:
  `// Authoritative in <Policy> (server); mirrored here for form UX only.`
- Aggregation / derivation of displayed values (totals, balances, breakdowns).
  The controller returns computed numbers; the script only draws them.
- HTML sanitization, allow-listing, or content parsing for stored user content —
  use `IHtmlContentSanitizerService` / `IHtmlParserService` server-side.
- Business decisions about persistence, permissions, or workflow state.

TypeScript's types do not change this: `tsc` passing is not evidence that a rule
belongs client-side. A typed `ApplyAllowedOptions` / `Validate*` is still only a
UX mirror of a Domain policy.

### `site.ts` MAY contain

jQuery element caching, `.on(...)` event wiring, datepicker/tabs/Chart.js/summernote
init, `$.ajax` calls to controller endpoints, `toastr` messages, Bootstrap modal
show/hide, MvcGrid reload, double-submit guards, base64→blob rendering, and
building `<option>`/DOM from a server response. Each file is an IIFE-wrapped global
script (no `import`/`export`); keep it that way.

## When asked to "move business logic to DDD .cs"

1. Identify the rule and its aggregate; add/extend a **policy or domain method**
   in `Maroik.Core.Domain` with unit tests in `Maroik.Core.Domain.Tests`.
2. Call it from the relevant `Maroik.Core.Service` service; add a `Contract`
   interface member + DTO if the shape changes.
3. Have the controller return the computed result / expose the policy via the
   view model.
4. In `.cshtml`, replace the logic with a render of the view-model value, or a
   `@Html.Hidden(...)` of the serialized policy.
5. In the page's `site.ts`, delete the duplicated rule; read the hidden field
   instead and leave only the UX mirror with its "authoritative on server" comment.
   Never touch the generated `wwwroot/**/site.js` directly.
6. Build the affected `*.Tests` project; if `Maroik.Website/node_modules` exists,
   `dotnet build` also runs `TypeScripts.Tests` (Vitest) — or `npm test` in
   `Maroik.Website/`.

## Current state (2026-09-02)

* The DDD/Clean-Architecture move of business logic out of the client scripts and
  views is substantially complete (commit `88941c57 feat: DDD + Clean architecture`).
  Policies exist for Finance class taxonomies, calendar reminders, fixed schedules,
  passwords and image upload. Treat new findings as regressions to fix,
  not a fresh migration.
* `CulturePolicy` (supported UI cultures, the culture → default time-zone pre-select) is a
  presentation setting, not a business rule: it lives in `Maroik.Website/Constants`, not in
  `Maroik.Core.Domain` (moved 2026-10-09; only the Website ever used it).
* The 22 client scripts were migrated JS → TypeScript (`TypeScripts/`), byte-faithful,
  with jsdom tests in `TypeScripts.Tests/`. `wwwroot/**/site.js` is now generated
  (and, since 2026-09-27, git-ignored rather than committed).
* `site.css` (2026-10-09): the page / layout stylesheets use native nesting and custom properties (no Sass or
  other toolchain); a before/after comparison of every element's computed style in Chromium showed no change, and
  `Maroik.E2E.Tests/Flows/StylesheetFlowTests` pins each rule's computed effect on the real pages.
  They are not put in `@layer`: they override third-party CSS (AdminLTE / Bootstrap / FullCalendar) that loads
  unlayered, and a layered rule loses to every unlayered one. Layering would first need the vendor sheets
  imported into a layer of their own. The ASP.NET template's `wwwroot/{area}/css/site.css` was unused and is gone.