# DARAK Phase 2 â€” Commercial Demo & Buyer Presentation

This patch adds a non-invasive buyer presentation layer:

- Demo seed blueprint
- Commercial demo mode
- Buyer presentation pack
- Phase 2 documentation
- Service/controller/tests

No migrations are added. No tables are changed.

Run:

```powershell
dotnet build .\DARAK.sln --no-incremental
dotnet test .\DARAK.sln --no-build
dotnet ef database update --project .\DARAK.Api\DARAK.Api.csproj --startup-project .\DARAK.Api\DARAK.Api.csproj --connection "Server=localhost,1433;Database=DARAKDb;User Id=sa;Password=YOUR_SQLSERVER_PASSWORD_HERE;TrustServerCertificate=True;Encrypt=False"
dotnet ef migrations has-pending-model-changes --project .\DARAK.Api\DARAK.Api.csproj --startup-project .\DARAK.Api\DARAK.Api.csproj
```

