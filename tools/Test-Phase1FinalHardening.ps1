param(
    [switch]$SkipDotnet,
    [switch]$SkipDatabase,
    [string]$ConnectionString,
    [string]$ExpectedTestCount
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root = (Resolve-Path ".").Path
$solution = Join-Path $root "DARAK.sln"

if (-not (Test-Path $solution)) {
    throw "Run this script from the DARAK repository root. DARAK.sln was not found."
}

Write-Host "DARAK final hardening gate started..." -ForegroundColor Cyan

$requiredFiles = @(
    "README.md",
    ".env.example",
    ".gitignore",
    ".dockerignore",
    "docker-compose.yml",
    "Dockerfile",
    "DARAK.Api\DARAK.Api.csproj",
    "DARAK.Tests\DARAK.Tests.csproj",
    "docs\API-Coverage.md",
    "docs\Buyer-Handoff.md",
    "docs\Buyer-Operations-Runbook.md",
    "docs\Commercial-Feature-Matrix.md",
    "docs\Commercial-Handover-Report.md",
    "docs\Commercial-Value-Summary.md",
    "docs\Commercial-Verification-Evidence.md",
    "docs\Deployment-Runbook.md",
    "docs\Environment-Variables-Reference.md",
    "docs\Final-Commercial-Release-Notes.md",
    "docs\Final-Status-Report.md",
    "docs\Migration-Governance.md",
    "docs\Production-Readiness-Checklist.md",
    "docs\Release-Governance.md",
    "docs\Security-Authorization-Final-Review.md",
    "docs\Security-Checklist.md",
    "docs\Testing-Evidence.md",
    "tools\Clean-BeforeGitHub.ps1",
    "tools\Clean-Phase1DeliveryArtifacts.ps1",
    "tools\New-Phase1CleanDeliveryZip.ps1",
    "tools\Test-Phase1FinalHardening.ps1"
)

foreach ($file in $requiredFiles) {
    $path = Join-Path $root $file
    if (-not (Test-Path $path)) {
        throw "Missing required final hardening file: $file"
    }
}

$legacyRootReadmes = @(Get-ChildItem -Path $root -File -Force -Filter "README-PHASE*.md" -ErrorAction SilentlyContinue)
if ($legacyRootReadmes.Count -gt 0) {
    $preview = $legacyRootReadmes | Select-Object -First 20 | ForEach-Object { $_.Name }
    throw "Legacy README-PHASE files still exist at repository root. Move them to docs\Phase-Readme-Archive or remove them:`n$($preview -join [Environment]::NewLine)"
}

$blocked = @(Get-ChildItem -Path $root -Force -Recurse -ErrorAction SilentlyContinue | Where-Object {
    $relative = $_.FullName.Substring($root.Length).TrimStart('\')

    $insideGeneratedDirectory =
        $relative -match '(^|[\\/])(\.vs|\.vscode|bin|obj|TestResults|logs|coverage)([\\/]|$)' -or
        $relative -like 'DARAK.Api\App_Data\Uploads\*' -or
        $relative -match '(^|[\\/])(_phase|_pack|_final|_backup_phase|_backup_batch|_artifact-backup-|_ef_repair_backup_|_tmp|tmp|_remediation|DARAK-REMEDIATION-)([\\/]|$)'

    $blockedFile = -not $_.PSIsContainer -and (
        $_.Name -eq ".env" -or
        $_.Name -like "apply_*.ps1" -or
        $_.Name -like "fix_*.ps1" -or
        $_.Extension -eq ".zip" -or
        $_.Extension -eq ".bak" -or
        $_.Extension -eq ".log" -or
        $_.Extension -eq ".trx" -or
        $_.Extension -eq ".coverage"
    )

    $insideGeneratedDirectory -or $blockedFile
})

if ($blocked.Count -gt 0) {
    $preview = $blocked | Select-Object -First 40 | ForEach-Object { $_.FullName.Substring($root.Length).TrimStart('\') }
    throw "Final hardening gate failed. Remove local/generated artifacts:`n$($preview -join [Environment]::NewLine)"
}

$gitignore = Get-Content (Join-Path $root ".gitignore") -Raw
foreach ($requiredPattern in @("*.zip", ".env", "*.bak", "bin/", "obj/", "TestResults/", "DARAK-REMEDIATION-*/")) {
    if ($gitignore -notlike "*$requiredPattern*") {
        throw ".gitignore is missing required ignore rule: $requiredPattern"
    }
}

$dockerignore = Get-Content (Join-Path $root ".dockerignore") -Raw
foreach ($requiredPattern in @("**/*.zip", ".env", "*.bak", "**/bin", "**/obj", "DARAK-REMEDIATION-*")) {
    if ($dockerignore -notlike "*$requiredPattern*") {
        throw ".dockerignore is missing required ignore rule: $requiredPattern"
    }
}

$secretScanFiles = @(Get-ChildItem -Path $root -File -Recurse -Include *.json,*.cs,*.csproj,*.ps1,*.md,*.yml,*.yaml,*.example -ErrorAction SilentlyContinue | Where-Object {
    $_.FullName -notmatch '[\\/]bin[\\/]' -and
    $_.FullName -notmatch '[\\/]obj[\\/]' -and
    $_.FullName -notmatch '[\\/]Migrations[\\/]' -and
    $_.FullName -notmatch '[\\/]DARAK-REMEDIATION-[^\\/]+[\\/]' -and
    $_.FullName -notlike "*tools\Test-Phase1FinalHardening.ps1" -and
    $_.FullName -notlike "*tools\Test-FinalReleaseGate.ps1" -and
    $_.FullName -notlike "*tools\Test-CommercialReadiness.ps1" -and
    $_.FullName -notlike "*tools\Test-Phase7ReleaseGate.ps1" -and
    $_.FullName -notlike "*docs\Migration-Governance.md" -and
    $_.FullName -notlike "*docs\Commercial-Verification-Evidence.md"
})

$dangerousPatterns = @(
    ('Darak_dev_' + '2026!'),
    'Password=(?!YOUR_|[$][{]|<REAL)[^;`r`n]{8,};',
    'sk_live_',
    'xoxb-',
    'AKIA[0-9A-Z]{16}'
)

foreach ($file in $secretScanFiles) {
    $text = Get-Content $file.FullName -Raw -ErrorAction SilentlyContinue
    foreach ($pattern in $dangerousPatterns) {
        if ($text -match $pattern) {
            $relative = $file.FullName.Substring($root.Length).TrimStart('\')
            throw "Potential real secret found in $relative. Pattern: $pattern"
        }
    }
}

$appsettings = Get-Content (Join-Path $root "DARAK.Api\appsettings.json") -Raw
if ($appsettings -notmatch "YOUR_SQLSERVER_PASSWORD_HERE" -or $appsettings -notmatch "YOUR_JWT_SECRET_KEY_HERE") {
    throw "appsettings.json must remain placeholder-only. Put real secrets in environment variables or ignored .env files."
}

$developmentSettings = Get-Content (Join-Path $root "DARAK.Api\appsettings.Development.json") -Raw
if ($developmentSettings -notmatch '"SecretKey"' -or $developmentSettings -match '"Key"\s*:') {
    throw "appsettings.Development.json must use Jwt:SecretKey, not Jwt:Key."
}
if ($developmentSettings -notmatch '"BootstrapAdmin"' -or $developmentSettings -notmatch '"Registration"') {
    throw "appsettings.Development.json must use BootstrapAdmin and Registration sections."
}
if ($developmentSettings -match ('Darak_dev_' + '2026!') -or $developmentSettings -match "Password=(?!YOUR_)" -or $developmentSettings -match "sk_live_" -or $developmentSettings -match "xoxb-") {
    throw "appsettings.Development.json appears to contain real credentials. Keep it placeholder-only."
}

if (-not $SkipDotnet) {
    Write-Host "Running dotnet clean..." -ForegroundColor Cyan
    dotnet clean .\DARAK.sln
    if ($LASTEXITCODE -ne 0) { throw "dotnet clean failed." }

    Write-Host "Running dotnet build..." -ForegroundColor Cyan
    dotnet build .\DARAK.sln --no-incremental
    if ($LASTEXITCODE -ne 0) { throw "dotnet build failed." }

    Write-Host "Running dotnet test..." -ForegroundColor Cyan
    $testOutput = dotnet test .\DARAK.sln --no-build 2>&1
    $testOutput | Write-Host
    if ($LASTEXITCODE -ne 0) { throw "dotnet test failed." }

    if (-not [string]::IsNullOrWhiteSpace($ExpectedTestCount)) {
        $summaryLine = $testOutput | Where-Object { $_ -match 'Test summary:' } | Select-Object -Last 1
        if ($summaryLine -and $summaryLine -notmatch "total:\s*$ExpectedTestCount") {
            throw "Unexpected test count. Expected $ExpectedTestCount. Summary: $summaryLine"
        }
    }
}

if (-not $SkipDatabase) {
    if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
        throw "ConnectionString is required unless -SkipDatabase is used."
    }

    Write-Host "Verifying database is up to date..." -ForegroundColor Cyan
    dotnet ef database update `
        --project ".\DARAK.Api\DARAK.Api.csproj" `
        --startup-project ".\DARAK.Api\DARAK.Api.csproj" `
        --connection $ConnectionString
    if ($LASTEXITCODE -ne 0) { throw "dotnet ef database update failed." }

    Write-Host "Verifying no pending model changes..." -ForegroundColor Cyan
    dotnet ef migrations has-pending-model-changes `
        --project ".\DARAK.Api\DARAK.Api.csproj" `
        --startup-project ".\DARAK.Api\DARAK.Api.csproj"
    if ($LASTEXITCODE -ne 0) { throw "EF pending model changes check failed." }
}

Write-Host "DARAK final hardening gate passed." -ForegroundColor Green
