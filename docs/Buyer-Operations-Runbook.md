# DARAK Buyer Operations Runbook

## Daily Operations

- Review `GET /api/admin/system/health`.
- Review open integration failures.
- Review background job runs.
- Review operational command center.
- Review overdue payments and aging report.
- Review support dashboard and escalated cases.
- Review pending approvals and high-priority risk flags.

## Weekly Operations

- Export financial management report.
- Review audit logs for sensitive actions.
- Review unresolved support cases.
- Review expired or missing documents.
- Review failed notification deliveries.
- Confirm SQL backups completed successfully.

## Monthly Operations

- Review revenue summary.
- Review occupancy report.
- Review maintenance performance.
- Review support performance.
- Review user/role assignments.
- Review license profile and usage limits.

## Incident Workflow

1. Open system health dashboard.
2. Check integration failure events.
3. Check recent audit logs.
4. Put system in maintenance mode if data integrity is at risk.
5. Notify affected admin users.
6. Record resolution notes.
7. Confirm recovery with health checks and targeted API tests.

## Admin Role Policy

- Use `SuperAdmin` only for global settings, license, system configuration and emergency recovery.
- Use `CompoundAdmin` for compound-specific operations.
- Use `Accountant` for financial workflows and reports.
- Use `Guard` only for guard-related operations.
- Use `Resident` only through resident-facing APIs.


## Release Operation Rules

- Keep one verified clean baseline ZIP before each change wave.
- Do not apply old patch ZIPs after a newer baseline is created.
- Keep build/test/EF evidence for every buyer/demo package.
- Review `docs/Migration-Governance.md` before approving any database migration.
- Run `tools/Test-Phase7ReleaseGate.ps1` before commercial handover.
