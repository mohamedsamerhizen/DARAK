# DARAK Buyer Handoff

## Handoff Objective

This document explains how DARAK should be delivered to a technical buyer, reviewer, or hiring portfolio evaluator.

## Package Contents

A clean handoff should include:

- Source code archive without `.env`, build output, logs, or local ZIP files.
- README.
- Docker and environment examples.
- Migration governance.
- Deployment runbook.
- Production readiness checklist.
- Commercial feature matrix.
- API coverage summary.
- Security checklist.
- Testing evidence.
- Final status report.

## What The Buyer Receives

DARAK is a backend-only ASP.NET Core API for residential compound management. It includes:

- Identity and role management.
- Compound-scoped administration.
- Resident financial and document workflows.
- Visitor and guard access workflows.
- Maintenance, complaints, operations, approvals, notifications, audit, and reporting foundations.

## What Is Intentionally Not Included

- Frontend/mobile client.
- Real payment gateway contract.
- Real SMS/email credentials.
- Production object storage account.
- Live customer data.
- Demo/seed data unless explicitly created later.

## Acceptance Checklist

The buyer/reviewer should verify:

- `dotnet build` succeeds.
- `dotnet test` succeeds.
- EF database update succeeds against a configured SQL Server instance.
- Environment variables are set outside source control.
- Release gate passes.
- No secrets are included in the archive.
