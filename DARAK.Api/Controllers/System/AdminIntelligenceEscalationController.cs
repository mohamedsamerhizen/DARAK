using DARAK.Api.DTOs.System;
using DARAK.Api.Helpers;
using DARAK.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DARAK.Api.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.SystemReaders)]
[Route("api/admin/intelligence-escalation")]
public sealed class AdminIntelligenceEscalationController(IIntelligenceEscalationService intelligenceEscalationService)
    : ApiControllerBase
{
    [HttpGet("compounds/{compoundId:guid}/dashboard")]
    public async Task<ActionResult<IntelligenceEscalationDashboardResponse>> GetCompoundDashboard(
        Guid compoundId,
        [FromQuery] int limit,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await intelligenceEscalationService.GetCompoundEscalationDashboardAsync(
            compoundId,
            limit,
            cancellationToken));
    }

    [HttpGet("compounds/{compoundId:guid}/queue")]
    public async Task<ActionResult<IntelligenceEscalationQueueResponse>> GetCompoundQueue(
        Guid compoundId,
        [FromQuery] string? area,
        [FromQuery] string? severity,
        [FromQuery] int limit,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await intelligenceEscalationService.GetCompoundEscalationQueueAsync(
            compoundId,
            area,
            severity,
            limit,
            cancellationToken));
    }

    [HttpGet("residents/{residentId:guid}/decision-brief")]
    public async Task<ActionResult<ResidentDecisionBriefResponse>> GetResidentDecisionBrief(
        Guid residentId,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await intelligenceEscalationService.GetResidentDecisionBriefAsync(
            residentId,
            cancellationToken));
    }
}
