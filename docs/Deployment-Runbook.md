# DARAK Deployment Runbook

## 1. Prepare Environment

Install:

- .NET SDK/runtime matching the project target.
- SQL Server or SQL Server container.
- Docker Desktop if container deployment is used.
- EF Core CLI matching the installed SDK.

## 2. Configure Environment Variables

Create a real `.env` locally on the server. Do not commit it. Use `.env.example` as the template.

Required areas:

- SQL Server password and connection settings.
- JWT issuer, audience and secret key.
- Development or production SuperAdmin bootstrap values.
- Email provider settings when enabled.
- SMS provider settings when enabled.
- Notification worker settings.

## 3. Build and Test

Run:

```powershell
cd C:\Path\To\DARAK

dotnet clean .\DARAK.sln
dotnet build .\DARAK.sln --no-incremental
dotnet test .\DARAK.sln --no-build
.\tools\Test-Phase7ReleaseGate.ps1 -SkipDotnet
```

## 4. Apply Migrations

Apply migrations only after build/test pass. See `docs/Migration-Governance.md` before creating or applying new migrations.


Run:

```powershell
dotnet ef database update --project .\DARAK.Api\DARAK.Api.csproj --startup-project .\DARAK.Api\DARAK.Api.csproj --connection "<REAL_CONNECTION_STRING>"
```

## 5. Start Application

For Docker-based local deployment:

```powershell
docker compose up --build
```

For direct run:

```powershell
dotnet run --project .\DARAK.Api\DARAK.Api.csproj
```

## 6. Verify Deployment

Check:

- `GET /api/system/version`
- `GET /api/admin/system/health`
- Login with a valid admin account.
- Confirm maintenance mode is disabled unless planned.
- Confirm notification worker status.
- Confirm database migrations were applied.

## 7. Backup Plan

Before production use:

- Configure scheduled SQL Server backups.
- Store backups outside the application server.
- Test restore at least once.
- Document backup owner and frequency.

## 8. Rollback Plan

Keep:

- Previous clean source package.
- Previous database backup.
- Release notes for changed migrations.
- Current `.env` backup stored securely.


## 9. Commercial Delivery Gate

Before creating a buyer package, run:

```powershell
.\tools\Test-CommercialReadiness.ps1
.\tools\Test-Phase7ReleaseGate.ps1 -SkipDotnet
```

Then create the package with:

```powershell
.\tools\New-CommercialReleasePackage.ps1
```

Store build/test/EF output in `docs/Commercial-Verification-Evidence.md` or in a separate evidence file outside the source package.


## PASS 09 Final Migration Gate Before Buyer Package

Before producing a buyer/commercial package, run the final release gate with a real database connection string:

```powershell
.\tools\Test-FinalReleaseGate.ps1 `
  -ConnectionString "<REAL_CONNECTION_STRING>" `
  -ExpectedTestCount "<EXPECTED_TEST_COUNT>"
```

If the buyer package is created with `-SkipDatabase`, attach a separate evidence file containing the EF database update or pending-model-check result for the exact source tree.
