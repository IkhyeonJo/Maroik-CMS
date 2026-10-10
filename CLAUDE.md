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
- Judge code against the schema, the seed scripts and the tests only. The production data is the owner's
  responsibility: do not raise checks against existing production rows, ask for queries to be run against it, or
  add safeguards for hypothetical legacy rows.
- `Maroik.Core.PostgreSQL` (`ApplicationDbContext`, `Models/*`) is EF Core scaffold output. Leave it out of
  repo-wide comment / style passes; touch it only when the schema mapping itself changes.

Operations and hosting
- Maroik only ever runs under docker-compose on Linux (Ubuntu). Behaviour that differs only on a Windows dev box
  (path separators, a test that fails only there) is an environment difference, not a defect.
- The containers run as root (no `USER` in the Dockerfiles).
- In production TLS ends at Cloudflare: the site is reached only through a Cloudflare Tunnel, which hands requests
  to the website container over plain HTTP, so there is no origin certificate. `Program.cs` takes
  `X-Forwarded-Proto` from private-network peers only (pinned by `ForwardedProtoTests`).
- `AllowedHosts` is `*`. The `amqp://guest:guest@localhost` fallbacks (`Program.cs`, `WorkerHost`) are never
  used in a deployment (the env files always set the connection string).
- Data Protection keys are stored unencrypted (Valkey, or the file fallback); they only live on the server.

Authorization and navigation
- The navigation menu is the access-control list: a `Category` maps to one controller, a `SubCategory`
  to one action, and `AuthorizationFilter` allows a GET only when it resolves to a menu item for the
  role. Controller boundaries follow the menu, so large controllers (organized with `#region`) and
  mode-switching GET actions such as `FreeForum(method = "edit")` are intended. Do not split controllers
  (including into partial classes) or add GET actions that would need new menu rows.
- `AuthorizationFilter.Deny()` redirects to `/Dashboard/AnonymousIndex`, and each role's `_Layout`
  script has a global `ajaxError` handler that sends every failed AJAX call to the same page. The two
  work as a pair; keep both.
- Admin safeguards (`AdminSafeguardPolicy`, applied by `ManagementAccountService` update and delete): an administrator
  cannot lock, delete or demote their own account (re-saving it unchanged, unlocking and restoring it stay allowed), and
  nobody can lock, delete or demote the last active administrator (Admin, not locked, not deleted). Each admin edit / delete
  first locks the active administrators' rows (`FindActiveAdminsForUpdateAsync`, email order), then the edited row, so two
  concurrent edits cannot leave no administrator. A failed-login lockout is not covered (see Accounts). The menu has no
  safeguard: an administrator can still edit or delete any menu entry, including the ones that grant their own access.
- The admin-only GET pages (`Management/Account`, `Management/Menu`, `Dashboard/AdminIndex`, ...) are protected
  by the menu alone, while their POSTs also carry a role attribute. Getting the menu's `Role` right is the
  administrator's job; do not add a second role check to those GETs.
- `AuthorizationFilter` sends a signed-out visitor who opens an unlisted page to the public dashboard without
  logging it (pinned by `OnAuthorizationAsync_LogsNothing_WhenAnAnonymousVisitorIsSentFromAnUnlistedPage`).
  Denials of signed-in accounts go through `Deny()` and are logged.
- The `ReturnUri` the layout uses after a culture change is not an open redirect: the router answers 404 to any
  `//…` or `/\…` path, so no layout renders it. No extra validation is wanted.

Accounts
- A failed-login lockout is permanent until the user resets the password or an admin unlocks the
  account. It only refuses new logins: sessions already signed in keep working, so guessing wrong on purpose
  cannot throw the owner out. An administrator locking an unlocked account (`Account.LockAndEndSessions`)
  also replaces the security stamp and so ends the account's sessions. A successful password reset always unlocks the account, including an admin-imposed lock, and signs
  the user in straight away (fresh session id, new security stamp) unless the account has not accepted the
  service terms. This does not weaken the lockout: the reset needs the single-use mailed token, not a guessed
  password. Admin sanctions use `AdminResetPassword` + `MustChangePassword`, not `Locked`.
