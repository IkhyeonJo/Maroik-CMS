# Maroik — project instructions

## Architecture: DDD + Clean Architecture

Business logic lives **only** in `.cs` under `Maroik.Core.*`, never in
`.cshtml` views or the custom client scripts. Views render a ready-made
view model; the client scripts do DOM wiring, widgets, and AJAX transport only.
Client-side rule checks are UX mirrors — the server always re-validates.

The custom client scripts are authored in **TypeScript** at
`Maroik.Website/TypeScripts/**/custom/**/site.ts` and compiled 1:1 by `tsc` to
`Maroik.Website/wwwroot/**/custom/**/site.js` — **that `.js` is generated and git-ignored; edit the
`.ts`** (see `Maroik.Website/TypeScripts/README.md`).

Layer map (dependency direction: Website → Service → Domain; Domain depends on nothing):

- `Maroik.Core.Domain` — entities, invariants, value objects, **policies / business rules**
- `Maroik.Core.Service` — use-case orchestration, transactions, DTO mapping
- `Maroik.Core.Contract` — interfaces + DTOs
- `Maroik.Core.Repository` — EF Core persistence
- `Maroik.Core.Client` — external systems (SMTP mail, file storage, ClamAV, RabbitMQ)
- `Maroik.Website` — controllers (HTTP surface), views, static assets

Full rule, including "what must not appear where" and the step-by-step for moving
logic into the Core layers:

@.claude/rules/frontend-no-business-logic.md

## Intended design — do not "fix" these

The items below look like gaps or smells but are deliberate. Do not change them, add guards around
them, or refactor them away unless the owner explicitly asks for that specific change.

Deployment and schema
- Every deployment wipes the server data and recreates the database from `Init.sql`. There are no
  production migrations, no data back-fills and no backward-compatibility concerns. A schema change
  means editing `Init.sql` (every variant) and the EF Core model so they match — nothing else.

Authorization and navigation
- The navigation menu is the access-control list: a `Category` maps to one controller, a `SubCategory`
  to one action, and `AuthorizationFilter` allows a GET only when it resolves to a menu item for the
  role. Controller boundaries follow the menu, so large controllers (organized with `#region`) and
  mode-switching GET actions such as `FreeForum(method = "edit")` are intended. Do not split controllers
  (including into partial classes) or add GET actions that would need new menu rows.
- `AuthorizationFilter.Deny()` redirects to `/Dashboard/AnonymousIndex`, and each role's `_Layout`
  script has a global `ajaxError` handler that sends every failed AJAX call to the same page. The two
  work as a pair; keep both.
- An administrator can change, lock or delete their own account and edit or delete any menu entry,
  including the ones that grant their own access. No self-lockout or last-admin protection is wanted.

Accounts
- A failed-login lockout is permanent until the user resets the password or an admin unlocks the
  account. A successful password reset always unlocks the account, including an admin-imposed lock.
  Admin sanctions use `AdminResetPassword` + `MustChangePassword`, not `Locked`.
- Registration and password-reset tokens are stored in the database as plain values (they expire after
  24 hours). The admin account grid intentionally shows and searches `HashedPassword`,
  `RegistrationToken` and `ResetPasswordToken`. Its Excel export (`ExcelExportService.CreateAccountExcel`)
  deliberately leaves those three columns out — keep them out of the export.
- IP-based rate limiting is done at Cloudflare. Do not add `AddRateLimiter` or IP-keyed throttling in
  the app (behind Cloudflare, `RemoteIpAddress` is an edge address shared by every visitor).

Finance
- An asset's currency label can be edited at any time (e.g. "원" → "KRW"). Never reject a currency
  change, whatever the existing balance looks like.

Files and content
- Maroik.FileStorage is only ever reachable on the internal network; its `[AllowAnonymous]` endpoints
  and always-on Swagger UI are intended.
- Editing a post, private note or calendar event without choosing a new file removes the existing
  attachment (pinned by tests such as `EditBoardAsync_ClearsTheExistingAttachmentRecord_WhenNoFileIsSubmitted`).
