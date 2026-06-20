# DARAK Admin Reporting and Export Governance

## Purpose

This document defines the release and handoff rules for administrative reports and export-style workflows.

Phase 9 does not add heavy reporting infrastructure. It finalizes the commercial expectations around report access, export safety, scope, and handoff evidence.

## Report Categories

The project should present reporting coverage across these areas:

- Financial dashboards and summaries.
- Payment and receipt history.
- Document compliance.
- Visitor and guard access logs.
- Maintenance, complaints, violations, and operations.
- Audit and management intelligence.

## Access Rules

Reporting is an admin capability. The expected rules are:

- Residents cannot access admin reports.
- Guards cannot access finance or management reports.
- Compound admins see only assigned compounds.
- Accountants see only allowed financial/reporting areas.
- SuperAdmin can view global reports.

## Export Safety Rules

If a report is exported as CSV or another machine-readable format:

- Apply the same filters and scoping as the API query.
- Do not export secrets, password hashes, refresh tokens, or access codes.
- Mask sensitive visitor access codes.
- Include generated timestamp and query scope in export metadata when practical.
- Avoid storing generated files permanently unless a retention policy exists.

## Job Lifecycle Rule

Long-running exports should use a controlled lifecycle:

```text
Queued -> Processing -> Completed
Queued -> Cancelled
Processing -> Failed
```

Phase 9 treats this as governance unless the project already has an export job implementation available.

## Commercial Review Evidence

A reviewer should be able to verify:

- Report controllers are role-protected.
- Service methods apply compound scope.
- Search and list endpoints paginate results.
- Sensitive fields are excluded or masked.
- Export/runbook documentation exists.

## Verification Checklist

- Build succeeds.
- Tests succeed.
- Reporting docs exist.
- Release gate checks docs and scripts.
- No generated report files are committed.
