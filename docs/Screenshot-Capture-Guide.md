# DARAK Screenshot Capture Guide

DARAK is backend-only. This repository does not include an admin, resident, guard, or mobile frontend, so product UI screenshots must not be mocked or fabricated.

## Current Capture Status

A real local Swagger capture was completed after configuring Swagger schema IDs to use fully qualified DTO type names. The OpenAPI JSON was verified at:

```text
GET /swagger/v1/swagger.json
HTTP 200
```

Reviewed screenshots are committed under `docs/assets/screenshots/`:

- `swagger-overview.png`
- `auth-endpoints.png`
- `admin-modules.png`
- `resident-endpoints.png`
- `guard-access-endpoints.png`

## What Can Be Captured Honestly

- Swagger/OpenAPI screen in Development after the API starts and the OpenAPI definition loads.
- Specific Swagger sections such as auth, residents, finance, visitors/guards, maintenance, documents, reports, notifications, and health/readiness endpoints.
- Terminal output for restore, build, test, EF database update, and pending-model-change checks.
- SQL Server object explorer or query output during local verification.
- GitHub Actions workflow output after pushing the repository.
- GitHub-rendered architecture diagrams from `docs/assets/diagrams/`.

## Local API Capture Steps

1. Configure local values from `.env.example` or environment variables. Do not use real production secrets for screenshots.
2. Start SQL Server:

```powershell
docker compose up -d sqlserver
```

3. Run migrations:

```powershell
dotnet ef database update `
  --project .\DARAK.Api\DARAK.Api.csproj `
  --startup-project .\DARAK.Api\DARAK.Api.csproj
```

4. Start the API in Development:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --project .\DARAK.Api\DARAK.Api.csproj
```

5. Open the Swagger URL printed by ASP.NET Core.
6. Confirm the OpenAPI definition loads successfully before capturing.
7. Save reviewed screenshots under `docs/assets/screenshots/` with descriptive names, for example:

```text
swagger-overview.png
swagger-auth-endpoints.png
swagger-finance-endpoints.png
swagger-maintenance-endpoints.png
swagger-visitor-guard-endpoints.png
```

## Terminal Evidence Capture

Capture or copy terminal evidence for:

```powershell
dotnet restore .\DARAK.sln
dotnet build .\DARAK.sln --configuration Release --no-restore
dotnet test .\DARAK.sln --configuration Release --no-build
```

When SQL Server is available, also capture:

```powershell
dotnet ef database update `
  --project .\DARAK.Api\DARAK.Api.csproj `
  --startup-project .\DARAK.Api\DARAK.Api.csproj

dotnet ef migrations has-pending-model-changes `
  --project .\DARAK.Api\DARAK.Api.csproj `
  --startup-project .\DARAK.Api\DARAK.Api.csproj
```

Record command output in `docs/Verification-Evidence.md` for release claims.

## Redaction Rules

- Do not show bearer tokens, refresh tokens, cookies, passwords, connection strings, API keys, real emails, real phone numbers, or real resident data.
- Use local demo data only.
- Crop or redact terminal paths only when needed; do not alter pass/fail results.
- Do not commit screenshots generated from a failed Swagger page.
- Do not create placeholder images to make the README look complete.

## Related Assets

- Social preview: `docs/assets/social-preview/darak-social-preview.svg`
- Diagrams: `docs/assets/diagrams/`
- Screenshot folder policy: `docs/assets/screenshots/README.md`