- Registration and password-reset tokens are stored in the database as plain values (they expire after
  24 hours). `HashedPassword`, `RegistrationToken` and `ResetPasswordToken` never reach the admin screens:
  `IManagementAccountService` returns plain `AccountResponse` rows, so the account grid, its whole-row search
  and the Excel export neither show nor match them (a live token on screen hands the account to whoever sees it;
  a searchable token or hash is a guessing oracle). Pinned by `AccountGrid_DoesNotRenderTheHashOrTheTokens`,
  `SearchAsync_DoesNotMatchTheHashOrTheTokens` and the `ManagementAccountServiceTests` row checks. Do not add
  them back.
- A successful self-service password change ends the session; the user signs in again (as on other sites), and
  every other session of the account is invalidated through the security stamp. Because it proves the current
  password, it also lifts a lock and clears the failed-login counter (`Account.ChangePassword`) — otherwise an owner
  locked by someone else's guesses, still signed in, would change the password and then be refused at that sign-in.
- A deleted account's login answers "Your Account is Deleted. Please contact the administrator." — it is not
  re-registrable.
- The Login page is pre-filled with the public demo account (`demo@maroik.com` / `demoO12!!`), and the demo
  account's dashboard always shows the fixed period from `ServerSetting.DemoDashboardYear/Month`.
- Nicknames reject `<`, `>`, `"`, the backtick and `\` (after NFKC) but allow `&` and `'` ("R&D팀",
  "O'Brien"): nicknames are always output encoded, so those two are not an injection vector. An
  administrator may create an account under a reserved nickname; self-registration may not.
- IP-based rate limiting is done at Cloudflare. Do not add `AddRateLimiter` or IP-keyed throttling in
  the app (behind Cloudflare, `RemoteIpAddress` is an edge address shared by every visitor). Login BCrypt CPU
  cost and registration / reset mail volume are infrastructure concerns in the same way.
- The public demo account has no server-side write protection: anyone can change its password, lock it or post
  as it. Anyone can also lock any account with `MaxLoginAttempt` wrong guesses. Both are accepted.

Data access and transactions
- Every service write spells out its own `BeginAsync` / `CommitAsync` / `RollbackAsync` (and the service tests
  assert them). Do not fold them into a decorator, filter or "unit of work per request" helper.
- Several rows are locked in one statement, `... ORDER BY <column> FOR UPDATE` without `LIMIT` (e.g. the asset
  balance lock). This is a proven fix for a lost-update bug; do not report it as a deadlock-ordering risk.
  (`LIMIT` combined with `ORDER BY ... FOR UPDATE` would be a different, documented caveat.)
