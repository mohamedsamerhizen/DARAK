# DARAK Testing Evidence

## Latest Phase

Phase 9 adds final commercial completion documentation, a final release gate script, and governance tests. It does not introduce a database migration.

## Required Local Evidence

Record the output of:

```powershell
dotnet clean .\DARAK.sln
dotnet build .\DARAK.sln --no-incremental
dotnet test .\DARAK.sln --no-build
```

## Expected Verification Areas

- Authentication and authorization.
- Compound scope boundaries.
- Resident privacy.
- Billing, payments, receipts, and dispute foundations.
- Documents, compliance, and upload safety.
- Visitor and guard workflows.
- Maintenance, complaints, operations, and approvals.
- Notifications, audit, reporting, and commercial readiness.

## Final Evidence Rule

Attach the terminal test summary to the final delivery notes. Do not claim production readiness without a successful build, test run, and clean release archive.
