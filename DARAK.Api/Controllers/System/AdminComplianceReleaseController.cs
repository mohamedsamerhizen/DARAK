using DARAK.Api.DTOs.System;
using DARAK.Api.Helpers;
using DARAK.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DARAK.Api.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.SystemReaders)]
[Route("api/admin/compliance-release")]
public sealed class AdminComplianceReleaseController(
    IComplianceReleaseGovernanceService complianceReleaseGovernanceService)
    : ApiControllerBase
{
    [HttpGet("readiness-board")]
    public async Task<ActionResult<ReleaseReadinessBoardResponse>> GetReadinessBoard(
        [FromQuery] ReleaseGovernanceQuery query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await complianceReleaseGovernanceService.GetReleaseReadinessBoardAsync(query, cancellationToken));
    }

    [HttpGet("audit-evidence-dashboard")]
    public async Task<ActionResult<AuditEvidenceDashboardResponse>> GetAuditEvidenceDashboard(
        [FromQuery] ReleaseGovernanceQuery query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await complianceReleaseGovernanceService.GetAuditEvidenceDashboardAsync(query, cancellationToken));
    }

    [HttpGet("compliance-exceptions")]
    public async Task<ActionResult<ComplianceExceptionQueueResponse>> GetComplianceExceptions(
        [FromQuery] ReleaseGovernanceQuery query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await complianceReleaseGovernanceService.GetComplianceExceptionQueueAsync(query, cancellationToken));
    }

    [HttpGet("buyer-handoff-readiness")]
    public async Task<ActionResult<BuyerHandoffReadinessResponse>> GetBuyerHandoffReadiness(
        [FromQuery] ReleaseGovernanceQuery query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await complianceReleaseGovernanceService.GetBuyerHandoffReadinessAsync(query, cancellationToken));
    }

    [HttpGet("governance-timeline")]
    public async Task<ActionResult<GovernanceTimelineResponse>> GetGovernanceTimeline(
        [FromQuery] ReleaseGovernanceQuery query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await complianceReleaseGovernanceService.GetGovernanceTimelineAsync(query, cancellationToken));
    }
}
