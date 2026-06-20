# DARAK API Coverage Summary

## Public Development Surface

This file is a commercial summary of the implemented backend surface. It is not a Swagger replacement and does not include every DTO field.

## Main Areas

| Area | Route Family | Audience |
|---|---|---|
| Authentication | `api/auth` | All clients |
| Admin dashboard | `api/admin/dashboard` | Admin roles |
| Compounds and structure | `api/admin/compounds`, `api/admin/property-structure` | Admin roles |
| Residents and occupancy | `api/admin/residents`, `api/admin/occupancies` | Admin roles |
| Resident account | `api/resident/account` | Residents |
| Billing and meters | `api/admin/billing`, `api/admin/meters` | Admin/accounting |
| Payments | `api/admin/payments`, `api/resident/payments` | Admin/residents |
| Contracts | `api/admin/rent`, `api/admin/ownership` | Admin/accounting |
| Visitors | `api/admin/visitor-passes`, `api/resident/requests/visitor-passes`, `api/guard/access` | Admin/resident/guard |
| Documents | `api/admin/document-management`, resident document routes | Admin/residents |
| Maintenance and operations | Admin operations routes | Admin/maintenance |
| Communication | Admin/resident communication routes | Admin/residents |
| Approvals | `api/admin/approvals` | Admin roles |
| Audit | `api/admin/audit` | Admin roles |
| Notifications | Admin notification processing routes | Admin roles |

## Security Coverage

The API is designed around:

- Role-based authorization.
- Compound-scoped access checks.
- Resident identity scoping.
- Guard-specific visitor permissions.
- Audit trail for sensitive actions.
- Secure file upload validation.
- Refresh-token reuse protection.

## Handoff Rule

Before using this as a commercial delivery, regenerate Swagger/OpenAPI from the running API and attach screenshots or exported OpenAPI evidence to the final handoff package.
