# DARAK Final Commercial Release Notes

## Release Goal

This release closes the first commercial version of DARAK as a buyer-ready backend package with explicit deployment, migration, release, packaging and verification governance.

## Completed Commercial Capabilities

- Compound and property structure management.
- Resident, ownership and occupancy management.
- Utility billing, payments, installments and rent workflows.
- Meter reading management and commercial billing rule extensions.
- Admin/resident communication and notification outbox.
- Document management with requirements, expiry and approval/rejection.
- Admin action approvals for sensitive workflows.
- Resident risk flags and operational command center.
- Financial control center with ledger, adjustments, statements, aging and revenue reporting.
- Audit, compliance and legal traceability.
- Support cases, SLA foundation and management intelligence reports.
- Commercial settings, license profile, maintenance mode, system health and observability.
- Phase 5B hardening for documents, visitors and communication.
- Phase 5C governance for operations and approval execution.
- Phase 7 governance for deployment, migrations, release evidence and buyer handover.

## Release Validation

Before handover, run:

```powershell
.\tools\Test-CommercialReadiness.ps1
.\tools\Test-Phase7ReleaseGate.ps1 -SkipDotnet
.\tools\New-CommercialReleasePackage.ps1
```

## Known Production Decisions

The following must be decided with the buyer before live production:

- Hosting environment.
- Backup schedule.
- SMS/Email providers.
- Document storage policy.
- Support SLA terms.
- License terms.
- SuperAdmin ownership and recovery process.

## Recommended Next Version

After first buyer pilot, prioritize:

- SQL-backed relational test pack.
- Row-version concurrency for high-risk workflows.
- Document storage abstraction.
- Advanced payment reconciliation.
- Buyer-specific report exports.


## Current Validation Target

The current remediation baseline after Phase 5C/7 should pass:

```text
Build: OK
Tests: capture the latest `dotnet test .\DARAK.sln --no-build` output for the delivered source tree.
EF database update: no pending migrations or successfully applied target migrations
```

Phase 7 requires no migration.
