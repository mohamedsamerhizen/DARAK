using DARAK.Api.DTOs.System;
using DARAK.Api.Helpers;
using DARAK.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DARAK.Api.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.SystemReaders)]
[Route("api/admin/saas-intelligence")]
public sealed class AdminSaasTenantIntelligenceController(
    ISaasTenantIntelligenceService saasTenantIntelligenceService)
    : ApiControllerBase
{
    [HttpGet("portfolio")]
    public async Task<ActionResult<SaasPortfolioOverviewResponse>> GetPortfolioOverview(
        [FromQuery] int limit,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await saasTenantIntelligenceService.GetPortfolioOverviewAsync(
            limit,
            cancellationToken));
    }

    [HttpGet("compounds/{compoundId:guid}/tenant-readiness")]
    public async Task<ActionResult<SaasTenantReadinessResponse>> GetTenantReadiness(
        Guid compoundId,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await saasTenantIntelligenceService.GetTenantReadinessAsync(
            compoundId,
            cancellationToken));
    }

    [HttpGet("prioritization-brain")]
    public async Task<ActionResult<DarakPrioritizationBrainResponse>> GetPrioritizationBrain(
        [FromQuery] string? area,
        [FromQuery] int limit,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await saasTenantIntelligenceService.GetPrioritizationBrainAsync(
            area,
            limit,
            cancellationToken));
    }
}
