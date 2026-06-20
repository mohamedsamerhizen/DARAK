param(
    [switch]$SkipDotnet,
    [switch]$SkipDatabase,
    [string]$ConnectionString,
    [string]$ExpectedTestCount,
    [string]$EvidencePath
)

# Creates a buyer-grade DARAK commercial source package.
# Run from the DARAK repository root after build/test/migrations are verified.
# PASS 09 governance: this script must pass the final release gate or require recorded evidence when database checks are skipped.

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Assert-EvidenceFile($Path) {
    if ([string]::IsNullOrWhiteSpace($Path)) {
        throw "EvidencePath is required when -SkipDatabase is used. Provide a build/test/EF/gate evidence file for this exact source tree."
    }

    $resolved = Resolve-Path -Path $Path -ErrorAction Stop
    $content = Get-Content -Path $resolved -Raw

    $requiredMarkers = @(
        "Build succeeded",
        "failed: 0",
        "DARAK final hardening gate passed"
    )

    foreach ($marker in $requiredMarkers) {
        if ($content -notlike "*$marker*") {
            throw "Evidence file is missing required marker: $marker"
        }
    }

    $hasEfEvidence =
        $content -like "*dotnet ef database update*" -or
        $content -like "*migrations has-pending-model-changes*" -or
        $content -like "*EF Database Evidence*"

    if (-not $hasEfEvidence) {
        throw "Evidence file must include EF database update or pending-model-check evidence when -SkipDatabase is used."
    }

    Write-Host "Validated commercial evidence file: $resolved" -ForegroundColor Cyan
}

$root = (Resolve-Path ".").Path
$solution = Join-Path $root "DARAK.sln"

if (-not (Test-Path $solution)) {
    throw "Run this script from the DARAK repository root. DARAK.sln was not found."
}

Write-Host "Preparing DARAK commercial release package..." -ForegroundColor Cyan

if ($SkipDatabase) {
    Assert-EvidenceFile -Path $EvidencePath
}

# Clean local artifacts first; this removes ZIPs, bin/obj, uploads and logs.
& .\tools\Clean-BeforeGitHub.ps1

# Validate that commercial handover docs and safety gates exist.
& .\tools\Test-CommercialReadiness.ps1

$finalGateArgs = @{}
if ($SkipDotnet) { $finalGateArgs["SkipDotnet"] = $true }
if ($SkipDatabase) { $finalGateArgs["SkipDatabase"] = $true }
if (-not [string]::IsNullOrWhiteSpace($ConnectionString)) { $finalGateArgs["ConnectionString"] = $ConnectionString }
if (-not [string]::IsNullOrWhiteSpace($ExpectedTestCount)) { $finalGateArgs["ExpectedTestCount"] = $ExpectedTestCount }

# PASS 09: buyer packaging must use the strongest final release gate.
& .\tools\Test-FinalReleaseGate.ps1 @finalGateArgs

$outputPath = Join-Path (Split-Path $root -Parent) "DARAK-commercial-release.zip"
if (Test-Path $outputPath) {
    Remove-Item $outputPath -Force
}

$items = Get-ChildItem -Path $root -Force |
    Where-Object {
        $_.Name -notin @('.git', '.vs', '.env', '.env.local.backup') -and
        $_.Extension -ne '.zip'
    }

Compress-Archive -Path $items.FullName -DestinationPath $outputPath -Force

Write-Host "Created commercial release package: $outputPath" -ForegroundColor Green
Write-Host "Archive build/test/migration/final-gate logs separately as buyer evidence." -ForegroundColor Yellow
