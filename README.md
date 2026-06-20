# DARAK API

DARAK is an ASP.NET Core backend for compound and residential property operations. It covers identity, compound structure, residents, occupancy, utilities, meters, payments, contracts, visitors, maintenance, complaints, documents, work orders, analytics, support, audit, reporting, release governance, and commercial handover.

## Stack

- .NET 10 / ASP.NET Core Web API
- Entity Framework Core with SQL Server
- ASP.NET Core Identity with JWT access tokens and refresh tokens
- Serilog console/file logging
- Swagger in Development
- xUnit, FluentAssertions, EF Core InMemory tests

## Projects

- `DARAK.Api` - API, EF Core model, migrations, controllers, services, authentication, middleware.
- `DARAK.Tests` - service/controller tests for financial flows, access scoping, ownership, meters, documents, visitors, operations, approvals, communication, and API result mapping.

## Configuration

Copy `.env.example` to `.env` for Docker, or set equivalent user secrets/environment variables for local runs.

Required values:

- `ConnectionStrings__DefaultConnection` or `ConnectionStrings:DefaultConnection`
- `Jwt__Issuer`
- `Jwt__Audience`
- `Jwt__SecretKey` with at least 32 bytes
- `Jwt__AccessTokenMinutes`
- `Jwt__RefreshTokenDays`
- `DevelopmentSuperAdmin__Email` in Development
- `DevelopmentSuperAdmin__Password` in Development
- `DevelopmentSuperAdmin__FullName` in Development

Optional notification delivery values:

- `Notifications__WorkerEnabled`
- `Notifications__Email__Enabled`, `Notifications__Email__Host`, `Notifications__Email__Username`, `Notifications__Email__Password`, `Notifications__Email__FromEmail`
- `Notifications__Sms__Enabled`, `Notifications__Sms__EndpointUrl`, `Notifications__Sms__ApiKey`, `Notifications__Sms__SenderId`

Do not commit real secrets. The checked-in settings use placeholders. Before creating a GitHub archive, run `tools/Clean-BeforeGitHub.ps1` to remove `.env`, ZIP handoff files, build outputs, logs, and local uploads.

## Run Locally

```powershell
dotnet restore .\DARAK.sln
dotnet build .\DARAK.sln
dotnet ef database update --project .\DARAK.Api\DARAK.Api.csproj --startup-project .\DARAK.Api\DARAK.Api.csproj
dotnet run --project .\DARAK.Api\DARAK.Api.csproj
```

Development Swagger is available at `/swagger`. Health checks are available at `/health` and are intentionally unauthenticated for local Docker/container orchestration checks.

## Docker

```powershell
copy .env.example .env
docker compose up --build
```

The API listens on `http://localhost:8080`, and SQL Server is exposed on `localhost:1433`.

## Database Migrations

EF migrations are part of the release contract. Do not delete old migrations, do not suppress `PendingModelChangesWarning`, and do not create migrations only to silence EF. See `docs/Migration-Governance.md`.

Create a migration only after an intentional EF model change:

```powershell
dotnet ef migrations add MigrationName --project .\DARAK.Api\DARAK.Api.csproj --startup-project .\DARAK.Api\DARAK.Api.csproj
```

Apply migrations only after build and tests pass:

```powershell
dotnet ef database update --project .\DARAK.Api\DARAK.Api.csproj --startup-project .\DARAK.Api\DARAK.Api.csproj --connection "<REAL_CONNECTION_STRING>"
```

Documentation, packaging, service-only rules, controller authorization changes, and tests do not require migrations. Phase 7 is documentation/governance only and introduces no EF model changes.

## Tests

```powershell
dotnet test .\DARAK.sln
```

If dependencies are already restored and local NuGet config access is restricted:

```powershell
dotnet test .\DARAK.sln --no-restore
```

## Roles And Access

Identity roles are seeded from `UserRole`:

- `SuperAdmin`
- `CompoundAdmin`
- `Accountant`
- `Guard`
- `MaintenanceStaff`
- `Resident`

`SuperAdmin` is global. `CompoundAdmin`, `Accountant`, and `Guard` are scoped through `UserCompoundAssignments`, managed by `api/admin/compound-assignments`. Services use `ICompoundAccessService` to filter admin/reporting queries and to block actions against compounds outside the caller's active assignments.

## Main API Areas

- Auth: `api/auth`
- Admin dashboard/reporting: `api/admin/dashboard`
- Compound structure: `api/admin/compounds`, `api/admin/property-structure`
- Residents and occupancy: `api/admin/residents`, `api/admin/occupancies`, `api/resident/account`
- Billing and meters: `api/admin/billing`, `api/admin/meters`
- Financial operations: `api/admin/payments`, `api/resident/payments`, `api/admin/financial-operations`
- Rent and ownership: `api/admin/rent`, `api/admin/ownership`
- Visitors and guard access: `api/admin/visitor-passes`, `api/resident/requests/visitor-passes`, `api/guard/access`
- Maintenance, complaints, violations, communication, documents, work orders, notification outbox, and admin action approvals

## Operational Notes

- Development startup seeds roles and requires configured development SuperAdmin credentials.
- Refresh-token reuse revokes the replacement chain for the same user.
- Document uploads validate extension, file size, MIME type, file signature, owner scope, and immutable document compound ownership.
- Overdue processing is explicit through `api/admin/financial-operations/overdue-status/process`; it does not run as a background job.
- Notification delivery uses an outbox with optional SMTP email and HTTP SMS providers. Providers are disabled by default until environment variables are configured.

## Final Polish Notes

The API now includes SuperAdmin-only user role management through `api/admin/users`. A user must still have the correct Identity role before a scoped `UserCompoundAssignment` can be created; role assignment and removal are intentionally restricted to `SuperAdmin`.

Auth endpoints use fixed-window rate limiting policies:

- `api/auth/register`
- `api/auth/login`
- `api/auth/refresh`

Refresh-token failures intentionally return a generic authentication failure message to avoid leaking whether a token was previously valid.


## Phase 11 — Admin Action Approval Workflow

Sensitive admin operations can now be represented by approval requests under `api/admin/approvals`. The workflow supports compound-scoped creation, search, details, approve, reject, cancel, mark-executed, and dashboard summaries. Approval requests write ActivityTimeline entries and InApp NotificationOutbox records for requested, approved, rejected, cancelled, and executed events.

Current approval rules are intentionally conservative: `SuperAdmin`, `CompoundAdmin`, and `Accountant` can request approvals; only `SuperAdmin` and scoped `CompoundAdmin` can approve/reject; self-approval is blocked by default; residents and guards are excluded from approval APIs.

## Remediation Track Status

The current remediation baseline should be verified with the latest `dotnet test .\DARAK.sln --no-build` output on the exact source tree being delivered.

Completed remediation phases:

- Phase 1A-1: controller authorization boundaries.
- Phase 1A-2: service-level compound scope.
- Phase 1B: authentication/runtime hardening.
- Phase 2A: financial ledger integrity.
- Phase 2B: payment safety and billing correctness.
- Phase 3A: row-version/concurrency handling.
- Phase 3B: database integrity, indexes and outbox atomicity.
- Phase 5A: ownership and occupancy lifecycle.
- Phase 5B: documents, visitors and communication hardening.
- Phase 5C: operations and approvals governance.
- Phase 7: deployment, documentation, packaging and release governance.

Phase 7 does not add domain features or migrations. It closes the handover workflow: release rules, migration governance, environment-variable reference, verification evidence template, and a final release gate script.

## Known Limitations / Intentional Scope

DARAK is a portfolio-grade backend project, not a production SaaS deployment. Current limitations are explicit:

- Tests are mostly service-level tests using EF Core InMemory, not full HTTP/integration tests.
- Overdue processing is an explicit admin operation, not a scheduled background job.
- Document uploads are persisted to a Docker volume in local compose; production should use durable external storage such as object/blob storage.
- The project does not include a frontend, mobile app, facility booking module, or real payment gateway integration. SMS/email delivery is real-ready through configurable providers but disabled by default.
- CORS policy is intentionally not opened globally; define and tighten a real CORS policy when a frontend/mobile client is deployed.
- Financial entities use row-version concurrency tokens for the main payment targets; production deployment should still add broader retry/observability policies around high-contention financial workflows.
- Swagger is intended for Development only. Do not run deployment environments with `ASPNETCORE_ENVIRONMENT=Development`.

## Interview Talking Points

- Compound isolation is enforced through `UserCompoundAssignment` records and `ICompoundAccessService`, not through client-provided compound IDs alone.
- Resident-facing financial access is derived from the authenticated user's resident profiles.
- Payments use typed payment targets, partial-payment state transitions, refunds, receipts, and idempotency keys.
- Refresh token reuse triggers replacement-chain revocation for the same user.
- Document uploads validate file extension, MIME type, size, and resolved storage path.

## Phase 22 — Operational Resident Risk Flags

Adds an operational resident risk flag workflow for admin/support use without exposing it to resident-facing APIs.

Included:

- Resident risk flag domain model and action/audit history.
- Compound-scoped admin APIs under `/api/admin/risk-flags`.
- Resident context endpoint: `GET /api/admin/residents/{id}/risk-flags`.
- Role protection for SuperAdmin, CompoundAdmin, and Accountant readers/managers.
- Closure restricted to SuperAdmin and CompoundAdmin.
- Source entity compound/resident consistency validation.
- Activity timeline integration for creation, assignment, severity change, review, resolution, dismissal, and notes.
- Notification outbox integration for important risk flag events.
- Dashboard counts for active, monitoring, resolved, dismissed, expired, overdue review, unassigned, and high/critical flags.
- Service-level tests for authorization, scope isolation, source consistency, actions, timeline, notifications, and dashboard counts.

Migration name:

```powershell
Phase22OperationalResidentRiskFlags
```

## Phase 23 - Service and Controller Organization

Phase 23 keeps the public HTTP surface stable while improving maintainability:

- Moved application service registration out of `Program.cs` into `AddDarakApplicationServices`.
- Kept all existing controller routes unchanged.
- Added organization guard tests for service registration and critical route stability.
- No database model changes were introduced in this phase.
- No migration is required for this phase.


## Phase 24 - Final Security and Portfolio Hardening

Phase 24 adds final GitHub-readiness and startup hardening without changing the database model or public HTTP routes.

Included:

- Startup configuration validation through `StartupSecurityValidator`.
- Explicit rejection of placeholder JWT secrets, connection strings, development SuperAdmin credentials, and enabled SMS/email providers with incomplete credentials.
- Stronger `tools/Clean-BeforeGitHub.ps1` cleanup validation for `.env`, ZIP handoff files, build outputs, logs, test artifacts, and local upload storage.
- New `tools/New-GitHubReadyArchive.ps1` helper for creating a source-only GitHub archive after cleanup.
- Security/readiness tests covering placeholder rejection, Docker secret sourcing, `.gitignore`, appsettings templates, and cleanup-script safeguards.
- No EF model changes were introduced in this phase.
- No migration is required for this phase.

Before publishing to GitHub:

```powershell
cd C:\Users\lenovo\Desktop\DARAK
.\tools\Clean-BeforeGitHub.ps1
dotnet build .\DARAK.sln
dotnet test .\DARAK.sln
```

To create a source-only archive after cleanup:

```powershell
cd C:\Users\lenovo\Desktop\DARAK
.\tools\New-GitHubReadyArchive.ps1
```

## Commercial Phase 15 - Security and Data Integrity Hardening

Commercial Phase 15 treats DARAK as a buyer-grade product and focuses on closing high-risk security and data-integrity gaps before adding new commercial modules.

Included:

- Enforced compound scope for admin document deletion.
- Enforced compound scope for admin document access-log reads.
- Kept out-of-scope document access returning `NotFound` to reduce ID enumeration and side-channel leakage.
- Enforced `ApprovalPolicy.RequiredApproverRoles` during approval decisions.
- Added fail-closed behavior for invalid approval policy role configuration.
- Extended tests for document scope isolation and approval policy enforcement.
- No EF model changes were introduced in this phase.
- No migration is required for this phase.

Recommended verification:

```powershell
cd C:\Users\lenovo\Desktop\DARAK
dotnet clean .\DARAK.sln
dotnet build .\DARAK.sln
dotnet test .\DARAK.sln
```

## Phase 16 — Financial Control Center

Phase 16 adds a commercial-grade financial control surface for DARAK:

- Admin finance dashboard with outstanding balances, overdue exposure, collections, refunds, and adjustment status counts.
- Resident account statement that combines existing charges, payments, refunds, and applied ledger adjustments.
- Manual financial adjustment workflow with mandatory approval request creation.
- Adjustment apply/cancel lifecycle with activity timeline and notification outbox records.
- Aging report buckets for current, 1-30, 31-60, 61-90, and 90+ day receivables.
- Revenue summary grouped by payment method and payment target type.
- Compound-scoped finance APIs under `/api/admin/finance`.

This phase introduces new database tables and requires a migration named `Phase23FinancialControlCenter`.

## Phase 17 — Full Audit, Compliance & Legal Traceability

Phase 17 adds a buyer-grade audit foundation for DARAK. The goal is legal and operational traceability: who did what, when, inside which compound, against which entity, and what changed.

### Added

- `AuditLogEntry` and `AuditLogChange` domain entities.
- Audit enums for action, entity, and severity classification.
- `IAuditLogService` / `AuditLogService`.
- Admin audit APIs:
  - `GET /api/admin/audit/logs`
  - `GET /api/admin/audit/logs/{id}`
  - `GET /api/admin/audit/dashboard`
  - `GET /api/admin/audit/entities/{entityType}/{entityId}`
  - `GET /api/admin/audit/residents/{residentProfileId}`
- Compound-scoped audit search and details access.
- Sensitive change masking in audit details.
- Audit dashboard grouped by severity, action, entity type, and source module.
- Audit integration for approval lifecycle operations.
- Audit integration for financial adjustment request/apply/cancel operations.
- Audit integration for resident ledger entry creation from applied financial adjustments.

### Commercial value

This phase moves DARAK closer to a sellable system by providing traceability for sensitive administrative and financial operations. It helps answer:

- Who performed the action?
- What role performed it?
- Which compound was affected?
- Which resident/entity was affected?
- What was changed?
- Why was it changed?
- When did it happen?

### Migration

This phase requires a migration:

```powershell
 dotnet ef migrations add Phase24FullAuditComplianceTraceability --project .\DARAK.Api\DARAK.Api.csproj --startup-project .\DARAK.Api\DARAK.Api.csproj
```

## Phase 25 / Commercial Phase 18 — Operational Command Center

DARAK now includes a commercial operational command center for daily compound management:

- `GET /api/admin/operations/command-center`
- `GET /api/admin/operations/sla-breaches`
- `GET /api/admin/operations/staff-performance`
- `GET /api/admin/operations/compound-health`
- `GET /api/admin/operations/tasks`
- `GET /api/admin/operations/tasks/{id}`
- `POST /api/admin/operations/tasks`
- `POST /api/admin/operations/tasks/{id}/complete`
- `POST /api/admin/operations/tasks/{id}/cancel`

The module aggregates maintenance, complaints, work orders, approvals, finance adjustments, risk flags, and operational tasks into a single command center. It adds SLA breach detection, compound health scoring, staff workload/performance metrics, compound-scoped task management, and audit logging for task lifecycle operations.

## Phase 19 — Commercial Communication & Document Automation

Phase 19 upgrades DARAK from basic messaging/document storage into a commercial communication and document-control layer.

### Communication automation