- The same holds when an admin clears the lock on someone else's FreeForum post (the
  `adminClearingSomeoneElsesLock` path of `BoardService.EditBoardAsync`): `HandleAttachmentAsync` still runs, so
  a request without a file removes the writer's attachment (pinned by
  `EditBoardAsync_AdminNonOwner_ClearingALockWithoutAFile_ClearsTheWritersAttachmentRecord`).
- Inline images in post / event bodies are fetched and embedded as base64 on every view
  (`PrepareHtmlForDisplayAsync`). Keep that rendering path.
- Replacing or clearing an attachment (post, private note, calendar event) only rewrites its database
  record; the old file stays in Maroik.FileStorage, as does a file uploaded by a write whose transaction
  later rolls back. There is no delete API on purpose — do not add one or a cleanup job.
- `BoardOutputViewModel.BoardAttachedFilePath` (Forum and Management) is populated by the controllers but
  not rendered by any view. Keep it; do not remove it, and do not render the storage path in a view or
  JSON response.

Calendar
- `GetCalendarEvents` loads every event of the selected calendars at once and the client pages through
  them locally; `CalendarController` filters the requested calendars down to the ones the viewer may
  see. No date-range querying.
- Event reminders are stored but never delivered, and `CalendarRecurrence` / `RecurrenceId` are unused.
  Both are unimplemented on purpose — do not implement delivery and do not drop the table.

Client scripts
- No bundler, no `import`/`export`: every `site.ts` is an IIFE global script compiled 1:1.
- Each role (`admin` / `user` / `anonymous`) keeps its own script per view, even when two scripts are
  nearly identical. Do not merge them into shared scripts; the only shared helpers are the `window`
  globals each role's `_Layout` script defines.

Error handling
- Controller actions keep their per-action `catch` blocks and return HTTP 200 with
  `{ result, error }`; exceptions are logged on the server. A genuine input-validation failure shows its
  specific reason (or "Input is invalid"), while an infrastructure failure shows a generic
  "temporary error, please try again" message.

## Strict TDD — no test, no code

All new and changed code is written test-first. This is a hard rule, not a preference:

1. **Red.** Write the test that describes the behaviour *before* the production code. Run it and watch it fail, for the
   right reason (the behaviour is missing — not a compile error or a typo in the test).
2. **Green.** Write the least production code that makes it pass. Nothing the tests do not demand.
3. **Refactor.** Clean up with the tests green. Behaviour changes need a new failing test first.
4. **A bug fix starts with a regression test** that fails on the current code. Revert the fix and confirm the test fails again
   (a test that passes without the fix proves nothing).

Consequences that apply to every change — C# under `Maroik.Core.*` / `Maroik.Website` / `Maroik.Worker` /
`Maroik.FileStorage`, and `TypeScripts/**/site.ts`:

- **Production code that no test exercises does not stay.** If a line, branch, method, class or script has no test that would
  fail without it, either write that test now or delete the code. "Defensive" branches that cannot be reached are not
  a reason to keep untested code: prove them reachable with a test, or remove them.
- **Tests must be able to fail.** No tests that only assert "does not throw", that pass regardless of the code, or that mock away
  the thing under test. Assert the concrete result (value, status code, message, persisted row, request body, DOM).
- **Prefer real collaborators over mocks** where the behaviour is about PostgreSQL, RabbitMQ, SMTP or HTTP: use the
  Testcontainers / in-process fakes already in the test projects (see "Tests that need Docker").
- **Do not lower a gate to make a change pass.** The bar is line >= 80 % / branch >= 65 % coverage, the architecture tests, and the
  client-script tests against freshly compiled `wwwroot/**/site.js`. A change that needs an exemption gets a discussion, not a quiet skip.
- **Done means the suite is green** (`dotnet test` for the touched projects, `npm test` for scripts), the build has zero warnings,
  and the tests for the change are in the same commit as the change.

When asked for a change and it is unclear what test would drive it, ask that question first — the answer is the specification.

## Logging — written and tested with the code, like TDD

