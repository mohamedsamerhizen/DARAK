# DARAK Production Readiness Checklist

## Build and Test Gate

- [ ] `dotnet clean .\DARAK.sln` succeeds.
- [ ] `dotnet build .\DARAK.sln` succeeds with zero errors.
- [ ] `dotnet test .\DARAK.sln` succeeds.
- [ ] EF migrations list contains all expected commercial phases.
- [ ] Database update succeeds against the target SQL Server instance.
- [ ] `tools\Test-Phase7ReleaseGate.ps1` succeeds or is run with `-SkipDotnet` after separate build/test evidence is captured.
- [ ] Release evidence is recorded using `docs\Commercial-Verification-Evidence.md`.

## Security Gate

- [ ] .env is not included in the delivery package.
- [ ] `Jwt:SecretKey` is a strong real secret and not a placeholder.
- [ ] SQL Server password is stored in environment variables only.
- [ ] Development SuperAdmin placeholders are replaced in deployment environment only.
- [ ] Email/SMS credentials are not stored in `appsettings.json`.
- [ ] Startup security validation is enabled.
- [ ] Admin/resident route separation is preserved.
- [ ] Compound-scoped access tests pass.

## Data Integrity Gate

- [ ] Financial adjustments require approval.
- [ ] Payments, refunds and manual corrections are audited.
- [ ] Sensitive document actions are scoped by compound.
- [ ] Audit logs are written for support, reports, settings, license and maintenance actions.
- [ ] Database backups are configured before accepting production data.

## Operations Gate

- [ ] License profile is configured.
- [ ] Maintenance mode policy is documented.
- [ ] Notification providers are tested in disabled/sandbox mode first.
- [ ] System health endpoint is checked after deployment.
- [ ] Background job tracking is monitored.
- [ ] Integration failures are reviewed after first notification run.

## Handover Gate

- [ ] Buyer receives deployment runbook.
- [ ] Buyer receives operations runbook.
- [ ] Buyer receives release notes.
- [ ] Buyer receives environment variable list.
- [ ] Buyer receives release governance and migration governance documents.
- [ ] Seller keeps a clean source archive and a backup archive separately.

