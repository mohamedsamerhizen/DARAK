# DARAK Interview Talking Points

Use this document to present DARAK honestly in technical interviews.

---

## One-Minute Project Description

DARAK is a large ASP.NET Core Web API backend for residential compound operations. It models residents, units, billing, payments, rent/installments, visitors, guard access, maintenance, documents, approvals, audit logs, notifications, and reporting.

The goal is to demonstrate backend design across authentication, authorization, tenant scoping, financial workflow foundations, EF Core modeling, testing, and release hygiene.

---

## What To Emphasize

- The project is backend-only and intentionally focuses on API/domain logic.
- The domain is bigger than simple CRUD.
- Authorization is split by roles such as SuperAdmin, CompoundAdmin, Accountant, Guard, MaintenanceStaff, and Resident.
- Authentication includes JWT access tokens, refresh-token rotation, and login blocking for unconfirmed users.
- Public registration and first SuperAdmin bootstrap are intentionally controlled by configuration.
- Resident-facing endpoints should derive ownership from authenticated identity, not arbitrary client IDs.
- Compound scoping is a central design concern.
- Financial modules are treated as high-risk and require tests for idempotency and ledger consistency.
- The remediation plan keeps known limitations explicit instead of pretending everything is production-ready.

---

## What Not To Overclaim

Do not say:

- It is a complete production SaaS.
- It has a real payment gateway.
- It includes frontend/mobile clients.
- All tests passed unless you have fresh evidence.
- It is commercially verified unless the verification evidence is filled.

---

## Strong Technical Stories

### Authentication

Explain JWT access tokens, refresh tokens, and why refresh-token workflows must handle expiry, rotation, and reuse detection.

### Authorization

Explain how roles and endpoint boundaries protect admin, resident, guard, and staff workflows.

### Tenant Isolation

Explain why every compound-scoped resource must be filtered or validated against the current user's allowed compound IDs.

### Financial Logic

Explain why payments, receipts, bills, rent, installments, refunds, adjustments, and reconciliation must be consistent and testable.

### Testing

Explain the difference between tests existing in source and tests being executed as release evidence.

### Documentation Honesty

Explain that the README was deliberately changed to avoid overclaiming production/commercial readiness before proof exists.

### Next Hardening Steps

Explain that later phases should focus on SQL Server-backed integration tests, financial idempotency, access-code hashing, tenant isolation hardening for operational resources, real notification/payment providers, and production operations.