Logging is part of the behaviour, not decoration added afterwards. Every new or changed use case, endpoint, worker step or
client call ships with its logging **and the test that proves it**, in the same change. The order is the TDD order:

1. **Red.** Before the production code, write the test that asserts the log entry (level, message template / rendered text,
   whether an exception is attached, and — where relevant — that no secret is present). Use a recording `ILogger<T>` (see
   `RecordingLogger` in `Maroik.Core.Client.Tests/Clients/FileClientTests.cs`) and watch it fail because the log is missing.
2. **Green.** Add the smallest logging that passes.
3. **A log call no test would notice disappearing does not stay.** Remove the log call and confirm the test fails.

What must be logged:

- **Every `catch` either logs or rethrows.** A swallowed exception with no log is a defect. The only allowed silent catches are
  deliberate, commented, and tested translations of an *expected* condition into a result (e.g. a PostgreSQL unique-violation
  turned into `Conflict`, `OperationCanceledException` on shutdown, a health check returning its own `HealthCheckResult`).
  Everything else logs with the exception object as the first argument (`logger.LogError(ex, "… {Id}", id)`), never
  `ex.Message` alone.
- **Security and audit events are logged at `Information`/`Warning`**, whether they succeed or fail: login success / failure /
  lockout, registration, email confirmation, password-reset request and completion, forced password change, role / lock /
  delete changes made by an admin, permission denials, and rejected uploads. The log must let an operator answer "who did what to
  which account, and when" without reading the database.
- **Every failed write use case** logs why it failed (a result of `Failure`/`Conflict`/`Validation` produced by an unexpected
  condition is logged; a plain user-input validation error is not an event).
- **External calls** (SMTP, RabbitMQ, file storage, ClamAV) log a failure with the target, and a degraded-but-continuing
  path (fallback, retry, dropped item) logs a `Warning`.

Levels: `Error` = unexpected failure someone must look at; `Warning` = abnormal but handled (hostile-looking input, degraded
dependency, retry); `Information` = business / audit events; `Debug`/`Trace` = diagnostics only, off in production.

How:

- **Structured templates with named placeholders** (`"Locked account {Email} after {Attempts} attempts"`), never string
  interpolation or concatenation, so the values stay queryable and cannot forge extra log lines. The console output templates
  (`Maroik.Website/Constants/LogTemplates.cs`, `Maroik.FileStorage/Helpers/LogTemplates.cs`, `WorkerHost.ConsoleOutputTemplate`)
  render `{Message:j}`, not Serilog's usual `:lj` — `:l` writes a string value raw, so a line break inside it would start a
  forged line. Keep `:j`; each host has a test that fails if a value can break the line.
- **Never log secrets or content:** passwords, password hashes, registration / reset tokens or their RSA ciphertext, session
  or antiforgery values, connection strings, mail bodies, uploaded file bytes, post/comment text. Identify an account by the
  identifier the existing logs already use (its email). A test asserts a secret does not appear when one could leak.
- **Layering:** `Maroik.Core.Domain` never logs (it stays pure — it returns errors); `Maroik.Core.Repository` does not log (the
  exception propagates to the service that owns the transaction and logs there). Services, clients, workers, controllers and
  filters log. Do not log-and-rethrow the same exception at several layers — log it once, where it is handled.
- **Done also means** every `catch` you added or touched satisfies the rules above and its test is green.

Not enforced by tests: the logging rules above are followed by convention and checked in review. The earlier
`*LoggingArchitectureTests` / `LoggingRulesSelfTests` (and the `Maroik.Tests.Shared` project that held them) were removed on
purpose — do not reintroduce them. The only automated logging check left is `DomainArchitectureTests.Domain_ShouldNot_Log`
(`Maroik.Core.Domain` must not depend on `Microsoft.Extensions.Logging`).


## Always write resx + unit tests alongside code

Whenever you add or change code, also write the matching pieces in the same change —
don't leave them for later or treat them as optional:

