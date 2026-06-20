# Validates that the DARAK source tree is safe to package for commercial handover.
# Run from the DARAK repository root.

$ErrorActionPreference = "Stop"

$root = (Resolve-Path ".").Path
$solution = Join-Path $root "DARAK.sln"

if (-not (Test-Path $solution)) {
    throw "Run this script from the DARAK repository root. DARAK.sln was not found."
}

Write-Host "Validating DARAK commercial readiness..." -ForegroundColor Cyan

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
    "tools\Test-Phase7ReleaseGate.ps1",
    "tools\New-CommercialReleasePackage.ps1"
)

foreach ($file in $requiredFiles) {
    $path = Join-Path $root $file
    if (-not (Test-Path $path)) {
        throw "Missing commercial handover file: $file"
    }
}

if (Test-Path (Join-Path $root ".env")) {
    throw ".env exists in the source tree. Remove it before commercial handover."
}

$legacyRootReadmes = @(Get-ChildItem -Path $root -File -Force -Filter "README-PHASE*.md" -ErrorAction SilentlyContinue)
if ($legacyRootReadmes.Count -gt 0) {
    $preview = $legacyRootReadmes | Select-Object -First 20 | ForEach-Object { $_.Name }
    throw "Commercial readiness failed. Legacy README-PHASE files still exist at root:`n$($preview -join [Environment]::NewLine)"
}

$blockedFiles = @(Get-ChildItem -Path $root -File -Recurse -Force -ErrorAction SilentlyContinue | Where-Object {
    $relative = $_.FullName.Substring($root.Length).TrimStart('\')
    $isGenerated = $relative -match '(^|[\\/])(bin|obj|TestResults|logs|coverage)([\\/]|$)' -or
        $relative -like 'DARAK.Api\App_Data\Uploads\*' -or
        $relative -match '(^|[\\/])(_phase|_backup_phase|_ef_repair_backup_|_remediation|DARAK-REMEDIATION-)([\\/]|$)'
    $isLocalArtifact = $_.Extension -eq '.zip' -or $_.Name -eq '.env'
    $isSourceControl = $relative -match '(^|[\\/])\.git([\\/]|$)' -or $relative -match '(^|[\\/])\.vs([\\/]|$)'
    -not $isSourceControl -and ($isGenerated -or $isLocalArtifact)
})

if ($blockedFiles.Count -gt 0) {
    $preview = $blockedFiles | Select-Object -First 25 | ForEach-Object { $_.FullName.Substring($root.Length).TrimStart('\') }
    throw "Commercial readiness failed. Remove generated/local artifacts before handover:`n$($preview -join [Environment]::NewLine)"
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

$dangerousSecretPatterns = @(
    ('Darak_dev_' + '2026!'),
    'Password=(?!YOUR_|[$][{]|<REAL)[^;`r`n]{8,};',
    'sk_live_',
    'xoxb-',
    'AKIA[0-9A-Z]{16}'
)

foreach ($file in $secretScanFiles) {
    $text = Get-Content $file.FullName -Raw -ErrorAction SilentlyContinue
    foreach ($pattern in $dangerousSecretPatterns) {
        if ($text -match $pattern) {
            $relative = $file.FullName.Substring($root.Length).TrimStart('\')
            throw "Potential real secret found in $relative. Pattern: $pattern"
        }
    }
}

Write-Host "Commercial readiness validation passed." -ForegroundColor Green
