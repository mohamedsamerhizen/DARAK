# DARAK Verification Evidence

This file records verification results for the exact source tree being published or presented.

Status: **Recorded**

---

## GitHub Presentation Asset Note

- Presentation pass date: `2026-06-30`
- Scope: README, documentation, architecture diagrams, screenshot guide, and GitHub metadata guidance.
- Backend behavior changed: no.
- Migration required: none.
- Real visual assets added under `docs/assets/diagrams/` and `docs/assets/social-preview/`.
- Swagger screenshot status: not committed. A real local capture attempt reached Swagger UI, but OpenAPI JSON generation failed because `DARAK.Api.DTOs.Support.SupportDashboardResponse` and `DARAK.Api.DTOs.Communication.SupportDashboardResponse` collide on the same Swagger schema ID.
- Screenshot policy: capture real reviewed screenshots only after the running API successfully serves the OpenAPI definition. See `docs/Screenshot-Capture-Guide.md`.

---

## Final GitHub-Ready Backend Evidence

- Project: DARAK Backend
- Source folder: `<repo-root>`
- Target package: `DARAK-GITHUB-READY-FINAL.zip`
- Operator: Codex
- Date/time: `2026-06-30 17:51:57 +03:00`
- Git commit, if applicable: `09faf79` with local uncommitted GitHub-readiness changes
- .NET SDK version: `10.0.103`

Restore command:

```powershell
dotnet restore .\DARAK.sln
```

Result:

```text
Determining projects to restore...
All projects are up-to-date for restore.
Exit code: 0
```

Release build command:

```powershell
dotnet build .\DARAK.sln --configuration Release --no-restore
```

Result:

```text
DARAK.Api -> <repo-root>\DARAK.Api\bin\Release\net10.0\DARAK.Api.dll
DARAK.Tests -> <repo-root>\DARAK.Tests\bin\Release\net10.0\DARAK.Tests.dll

Build succeeded.
0 Warning(s)
0 Error(s)
Exit code: 0
```

Release test command:

```powershell
dotnet test .\DARAK.sln --configuration Release --no-build
```

Result:

```text
Passed!  - Failed:     0, Passed:   677, Skipped:     0, Total:   677, Duration: 43 s - DARAK.Tests.dll (net10.0)
Exit code: 0
```

SQL Server availability:

```text
docker info: Docker server running, version 29.3.1.
Test-NetConnection localhost:1433: TcpTestSucceeded True.
```

EF database update command:

```powershell
$env:ConnectionStrings__DefaultConnection = "<local SQL Server connection string>"
dotnet ef database update `
  --project .\DARAK.Api\DARAK.Api.csproj `
  --startup-project .\DARAK.Api\DARAK.Api.csproj
```

Result:

```text
Build started...
Build succeeded.
Acquiring an exclusive lock for migration application.
No migrations were applied. The database is already up to date.
Done.
Exit code: 0
```

Pending model changes command:

```powershell
$env:ConnectionStrings__DefaultConnection = "<local SQL Server connection string>"
dotnet ef migrations has-pending-model-changes `
  --project .\DARAK.Api\DARAK.Api.csproj `
  --startup-project .\DARAK.Api\DARAK.Api.csproj
```

Result:

```text
Build started...
Build succeeded.
No changes have been made to the model since the last migration.
Exit code: 0
```

SQL integration test note:

```text
The optional SQL integration tests were run against local SQL Server before a historical migration-chain fix and exposed a fresh-database duplicate-index failure in 20260614114609_Phase5AOwnershipTransferPropertyUnitIndexFixFinal.
The migration was corrected to use idempotent IF NOT EXISTS SQL, and the full Release suite passed afterward.
A post-fix live SQL integration rerun was requested but blocked by the approval system, so fresh-database SQL integration proof after that exact fix was not rerun in this session.
```

---

## Phase 06-08 Remediation Evidence

- Project: DARAK Backend
- Source folder: `<repo-root>`
- Source ZIP/package: `DARAK-REMEDIATION-PHASES-06-07-08.zip`
- Operator: Codex
- Date: `2026-06-30`
- .NET SDK version: `10.0.103`
- Migration applied: `20260630133311_HashAccessCodesContractorAuditMaintenanceInventoryGuards`

Restore command:

```powershell
dotnet restore .\DARAK.sln
```

Result:

```text
Determining projects to restore...
All projects are up-to-date for restore.
Exit code: 0
```

Build command:

```powershell
dotnet build .\DARAK.sln --configuration Release --no-restore
```

Result:

```text
DARAK.Api -> <repo-root>\DARAK.Api\bin\Release\net10.0\DARAK.Api.dll
DARAK.Tests -> <repo-root>\DARAK.Tests\bin\Release\net10.0\DARAK.Tests.dll

Build succeeded.
0 Warning(s)
0 Error(s)
Exit code: 0
```

Test command:

```powershell
dotnet test .\DARAK.sln --configuration Release --no-build
```

Result:

```text
Passed!  - Failed:     0, Passed:   659, Skipped:     0, Total:   659, Duration: 39 s - DARAK.Tests.dll (net10.0)
Exit code: 0
```

SQL Server availability:

```text
docker info: Docker server running.
Test-NetConnection localhost:1433: TcpTestSucceeded True.
```

EF database update command:

```powershell
$env:ConnectionStrings__DefaultConnection = "Server=localhost,1433;Database=DARAKDb;User Id=sa;Password=Darak_dev_2026!;TrustServerCertificate=True;Encrypt=True"

dotnet ef database update `
  --project .\DARAK.Api\DARAK.Api.csproj `
  --startup-project .\DARAK.Api\DARAK.Api.csproj `
  --connection "$env:ConnectionStrings__DefaultConnection"
```

Result:

```text
Build started...
Build succeeded.
Applying migration '20260630133311_HashAccessCodesContractorAuditMaintenanceInventoryGuards'.
Done.
Exit code: 0
```

Pending model changes command:

```powershell
dotnet ef migrations has-pending-model-changes `
  --project .\DARAK.Api\DARAK.Api.csproj `
  --startup-project .\DARAK.Api\DARAK.Api.csproj
```

Result:

```text
Build started...
Build succeeded.
No changes have been made to the model since the last migration.
Exit code: 0
```

Package evidence:

```text
Created <repo-root>\DARAK-REMEDIATION-PHASES-06-07-08.zip
ZIP entry count: 943
Forbidden entry count: 0
Excluded: .git, .agents, .codex, bin, obj, .vs, .idea, TestResults, coverage, logs, backup folders, root files/, secret .env files, nested ZIPs, TRX files, and .log files.
```

---

## Phase 03-05 Remediation Evidence

- Project: DARAK Backend
- Source folder: `<repo-root>`
- Source ZIP/package: `DARAK-REMEDIATION-PHASES-03-04-05.zip`
- Operator: Codex
- Date: `2026-06-30`
- .NET SDK version: `10.0.103`

Restore command:

```powershell
dotnet restore .\DARAK.sln
```

Result:

```text
Determining projects to restore...
All projects are up-to-date for restore.
Exit code: 0
```

Build command:

```powershell
dotnet build .\DARAK.sln --configuration Release --no-restore
```

Result:

```text
DARAK.Api -> <repo-root>\DARAK.Api\bin\Release\net10.0\DARAK.Api.dll
DARAK.Tests -> <repo-root>\DARAK.Tests\bin\Release\net10.0\DARAK.Tests.dll