- **Resource (.resx) files.** Any user-facing string (controller messages, ViewModel
  labels/validation messages, etc.) goes through the resx pair for its class, mirroring
  the source path 1:1 under `Maroik.Website/Resources/...` — e.g.
  `Controllers/FooController.cs` → `Resources/Controllers/FooController.en-US.resx` +
  `FooController.ko-KR.resx`. New class needing localized strings ⇒ new resx pair, both
  cultures filled in (no missing `ko-KR` or `en-US` entries). This is a server-side
  (`.cs`/`.cshtml`) concern only — `TypeScripts/**/site.ts` never calls a localizer or
  owns a resx; it only reads text the server already localized into the DOM (hidden
  inputs / data attributes a Razor view rendered via `IStringLocalizer`), so there is
  nothing for a `.ts` change to add here.
- **Unit tests.** Every `.csproj` source project has a matching `*.Tests` project
  (`Maroik.Core.Domain` → `Maroik.Core.Domain.Tests`, `Maroik.Core.Service` →
  `Maroik.Core.Service.Tests`, etc. — see the list under `ls -d */ | grep Tests`), **and**
  `Maroik.Website/TypeScripts/**/site.ts` has its own counterpart,
  `Maroik.Website/TypeScripts.Tests/` (Vitest + jsdom; no `.csproj`, not in `Maroik.sln`,
  run via `npm test` from `Maroik.Website/` — see `TypeScripts.Tests/README.md`). New or
  changed behavior in a source project **or in a `.ts` file** gets corresponding tests
  added in its `.Tests` project in the same change, not deferred.

## Aggregate model

Every domain type persisted independently through its own `I*Repository`
(`IGenericRepository<T>` — its own `CreateAsync`/`UpdateEntityAsync`, not a cascade
through a parent) is an `AggregateRoot<TId>`, never a bare `Entity<TId>`. This
includes `BoardComment`, `BoardAttachedFile`, `CalendarEventReminder`,
`CalendarEventAttachedFile`, `CalendarShared` and `OtherCalendar` — reclassified
from `Entity<T>` to `AggregateRoot<T>` (base class only; parent-reference fields
are plain IDs, not object references), the same criterion applied to
`Category`/`SubCategory` in commit `284dc52f`. Enforced by
`ContractArchitectureTests.EveryRepositoryInterface_MustPersist_AnAggregateRoot`.

## Repository tests

`Maroik.Core.Repository.Tests` runs entirely against a real PostgreSQL 17 database
(Testcontainers) — there is no EF Core InMemory provider. One container is shared by
the whole assembly (`Infrastructure/PostgresContainerFixture`, an xUnit v3 assembly
fixture); it loads the real schema **and seed data** from
`Maroik.DB/PostgreSQL/SQL_Init_Script/Debugging/Init.sql` into a template database,
and each test class gets its own throwaway DB cloned from that template
(`DatabaseFixture` + `RepositoryTestBase`). Docker must be available;
test-collection parallelism is off (`xunit.runner.json`).

Because every test starts from the seed (menu/category rows plus the admin and demo accounts), a test must not assume an
empty table: insert under keys unique to the test (`RepositoryTestBase.UniqueEmail`
/ `Unique`, or `EnsureAccountsAsync` / `EnsureAssetsAsync` / `InsertCalendarAsync` /
`InsertCalendarEventAsync` for FK parents, and `EnsureNicknamesAsync` for a `Board` / `BoardComment`
`Writer`, which `Board_fk_0` / `BoardComment_fk_1` point at `Account.Nickname`) and scope "list/count" assertions to the
rows or account the test itself created. Real CHECK / FK / composite-PK behaviour is
now exercised (e.g. `FixedIncome_DepositDayMonth_check`,
`CalendarEventReminder_Method_check`, `Board_Type_check`).

## Tests that need Docker

Besides `Maroik.Core.Repository.Tests` (PostgreSQL) the following start throwaway containers with Testcontainers, so Docker must
be available: `Maroik.Core.PostgreSQL.Tests` (PostgreSQL 17, the init script — the EF model must match
the real schema), `Maroik.Website.Tests` and `Maroik.E2E.Tests` (PostgreSQL 17), `Maroik.Core.Client.Tests` and
`Maroik.Worker.Tests` (RabbitMQ 4; the mail path uses an in-process SMTP server, `FakeSmtpServer`). Prefer these real services over
mocks when a test is about how the code behaves against PostgreSQL / RabbitMQ.

