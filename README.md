# Maroik [https://www.maroik.com]

# Contact: admin@maroik.com

## Introduction
Maroik is a modern web application built with ASP.NET Core MVC, featuring a comprehensive set of tools for personal and business management. It includes features such as expense tracking, calendar management, bulletin boards, and role-based user management.

This repository is the open-source portfolio edition of Maroik: the full application source and test suites, a local Docker Compose stack, and a seed database that contains only the default admin and demo accounts. Production deployment scripts, CI/CD pipelines, and production data are not included.

### About the commit history
This repository is a portfolio snapshot of Maroik. The original Maroik repository is private, and its full development history (nearly 4,000 commits) lives there — so the short history on this repository's `main` branch does not reflect how the project was actually built. To see that ongoing activity, check the contribution graph on [IkhyeonJo's GitHub profile](https://github.com/IkhyeonJo).

## Key Features

### 1. Personal Finance Management
- Expense tracking and categorization
- Income recording
- Fixed (recurring) income and expenditure with deposit-day reminders — the next deposit is found across the year end (e.g. 28 Dec → 2 Jan), and a 29 February schedule is skipped in common years
- Amounts are kept exactly to four decimal places (`numeric(20,4)`); a fifth decimal is rejected instead of silently rounded, so no recorded amount is ever changed by rounding
- Every income and expenditure updates its asset's balance in the same transaction, under a row lock, so concurrent records never lose an update; the balance can also be corrected by hand when editing the asset- Financial reports and analytics
- Export to Excel — amounts as numeric cells (they sum and sort in Excel), timestamps as `yyyy-MM-dd HH:mm:ss` in the viewer's time zone

### 2. Calendar and Schedule Management
- Monthly calendar view
- Event creation and management, with a file attachment and inline images per event
- Calendars shared with other users or the public

### 3. Bulletin Board System
- Post creation, editing, and deletion
- Comment system
- File attachments (virus-scanned with ClamAV), downloaded through a separate access-checked POST action and streamed — the post page itself carries only the file's name and size
- Rich-text editor images, embedded in the post when it is viewed
- Search functionality

### 4. User Management
- Role-based access control (Admin/User)
- User profile management, including an avatar
- Session-based authentication
- Password management (registration confirmation, password reset, forced password change)
- E-mail addresses are matched trimmed and case-insensitively at login, password reset and confirmation resend
- Privacy: uploaded avatars and editor images are re-encoded without their metadata (EXIF GPS position, camera model, capture time), after first being turned upright by their EXIF orientation

### 5. Admin Dashboard
- User management — role change, lock/unlock, e-mail confirmation, terms agreement, soft delete/restore, password reset with a forced change at next login; role, lock and delete changes are audit-logged with the acting admin
- Navigation menu management (the menu doubles as the access-control list)
- Account export to Excel without password hashes or registration/reset tokens
- Analytics dashboard

### Error messages
- A refused write shows its actual reason, localized in English and Korean (e.g. "Only zip extension allowed.", "Cannot add a comment to a locked post.", "Role must be Admin, User or Anonymous.").
- A server-side fault (database, file storage) shows "A temporary error occurred. Please try again later." instead of blaming the user's input; the exception is logged once, where it is handled.

## Engineering Highlights
- **DDD + Clean Architecture, enforced by tests.** Dependencies point Website → Service → Domain; NetArchTest rules fail the build when a layer reaches the wrong way, when the domain logs, reads the clock, or builds an error outside `DomainError`, and when a project is missing from the solution.
- **Deterministic time.** Domain methods take the current time as a `DateTime utcNow` argument; each use case reads `TimeProvider` once and passes the same instant everywhere, so `Created == Updated` on creation and token expiry is tested at its exact boundaries with `FakeTimeProvider`.
- **Rich domain model.** Value objects (`Email`, `Money`, `CurrencyCode`, `AccountRole`, …) replace primitive strings; admin actions are intent-revealing aggregate operations (`ChangeRole`, `Lock`/`Unlock`, `ForceConfirmEmail`/`RevokeEmailConfirmation`, `AcceptServiceTerms`/`RevokeServiceTerms`, `SoftDelete`/`Restore`, `AdminResetPassword`) instead of a field-overwriting update.
- **Use-case DTOs.** Self-registration, admin account creation and admin account update each have their own request type, so a registration form can never carry a role, lock or confirmation flag.
- **Test-first development.** Every change starts with a failing test, including the log entries it must write; real PostgreSQL and RabbitMQ run in Testcontainers instead of mocks wherever the behaviour depends on them.

## Architecture

### Layers — DDD + Clean Architecture

```mermaid
flowchart TB
    subgraph hosts["Application hosts"]
        Website["Maroik.Website<br/>MVC controllers · views · TypeScript"]
        Worker["Maroik.Worker<br/>e-mail consumer"]
        FileStorage["Maroik.FileStorage<br/>internal file API"]
    end
    Service["Maroik.Core.Service<br/>use cases · transactions · logging"]
    Contract["Maroik.Core.Contract<br/>interfaces · DTOs"]
    Domain["Maroik.Core.Domain<br/>aggregates · value objects · policies"]
    Repository["Maroik.Core.Repository<br/>EF Core repositories"]
    PostgreSQL["Maroik.Core.PostgreSQL<br/>DbContext · mapped models"]
    Client["Maroik.Core.Client<br/>SMTP · file storage · ClamAV · RabbitMQ"]

    Website --> Service
    Worker --> Service
    Service --> Contract --> Domain
    Repository --> Contract
    Repository --> PostgreSQL
    Client --> Contract
    FileStorage --> Client
    Website -. "composition root only" .-> Repository
    Website -. "composition root only" .-> Client
    Worker -. "composition root only" .-> Repository
    Worker -. "composition root only" .-> Client
```

- **Domain** holds the business rules — aggregates, value objects (`Email`, `Money`, `CurrencyCode`, …) and policies
  (finance class taxonomies, reminder limits, fixed schedules, passwords, image uploads). It references no other
  project, logs nothing, never reads the clock (the current time is passed in) and reports every error through
  `DomainError`, so the outer layers can translate it.
- **Service** orchestrates a use case: it loads aggregates through repository interfaces, calls domain methods, owns
  the transaction (each write spells out `BeginAsync` / `CommitAsync` / `RollbackAsync`), maps to DTOs and logs. It
  depends on **Contract** only.
- **Repository** (EF Core) and **Client** (SMTP, file storage, ClamAV, RabbitMQ) implement the Contract interfaces.
  The hosts reference them only to register them in the composition root; controllers and views cannot see them.
- **Website** controllers bind the request, call a service and return the view model or a `{ result, error }`
  JSON reply; views only render. The navigation menu doubles as the access-control list (`AuthorizationFilter`).
- **No business logic on the client.** The client scripts do DOM wiring and AJAX only; a rule the UI mirrors comes
  from a domain policy serialized into the page, and the server re-validates every write.

All of this is enforced by architecture tests that fail the build, among them:
`Domain_ShouldNot_DependOnAnyOtherLayer`, `Domain_ShouldNot_Log`, `Domain_ShouldNot_ReadTheClock`,
`Domain_ErrorMessages_MustGoThrough_DomainError`, `BoundedContext_ShouldNot_DependOnAnotherBoundedContext`,
`ServiceLayer_MustNotProjectReference_AnyInfrastructureOrPersistenceProject`,
`InfrastructureLayers_MustBuildOnContractOnly_NotOnEachOtherOrService`, `ProjectReferenceGraph_MustBeAcyclic`,
`Controllers_ShouldNot_DependOnRepositoryOrClientOrServiceNamespace`,
`PostActions_MustCarry_AntiforgeryAndRoleAttributes` and `ServiceResults_TakeTheirMessageKey_FromServiceErrorKeys`.

### Runtime

```mermaid
flowchart LR
    Browser --> Website["Maroik.Website"]
    Website --> PostgreSQL[("PostgreSQL 17")]
    Website --> Valkey[("Valkey<br/>session · Data Protection keys")]
    Website --> FileStorage["Maroik.FileStorage"]
    Website --> ClamAV["ClamAV"]
    FileStorage --> ClamAV
    Website -- "mail jobs" --> RabbitMQ[("RabbitMQ")]
    RabbitMQ --> Worker["Maroik.Worker"]
    Worker --> SMTP["SMTP relay"]
```

Every service runs as a container under Docker Compose. Uploads are scanned by ClamAV before they are stored, the
file storage is reachable only on the internal network, and outgoing mail is queued in RabbitMQ and sent by the
worker. In production the site is reached through a Cloudflare Tunnel, so the host publishes no port.

### Client scripts

The page scripts are written in **TypeScript** (`Maroik.Website/TypeScripts/**/site.ts`) and compiled 1:1 by `tsc`
into plain global scripts — no bundler, no runtime framework. They are type-checked with `strict` plus
`noUncheckedIndexedAccess`, `exactOptionalPropertyTypes` and the other strict options, and contain **no `any` and no
type assertion**. Anything the compiler cannot prove is checked at run time instead: each role's layout script
provides a small toolkit (`fieldValue`, `parseJson`, `byId`, `required`, `onReply`, …), every AJAX reply is
validated against a check that mirrors the controller's JSON field for field, and a reply of the wrong shape shows a
generic error instead of failing half-way through a handler.

## Technologies Used

### Backend
- ASP.NET Core MVC 10.0
- PostgreSQL 17
- Entity Framework Core 10.0
- Valkey (Redis-compatible) — session store and Data Protection key ring
- RabbitMQ 4 — outbound e-mail queue consumed by `Maroik.Worker`
- ClamAV — upload scanning in `Maroik.FileStorage`
- Magick.NET — image validation and metadata stripping
- DocumentFormat.OpenXml — Excel export
- Docker
  - Multi-stage builds
  - Container orchestration
  - Environment isolation
- Authentication & Authorization
  - Session Authentication
  - Role-based access control
- Cross-Origin Resource Sharing (CORS)

### Frontend
- AdminLTE 3.1.0
- Bootstrap 4.6.0
- jQuery 3.6.0
- HTML5/CSS3
- JavaScript ES2024
- TypeScript 7.0
  - The custom client scripts are authored in TypeScript at `Maroik.Website/TypeScripts/{admin,anonymous,user}/custom/**/site.ts` and compiled 1:1 by `tsc` to `Maroik.Website/wwwroot/**/custom/**/site.js` (no bundler, no Babel — the emitted `.js` is byte-faithful to the original, minus comments and an IIFE wrapper).
  - Compiled under `--strict`; each file is an IIFE-wrapped global script (no `import`/`export`).
  - Scripts keep only DOM wiring, widget init, AJAX transport, and notifications — business logic lives in `Maroik.Core.*` (DDD + Clean Architecture). Client-side rule checks are UX mirrors; the server always re-validates.
  - `wwwroot/**/site.js` is build output and git-ignored. Docker Compose builds generate it in the Dockerfile's Node stage; running the site on the host, or running the client-script tests, needs Node 22 + `npm ci` in `Maroik.Website/`.
  - MSBuild runs `tsc` on `dotnet build` when `node_modules/` is present; Docker builds run it in a dedicated `node:22-alpine` stage.
  - Unit-tested with Vitest + jsdom in `Maroik.Website/TypeScripts.Tests/` (one `*.test.ts` per script, exercising the compiled `site.js`).
  - Details: `Maroik.Website/TypeScripts/README.md`.
- NonfactorGrid
- Chart.js 2.9.4
- Font Awesome 5.15.3
- Summernote 0.8.18
- FullCalendar 5.5.1
- jQuery UI 1.12.1
- jquery-confirm 3.3.4
- toastr 2.1.4
- Moment.js 2.30.1

### Development Tools
- Visual Studio
- JetBrains Rider
- Git
- Docker Compose
- Postman

## Project Structure
```
Maroik/
├── Maroik.Core.Domain/      # Entities, value objects, policies (depends on nothing)
├── Maroik.Core.Contract/    # Interfaces and DTOs
├── Maroik.Core.Service/     # Use-case orchestration, transactions, DTO mapping
├── Maroik.Core.Repository/  # EF Core repositories
├── Maroik.Core.PostgreSQL/  # EF Core DbContext and ORM models
├── Maroik.Core.Client/      # SMTP, file storage, ClamAV, RabbitMQ clients
├── Maroik.Website/          # ASP.NET Core MVC host (controllers, views, TypeScript)
├── Maroik.FileStorage/      # Internal file upload/download API
├── Maroik.Worker/           # Background e-mail sender (RabbitMQ consumer)
├── Maroik.DB/               # PostgreSQL init script (schema + admin/demo seed data)
├── Maroik.SSL/              # Self-signed localhost certificate for local HTTPS
└── *.Tests/                 # One test project per source project, plus E2E
```

## Naming Conventions
- Code projects (assemblies, namespaces, types) follow the .NET Framework Design Guidelines for acronym casing: acronyms of three or more letters use PascalCase (e.g. `Ssl`, `Http`), while two-letter acronyms stay uppercase (e.g. `DB`, `IO`).
- Folders that are not code projects — `Maroik.SSL`, `Maroik.DB`, `Maroik.Log` — intentionally keep their existing casing, because docker-compose volume mounts reference them by name.

## Testing Strategy

The goal is that a test fails whenever the behaviour it describes breaks — not that lines are merely executed.

- **Test-first.** Every change starts with a failing test, a bug fix with a regression test that fails on the old
  code, and the log entries a use case must write are asserted like any other result.
- **Real dependencies where the behaviour depends on them.** Repositories run against a real PostgreSQL 17 loaded
  with the production schema and seed data (Testcontainers, one throwaway database per test class); messaging against
  a real RabbitMQ 4; mail against an in-process SMTP server; controllers through `WebApplicationFactory` against a real
  database; and the whole site in a real browser with Playwright.
- **Mocks only at layer boundaries.** Service tests mock the repositories but assert the transaction calls
  (`BeginAsync` / `CommitAsync` / `RollbackAsync`), the exact error code / key / type, and every `catch`'s log entry.
- **Contracts between the layers are tested.** The EF model is compared with the SQL schema
  (`ModelMatchesSchemaTests`); every message key a controller can receive must exist in its `en-US` and `ko-KR`
  resources (`ServiceErrorKeyResxTests`); every localized text a page script reads must be rendered by its view.
- **Architecture as tests.** The layering rules above, the project-reference graph and the controller conventions
  fail the build when broken.
- **Client scripts run as shipped.** Vitest + jsdom run the *compiled* `site.js` together with the real layout
  script, against fixtures that carry what the real views and replies carry. Guard tests keep `any` and type
  assertions out of the TypeScript, and a compile probe proves that untyped library values are rejected.
- **Quality gates.** Line coverage >= 80 %, branch coverage >= 65 %, zero build warnings, and on-demand mutation
  testing with Stryker.NET (Domain 92 %, Service 96 % — the survivors left are equivalent mutants).

### Test projects
Every source project has a matching `*.Tests` project (xUnit v3 on Microsoft.Testing.Platform), plus the client-script suite and an end-to-end suite. `Maroik.sln` builds them all. Coverage target: line >= 80 %, branch >= 65 %. At the time of writing the suites hold about 4,300 tests (about 3,300 .NET + 980 Vitest), all passing.

| Tests | What they cover | Needs Docker |
|---|---|---|
| `Maroik.Core.Domain.Tests`, `Maroik.Core.Contract.Tests` | entities, policies, value objects, DTOs, the layering / package-reference architecture rules | no |
| `Maroik.Core.Service.Tests` | use-case orchestration (mocked repositories) | no |
| `Maroik.Core.Repository.Tests` | EF Core repositories against a real PostgreSQL 17 loaded from `Maroik.DB/.../Debugging/Init.sql` | yes |
| `Maroik.Core.PostgreSQL.Tests` | the EF model against the real schema of the init script (tables, columns, keys, foreign keys, indexes) | yes |
| `Maroik.Core.Client.Tests` | mail (in-process SMTP server), file storage, ClamAV, RabbitMQ publisher and health check (real RabbitMQ) | yes |
| `Maroik.Worker.Tests` | the e-mail consumer and the worker host, against a real RabbitMQ and an SMTP relay | yes |
| `Maroik.FileStorage.Tests` | the file-storage service | no |
| `Maroik.Website.Tests` | controllers, filters, views and start-up through `WebApplicationFactory` against a real PostgreSQL | yes |
| `Maroik.Website/TypeScripts.Tests` | every client script (Vitest + jsdom), run with `npm test` in `Maroik.Website/` | no |
| `Maroik.E2E.Tests` | the running site in a real browser (Playwright) | yes |

```bash
dotnet build Maroik.sln -p:RunClientScriptTests=false
dotnet test --project Maroik.Core.Service.Tests            # any single project
(cd Maroik.Website && npm ci && npm test)                  # client scripts
```

## Getting Started

### Prerequisites
- .NET 10.0 SDK or later
- Docker and Docker Compose
- Visual Studio (Windows) or JetBrains Rider (Linux/Mac) — optional, for container debugging

### Quick start (Docker Compose)
```bash
docker compose -f docker-compose.debug.yml up -d --build
```
- Website: https://localhost/ (self-signed certificate — accept the browser warning)
- Mail inbox (Mailpit): http://localhost:8025 — every e-mail the app sends (registration confirmation, password reset) lands here, so no real mail account is needed.

The database is created from `Maroik.DB/PostgreSQL/SQL_Init_Script/Debugging/Init.sql` on the first start. To start over from a clean seed, run `docker compose -f docker-compose.debug.yml down -v`.

### IDE debugging

#### Windows
1. Install Visual Studio
2. Open Maroik.sln
3. Set docker-compose as startup project
4. Press F5 to run in debug mode

#### Linux/Mac
1. Install JetBrains Rider
2. Start debug mode with docker-compose.debug.yml

### Configuration
All local settings live in `.env.debug` (read by `docker-compose.debug.yml`). Every value in it — database and broker passwords, the RSA key pair, the self-signed certificate in `Maroik.SSL/` — is a throwaway default generated for this public repository. **Generate your own before exposing an instance anywhere.** The file documents how to regenerate the RSA key pair.

## Default Accounts

### Admin Account
- ID: admin@maroik.com
- Password: demoO12!!

### User Account (demo)
- ID: demo@maroik.com
- Password: demoO12!!

The login page is pre-filled with the demo account. Its dashboard is pinned to June 2025, where the seeded sample data lives.

## Screenshots

### Login
<img width="551" height="574" alt="0" src="https://github.com/user-attachments/assets/748bba8d-09d0-4dff-9d40-b85df3aa7764" />

### User Dashboard
<img width="1893" height="934" alt="1" src="https://github.com/user-attachments/assets/663dd6a7-9325-4f02-9ae5-057fe3cc5034" />

### Calendar
<img width="1894" height="932" alt="2" src="https://github.com/user-attachments/assets/8cf018c1-0edb-4963-869e-aa9a16da3f75" />

### Forum
<img width="1911" height="935" alt="3" src="https://github.com/user-attachments/assets/222a259e-0be9-44f2-896a-62834060d87c" />

### NonfactorGrid
<img width="1914" height="935" alt="4" src="https://github.com/user-attachments/assets/278f170f-f2ee-4c02-9a3a-1ef6bb6f1c3c" />
<img width="1916" height="930" alt="5" src="https://github.com/user-attachments/assets/74695073-ba46-4ffe-a9ca-e8fe7a995cb4" />

### User Profile
<img width="1885" height="932" alt="6" src="https://github.com/user-attachments/assets/bb54de54-3032-4d09-8e94-ded722824c51" />


## Contributing
1. Fork the repository
2. Create your feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

## Support
For issues and questions, please use the GitHub issue tracker.

## License
This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
