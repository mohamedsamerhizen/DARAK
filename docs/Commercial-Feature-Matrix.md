# DARAK Commercial Feature Matrix

## Core Platform

| Area | Status | Commercial Notes |
|---|---:|---|
| Identity and roles | Implemented | JWT, refresh tokens, lockout, role-based access. |
| Compound scoping | Implemented | Admin data access is tied to compound assignments. |
| Resident portal APIs | Implemented | Resident-facing account, bills, payments, documents, visitors, and communication. |
| Admin portal APIs | Implemented | Management, finance, operations, documents, reports, approvals, and audit. |
| Guard workflow | Implemented | Visitor list, masked access codes, verification, check-in/out. |

## Financial Operations

| Area | Status | Commercial Notes |
|---|---:|---|
| Utility billing | Implemented | Billing cycles, bills, states, and resident visibility. |
| Payments and receipts | Implemented | Demo/mock payment providers and receipt flows; real gateway settlement remains outside current scope. |
| Rent and ownership | Implemented | Rent invoices and ownership/installment foundations. |
| Disputes | Implemented foundation | Governance documented in Phase 9. |
| Refunds | Implemented foundation / extension-ready | Real gateway settlement remains outside current scope. |

## Operations

| Area | Status | Commercial Notes |
|---|---:|---|
| Maintenance | Implemented | Requests, work orders, operations command center. |
| Complaints and violations | Implemented | Complaint workflow and fine/violation foundations. |
| Documents | Implemented | Upload security, approval, requirements, compliance reporting. |
| Notifications | Implemented | Outbox, retries, provider configuration, reliability governance. |
| Audit | Implemented | Traceability across sensitive workflows. |

## Delivery Assets

| Asset | Status |
|---|---:|
| README | Present |
| Docker files | Present |
| Migration governance | Present |
| Release governance | Present |
| Production readiness checklist | Present |
| Final release gate script | Added in Phase 9 |
| Buyer handoff package | Added in Phase 9 |
