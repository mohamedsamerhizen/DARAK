# DARAK Known Limitations

This document keeps the project honest for GitHub, CV, interviews, and company demos.

DARAK is a serious backend portfolio project, but this backend-only GitHub package should not be described as a fully production-ready SaaS product.

---

## Current Limitations

### 1. Frontend/mobile app is not included

DARAK is backend-only. Admin, resident, guard, and buyer demo interfaces are not included in this repository.

### 2. Real payment-provider integration is not included

The project includes payment workflow foundations, mock/payment-attempt concepts, receipts, reconciliation foundations, and financial governance concepts. Live provider settlement requires real gateway integration, sandbox testing, webhooks, idempotency, and reconciliation proof.

### 3. Email/SMS provider integration is not included

Notification outbox and delivery abstractions exist, but real SMTP/SMS provider credentials, sandbox verification, bounce/failure handling, and production delivery operations are not included.

### 4. Financial workflows require recorded SQL Server evidence before commercial use

The backend includes payment idempotency, payment/ledger consistency for payment plans and sale/down payments, balance-forward double-count protection, and move-out financial clearance enforcement. Production-style claims require recorded SQL Server-backed migration, transaction, uniqueness, and concurrency evidence for the exact release tree.

### 5. Tenant isolation still requires SQL Server and API boundary evidence before commercial use

The backend includes compound scoping for staff/vendors and blocks cross-compound operational assignments. Production-style claims still require SQL Server-backed foreign-key/index evidence and broader API authorization evidence across every controller surface.

### 6. Visitor/access credentials are improved but not a complete physical security system

The backend stores visitor/access credential secrets as hashes, masks normal responses, requires contractor credentials for guard check-in, and adds contractor access logs. Production-style gate-security claims still require physical-device integration, operational procedures, monitoring, and SQL Server-backed verification evidence.

### 7. SQL Server-backed integration evidence is required

The source includes automated tests and optional SQL Server integration tests, but final release claims require actual execution evidence. If SQL Server is unavailable during verification, say that plainly.

### 8. Production operations are not included

The backend source alone does not include:

- Production hosting.
- Backups.
- Monitoring.
- Incident response.
- SLA operations.
- Deployment ownership.
- Legal/commercial license terms.

### 9. Not a complete production SaaS yet

The backend has substantial domain coverage, but it is not a complete production SaaS without frontend clients, real provider integrations, production operations, SQL Server evidence, security hardening, and deployment ownership.

### 10. Swagger screenshot capture currently needs a schema-name fix

A real local Swagger screenshot attempt reached Swagger UI, but OpenAPI JSON generation failed because two DTOs share the `SupportDashboardResponse` schema name. No fake screenshot is committed. Resolve the duplicate schema ID issue before adding Swagger screenshots to `docs/assets/screenshots/`.

---

## Correct Public Positioning

Use this wording:

```text
DARAK is a large ASP.NET Core backend portfolio project that models residential compound operations and demonstrates authentication, authorization, tenant scoping, financial workflow foundations, testing, and commercial-style backend design.
```

Avoid this wording until fully proven:

```text
DARAK is production-ready SaaS.
DARAK is commercially verified.
All tests passed.
Final hardening is complete.
```

## Related Docs

- `README.md`
- `docs/Screenshot-Capture-Guide.md`
- `docs/Verification-Evidence.md`
- `docs/GitHub-Profile-Setup.md`
