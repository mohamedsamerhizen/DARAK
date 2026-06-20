# DARAK Release Governance

## Purpose

This document defines the release rules for the DARAK commercial backend. Its goal is to stop accidental regressions, migration drift, unsafe packaging, and unclear buyer handover.

## Official Baseline Rule

Every release must start from the latest verified clean baseline ZIP only. A baseline is valid only when all gates below were completed on the same source tree:

- `dotnet build .\DARAK.sln`
- `dotnet test .\DARAK.sln`
- `dotnet ef database update` against the target SQL Server database
- clean source ZIP created without secrets, build output, logs, local uploads, `.env`, or old patch archives

Current verified remediation baseline after Phase 5C:

- Phase 5B: documents, visitors and communication hardening completed
- Phase 5C: operations and approvals governance completed
- Tests: latest clean `dotnet test .\DARAK.sln --no-build` output captured for the delivered source tree
- EF database status: no pending model drift expected

## Release Branch Discipline

Recommended workflow:

1. Start from the latest verified baseline.
2. Apply exactly one phase or hotfix.
3. Run build and test.
4. Run EF update only after tests pass.
5. Create a new clean baseline ZIP.
6. Do not apply older phase ZIPs over a newer baseline.

## No-Overwrite Rule

Never extract a baseline ZIP over an existing working project. Restore into a new folder or rename the old folder first. Patch ZIPs may be extracted over the active project only when they were built for the exact current baseline.

## Hotfix Rule

A hotfix is allowed only to repair a failed phase. It must not introduce unrelated features. After the hotfix passes, create a new success baseline and treat the failed intermediate state as obsolete.

## Release Evidence Required

Each buyer/demo release should preserve a short evidence file outside the source package containing:

- build command and result
- test command and result
- EF database update result
- final ZIP name
- known limitations
- date/time and operator

Use `docs/Commercial-Verification-Evidence.md` as the template.

## Delivery Rule

The buyer/source archive must not contain:

- `.env`
- real secrets
- `bin` / `obj`
- `.git` / `.vs` / `.vscode`
- `TestResults`
- logs
- local uploads
- ZIP/patch archives
- repair backup folders

Use `tools/Test-Phase7ReleaseGate.ps1` before creating a delivery archive.


## PASS 09 Migration Gate Requirement

Commercial release packaging must run `tools/Test-FinalReleaseGate.ps1` before creating the buyer ZIP. `tools/Test-Phase7ReleaseGate.ps1` is not sufficient for buyer delivery because it does not execute the EF database update and pending-model-change checks.

When `-SkipDatabase` is used for packaging, a separate evidence file must be supplied and archived with the release. The evidence must include build, test, EF database update or pending-model-check output, and final gate output for the exact source tree being delivered.