- Posts and comments are owned by `Nickname`, not by email: a nickname is unique and never changes after
  registration (only an unconfirmed account's can, via `ReplaceUnconfirmedRegistration`), and accounts are
  only soft-deleted.

Finance
- An asset's currency label can be edited at any time (e.g. "원" → "KRW"). Never reject a currency
  change, whatever the existing balance looks like.
- A transfer-type expenditure (deposit / investment / public pension / debt repayment), one-off or fixed,
  requires its payment method and its deposit asset to carry the same currency label, on create and on update. Relabelling only one
  of the two assets therefore makes editing an older transfer fail — intended.
- A negative amount typed into an income / expenditure (one-off or fixed) is stored as its absolute value
  (`Math.Abs`).
- The dashboard's first year (`StartYear`) is found from the account's earliest income / expenditure by loading
  them all.
- `FixedSchedulePolicy.IsNoticed`'s behaviour around the year end and 29 February is kept as it is.
- Amounts are shown with every decimal place the `numeric(20,4)` columns store: the finance grids use
  `AmountDisplay.GridFormat` (`#,0.####`, trailing zeros dropped) and the dashboard tooltips read
  `FinanceAmountPolicy.MaxDecimalPlaces` from `#amountMaxDecimalPlaces`. The currency is a free-text label, so there is
  no per-currency number of decimals (USD 12.50 shows as "12.5").
- The edit forms receive amounts as JSON numbers, so an amount beyond ~15 significant digits (≥ ~9×10¹⁵) can
  change on a re-save. Not a defect to raise.

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
- `HtmlContentSanitizerService` starts from the Ganss defaults plus `class` and narrows only three things: URL
  schemes to http/https, no `data-*` attributes, and no `position` / `top` / `left` / `right` / `bottom` /
  `z-index` / `opacity` in inline styles (so a post cannot overlay the page). The rest of the default breadth —
  the `name` attribute, form elements (`form`, `input`, `button`, `select`, `textarea`) — is kept on purpose. Do
  not raise it again.
- An administrator cannot clear someone else's post lock from the UI (the edit page is owner-only); only a direct
  POST to the edit endpoint can.
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
- No `any` and no type assertion (`x as T`, `x!`; `as const` aside) in `TypeScripts/**` or `TypeScripts.Tests/**`
  (pinned by `TypeScripts.Tests/_common/noExplicitAny.test.ts` / `noImplicitAny.test.ts`), with `strict` plus
  `noUncheckedIndexedAccess`, `exactOptionalPropertyTypes` and the other strict options in both tsconfigs. A value
  the code cannot prove goes through the runtime-check toolkit each role's `_Layout` script puts on `window`
  (`fieldValue`, `parseJson`, `byId`, `required`, `onReply`, … — see `TypeScripts/README.md`): a missing element
  throws, and an ajax reply that does not pass its check shows the generic "temporary error" toast. Each page
  declares its reply checks to mirror the controller's `Json(...)` result field for field, so a controller's reply
  change updates the page's check in the same change. `global.d.ts` is type-only (no runtime code).

Code and tests
- The DDD + Clean Architecture layering is deliberate preparation for growth, not over-engineering.
- Long rationale comments are wanted; do not trim them in a review.
- Service tests mock the repositories, and the Service layer's mutation score sits below the Domain's on purpose:
  the business rules live in Domain (with its own tests), and real-database coverage comes from the Repository
  and Website integration tests.
- A service reachable only on the internal Docker network gets no app-level authentication (API keys, shared
  secrets, JWT) and no `IsDevelopment()` gating: the network boundary is its security boundary.

Error handling
- Controller actions keep their per-action `catch` blocks and return HTTP 200 with
  `{ result, error }`; exceptions are logged on the server. A genuine input-validation failure shows its
  specific reason (or "Input is invalid"), while an infrastructure failure shows a generic
  "temporary error, please try again" message.

## Reviewing this repo

- **Read "Intended design" above before starting any review**, and check every candidate finding against it
  before reporting.
- Items listed under "Intended design" above stay closed; re-raise one only if its premise changes (for example
  `Maroik.FileStorage` becomes reachable from outside the internal network).

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

`Maroik.E2E.Tests` must not run alongside the other Testcontainers suites: when another session's container leaves the Docker
bridge, the browser aborts a request in flight (`net::ERR_NETWORK_CHANGED`), and `OtherTestcontainersTests` expects no other
session to be running. A solution-wide run must therefore run the modules one at a time:

    dotnet test --solution Maroik.sln --max-parallel-test-modules 1

## Mutation testing (on demand)

Coverage says code ran, not that a test would notice it breaking. Stryker.NET (not pinned in the repo — install it
globally, version 5.0.0) changes the production code and reports which changes NO test catches ("survivors"). Run it from the
test project's folder, with the Microsoft.Testing.Platform runner (this repo uses xUnit v3 / MTP; the default vstest runner reports a false 0 %):

    dotnet tool install -g dotnet-stryker --version 5.0.0   # once
    cd Maroik.Core.Domain.Tests
    dotnet stryker --test-runner mtp --project Maroik.Core.Domain.csproj --reporter json --reporter cleartext

Read the survivors, not just the score. Known limits of the MTP runner: mutants inside **static field initializers**
(`static readonly` tables such as the reserved nicknames or the class taxonomies) are reported as survived even when a test does fail
for them (apply the mutation by hand to check), and a few mutants are equivalent (an `>=` vs `>` that only differs at an
exact-`now` instant). Baseline: `Maroik.Core.Domain` 92 % (the rest is those two categories), `Maroik.Core.Service` 96 % (2026-10-05;
the survivors left are equivalent — `?? ""` fallbacks on a value that is never null, duplicated guards, ordering of groups that hold a
single row). Service tests assert Begin/Commit/Rollback, the exact `ErrorCode`/`ErrorKey`/`ErrorType`, and every `catch`'s log entry;
keep new Service tests to that standard.

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

## Domain error messages must go through DomainError

The domain speaks English only and does no translation. Every `Maroik.Core.Domain` error is built via
`Maroik.Core.Domain.Errors.DomainError` (`.Validation`/`.Conflict`/`.Failure`) — **never** call the raw
`ErrorOr.Error.Validation` / `.Conflict` / `.Failure` / `.NotFound` / `.Forbidden` / `.Unexpected` factories
directly outside `DomainError.cs` itself. `DomainError` keeps the English message template and its values
separately in `Error.Metadata` (`MessageTemplate` / `MessageArgs`), so the outer layers can translate it.
This applies even to a message with no runtime value to interpolate (pass zero `args`) — a message that
starts static and later gains an interpolated value must not be able to silently lose its template.

Why: a raw `Error.X(code, "some message")` bakes the description straight into `Error.Description`
with no composite-format template in `Error.Metadata`, so the Website's `IHtmlLocalizer` resx lookup
falls back to the baked (often English, sometimes value-specific) string instead of resolving a
`ko-KR` translation. This was the actual state of ~98 of 104 Domain error call sites, and for
`Money.Add`/`Subtract`'s currency-mismatch errors it was a live bug: a genuinely runtime currency
code was interpolated straight into the message, so no resx key could ever match it.

Enforced by `DomainArchitectureTests.Domain_ErrorMessages_MustGoThrough_DomainError`
(`Maroik.Core.Domain.Tests/Architecture/DomainArchitectureTests.cs`) — a NetArchTest custom rule that
inspects each type's IL for a direct call to a raw `ErrorOr.Error` factory method (a type-dependency
check doesn't work here since every Domain type legitimately depends on the unrelated `ErrorOr<T>`
return type). Keep it green; when a new error is added, add it via `DomainError`, and add the
matching resx entry — with a `{0}`/`{1}` composite-format key, not a baked value — to every controller
resx pair (`en-US` + `ko-KR`) that can surface it.

