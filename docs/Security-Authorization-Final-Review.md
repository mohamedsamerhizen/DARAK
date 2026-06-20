# DARAK Final Security and Authorization Review

## Current Security Position

DARAK includes JWT authentication, refresh token hardening, Identity roles, compound-scoped authorization, startup configuration validation, environment-driven secrets and tests for critical admin/resident separation.

## Critical Authorization Boundaries

- `Resident` must never access `/api/admin/*` APIs.
- `Guard` must remain restricted to guard workflows and must not access finance, billing, audit or approvals.
- `CompoundAdmin` must only manage assigned compounds.
- `Accountant` should remain limited to financial/reporting operations and must not close security-sensitive workflows unless explicitly allowed.
- `SuperAdmin` is global and must be protected by strong credentials and operational policy.

## Buyer-Grade Security Checks

- Compound scope must be checked before returning, deleting or mutating compound-owned data.
- Search endpoints must not leak existence of resources outside scope.
- Sensitive document actions must return `NotFound` for inaccessible resources when appropriate.
- Approval decisions must obey policy-required roles and self-approval rules.
- Notification, report and support workflows must write audit logs.
- System settings and license changes must be audited.
- Visitor search/list views must not expose raw access codes.
- Document uploads must validate file signature and owner/compound consistency.
- Approval execution must not be performed by the requester or by the same approver who approved the request.
- Work orders must follow valid assignment, scheduling, cost and final-state rules.

## Operational Risks to Monitor

- Misconfigured `.env` values.
- Weak JWT secret in production.
- SMS/Email provider failures.
- Unreviewed background job failures.
- Unscoped future endpoints introduced after this release.
- Manual database edits outside the API.

## Recommended Production Enhancements After First Sale

- Add row-version concurrency tokens to high-risk entities.
- Add relational SQL-backed integration tests for finance and approval workflows.
- Add provider-specific SMS/Email sandbox tests.
- Add storage abstraction for documents if the buyer needs cloud object storage.
- Add centralized rate limiting policy per sensitive endpoint group.
