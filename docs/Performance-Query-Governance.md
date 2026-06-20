# DARAK Performance & Query Governance

This document defines the minimum query-performance rules for continuing DARAK development after Phase 8C.

## Phase 8C status

Phase 8C adds safe query-performance hardening without changing API contracts, database schema, migrations, endpoints, or business behavior.

## Rules

1. Read-only list/search/detail queries must use `AsNoTracking()` unless the entity is intentionally updated in the same unit of work.
2. Read-only queries that load multiple navigation collections or broad include graphs should use split queries to avoid cartesian explosion.
3. Dashboard/reporting endpoints must aggregate in the database when possible instead of loading full rows and counting in memory.
4. Search/list endpoints must keep pagination before materializing arrays whenever the response does not require full in-memory processing.
5. Performance changes must preserve compound-scope authorization and resident privacy checks.
6. Performance changes must not add migrations unless they introduce intentional database indexes or schema-level optimizations.

## Current hardening areas

- Audit dashboard aggregation now uses database-side grouping/counting.
- Read-heavy detail query factories use read-only split queries for include graphs.
- Work order include graphs keep split query protection for cost items and ratings.
- Regression tests protect the above rules from accidental removal.

## Verification gate

Run:

```powershell
dotnet build .\DARAK.sln --no-incremental
dotnet test .\DARAK.sln --no-build
```

Expected behavior after Phase 8C: all tests pass and no migration is required.
