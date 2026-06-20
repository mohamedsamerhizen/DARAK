param(
    [switch]$SkipDotnet,
    [string]$ExpectedTestCount
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root = (Resolve-Path ".").Path
$solution = Join-Path $root "DARAK.sln"

if (-not (Test-Path $solution)) {
    throw "Run this script from the DARAK repository root. DARAK.sln was not found."
}

Write-Host "DARAK Phase 7 release gate started..." -ForegroundColor Cyan

$requiredFiles = @(
    "README.md",
    ".env.example",
    ".gitignore",
    ".dockerignore",
    "docker-compose.yml",
    "Dockerfile",
    "docs\Commercial-Handover-Report.md",
    "docs\Production-Readiness-Checklist.md",
    "docs\Security-Authorization-Final-Review.md",
    "docs\Deployment-Runbook.md",
    "docs\Buyer-Operations-Runbook.md",
    "docs\Final-Commercial-Release-Notes.md",
    "docs\Release-Governance.md",
    "docs\Migration-Governance.md",
    "docs\Commercial-Verification-Evidence.md",
    "docs\Environment-Variables-Reference.md",
    "tools\Clean-BeforeGitHub.ps1",
    "tools\Test-CommercialReadiness.ps1",
    "tools\Test-Phase7ReleaseGate.ps1",
    "tools\New-CommercialReleasePackage.ps1"
)

foreach ($file in $requiredFiles) {
    $path = Join-Path $root $file
    if (-not (Test-Path $path)) {
        throw "Missing required release/governance file: $file"
    }
}

$legacyRootReadmes = @(Get-ChildItem -Path $root -File -Force -Filter "README-PHASE*.md" -ErrorAction SilentlyContinue)
if ($legacyRootReadmes.Count -gt 0) {
    $preview = $legacyRootReadmes | Select-Object -First 20 | ForEach-Object { $_.Name }
    throw "Release gate failed. Legacy README-PHASE files still exist at root:`n$($preview -join [Environment]::NewLine)"
}

$blocked = @(Get-ChildItem -Path $root -Force -Recurse -ErrorAction SilentlyContinue | Where-Object {
    $relative = $_.FullName.Substring($root.Length).TrimStart('\')
    $insideIgnored =
        $relative -match '(^|[\\/])(\.git|\.vs|\.vscode|bin|obj|TestResults|logs|coverage)([\\/]|$)' -or
        $relative -like 'DARAK.Api\App_Data\Uploads\*' -or
        $relative -match '(^|[\\/])(_phase|_backup_phase|_ef_repair_backup_|_remediation|DARAK-REMEDIATION-)([\\/]|$)'

    $isBlockedFile = -not $_.PSIsContainer -and (
        $_.Name -eq ".env" -or
        $_.Extension -eq ".zip" -or
        $_.Extension -eq ".log" -or
        $_.Extension -eq ".trx" -or
        $_.Extension -eq ".coverage"
    )

    $insideIgnored -or $isBlockedFile
})

if ($blocked.Count -gt 0) {
    $preview = $blocked | Select-Object -First 30 | ForEach-Object { $_.FullName.Substring($root.Length).TrimStart('\') }
    throw "Release gate failed. Local/generated artifacts are still present:`n$($preview -join [Environment]::NewLine)"
}

$secretScanFiles = @(Get-ChildItem -Path $root -File -Recurse -Include *.json,*.cs,*.csproj,*.ps1,*.md,*.yml,*.yaml,*.example -ErrorAction SilentlyContinue | Where-Object {
    $_.FullName -notmatch '[\\/]bin[\\/]' -and
    $_.FullName -notmatch '[\\/]obj[\\/]' -and
    $_.FullName -notmatch '[\\/]Migrations[\\/]' -and
    $_.FullName -notmatch '[\\/]DARAK-REMEDIATION-[^\\/]+[\\/]' -and
    $_.FullName -notlike "*tools\Test-CommercialReadiness.ps1" -and
    $_.FullName -notlike "*tools\Test-Phase7ReleaseGate.ps1" -and
    $_.FullName -notlike "*tools\Test-Phase1FinalHardening.ps1" -and
    $_.FullName -notlike "*tools\Test-FinalReleaseGate.ps1" -and
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

if (-not $SkipDotnet) {
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

Write-Host "DARAK Phase 7 release gate passed." -ForegroundColor Green
