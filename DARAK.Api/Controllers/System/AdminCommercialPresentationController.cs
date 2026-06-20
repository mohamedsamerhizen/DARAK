using DARAK.Api.DTOs.System;
using DARAK.Api.Helpers;
using DARAK.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DARAK.Api.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.SystemReaders)]
[Route("api/admin/commercial-presentation")]
public sealed class AdminCommercialPresentationController(
    ICommercialPresentationService commercialPresentationService)
    : ApiControllerBase
{
    [HttpGet("demo-seed-blueprint")]
    public async Task<ActionResult<DemoSeedBlueprintResponse>> GetDemoSeedBlueprint(
        [FromQuery] CommercialPresentationQuery query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await commercialPresentationService.GetDemoSeedBlueprintAsync(query, cancellationToken));
    }

    [HttpGet("demo-mode")]
    public async Task<ActionResult<CommercialDemoModeResponse>> GetDemoMode(
        [FromQuery] CommercialPresentationQuery query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await commercialPresentationService.GetCommercialDemoModeAsync(query, cancellationToken));
    }

    [HttpGet("buyer-presentation-pack")]
    public async Task<ActionResult<BuyerPresentationPackResponse>> GetBuyerPresentationPack(
        [FromQuery] CommercialPresentationQuery query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await commercialPresentationService.GetBuyerPresentationPackAsync(query, cancellationToken));
    }
}
