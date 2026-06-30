# DARAK Commercial Verification Evidence

This document is an evidence record, not a marketing document.

Status: **Recorded with SQL caveat**

DARAK must not be described as live production SaaS. This file records local backend verification for the exact source tree being packaged.

---

## Release Identity

- Release name: DARAK GitHub-ready backend package
- Baseline ZIP/source: `DARAK-GITHUB-READY-FINAL.zip`
- Operator: Codex
- Date/time: `2026-06-30 17:51:57 +03:00`
- Machine: local workspace
- Database target: local SQL Server on `localhost:1433`
- Environment: local verification

---

## Source Package Hygiene

- [x] Final package is intended to contain no `.env`.
- [x] Final package is intended to contain no `bin/` or `obj/`.
- [x] Final package is intended to contain no `.git`, `.vs`, `.vscode`.
- [x] Final package is intended to contain no logs/uploads/exports/TestResults.
- [x] Final package is intended to contain no nested ZIP/patch archives.
- [x] Final package is intended to contain no root `files/` overlay folder.

Evidence:

```text
Package hygiene output is recorded in `docs/Verification-Evidence.md` after final archive creation.
```

---

## Build Evidence

Command:

```powershell
dotnet build .\DARAK.sln --configuration Release --no-restore
```

Result:

```text
Build succeeded with 0 warnings and 0 errors.
```

---

## Test Evidence

Command:

```powershell
dotnet test .\DARAK.sln --configuration Release --no-build
```

Result:

```text
Passed!  - Failed:     0, Passed:   677, Skipped:     0, Total:   677, Duration: 43 s - DARAK.Tests.dll (net10.0)
```

---

## EF Database Evidence

Command:

```powershell
dotnet ef database update `
  --project .\DARAK.Api\DARAK.Api.csproj `
  --startup-project .\DARAK.Api\DARAK.Api.csproj
```

Result:

```text
Build succeeded. No migrations were applied. The database is already up to date.
```

---

## Migration Drift Evidence

Command:

```powershell
dotnet ef migrations has-pending-model-changes `
  --project .\DARAK.Api\DARAK.Api.csproj `
  --startup-project .\DARAK.Api\DARAK.Api.csproj
```

Result:

```text
Build succeeded. No changes have been made to the model since the last migration.
```

---

## Commercial Limitations

The following limitations must remain visible unless they are explicitly implemented and verified:

- Frontend/mobile app is not included.
- Real payment-provider integration is not included.
- SMS/Email providers require buyer credentials and sandbox testing.
- Production hosting, monitoring, backups, SLA, incident response, and license terms are not included in the backend source alone.
- Financial, tenant-isolation, access-code, and SQL-backed integration hardening must be completed before live commercial use.
- A post-fix live SQL integration rerun was blocked by the approval system in this session; see `docs/Verification-Evidence.md`.

---

## Sign-Off

- Technical validation accepted by:
- Buyer/operator accepted by:
- Notes:
