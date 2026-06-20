param(
    [switch]$WhatIf
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root = (Resolve-Path ".").Path
$solution = Join-Path $root "DARAK.sln"

if (-not (Test-Path $solution)) {
    throw "Run this script from the DARAK repository root. DARAK.sln was not found."
}

Write-Host "DARAK Phase 1 cleanup started..." -ForegroundColor Cyan

function Remove-IfExists {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [switch]$Recurse
    )

    if (Test-Path $Path) {
        if ($WhatIf) {
            Write-Host "Would remove: $Path" -ForegroundColor Yellow
            return
        }

        if ($Recurse) {
            Remove-Item $Path -Recurse -Force -ErrorAction SilentlyContinue
        }
        else {
            Remove-Item $Path -Force -ErrorAction SilentlyContinue
        }
    }
}

# Remove root-only phase/pack/hotfix readmes. Final product documentation must live in docs\ plus README.md.
Get-ChildItem -Path $root -File -Force -Filter "README-*.md" | ForEach-Object {
    Remove-IfExists -Path $_.FullName
}

# Remove local scripts produced during patch application/hotfix operations.
Get-ChildItem -Path $root -File -Force -Include "apply_*.ps1", "fix_*.ps1" | ForEach-Object {
    Remove-IfExists -Path $_.FullName
}

# Remove local archives, backup files, logs, and test/build artifacts.
Get-ChildItem -Path $root -File -Recurse -Force -Include "*.zip", "*.bak", "*.log", "*.trx", "*.coverage" | ForEach-Object {
    Remove-IfExists -Path $_.FullName
}

# Remove local secret files but keep .env.example.
Get-ChildItem -Path $root -File -Force -Filter ".env*" | Where-Object { $_.Name -ne ".env.example" } | ForEach-Object {
    Remove-IfExists -Path $_.FullName
}

$directoryNames = @(
    "bin",
    "obj",
    "TestResults",
    "coverage",
    "logs",
    ".vs",
    ".vscode",
    "_final_megapack_extract",
    "_tmp",
    "tmp"
)

foreach ($name in $directoryNames) {
    Get-ChildItem -Path $root -Directory -Recurse -Force -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -eq $name } |
        ForEach-Object { Remove-IfExists -Path $_.FullName -Recurse }
}

Get-ChildItem -Path $root -Directory -Force -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -like "_phase*" -or $_.Name -like "_pack*" -or $_.Name -like "_backup_phase*" -or $_.Name -like "_ef_repair_backup_*" } |
    ForEach-Object { Remove-IfExists -Path $_.FullName -Recurse }

Remove-IfExists -Path (Join-Path $root "DARAK.Api\App_Data\Uploads") -Recurse

Write-Host "DARAK Phase 1 cleanup completed." -ForegroundColor Green
