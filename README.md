# DARAK Backend

[![.NET CI](https://github.com/mohamedsamerhizen/DARAK/actions/workflows/dotnet.yml/badge.svg)](https://github.com/mohamedsamerhizen/DARAK/actions/workflows/dotnet.yml)
![.NET](https://img.shields.io/badge/.NET-10-512BD4)
![ASP.NET Core](https://img.shields.io/badge/ASP.NET%20Core-Web%20API-512BD4)
![EF Core](https://img.shields.io/badge/EF%20Core-SQL%20Server-blue)
![Status](https://img.shields.io/badge/Status-20--Pass%20Hardened-success)
![Backend](https://img.shields.io/badge/Scope-Backend--Only-orange)

**DARAK** is a commercial-grade ASP.NET Core backend for residential compound, property, utility, payment, visitor, maintenance, communication, approval, audit, reporting, and operational governanceEF%20Core-SQL%20Server-blue)
![Status](https://img.shields.io/badge/Status-20--Pass%20Hardened-success)
![Backend](https://img.shields.io/badge/Scope-Backend--Only-orange)

**DARAK** is a commercial-grade ASP.NET Core backend for residential compound, property, utility,.

It is designed as a serious backend foundation for residential compounds, gated communities, real-estate operators, property-management companies, and future SaaS expansion.

---

## Overview

DARAK is not a simple CRUD application.

It is a backend system built around real operational workflows inside a residential compound:

* Residents live in units.
* Units belong to buildings, floors, compounds, and ownership/rent contexts.
* Residents pay utilities, rent, installments, fines, and service-related charges.
* Guards handle visitor access.
* Administrators manage finance, complaints, documents, approvals, audits, reports, and operational risk.
* Maintenance teams and vendors handle work orders, assets, preventive maintenance, and reliability rules.
* The system enforces authorization boundaries, compound ownership, resident scoping, and release readiness gates.

The project has gone through a structured **20-pass hardening and remediation process** before this release.

---

## Current Verification Status

Latest verified local status:

```text
Build:                    Passed
Tests:                    617 passed / 0 failed
Commercial Readiness:     Passed
Final Hardening Gate:     Passed
Final Migration Status:   No migration required
```

The project was validated after a 20-pass remediation track covering:

```text
Repository hygiene
Startup configuration
Authentication and JWT
Authorization boundaries
Compound isolation
API surface consistency
DTO validation
EF entity integrity
Migration governance
Billing and payments
Financial governance
Contracts and installments
Resident portal safety
Resident lifecycle workflows
Maintenance and asset reliability
Staff and vendor governance
Visitor and guard access control
Communications and notifications
Audit, reports, approvals, and documents
Commercial readiness and GitHub preparation
```

---

## Tech Stack

* **.NET 10**
* **ASP.NET Core Web API**
* **Entity Framework Core**
* **SQL Server**
* **ASP.NET Core Identity**
* **JWT authentication**
* **Refresh token workflow**
* **Serilog**
* **Swagger / OpenAPI in Development**
* **xUnit test project**
* **GitHub Actions CI**
* **Docker Compose support**

---

## Repository Structure

```text
DARAK/
├── DARAK.Api/                  # Main ASP.NET Core Web API
├── DARAK.Tests/                # Automated tests and regression checks
├── docs/                       # Commercial, security, release, and operations documentation
├── tools/                      # Cleanup, validation, and release gate scripts
├── .github/workflows/          # GitHub Actions CI workflow
├── docker-compose.yml          # Local Docker setup
├── .env.example                # Safe environment variable template
├── .gitignore                  # Source-control protection rules
├── .dockerignore               # Docker build exclusion rules
└── DARAK.sln                   # Solution file
```

---

## Main Modules

### Authentication and Identity

DARAK includes backend support for:

* Login.
* JWT access tokens.
* Refresh tokens.
* Role-based access control.
* Startup configuration validation.
* Development SuperAdmin seeding through environment configuration.
* Server-side authorization boundaries.

Supported access models include:

* SuperAdmin
* CompoundAdmin
* Accountant
* Guard
* MaintenanceStaff
* Resident

---

### Compound and Property Structure

DARAK models the physical and administrative structure of residential communities:

* Compounds
* Buildings
* Floors
* Units
* Parking spaces
* Unit relationships
* Compound-level operational isolation

The backend is built around the idea that data access must respect compound ownership and operational boundaries.

---

### Residents and Occupancy

Resident-related workflows include:

* Resident profiles
* Occupancy records
* Family members
* Emergency contacts
* Resident portal access
* Resident account summaries
* Move-in and move-out lifecycle rules
* Unit lifecycle safety checks

Resident-facing access is designed to be derived from the authenticated user rather than arbitrary client-provided resident IDs.

---

### Billing and Payments

DARAK supports financial operations for:

* Billing cycles
* Utility bills
* Utility bill lines
* Resident account balances
* Payments
* Payment attempts
* Receipts
* Reconciliation safety
* Payment governance
* Fines and outstanding charges

The payment layer is prepared for real payment-provider integration while keeping mock gateway behavior controlled and disabled by default unless explicitly enabled.

---

### Rent, Ownership, and Installments

DARAK includes contract and property-finance workflows:

* Rent contracts
* Rent invoices
* Ownership sales
* Installment plans
* Installment rescheduling governance
* Contract validation rules
* Date consistency validation
* Overpayment prevention
* Financial source checks

---

### Visitors and Guard Access Control

The system includes guard-facing visitor workflows:

* Visitor passes
* Access code verification
* Visitor status handling
* Guard access endpoints
* Pending visitor protection
* Masked sensitive access-code details
* Check-in/check-out workflow foundation

The access-control layer distinguishes between visible visitor records and actual gate clearance.

---

### Maintenance and Asset Reliability

DARAK includes backend workflows for property operations:

* Maintenance assets
* Preventive maintenance plans
* Work orders
* Staff/vendor assignment rules
* Asset location hierarchy validation
* Preventive due-date validation
* Reliability and maintenance governance checks

The module is prepared for future expansion into SLA escalation, vendor performance, and maintenance intelligence.

---

### Staff, Vendors, and Operations

Operational administration includes:

* Staff records
* Vendor records
* Assignment governance
* Sensitive staff data protection
* User-to-staff uniqueness validation
* Foundation for workforce and vendor operations

Sensitive internal staff fields are protected from broad search/list exposure.

---

### Complaints, Violations, Fines, and Disputes

DARAK supports resident-facing and admin-facing governance workflows:

* Complaints
* Violations
* Violation fines
* Dispute initiation
* Support conversation links
* Resident dashboard financial inclusion
* Admin operational review flows

Financial disputes and objections are designed to connect into communication and support workflows instead of becoming isolated records without operational follow-up.

---

### Communications and Notifications

Communication workflows include:

* Admin-resident conversations
* Conversation assignment
* Resident-linked notifications
* Manual notification creation
* Compound-aware notification scoping
* Notification outbox governance
* Recipient validation
* Announcement-style communication foundation

Notification handling includes safeguards against linking a resident profile to an unrelated user account.

---

### Documents, Approvals, Audit, and Reports

DARAK includes commercial governance features:

* Document governance
* Admin approvals
* Audit logs
* Timeline and traceability foundations
* Saved reports
* Export jobs
* JSON filter validation
* Commercial reporting controls

Saved report and export filter JSON is validated, normalized, and protected against silent truncation.

---

## Commercial Hardening Highlights

The project includes hardening work across several important areas:

* No generated `bin`, `obj`, `TestResults`, or `coverage` folders should be committed.
* Local secrets are excluded from source control.
* `.env.example` is safe for public source review.
* Startup rejects unsafe placeholder secrets.
* Mock payment gateway endpoints are controlled by configuration.
* Resident access is scoped server-side.
* Linked entities in conversations are validated against resident and compound ownership.
* Financial collection and reconciliation workflows include safety validation.
* DTO validation and API error formatting are normalized.
* Release scripts block unsafe handover artifacts.
* GitHub Actions CI is included for restore/build/test.

---

## Configuration

Do not commit real secrets.

For local Docker-based development, copy:

```powershell
copy .env.example .env
```

Required configuration values include:

```text
ConnectionStrings__DefaultConnection
Jwt__Issuer
Jwt__Audience
Jwt__SecretKey
Jwt__AccessTokenMinutes
Jwt__RefreshTokenDays
DevelopmentSuperAdmin__Email
DevelopmentSuperAdmin__Password
DevelopmentSuperAdmin__FullName
```

Optional provider configuration exists for future notification delivery integrations.

---

## Run Locally

### Requirements

* .NET 10 SDK
* Docker Desktop, if using SQL Server through Docker
* SQL Server or SQL Server container
* EF Core CLI tools, if applying migrations manually

### Restore and Build

```powershell
dotnet restore .\DARAK.sln
dotnet build .\DARAK.sln
```

### Apply Database Migrations

```powershell
dotnet ef database update `
  --project .\DARAK.Api\DARAK.Api.csproj `
  --startup-project .\DARAK.Api\DARAK.Api.csproj
```

### Run the API

```powershell
dotnet run --project .\DARAK.Api\DARAK.Api.csproj
```

Swagger is available in Development mode:

```text
/swagger
```

Health endpoints:

```text
/health
/health/live
```

---

## Docker

```powershell
copy .env.example .env
docker compose up --build
```

Default local Docker endpoints:

```text
API:        http://localhost:8080
SQL Server: localhost:1433
```

---

## Testing

Run all tests:

```powershell
dotnet test .\DARAK.sln
```

After a build has already completed:

```powershell
dotnet test .\DARAK.sln --no-build
```

Latest verified result:

```text
Test summary: total: 617, failed: 0, succeeded: 617, skipped: 0
```

---

## Release Validation

Before publishing, handover, or archiving, remove generated artifacts and run the release gates:

```powershell
Set-ExecutionPolicy -Scope Process Bypass -Force

Get-ChildItem . -Directory -Recurse -Force |
    Where-Object { $_.Name -in @("bin", "obj", "TestResults", "coverage") } |
    ForEach-Object {
        Remove-Item $_.FullName -Recurse -Force -ErrorAction SilentlyContinue
    }

.\tools\Test-CommercialReadiness.ps1
.\tools\Test-FinalReleaseGate.ps1 -SkipDotnet -SkipDatabase
```

Expected result:

```text
Commercial readiness validation passed.
DARAK final hardening gate passed.
```

---

## GitHub Actions

The repository includes a CI workflow for restore, build, and test.

Workflow path:

```text
.github/workflows/dotnet.yml
```

The workflow runs on push and pull request events.

---

## Security Notes

* Real secrets must be supplied through environment variables, deployment configuration, user secrets, or local `.env` files.
* Real `.env` files must not be committed.
* `.env.example` is provided as a safe template.
* Swagger is intended for Development usage.
* Health endpoints are available for local and container orchestration checks.
* Role and compound access are enforced server-side.
* Resident-facing access is scoped through authenticated identity.
* Cleanup and release scripts exist to reduce accidental publication of generated or unsafe files.

---

## Dependency Note

The test project currently uses FluentAssertions. Its license terms should be reviewed before commercial redistribution or active commercial product use.

Recommended options for a later improvement phase:

* Keep FluentAssertions with a valid commercial license if required.
* Replace FluentAssertions with built-in xUnit assertions.
* Isolate test dependencies from commercial runtime distribution.

---

## Current Known Scope

DARAK is currently a backend source package.

It does not include:

* Frontend admin dashboard.
* Resident mobile application.
* Guard mobile/tablet application.
* Real payment gateway integration.
* Production cloud deployment.
* Production object storage configuration.
* Public SaaS subscription billing.
* Multi-tenant licensing system.
* Buyer demo UI.

These are intentionally deferred to later productization phases.

---

## Suggested Next Improvement Phase

Recommended next phase after GitHub upload:

1. Zero-warning cleanup.
2. FluentAssertions license decision or dependency replacement.
3. OpenAPI/Postman export.
4. Demo seed data.
5. Buyer presentation package.
6. Admin dashboard prototype.
7. Resident portal prototype.
8. Guard screen prototype.
9. SaaS and licensing layer.
10. Advanced escalation and intelligence features.

---

## Commercial Product Direction

DARAK is intended to become a complete residential compound management platform.

The backend already provides a hardened foundation for:

* Property operations
* Resident finance
* Utility billing
* Contract governance
* Visitor control
* Maintenance operations
* Admin approvals
* Audit and reporting
* Commercial handover
* Future SaaS expansion

The current repository is suitable for private GitHub storage, technical review, continued hardening, and later productization.

---

## Disclaimer

This repository is a backend engineering project and requires environment-specific configuration, deployment hardening, operational review, and integration testing before being used in a real production environment.
