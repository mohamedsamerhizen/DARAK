# Document Compliance Governance

Phase 8F adds a management-level compliance report for resident document requirements.

## Scope

- Counts active residents inside the current admin compound scope.
- Evaluates active mandatory document requirements.
- Marks gaps when a required document is missing, expired, rejected, pending review, or requires approval but is not approved.
- Keeps all calculations read-only and does not alter document records.

## Operational rules

- Compound scoping is enforced before report generation.
- Deleted documents are excluded.
- Expired documents do not satisfy mandatory requirements.
- Rejected documents do not satisfy mandatory requirements.
- Pending-review documents satisfy only requirements that do not require approval.
- The report is intended for admin/compliance dashboards, not for changing resident document state.

## Database impact

No database schema changes are introduced in this phase.
