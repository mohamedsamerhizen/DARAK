param(
    [string]$OutputPath = "$env:USERPROFILE\Desktop\DARAK-clean-ef-baseline.zip"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root = (Get-Location).Path
if (-not (Test-Path (Join-Path $root "DARAK.sln"))) {
    throw "Run this script from the DARAK solution root folder."
}

$temp = Join-Path $env:TEMP ("DARAK-clean-zip-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $temp | Out-Null

robocopy $root $temp /E `
  /XD ".git" ".vs" ".vscode" "bin" "obj" "TestResults" `
      "_phase1a1_patch" "_phase1a2_hotfix1_patch" "_phase1a2_patch" "_phase1b_patch" `
      "_phase2a_corrected_patch" "_phase2a_patch" "_phase2b_hotfix1_patch" "_phase2b_hotfix2_patch" "_phase2b_patch" `
      "_phase3a_hotfix1_patch" "_phase3a_patch" "_phase3b_patch" "_phase5a_ef_escape_patch" `
      "_phase*" "_backup_phase*" "_ef_repair_backup_*" `
  /XF "*.zip" "*.log" "*.user" "*.suo" ".env" "appsettings.Development.json" `
  /R:1 /W:1

if ($LASTEXITCODE -gt 7) {
    throw "Robocopy failed with code $LASTEXITCODE"
}

Remove-Item $OutputPath -Force -ErrorAction SilentlyContinue
Compress-Archive -Path "$temp\*" -DestinationPath $OutputPath -Force
Remove-Item $temp -Recurse -Force
Write-Host "Clean baseline ZIP created: $OutputPath" -ForegroundColor Green