Build succeeded.
0 Warning(s)
0 Error(s)
Exit code: 0
```

Test command:

```powershell
dotnet test .\DARAK.sln --configuration Release --no-build
```

Result:

```text
Passed!  - Failed:     0, Passed:   650, Skipped:     0, Total:   650, Duration: 36 s - DARAK.Tests.dll (net10.0)
Exit code: 0
```

EF pending model changes command:

```powershell
$env:ConnectionStrings__DefaultConnection='Server=(localdb)\mssqllocaldb;Database=DARAK_Migrations;Trusted_Connection=True;TrustServerCertificate=True'
dotnet ef migrations has-pending-model-changes --project .\DARAK.Api\DARAK.Api.csproj --startup-project .\DARAK.Api\DARAK.Api.csproj --no-build
```

Result:

```text
No changes have been made to the model since the last migration.
Exit code: 0
```

SQL Server verification not run because Docker/SQL Server was unavailable.

Docker/SQL checks:

```text
docker info: Docker client present, daemon unavailable at npipe:////./pipe/docker_engine.
Test-NetConnection localhost:1433: TcpTestSucceeded False.
```

Package evidence:

```text
Created <repo-root>\DARAK-REMEDIATION-PHASES-03-04-05.zip
ZIP entry count: 932
Forbidden entry count: 0
Excluded: .git, .agents, .codex, bin, obj, .vs, .idea, TestResults, coverage, logs, backup folders, root files/, secret .env files, nested ZIPs, and .log files.
```

---

## Baseline Identity

- Project: DARAK Backend
- Source folder: `<repo-root>`
- Source ZIP/package: `DARAK-REMEDIATION-PHASES-00-01-02.zip`
- Operator: Codex
- Machine: `DESKTOP-O6COQK7`
- Date/time: `2026-06-30 14:04:57 +03:00`
- .NET SDK version: `10.0.103`
- Git commit, if applicable: `09faf79` with local uncommitted remediation changes
- SQL Server target: unavailable locally during this run

---

## Restore Evidence

Command:

```powershell
dotnet restore .\DARAK.sln
```

Result:

```text
Determining projects to restore...
All projects are up-to-date for restore.
Exit code: 0
```

---

## Build Evidence

Command:

```powershell
dotnet build .\DARAK.sln --configuration Release --no-restore
```

Result:

```text
DARAK.Api -> <repo-root>\DARAK.Api\bin\Release\net10.0\DARAK.Api.dll
DARAK.Tests -> <repo-root>\DARAK.Tests\bin\Release\net10.0\DARAK.Tests.dll

Build succeeded.

0 Warning(s)
0 Error(s)
Exit code: 0
```

---

## Test Evidence

Command:

```powershell
dotnet test .\DARAK.sln --configuration Release --no-build
```

Result:

```text
Test run for <repo-root>\DARAK.Tests\bin\Release\net10.0\DARAK.Tests.dll (.NETCoreApp,Version=v10.0)
VSTest version 18.0.1 (x64)

Starting test execution, please wait...
A total of 1 test files matched the specified pattern.

Passed!  - Failed:     0, Passed:   629, Skipped:     0, Total:   629, Duration: 32 s - DARAK.Tests.dll (net10.0)
Exit code: 0
```

---

## EF Database Update Evidence

Command:

```powershell
dotnet ef database update `
  --project .\DARAK.Api\DARAK.Api.csproj `
  --startup-project .\DARAK.Api\DARAK.Api.csproj
```

Result:

```text
SQL Server verification not run because Docker/SQL Server was unavailable.

Docker check:
failed to connect to the docker API at npipe:////./pipe/docker_engine; check if the path is correct and if the daemon is running.

Local SQL Server port check:
Test-NetConnection localhost:1433 returned False.
```

---

## Pending Model Changes Evidence

Command:

```powershell
dotnet ef migrations has-pending-model-changes `
  --project .\DARAK.Api\DARAK.Api.csproj `
  --startup-project .\DARAK.Api\DARAK.Api.csproj
```

Result:

```text
SQL Server verification not run because Docker/SQL Server was unavailable.
```

---

## Package Hygiene Evidence

Checklist:

- [x] `.env` was not present in the repository root during verification.
- [x] Root generated batch ZIPs were moved into `_artifact-backup-20260630-134737`.
- [x] The historical duplicated root `files/` overlay is absent from the workspace root.
- [x] `.gitignore` excludes `bin/`, `obj/`, `.vs/`, `.idea/`, `.env`, ZIPs, logs, temp folders, test results, coverage, artifact backups, and root `files/`.
- [x] `.dockerignore` excludes local build/test/artifact folders from Docker build context.
- [x] Final remediation ZIP evidence recorded.

Evidence:

```text
Build/test generated local bin/obj folders and a test log under DARAK.Tests/bin. These are ignored and must be excluded from source packages.
Created DARAK-REMEDIATION-PHASES-00-01-02.zip on 2026-06-30 after final restore/build/test verification.
ZIP entry count: 926.
ZIP artifact scan found no .git, .agents, bin, obj, TestResults, coverage, logs, backup folders, root files overlay, .env, nested ZIP, TRX, coverage, or log entries.
```

---

## Final Verification Decision

- [x] Verified for local restore/build/test.
- [x] Verified for technical interview presentation as a backend portfolio project.
- [ ] SQL Server-backed database verification was not run.
- [ ] Not verified for live production/SaaS use.

Final notes:

```text
Restore, Release build, and Release test commands passed on 2026-06-30.
SQL Server/Docker verification was unavailable in this environment.
```

