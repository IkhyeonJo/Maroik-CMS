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
- Financial reports and analytics
- Export to Excel

### 2. Calendar and Schedule Management
- Monthly calendar view
- Event creation and management

### 3. Bulletin Board System
- Post creation, editing, and deletion
- Comment system
- File attachments (virus-scanned with ClamAV)
- Search functionality

### 4. User Management
- Role-based access control (Admin/User)
- User profile management
- Session-based authentication
- Password management

### 5. Admin Dashboard
- User management
- System settings
- Analytics dashboard

## Technologies Used

### Backend
- ASP.NET Core MVC 10.0
- PostgreSQL 17
- Entity Framework Core 10.0
- Valkey (Redis-compatible) — session store and Data Protection key ring
- RabbitMQ 4 — outbound e-mail queue consumed by `Maroik.Worker`
- ClamAV — upload scanning in `Maroik.FileStorage`
- Docker
  - Multi-stage builds
  - Container orchestration
  - Environment isolation
- Authentication & Authorization
  - Session Authentication
  - Role-based access control
- Cross-Origin Resource Sharing (CORS)

### Frontend
- AdminLTE 3
- Bootstrap 5.3
- jQuery 3.7
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
- Chart.js 4.4
- Font Awesome 6
- DataTables
- SweetAlert2

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

## Testing
Every source project has a matching `*.Tests` project (xUnit v3 on Microsoft.Testing.Platform), plus the client-script suite and an end-to-end suite. `Maroik.sln` builds them all. Coverage target: line >= 80 %, branch >= 65 %.

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
