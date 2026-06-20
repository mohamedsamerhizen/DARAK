# DARAK Phase 5 â€” SaaS + Prioritization Brain

This patch adds the commercial SaaS control layer for DARAK without adding database tables or migrations.

## Added

- SaaS portfolio overview.
- License and capacity intelligence.
- Tenant readiness scoring.
- DARAK prioritization brain for finance, legal, operations, support, and reliability actions.
- Compound-scope authorization for tenant intelligence.
- Automated tests for portfolio isolation, readiness, and priority filtering.

## Main endpoints

- `GET /api/admin/saas-intelligence/portfolio`
- `GET /api/admin/saas-intelligence/compounds/{compoundId}/tenant-readiness`
- `GET /api/admin/saas-intelligence/prioritization-brain`

## Verification

Run:

```powershell
dotnet build .\DARAK.sln --no-incremental
dotnet test .\DARAK.sln --no-build
dotnet ef database update --project .\DARAK.Api\DARAK.Api.csproj --startup-project .\DARAK.Api\DARAK.Api.csproj
dotnet ef migrations has-pending-model-changes --project .\DARAK.Api\DARAK.Api.csproj --startup-project .\DARAK.Api\DARAK.Api.csproj
```

