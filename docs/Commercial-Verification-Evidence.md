# DARAK Commercial Verification Evidence Template

Use this file outside or alongside a release package as proof of the exact validation performed before delivery. Fill it after running the commands on the buyer/demo baseline.

## Release Identity

- Release name:
- Baseline ZIP:
- Operator:
- Date/time:
- Machine:
- Database target:

## Source Package

- Final source ZIP:
- Created by:
- Contains no `.env`:
- Contains no `bin`/`obj`:
- Contains no `.git`/`.vs`/`.vscode`:
- Contains no logs/uploads/TestResults:
- Contains no nested ZIP/patch archives:

## Build Evidence

Command:

```powershell
dotnet build .\DARAK.sln
```

Result:

```text
Paste build result here.
```

## Test Evidence

Command:

```powershell
dotnet test .\DARAK.sln
```

Expected result for the delivered source tree:

```text
Paste the current test summary here after running the validation command.
```

Actual result:

```text
Paste test result here.
```

## EF Database Evidence

Command:

```powershell
dotnet ef database update `
  --project .\DARAK.Api\DARAK.Api.csproj `
  --startup-project .\DARAK.Api\DARAK.Api.csproj `
  --connection "<REAL_CONNECTION_STRING>"
```

Result:

```text
Paste EF result here.
```

## Commercial Readiness Gate

Command:

```powershell
.\tools\Test-Phase7ReleaseGate.ps1
```

Result:

```text
Paste gate result here.
```

## Known Limitations Accepted By Buyer

- Frontend/mobile app is not included.
- Real payment-provider integration is not included.
- SMS/Email providers require buyer credentials and sandbox testing.
- Document storage is local/container-volume by default; production should decide durable storage.
- Production hosting, backups, SLA terms, and license ownership must be agreed before live use.

## Sign-Off

- Technical validation accepted by:
- Buyer/operator accepted by:
- Notes:
