# DARAK Testing Evidence

This file explains how test claims must be recorded.

Status: **Recorded in `docs/Verification-Evidence.md`**

---

## Evidence Rule

Do not claim that tests passed unless the command was executed against the exact source tree being published or presented.

Correct wording before execution:

```text
The repository includes an automated test suite.
```

Correct wording after execution:

```text
The test suite was executed on <date> and produced <actual result>.
```

Incorrect wording without proof:

```text
617 tests passed.
```

---

## Required Local Test Command

```powershell
dotnet restore .\DARAK.sln
dotnet build .\DARAK.sln --configuration Release --no-restore
dotnet test .\DARAK.sln --configuration Release --no-build
```

The latest recorded local run is:

```text
Passed! - Failed: 0, Passed: 677, Skipped: 0, Total: 677, Duration: 43 s - DARAK.Tests.dll (net10.0)
```

The full command output is recorded in `docs/Verification-Evidence.md`.

---

## Recommended Evidence Types

- Console output from `dotnet test`.
- TRX test result file generated locally or in CI.
- GitHub Actions run URL after publishing.
- SQL Server-backed integration test output when available.

Do not commit TRX/TestResults folders unless the repository intentionally stores release evidence. Prefer attaching artifacts to release notes or keeping them outside source control.

---

## Test Quality Expectations

The test suite should protect:

- Authentication and refresh-token behavior.
- Role and endpoint authorization boundaries.
- Compound/tenant isolation.
- Resident privacy.
- Financial correctness and duplicate-payment prevention.
- Ledger/payment/bill/rent/installment consistency.
- Visitor/guard access workflows.
- Maintenance/SLA/preventive generation behavior.
- Procurement/inventory integrity.
- Notification outbox behavior.
- Migration and schema governance.
- Documentation and GitHub readiness checks.

---

## SQL Server Integration Evidence

EF InMemory tests are useful, but they do not fully prove:

- Foreign key behavior.
- Unique indexes.
- Delete behaviors.
- Decimal precision.
- SQL transactions.
- Concurrency behavior.
- Migration compatibility.

The suite includes optional SQL Server-backed tests. Release evidence must state whether they were actually run against SQL Server for the exact source tree.
