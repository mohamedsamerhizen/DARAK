# DARAK Testing Strategy

This document separates tests that exist, tests that have been run, and tests that still require SQL Server-backed evidence.

---

## Test Layers

### 1. Unit Tests

Used for isolated business rules and service behavior.

Examples:

- DTO validation behavior.
- Status transition rules.
- Permission helper logic.
- Financial calculation functions.

### 2. Service Tests

Used for domain workflows where repositories/DbContext behavior is involved.

Examples:

- Payment creation.
- Ledger entry creation.
- Move-out blockers.
- Preventive maintenance generation.
- Notification outbox creation.

### 3. API/Authorization Tests

Used for route, role, and endpoint boundary verification.

Examples:

- Resident cannot access another resident's data.
- Guard cannot call admin endpoints.
- Accountant cannot mutate risk flags if policy says read-only.
- Compound admin cannot access another compound.

### 4. SQL Server Integration Tests

Required for relational proof.

Examples:

- Unique indexes.
- Foreign keys.
- Delete behavior.
- Transaction rollback.
- Concurrency tokens.
- Migration compatibility.

---

## Minimum Release Gate

Before GitHub/interview/company-demo claims:

```powershell
dotnet restore .\DARAK.sln
dotnet build .\DARAK.sln --configuration Release --no-restore
dotnet test .\DARAK.sln --configuration Release --no-build
```

Record the actual terminal output in `docs/Verification-Evidence.md`. A test file existing in source is not the same thing as a test having passed on the current source tree.

Before production-style/commercial claims:

```powershell
dotnet ef database update `
  --project .\DARAK.Api\DARAK.Api.csproj `
  --startup-project .\DARAK.Api\DARAK.Api.csproj

dotnet ef migrations has-pending-model-changes `
  --project .\DARAK.Api\DARAK.Api.csproj `
  --startup-project .\DARAK.Api\DARAK.Api.csproj
```

---

## High-Risk Areas That Must Have Tests

- Authentication and refresh-token rotation.
- Public registration and first-admin provisioning.
- Role boundaries.
- Compound/tenant isolation.
- Financial idempotency.
- Payment/Ledger consistency.
- Balance-forward behavior.
- Payment plan payment recording.
- Cash sale/down-payment recording.
- Move-out financial clearance.
- Visitor/access-code verification.
- Contractor credential verification and contractor access logs.
- Preventive maintenance generation idempotency.
- SLA breach/escalation refresh behavior.
- Staff/vendor compound assignment.
- Inventory concurrency.
- Negative stock prevention and purchase-order receipt idempotency.
- Notification scoping and preferences.
- Report/export path safety.
- Demo seed disabled-by-default behavior, production guard, idempotency, and hashed access data.

---

## Tests That Exist

The repository includes a broad xUnit suite under `DARAK.Tests`, including controller behavior tests, service tests, authorization boundary tests, refresh-token tests, documentation/readiness checks, and startup configuration tests.

## Tests That Have Been Run

Use `docs/Verification-Evidence.md` as the source of truth. Do not claim a pass count or a green test suite unless the command output is recorded there for the exact current source tree.

Latest Phase 03-05 local evidence recorded on 2026-06-30:

```text
dotnet restore .\DARAK.sln
dotnet build .\DARAK.sln --configuration Release --no-restore
dotnet test .\DARAK.sln --configuration Release --no-build

Passed: 650
Failed: 0
Skipped: 0
```

Latest Phase 06-08 Release verification recorded on 2026-06-30:

```text
dotnet restore .\DARAK.sln
dotnet build .\DARAK.sln --configuration Release --no-restore
dotnet test .\DARAK.sln --configuration Release --no-build

Passed: 659
Failed: 0
Skipped: 0
```

Latest final GitHub-ready evidence recorded on 2026-06-30:

```text
dotnet restore .\DARAK.sln
dotnet build .\DARAK.sln --configuration Release --no-restore
dotnet test .\DARAK.sln --configuration Release --no-build

Passed: 677
Failed: 0
Skipped: 0
```

Final verification for the current source tree must still be read from `docs/Verification-Evidence.md` and refreshed whenever backend code changes.

## InMemory Tests

Many tests use EF Core InMemory for fast behavior checks. These tests are valuable for service logic, authorization checks, controller behavior, and regression coverage.

InMemory tests do not prove SQL Server foreign keys, indexes, migrations, transaction behavior, locking, concurrency, query translation, or provider-specific constraints.

## SQL Server-Backed Tests

The test suite includes optional SQL Server integration tests in `SqlServerIntegrationReadinessTests`. They run when `DARAK_SQLSERVER_TEST_CONNECTION` or `ConnectionStrings__DefaultConnection` points to a reachable SQL Server instance. They create a unique temporary database, apply migrations, verify no pending migrations, check a real unique index, and exercise the negative-stock guard against SQL Server.

These tests pass through when no SQL Server connection is configured, so release evidence must explicitly say whether SQL Server was available during verification.
