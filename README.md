# DARAK Backend

![.NET](https://img.shields.io/badge/.NET-10-512BD4)
![ASP.NET Core](https://img.shields.io/badge/ASP.NET%20Core-Web%20API-512BD4)
![EF Core](https://img.shields.io/badge/EF%20Core-SQL%20Server-blue)
![Scope](https://img.shields.io/badge/Scope-Backend--Only-orange)
![Status](https://img.shields.io/badge/Status-GitHub--Ready-green)

DARAK is a backend-only ASP.NET Core Web API for residential compound and property-management operations. It models authentication, compound scoping, residents, occupancy, billing, payments, rent, property sales, visitor access, guards, maintenance, staff, vendors, procurement, inventory, documents, approvals, audit, reports, announcements, outages, and notification outbox workflows.

This repository does not include a frontend/mobile app, real payment provider integration, real SMS/email provider setup, production hosting, or production operations ownership.

## Verification

Use `docs/Verification-Evidence.md` as the source of truth for the exact source tree being published.

Minimum local gate:

```powershell
dotnet restore .\DARAK.sln
dotnet build .\DARAK.sln --configuration Release --no-restore
dotnet test .\DARAK.sln --configuration Release --no-build
```

When SQL Server is available:

```powershell
dotnet ef database update `
  --project .\DARAK.Api\DARAK.Api.csproj `
  --startup-project .\DARAK.Api\DARAK.Api.csproj

dotnet ef migrations has-pending-model-changes `
  --project .\DARAK.Api\DARAK.Api.csproj `
  --startup-project .\DARAK.Api\DARAK.Api.csproj
```

The test suite also includes optional SQL Server integration tests. Set `DARAK_SQLSERVER_TEST_CONNECTION` to run them against a temporary SQL database.

## Tech Stack

- .NET 10
- ASP.NET Core Web API
- Entity Framework Core and SQL Server migrations
- ASP.NET Core Identity
- JWT authentication and refresh tokens
- Serilog
- Swagger/OpenAPI in Development
- xUnit and FluentAssertions
- Docker Compose
- GitHub Actions

## Repository Structure

```text
DARAK/
|-- DARAK.Api/                  # ASP.NET Core Web API
|-- DARAK.Tests/                # Unit, service, boundary, readiness, and optional SQL tests
|-- docs/                       # Verification, security, testing, diagrams, and handoff docs
|-- tools/                      # Cleanup, validation, and packaging scripts
|-- .github/workflows/          # GitHub Actions CI
|-- docker-compose.yml          # Local SQL/API compose setup
|-- docker-compose.production.yml
|-- .env.example                # Safe local template
|-- .env.production.example     # Safe production template
`-- DARAK.sln
```

## Main Capabilities

- Identity: login, JWTs, refresh-token rotation, role boundaries, public registration control, first-SuperAdmin bootstrap.
- Structure: compounds, buildings, floors, units, parking, and compound assignment scope.
- Residents: profiles, family members, emergency contacts, active occupancies, lifecycle workflows.
- Finance: utility bills, bill lines, payments, attempts, receipts, ledger entries, rent contracts, sale installments, disputes, collections.
- Access: visitor passes, guard logs, contractor permits, hashed access credentials.
- Operations: maintenance requests, assets, work orders, SLA tracking, staff, vendors, procurement, inventory, purchase orders.
- Communications: announcements, utility outages, resident notification preferences, in-app notifications, notification outbox retry/backoff.
- Governance: documents, document access logs, approvals, audit logs, saved reports, export jobs.

## Local Setup

1. Install the .NET 10 SDK and Docker Desktop or SQL Server.
2. Copy the safe environment template:

```powershell
Copy-Item .\.env.example .\.env
```

3. Edit `.env` locally. Do not commit `.env`.
4. Start SQL Server:

```powershell
docker compose up -d sqlserver
```

5. Restore, build, migrate, and test:

```powershell
dotnet restore .\DARAK.sln
dotnet build .\DARAK.sln --configuration Release --no-restore
dotnet ef database update `
  --project .\DARAK.Api\DARAK.Api.csproj `
  --startup-project .\DARAK.Api\DARAK.Api.csproj
dotnet test .\DARAK.sln --configuration Release --no-build
```

6. Run the API:

```powershell
dotnet run --project .\DARAK.Api\DARAK.Api.csproj
```

Swagger is intended for Development only.

## Configuration Guardrails

`BootstrapAdmin` is disabled by default. It requires explicit local credentials, refuses weak or placeholder values, and skips when a SuperAdmin already exists.

`DemoSeed` is disabled by default. It can seed a broad local dataset for portfolio review, but it runs only in Development, Demo, or Testing unless explicitly overridden and requires a strong local demo password when user seeding is enabled.

`Notifications` are outbox-driven. Optional resident communications respect preferences; critical/urgent operational notices bypass opt-outs.

Report export completion stores sanitized filenames under the controlled report export root and rejects traversal or absolute paths.

## CI

GitHub Actions runs Release restore, build, and test. A separate SQL Server integration job starts a SQL Server container and runs the SQL-specific tests.

## Key Docs

- [Verification Evidence](docs/Verification-Evidence.md)
- [Testing Strategy](docs/Testing-Strategy.md)
- [Security Notes](docs/Security-Notes.md)
- [Known Limitations](docs/Known-Limitations.md)
- [Demo Seed Data](docs/Demo-Seed-Data.md)
- [Architecture Diagrams](docs/Architecture-Diagrams.md)
- [Screenshot Capture Guide](docs/Screenshot-Capture-Guide.md)
- [Deployment Runbook](docs/Deployment-Runbook.md)

## GitHub Hygiene

Before publishing or packaging:

```powershell
.\tools\Clean-BeforeGitHub.ps1
```

Generated files such as `bin/`, `obj/`, logs, `TestResults/`, coverage output, uploads, exports, backup folders, `.env`, and ZIP files are ignored and should not be committed.

## Public Positioning

Good wording:

```text
DARAK is a large ASP.NET Core backend portfolio project for residential compound operations, with authentication, authorization boundaries, compound scoping, financial workflow foundations, operations workflows, document/audit/reporting workflows, tests, and release hygiene.
```

Avoid claiming it is a complete production SaaS until frontend clients, real provider integrations, production operations, and release-specific SQL evidence are completed and recorded.

## Historical Markers

The repository keeps historical phase markers used by regression tests and handoff scripts. These are not a substitute for fresh verification evidence.

- Phase 9 - Final Commercial Completion Pack
  - No migration is required for Phase 9.
  - Gate script: `tools/Test-FinalReleaseGate.ps1`.
- Phase 23 - Final Commercial Review & Hardening Pass
  - Gate script: `tools/Test-CommercialReadiness.ps1`.
  - Package script: `tools/New-CommercialReleasePackage.ps1`.
  - Migration required: none.

## License

No license is currently declared. Add a license before public or commercial distribution.
