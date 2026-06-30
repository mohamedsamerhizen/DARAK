# DARAK Screenshot Capture Guide

DARAK is backend-only. This repository does not include a frontend or mobile client, so no product UI screenshots are committed or fabricated.

## What Can Be Captured Honestly

- Swagger/OpenAPI screen in Development after the API starts.
- GitHub Actions workflow run after pushing the repository.
- Terminal output for restore, build, test, and EF commands.
- SQL Server object explorer or query output during local verification.
- Mermaid diagrams from `docs/Architecture-Diagrams.md` rendered by GitHub.

## Local Capture Steps

1. Configure local environment variables or `.env` from `.env.example`.
2. Start SQL Server if database screenshots are needed:

```powershell
docker compose up -d sqlserver
```

3. Run migrations:

```powershell
dotnet ef database update `
  --project .\DARAK.Api\DARAK.Api.csproj `
  --startup-project .\DARAK.Api\DARAK.Api.csproj
```

4. Start the API:

```powershell
dotnet run --project .\DARAK.Api\DARAK.Api.csproj
```

5. In Development, open the Swagger URL printed by ASP.NET Core and capture only the actual running page.

## Rules

- Do not create mock UI screenshots for this backend repository.
- Do not include screenshots with secrets, bearer tokens, connection strings, or real personal data.
- Do not commit local generated screenshots unless they are reviewed and intentionally added.
- Prefer terminal evidence in `docs/Verification-Evidence.md` for release claims.

