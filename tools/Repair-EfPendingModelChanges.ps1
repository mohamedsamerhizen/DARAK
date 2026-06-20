param(
    [switch]$ApplyIfNonEmpty,
    [string]$ConnectionString = "Server=localhost,1433;Database=DARAKDb;User Id=sa;Password=YOUR_SQLSERVER_PASSWORD_HERE;TrustServerCertificate=True;Encrypt=False;Connection Timeout=60"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Fail($message) {
    Write-Host "ERROR: $message" -ForegroundColor Red
    exit 1
}

$root = (Get-Location).Path
if (-not (Test-Path (Join-Path $root "DARAK.sln"))) {
    Fail "Run this script from the DARAK solution root folder."
}

$apiProject = ".\DARAK.Api\DARAK.Api.csproj"
$startupProject = ".\DARAK.Api\DARAK.Api.csproj"
$migrationsDir = Join-Path $root "DARAK.Api\Migrations"

if (-not (Test-Path $migrationsDir)) {
    Fail "Migrations directory not found: $migrationsDir"
}

# Do not allow the old escape hatch to affect this repair.
Remove-Item Env:\DARAK_EF_SUPPRESS_PENDING_MODEL_WARNING -ErrorAction SilentlyContinue

$stamp = Get-Date -Format "yyyyMMddHHmmss"
$backupDir = Join-Path $root "_ef_repair_backup_$stamp"
New-Item -ItemType Directory -Path $backupDir | Out-Null
Copy-Item -Path $migrationsDir -Destination (Join-Path $backupDir "Migrations") -Recurse
Copy-Item -Path ".\DARAK.Api\Data\ApplicationDbContext.cs" -Destination $backupDir
Copy-Item -Path ".\DARAK.Api\Data\ApplicationDbContextFactory.cs" -Destination $backupDir

Write-Host "Backup created: $backupDir" -ForegroundColor Cyan

Write-Host "Step 1/5: dotnet build" -ForegroundColor Cyan
dotnet build .\DARAK.sln
if ($LASTEXITCODE -ne 0) { Fail "Build failed. Stop before touching migrations." }

Write-Host "Step 2/5: dotnet test" -ForegroundColor Cyan
dotnet test .\DARAK.sln
if ($LASTEXITCODE -ne 0) { Fail "Tests failed. Stop before touching migrations." }

$migrationName = "DarakEfModelSync_$stamp"
Write-Host "Step 3/5: creating EF model sync candidate migration: $migrationName" -ForegroundColor Cyan

dotnet ef migrations add $migrationName `
  --project $apiProject `
  --startup-project $startupProject `
  --output-dir Migrations

if ($LASTEXITCODE -ne 0) { Fail "EF migration add failed. Restore from backup if needed: $backupDir" }

$migrationFile = Get-ChildItem $migrationsDir -Filter "*_$migrationName.cs" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $migrationFile) {
    Fail "Generated migration file not found for $migrationName"
}

$content = Get-Content $migrationFile.FullName -Raw
$upMatch = [regex]::Match($content, 'protected override void Up\(MigrationBuilder migrationBuilder\)\s*\{(?<body>[\s\S]*?)\n\s*\}')
if (-not $upMatch.Success) {
    Fail "Could not inspect generated migration Up() method. File: $($migrationFile.FullName)"
}

$upBody = $upMatch.Groups['body'].Value
$hasOperations = $upBody -match 'migrationBuilder\.'

if (-not $hasOperations) {
    Write-Host "Generated migration is empty. Current model matches snapshot." -ForegroundColor Green
    Write-Host "Removing empty candidate migration..." -ForegroundColor Cyan
    dotnet ef migrations remove --force --project $apiProject --startup-project $startupProject
    if ($LASTEXITCODE -ne 0) { Fail "Could not remove empty candidate migration." }

    Write-Host "Step 4/5: database update" -ForegroundColor Cyan
    dotnet ef database update --project $apiProject --startup-project $startupProject --connection $ConnectionString
    if ($LASTEXITCODE -ne 0) { Fail "database update failed even though model has no pending changes." }

    Write-Host "Step 5/5: final build/test" -ForegroundColor Cyan
    dotnet build .\DARAK.sln
    if ($LASTEXITCODE -ne 0) { Fail "Final build failed." }
    dotnet test .\DARAK.sln
    if ($LASTEXITCODE -ne 0) { Fail "Final tests failed." }

    Write-Host "EF repair complete: no pending model changes were generated." -ForegroundColor Green
    exit 0
}

Write-Host "Generated migration contains real operations:" -ForegroundColor Yellow
Write-Host $migrationFile.FullName -ForegroundColor Yellow
Write-Host "Review this migration before applying. It is the exact EF-detected model/snapshot delta." -ForegroundColor Yellow

if (-not $ApplyIfNonEmpty) {
    Write-Host "STOPPED SAFELY. Nothing was applied to the database." -ForegroundColor Yellow
    Write-Host "To apply after review, run:" -ForegroundColor Cyan
    Write-Host ".\tools\Repair-EfPendingModelChanges.ps1 -ApplyIfNonEmpty" -ForegroundColor Cyan
    exit 2
}

Write-Host "Step 4/5: applying generated model-sync migration" -ForegroundColor Cyan
dotnet ef database update --project $apiProject --startup-project $startupProject --connection $ConnectionString
if ($LASTEXITCODE -ne 0) { Fail "database update failed after generated model-sync migration." }

Write-Host "Step 5/5: final build/test" -ForegroundColor Cyan
dotnet build .\DARAK.sln
if ($LASTEXITCODE -ne 0) { Fail "Final build failed." }
dotnet test .\DARAK.sln
if ($LASTEXITCODE -ne 0) { Fail "Final tests failed." }

Write-Host "EF repair complete. Model-sync migration applied without suppressing PendingModelChangesWarning." -ForegroundColor Green
Write-Host "Backup is still available at: $backupDir" -ForegroundColor Cyan
