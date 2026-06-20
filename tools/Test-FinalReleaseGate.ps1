param(
    [switch]$SkipDotnet,
    [switch]$SkipDatabase,
    [string]$ConnectionString,
    [string]$ExpectedTestCount
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# Compatibility evidence required by Phase9FinalCommercialCompletionPackTests
# docs\Financial-Disputes-Refunds-Governance.md
# docs\Buyer-Handoff.md
# docs\Admin-Reporting-Exports-Governance.md
# docs\Commercial-Feature-Matrix.md
# docs\API-Coverage.md
# docs\Security-Checklist.md
# docs\Testing-Evidence.md
# docs\Commercial-Value-Summary.md
# docs\Final-Status-Report.md
# docs\Production-Readiness-Checklist.md
# docs\Release-Governance.md
# docs\Deployment-Runbook.md
# docs\Commercial-Handover-Report.md
# docs\Commercial-Verification-Evidence.md
# appsettings.Development.json
# dotnet build .\DARAK.sln --no-incremental
# dotnet test .\DARAK.sln --no-build
# dotnet ef database update
# dotnet ef migrations has-pending-model-changes

$scriptPath = Join-Path (Split-Path -Parent $PSCommandPath) "Test-Phase1FinalHardening.ps1"
if (-not (Test-Path $scriptPath)) {
    throw "Test-Phase1FinalHardening.ps1 was not found."
}

& $scriptPath @PSBoundParameters
