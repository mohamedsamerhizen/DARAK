using DARAK.Api.DTOs.System;
using DARAK.Api.Helpers;
using DARAK.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DARAK.Api.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.SystemReaders)]
[Route("api/admin/commercial-delivery")]
public sealed class AdminCommercialDeliveryController(
    ICommercialProductizationService commercialProductizationService)
    : ApiControllerBase
{
    [HttpGet("module-registry")]
    public async Task<ActionResult<CommercialModuleRegistryResponse>> GetModuleRegistry(
        [FromQuery] FinalDeliveryQuery query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await commercialProductizationService.GetModuleRegistryAsync(query, cancellationToken));
    }

    [HttpGet("product-capability-map")]
    public async Task<ActionResult<ProductCapabilityMapResponse>> GetProductCapabilityMap(
        [FromQuery] FinalDeliveryQuery query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await commercialProductizationService.GetProductCapabilityMapAsync(query, cancellationToken));
    }

    [HttpGet("buyer-demo-readiness")]
    public async Task<ActionResult<BuyerDemoReadinessResponse>> GetBuyerDemoReadiness(
        [FromQuery] FinalDeliveryQuery query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await commercialProductizationService.GetBuyerDemoReadinessAsync(query, cancellationToken));
    }

    [HttpGet("client-onboarding-readiness")]
    public async Task<ActionResult<ClientOnboardingReadinessResponse>> GetClientOnboardingReadiness(
        [FromQuery] FinalDeliveryQuery query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await commercialProductizationService.GetClientOnboardingReadinessAsync(query, cancellationToken));
    }

    [HttpGet("final-scorecard")]
    public async Task<ActionResult<FinalCommercialDeliveryScorecardResponse>> GetFinalDeliveryScorecard(
        [FromQuery] FinalDeliveryQuery query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await commercialProductizationService.GetFinalDeliveryScorecardAsync(query, cancellationToken));
    }
}