- Resident notification preferences with in-app/email/SMS toggles.
- Category-level notification switches for bills, payments, maintenance, complaints, violations, visitors, documents, announcements and campaigns.
- Do-not-disturb windows.
- Admin communication campaigns.
- Compound/building/floor/unit/resident/overdue/risk-flag targeting foundation.
- Campaign recipient tracking.
- Notification outbox integration for campaign delivery.
- Delivery analytics endpoint.
- Audit logging for preference updates and campaign lifecycle.

### Document Management Pro foundation

- Document approval status on uploaded documents.
- Document review metadata: reviewer, review timestamp and reason.
- Document expiry tracking.
- Document version metadata foundation.
- Document requirements per compound/category/applies-to scope.
- Resident document checklist.
- Document management dashboard: pending review, approved, rejected, expired, expiring soon, active requirements and missing mandatory documents.
- Audit logging for requirements and document review.

### New admin APIs

- `GET /api/admin/communication-automation/preferences/{userId}`
- `PUT /api/admin/communication-automation/preferences/{userId}`
- `GET /api/admin/communication-automation/campaigns`
- `GET /api/admin/communication-automation/campaigns/{id}`
- `POST /api/admin/communication-automation/campaigns`
- `POST /api/admin/communication-automation/campaigns/{id}/send`
- `GET /api/admin/communication-automation/delivery-analytics`
- `GET /api/admin/document-management/dashboard`
- `GET /api/admin/document-management/requirements`
- `GET /api/admin/document-management/requirements/{id}`
- `POST /api/admin/document-management/requirements`
- `PUT /api/admin/document-management/requirements/{id}`
- `POST /api/admin/document-management/requirements/{id}/deactivate`
- `POST /api/admin/document-management/documents/{id}/approve`
- `POST /api/admin/document-management/documents/{id}/reject`
- `GET /api/admin/document-management/residents/{residentProfileId}/checklist`

### New resident APIs

- `GET /api/resident/communication/notification-preferences`
- `PUT /api/resident/communication/notification-preferences`

## Phase 20 — Advanced Billing, Metering, Contracts & Ownership Engine

Commercial hardening phase that extends DARAK from financial control into a fuller billing/contracts operations engine.

Added:

- Billing rules with fixed, per-unit, tiered, and fixed-plus-per-unit modes.
- Billing rule tiers for tiered utility pricing.
- Meter reading correction workflow with approval/rejection and audit traceability.
- Contract lifecycle events for rent/sale contracts.
- Unit handover checklists for move-in and move-out operations.
- Ownership transfer request workflow with approval/rejection.
- Installment reschedule request workflow with approval/rejection and controlled application.
- Commercial command dashboard aggregating pending commercial risks.
- Commercial audit events for all high-risk actions.

New API surface:

- `GET /api/admin/commercial-engine/dashboard`
- `GET /api/admin/commercial-engine/billing-rules`
- `POST /api/admin/commercial-engine/billing-rules`
- `POST /api/admin/commercial-engine/billing-rules/{id}/tiers`
- `GET /api/admin/commercial-engine/meter-corrections`
- `POST /api/admin/commercial-engine/meter-corrections`
- `POST /api/admin/commercial-engine/meter-corrections/{id}/approve`
- `POST /api/admin/commercial-engine/meter-corrections/{id}/reject`
- `POST /api/admin/commercial-engine/contracts/lifecycle-events`
- `GET /api/admin/commercial-engine/contracts/{contractType}/{contractId}/timeline`
- `POST /api/admin/commercial-engine/unit-handovers`
- `POST /api/admin/commercial-engine/unit-handovers/{id}/complete`
- `POST /api/admin/commercial-engine/ownership-transfers`
- `POST /api/admin/commercial-engine/ownership-transfers/{id}/approve`
- `POST /api/admin/commercial-engine/ownership-transfers/{id}/reject`
- `POST /api/admin/commercial-engine/installment-reschedules`
- `POST /api/admin/commercial-engine/installment-reschedules/{id}/approve`
- `POST /api/admin/commercial-engine/installment-reschedules/{id}/reject`

Migration required:

- `Phase27AdvancedBillingContractsOwnershipEngine`

## Phase 21 — Support, SLA, Reporting & Management Intelligence

Commercial operations and reporting expansion:

- Unified admin support cases with assignment, escalation, resolution, reopen, internal notes and timeline events.
- Support dashboard with open, escalated, overdue, critical, reopened and resolution-rate metrics.
- Management reports for finance, occupancy, maintenance, support, risk and audit.
- Saved reports and export job foundation for CSV/JSON-ready management exports.
- Compound-scoped access for support and reporting APIs.
- Audit logging for support case lifecycle, saved report creation and report export jobs.

New admin APIs:

- `GET /api/admin/support/cases`
- `POST /api/admin/support/cases`
- `GET /api/admin/support/cases/{id}`
- `POST /api/admin/support/cases/{id}/assign`
- `POST /api/admin/support/cases/{id}/escalate`
- `POST /api/admin/support/cases/{id}/resolve`
- `POST /api/admin/support/cases/{id}/reopen`
- `POST /api/admin/support/cases/{id}/notes`
- `GET /api/admin/support/dashboard`
- `GET /api/admin/reports/financial`
- `GET /api/admin/reports/occupancy`
- `GET /api/admin/reports/maintenance`
- `GET /api/admin/reports/support`
- `GET /api/admin/reports/risk-audit`
- `GET /api/admin/reports/saved`
- `POST /api/admin/reports/saved`
- `POST /api/admin/reports/exports`
- `POST /api/admin/reports/exports/{id}/complete`

Migration: `Phase28SupportReportingManagementIntelligence`.

## Phase 22 — Commercial Packaging, Licensing, Deployment & Observability

Phase 22 adds buyer-grade commercial packaging and operations readiness capabilities:

- System settings with global and compound-scoped configuration.
- License profile foundation for commercial handover.
- Maintenance mode management.
- Public/admin version endpoints.
- Deployment readiness checklist.
- System health dashboard with notification, integration, and background job signals.
- Background job run tracking.
- Integration failure event tracking and resolution.
- Correlation ID middleware through `X-Correlation-ID`.

New admin APIs:

- `GET /api/admin/system/version`
- `GET /api/admin/system/settings`
- `PUT /api/admin/system/settings`
- `GET /api/admin/system/license`
- `PUT /api/admin/system/license`
- `GET /api/admin/system/maintenance-mode`
- `POST /api/admin/system/maintenance-mode`
- `GET /api/admin/system/deployment-checklist`
- `GET /api/admin/system/health`
- `GET /api/admin/system/background-jobs`
- `POST /api/admin/system/background-jobs`
- `POST /api/admin/system/background-jobs/{id}/complete`
- `GET /api/admin/system/integration-failures`
- `POST /api/admin/system/integration-failures`
- `POST /api/admin/system/integration-failures/{id}/resolve`
- `GET /api/system/version`

Migration name: `Phase29CommercialPackagingObservability`.

## Phase 23 — Final Commercial Review & Hardening Pass

Phase 23 closes the first commercial release track of DARAK. This phase does not add heavy domain features; it adds buyer-handover documentation, release validation, and commercial packaging scripts.

Commercial handover additions:

- `docs/Commercial-Handover-Report.md`
- `docs/Production-Readiness-Checklist.md`
- `docs/Security-Authorization-Final-Review.md`
- `docs/Deployment-Runbook.md`
- `docs/Buyer-Operations-Runbook.md`
- `docs/Final-Commercial-Release-Notes.md`
- `tools/Test-CommercialReadiness.ps1`
- `tools/New-CommercialReleasePackage.ps1`

Commercial release workflow:

```powershell
cd C:\Users\lenovo\Desktop\DARAK

dotnet clean .\DARAK.sln
dotnet build .\DARAK.sln
dotnet test .\DARAK.sln

.\tools\Test-CommercialReadiness.ps1
.\tools\New-CommercialReleasePackage.ps1
```