Cultures are a Website concern: the supported UI cultures and the culture → default time-zone pre-select
live in `Maroik.Website/Constants/CulturePolicy.cs`, not in the domain. `Maroik.Core.Domain.Time.DateTimeExtensions`
is time-zone conversion, not language handling.

## Service error keys must come from ServiceErrorKeys

The message key of a failed service result (`ServiceResult`, `LoginResult`, `RegisterResult`, `SummernoteUploadResult`)
is also the resx key the controller looks it up under, so it is written as a `Maroik.Core.Contract.Dtos.ServiceErrorKeys`
constant, never a literal (the machine error *code*, e.g. `"Board.NotFound"`, stays a literal). Keys a controller compares
against and answers itself (`"invalid-image"`, `"reset-password-invalid"`, ...) live in `ServiceErrorKeys.Signals` and are
used by both sides. Rewording a value means renaming its resx key in every controller pair in the same change.

Enforced by `ServiceErrorKeyArchitectureTests` (`Maroik.Core.Service.Tests`, no literal keys in the service sources) and
`ServiceErrorKeyResxTests` (`Maroik.Website.Tests`), which follows the calls from each controller into the services with
Mono.Cecil and fails when a key the controller can receive is missing from its `en-US` / `ko-KR` resx, or when a key is
reached from no controller at all.
