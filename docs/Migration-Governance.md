# DARAK EF Migration Governance

## Purpose

DARAK uses Entity Framework Core migrations as the database contract. Migration mistakes can damage buyer trust, so migration changes must be treated as controlled release events.

## Non-Negotiable Rules

- Do not delete old migrations after they have been shared or applied.
- Do not suppress `PendingModelChangesWarning`.
- Do not create a migration only to silence EF.
- Do not edit `__EFMigrationsHistory` unless the database schema already matches the migration and the repair is explicitly documented.
- Do not apply old phase ZIPs after a newer baseline is created.
- Do not run EF update after failed tests.

## When a Migration Is Required

Create a migration only when the EF model changes, for example:

- adding/removing a table
- adding/removing/changing a column
- adding/removing/changing an index or constraint
- changing a relationship
- changing owned/embedded model mapping

No migration is required for:

- service-only rules
- controller authorization changes
- DTO-only changes that do not affect EF entities
- documentation
- packaging scripts
- tests only

## Safe Migration Workflow

```powershell
cd C:\Users\lenovo\Desktop\DARAK

dotnet clean .\DARAK.sln
dotnet build .\DARAK.sln --no-incremental
dotnet test .\DARAK.sln --no-build

# Only if the EF model was intentionally changed:
dotnet ef migrations add <MigrationName> `
  --project .\DARAK.Api\DARAK.Api.csproj `
  --startup-project .\DARAK.Api\DARAK.Api.csproj

dotnet build .\DARAK.sln --no-incremental
dotnet test .\DARAK.sln --no-build

dotnet ef database update `
  --project .\DARAK.Api\DARAK.Api.csproj `
  --startup-project .\DARAK.Api\DARAK.Api.csproj `
  --connection "<REAL_CONNECTION_STRING>"
```

## Pending Model Changes Response

If EF reports pending model changes:

1. Stop.
2. Do not suppress the warning.
3. Run a probe migration only for inspection.
4. If the probe migration is empty, remove it and investigate database history.
5. If the probe migration contains real operations, review whether those operations are intended.
6. Apply only after build/test are green and the migration is approved.

The helper `tools/Repair-EfPendingModelChanges.ps1` exists only for controlled diagnosis. It must not be used as a shortcut for normal development.

## Database-History Repair Rule

A direct `__EFMigrationsHistory` repair is allowed only when:

- the expected table/index/column already exists with the expected shape,
- EF is attempting to reapply a migration that the database already effectively contains,
- the repair script verifies the schema shape before inserting history,
- the repair is documented in release evidence.

## Phase 7 Status

Phase 7 is documentation, packaging, and governance only. It does not change EF entities and must not require a migration.


## Historical Migration Audit Notes

The following migration-history entries are retained for compatibility with any database that may already have applied them. They must not be deleted or renamed in a shared commercial baseline.

### Historical no-op migrations retained intentionally

These migrations currently have empty `Up` and `Down` methods and are retained only as historical EF migration records:

- `20260610093701_Phase17FinancialConcurrencyTokens.cs`
- `20260616194734_Phase31FinancialGovernanceAdjustmentLinks.cs`

Future empty migrations must be removed immediately before commit unless a written governance note is added before release.

### Historical manual migrations without designer files

These migrations are treated as intentional manual remediation migrations and are allowlisted by exact file name:

- `20260613121500_Phase3ARowVersionConcurrencyHandlers.cs` — rowversion/concurrency remediation.
- `20260613133000_Phase3BIndexesAndOutboxAtomicity.cs` — operational indexes and outbox atomicity remediation.
- `20260613143000_Phase5AOwnershipOccupancyLifecycle.cs` — ownership/occupancy lifecycle uniqueness remediation.

Future manual migrations must include a governance note explaining why EF generation was not used and what schema contract is being changed.

### Retrospective phase-name clarification

The `Phase3A`, `Phase3B`, and `Phase5A` migration names above are retrospective remediation names. Their execution order remains controlled by timestamp, not by the human-readable phase label. Future migrations should use names that match the actual release/remediation order.

## Commercial Packaging Schema Gate

The official commercial source package must not bypass the strongest schema-drift gate. `tools/New-CommercialReleasePackage.ps1` must call `tools/Test-FinalReleaseGate.ps1`, which delegates to `tools/Test-Phase1FinalHardening.ps1`.

If database checks are skipped for packaging, the operator must provide a separate evidence file through `-EvidencePath`. That evidence must belong to the exact source tree being packaged and must include build, test, EF database update or pending-model-check evidence, and final gate output.