The final commercial package must not contain `.env`, local ZIP packs, `bin`, `obj`, logs, uploaded documents, coverage output or local test artifacts.

Migration required: none.


## Remediation Phase 7 — Deployment, Docs and Governance

Phase 7 converts the current remediation baseline into a controlled handover baseline. It does not change the API model, routes, services, or EF entities.

Added/updated governance assets:

- `docs/Release-Governance.md`
- `docs/Migration-Governance.md`
- `docs/Commercial-Verification-Evidence.md`
- `docs/Environment-Variables-Reference.md`
- `tools/Test-Phase7ReleaseGate.ps1`
- updated commercial readiness and packaging scripts

Recommended final validation:

```powershell
cd C:\Users\lenovo\Desktop\DARAK

dotnet clean .\DARAK.sln
dotnet build .\DARAK.sln --no-incremental
dotnet test .\DARAK.sln --no-build

.\tools\Test-Phase7ReleaseGate.ps1 -SkipDotnet

dotnet ef database update `
  --project .\DARAK.Api\DARAK.Api.csproj `
  --startup-project .\DARAK.Api\DARAK.Api.csproj `
  --connection "<REAL_CONNECTION_STRING>"
```

Expected validation after Phase 5C/7: run the clean/build/test gate and capture the current test output.

Migration required: none.

## Remediation Phase 8B — Deep Security Verification Tests

Phase 8B adds security regression coverage without changing database schema, routes, services, or business behavior.

Expected validation after Phase 8B: run the clean/build/test gate and capture the current test output.

Migration required: none.

## Remediation Phase 8C — Performance and Query Optimization

Phase 8C adds safe query-performance hardening without demo data, seed data, endpoint changes, or EF model changes.

Implemented improvements:

- database-side aggregation for the audit dashboard instead of materializing full audit rows in memory
- read-only split queries for high-include detail/search query factories
- regression tests that protect query-performance rules from accidental removal
- documented query-performance governance in `docs/Performance-Query-Governance.md`

Validation:

```powershell
cd C:\Users\lenovo\Desktop\DARAK

dotnet clean .\DARAK.sln
dotnet build .\DARAK.sln --no-incremental
dotnet test .\DARAK.sln --no-build
```

Migration required: none.

## Phase 8D — Advanced Notification Reliability

Phase 8D strengthens the notification outbox processor without changing database schema or endpoint contracts.
It adds stale-processing recovery, safer manual retry behavior, and bounded retry backoff governance.

Validation target: all tests green after a clean rebuild.

### Phase 8E — Guard Visitor Pro Workflow

Phase 8E adds an explicit guard-side visitor access-code verification endpoint and required access-code validation during check-in. It keeps guard access compound-scoped, keeps list responses masked, and does not require database changes.


## Phase 8F - Document Compliance Pro

Phase 8F adds a read-only document compliance report for mandatory resident document requirements.
It introduces admin compliance reporting, resident-level gap summaries, and tests for missing, expired, and expiring documents.

Database impact: no migration.

## Phase 9 - Final Commercial Completion Pack

Phase 9 merges the remaining commercial completion work into one final pack:

- Financial disputes and refund governance.
- Admin reporting/export governance.
- Production deployment polish.
- Buyer handoff documents.
- Final commercial release gate.

No migration is required for Phase 9. It does not add demo data, seed data, or schema changes.

Final Phase 9 assets:

- `docs/Financial-Disputes-Refunds-Governance.md`
- `docs/Admin-Reporting-Exports-Governance.md`
- `docs/Commercial-Feature-Matrix.md`
- `docs/API-Coverage.md`
- `docs/Buyer-Handoff.md`
- `docs/Security-Checklist.md`
- `docs/Testing-Evidence.md`
- `docs/Commercial-Value-Summary.md`
- `docs/Final-Status-Report.md`
- `tools/Test-FinalReleaseGate.ps1`

Run the final gate from the repository root:

```powershell
.\tools\Test-FinalReleaseGate.ps1
```

Use `-SkipDotnet` only when build and test evidence was already captured separately.
