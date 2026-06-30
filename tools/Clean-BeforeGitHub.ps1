# Removes local-only generated artifacts before creating a GitHub-ready archive.
# Run from the DARAK repository root.

$ErrorActionPreference = "Stop"

Write-Host "Cleaning local DARAK artifacts..." -ForegroundColor Cyan

$root = (Resolve-Path ".").Path
$expectedSolution = Join-Path $root "DARAK.sln"

if (-not (Test-Path $expectedSolution)) {
    throw "Run this script from the DARAK repository root. DARAK.sln was not found."
}

function Remove-IfExists {
    param([Parameter(Mandatory = $true)][string]$Path)
    Remove-Item -Path $Path -Recurse -Force -ErrorAction SilentlyContinue
}

# .env must never remain inside the source tree. Backup outside the repository only.
$envPath = Join-Path $root ".env"
if (Test-Path $envPath) {
    $parent = Split-Path -Parent $root
    $backupPath = Join-Path $parent ("DARAK.env.local.backup.{0:yyyyMMddHHmmss}" -f (Get-Date))
    Copy-Item $envPath $backupPath -Force
    Remove-Item $envPath -Force
    Write-Host "Removed .env after creating an outside-repo backup: $backupPath" -ForegroundColor Yellow
    # Legacy note: .env.local.backup must not be created inside the repository.
}

# Move legacy phase README files away from repository root; root must contain README.md only.
$archiveDir = Join-Path $root "docs\Phase-Readme-Archive"
$legacyRootReadmes = @(Get-ChildItem -Path $root -File -Force -Filter "README-PHASE*.md" -ErrorAction SilentlyContinue)
if ($legacyRootReadmes.Count -gt 0) {
    New-Item -ItemType Directory -Path $archiveDir -Force | Out-Null
    foreach ($file in $legacyRootReadmes) {
        $text = Get-Content $file.FullName -Raw -ErrorAction SilentlyContinue
        $text = $text -replace ('Darak_dev_' + '2026!'), 'YOUR_SQLSERVER_PASSWORD_HERE'
        Set-Content -Path $file.FullName -Value $text -Encoding UTF8
        Move-Item -Path $file.FullName -Destination (Join-Path $archiveDir $file.Name) -Force
    }
    Write-Host "Moved legacy README-PHASE files to docs\Phase-Readme-Archive." -ForegroundColor Yellow
}

# Local handoff archives should never be committed.
Get-ChildItem -Path $root -Filter *.zip -File -Recurse -ErrorAction SilentlyContinue | Remove-Item -Force

# Build/test outputs.
Get-ChildItem -Path $root -Directory -Recurse -Include bin,obj,TestResults,coverage -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force

# Logs and local runtime storage.
Get-ChildItem -Path $root -Directory -Recurse -Include logs -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force

# Local phase patch/backup folders created during remediation.
Get-ChildItem -Path $root -Directory -Force -ErrorAction SilentlyContinue | Where-Object {
    $_.Name -like "_phase*" -or
    $_.Name -like "_backup_phase*" -or
    $_.Name -like "_backup_batch*" -or
    $_.Name -like "_artifact-backup-*" -or
    $_.Name -like "_ef_repair_backup_*" -or
    $_.Name -like "_remediation*" -or
    $_.Name -like "DARAK-REMEDIATION-*" -or
    $_.Name -like "DARAK-BATCH-*"
} | Remove-Item -Recurse -Force

Remove-IfExists (Join-Path $root "DARAK.Api\App_Data\Uploads")
Remove-IfExists (Join-Path $root "DARAK.Api\App_Data\Exports")
Remove-IfExists (Join-Path $root "DARAK.Api\logs")

# Temporary review files created during local development/review.
Remove-IfExists (Join-Path $root "empty-cs-files.txt")
Remove-IfExists (Join-Path $root "DARAK-build-output.txt")
Remove-IfExists (Join-Path $root "DARAK-test-output.txt")
Remove-IfExists (Join-Path $root "DARAK-structure.txt")
Remove-IfExists (Join-Path $root "DARAK-files-list.txt")
Remove-IfExists (Join-Path $root "tools\Phase5B-ObsoleteControllers.txt")

if (Get-Command git -ErrorAction SilentlyContinue) {
    git rm --cached .env 2>$null | Out-Null
    git rm --cached *.zip 2>$null | Out-Null
    git rm -r --cached .\DARAK.Api\bin .\DARAK.Api\obj .\DARAK.Tests\bin .\DARAK.Tests\obj 2>$null | Out-Null
    git rm -r --cached .\DARAK.Api\App_Data\Uploads 2>$null | Out-Null
    git rm -r --cached .\DARAK.Api\App_Data\Exports 2>$null | Out-Null
}

$blockedFiles = @(Get-ChildItem -Path $root -File -Recurse -Force -ErrorAction SilentlyContinue | Where-Object {
    $relative = $_.FullName.Substring($root.Length).TrimStart('\')
    $isSourceControl = $relative -match '(^|[\\/])(\.git|\.vs)([\\/]|$)'
    $isPatchPackage = $relative -match '(^|[\\/])DARAK-REMEDIATION-[^\\/]+([\\/]|$)'
    $isBlocked =
        $_.Name -eq ".env" -or
        $_.Extension -eq ".zip" -or
        $_.FullName -match '[\\/]bin[\\/]' -or
        $_.FullName -match '[\\/]obj[\\/]' -or
        $_.FullName -match '[\\/]TestResults[\\/]' -or
        $_.FullName -match '[\\/]logs[\\/]' -or
        $_.FullName -match '[\\/]App_Data[\\/]Exports[\\/]'

    -not $isSourceControl -and -not $isPatchPackage -and $isBlocked
})

if ($blockedFiles.Count -gt 0) {
    $blockedList = $blockedFiles | Select-Object -First 20 | ForEach-Object { $_.FullName.Substring($root.Length).TrimStart('\') }
    throw "GitHub cleanup failed. Blocked local artifacts remain:`n$($blockedList -join [Environment]::NewLine)"
}

Write-Host "Done. GitHub cleanup completed. Re-run dotnet build/test before publishing." -ForegroundColor Green