## Mutation testing (on demand)

Coverage says code ran, not that a test would notice it breaking. Stryker.NET (pinned in `.config/dotnet-tools.json`) changes the
production code and reports which changes NO test catches ("survivors"). Run it from the test project's folder, with the
Microsoft.Testing.Platform runner (this repo uses xUnit v3 / MTP; the default vstest runner reports a false 0 %):

    dotnet tool restore
    cd Maroik.Core.Domain.Tests
    dotnet stryker --test-runner mtp --project Maroik.Core.Domain.csproj --reporter json --reporter cleartext

Read the survivors, not just the score. Known limits of the MTP runner: mutants inside **static field initializers**
(`static readonly` tables such as the reserved nicknames or the class taxonomies) are reported as survived even when a test does fail
for them (apply the mutation by hand to check), and a few mutants are equivalent (an `>=` vs `>` that only differs at an
exact-`now` instant). Baseline: `Maroik.Core.Domain` 92 % (the rest is those two categories), `Maroik.Core.Service` 66 % — Service tests
mock the unit of work and rarely assert Begin/Commit/Rollback or the result codes, which is the next place to strengthen.

## Architecture validation tests

The layering / ArchNet rules are enforced by NetArchTest-based `*ArchitectureTests`
classes in each `*.Tests` project's `Architecture/` folder, plus the solution-wide
`SolutionLayeringArchitectureTests` (in `Maroik.Core.Contract.Tests`) that validates
the `.csproj` project- and package-reference graph itself. They run under
`dotnet test`, so a violation fails the test run. Keep them green; extend them
when the layer model changes.

**Every project — including a new one — must be registered in `Maroik.sln` under the matching solution folder** (`src`
for shipped code, `tests` for `*.Tests`): `dotnet sln Maroik.sln add <csproj> --solution-folder tests`. Enforced by
`SolutionLayeringArchitectureTests` (`EveryProject_MustBeRegisteredInTheSolution`,
`EveryProject_MustBeNestedUnderTheMatchingSolutionFolder`).

## Domain error messages must go through LocalizableError

Every `Maroik.Core.Domain` error is built via `Maroik.Core.Domain.Localization.LocalizableError`
(`.Validation`/`.Conflict`/`.Failure`) — **never** call the raw `ErrorOr.Error.Validation` /
`.Conflict` / `.Failure` / `.NotFound` / `.Forbidden` / `.Unexpected` factories directly outside
`LocalizableError.cs` itself. This applies even to a message with no runtime value to interpolate
(pass zero `args`) — a message that starts static and later gains an interpolated value must not be
able to silently regress to bypassing localization.

Why: a raw `Error.X(code, "some message")` bakes the description straight into `Error.Description`
with no composite-format template in `Error.Metadata`, so the Website's `IHtmlLocalizer` resx lookup
falls back to the baked (often English, sometimes value-specific) string instead of resolving a
`ko-KR` translation. This was the actual state of ~98 of 104 Domain error call sites, and for
`Money.Add`/`Subtract`'s currency-mismatch errors it was a live bug: a genuinely runtime currency
code was interpolated straight into the message, so no resx key could ever match it.

Enforced by `DomainArchitectureTests.Domain_ErrorMessages_MustGoThrough_LocalizableError`
(`Maroik.Core.Domain.Tests/Architecture/DomainArchitectureTests.cs`) — a NetArchTest custom rule that
inspects each type's IL for a direct call to a raw `ErrorOr.Error` factory method (a type-dependency
check doesn't work here since every Domain type legitimately depends on the unrelated `ErrorOr<T>`
return type). Keep it green; when a new error is added, add it via `LocalizableError`, and add the
matching resx entry — with a `{0}`/`{1}` composite-format key, not a baked value — to every controller
resx pair (`en-US` + `ko-KR`) that can surface it.
