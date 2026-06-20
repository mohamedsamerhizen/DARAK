# Creates a source-only DARAK archive after running the GitHub cleanup script.
# Run from the DARAK repository root.

$ErrorActionPreference = "Stop"

$root = (Resolve-Path ".").Path
$expectedSolution = Join-Path $root "DARAK.sln"

if (-not (Test-Path $expectedSolution)) {
    throw "Run this script from the DARAK repository root. DARAK.sln was not found."
}

& .\tools\Clean-BeforeGitHub.ps1

$outputPath = Join-Path (Split-Path $root -Parent) "DARAK-github-ready.zip"
if (Test-Path $outputPath) {
    Remove-Item $outputPath -Force
}

$items = Get-ChildItem -Path . -Force |
    Where-Object { $_.Name -notin @('.git', '.vs', '.env', '.env.local.backup') }

Compress-Archive -Path $items.FullName -DestinationPath $outputPath -Force

Write-Host "Created GitHub-ready source archive: $outputPath"
