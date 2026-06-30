param(
    [string]$DestinationZip = "<repo-root>_PHASE1_FINAL_HARDENING_CLEAN_DELIVERY.zip"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root = (Resolve-Path ".").Path
$solution = Join-Path $root "DARAK.sln"

if (-not (Test-Path $solution)) {
    throw "Run this script from the DARAK repository root. DARAK.sln was not found."
}

$stage = Join-Path ([System.IO.Path]::GetTempPath()) ("DARAK_PHASE1_CLEAN_" + [Guid]::NewGuid().ToString("N"))

Write-Host "Creating Phase 1 clean delivery staging folder..." -ForegroundColor Cyan
New-Item -ItemType Directory -Path $stage | Out-Null

robocopy $root $stage /E `
    /XD ".git" ".vs" ".vscode" "bin" "obj" "TestResults" "coverage" "logs" "_final_megapack_extract" "_tmp" "tmp" `
    /XF "*.zip" "*.bak" "*.log" "*.trx" "*.coverage" ".env" "apply_*.ps1" "fix_*.ps1" | Out-Host

if ($LASTEXITCODE -gt 7) {
    throw "Robocopy failed with code $LASTEXITCODE."
}

# Root phase/pack readmes are implementation artifacts, not final buyer documentation.
Get-ChildItem -Path $stage -File -Filter "README-*.md" -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue

# Remove any staged local generated folders that robocopy may have copied by pattern mismatch.
Get-ChildItem -Path $stage -Directory -Force -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -like "_phase*" -or $_.Name -like "_pack*" -or $_.Name -like "_backup_phase*" -or $_.Name -like "_ef_repair_backup_*" } |
    Remove-Item -Recurse -Force -ErrorAction SilentlyContinue

Remove-Item $DestinationZip -Force -ErrorAction SilentlyContinue

Write-Host "Creating Phase 1 clean delivery ZIP..." -ForegroundColor Cyan
Compress-Archive -Path (Join-Path $stage "*") -DestinationPath $DestinationZip -Force

Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue

if (-not (Test-Path $DestinationZip)) {
    throw "Clean delivery ZIP was not created."
}

Write-Host "DONE: Phase 1 clean delivery ZIP created:" -ForegroundColor Green
Write-Host $DestinationZip -ForegroundColor Yellow

