using DARAK.Api.DTOs.System;
using DARAK.Api.Helpers;
using DARAK.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DARAK.Api.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.SystemReaders)]
[Route("api/admin/darak-360")]
public sealed class AdminDarak360Controller(IDarak360ProfileService darak360ProfileService)
    : ApiControllerBase
{
    [HttpGet("residents/{residentId:guid}")]
    public async Task<ActionResult<Resident360ProfileResponse>> GetResident360Profile(
        Guid residentId,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await darak360ProfileService.GetResident360ProfileAsync(residentId, cancellationToken));
    }

    [HttpGet("units/{unitId:guid}")]
    public async Task<ActionResult<Unit360ProfileResponse>> GetUnit360Profile(
        Guid unitId,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await darak360ProfileService.GetUnit360ProfileAsync(unitId, cancellationToken));
    }

    [HttpGet("compounds/{compoundId:guid}/overview")]
    public async Task<ActionResult<Compound360OverviewResponse>> GetCompound360Overview(
        Guid compoundId,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await darak360ProfileService.GetCompound360OverviewAsync(compoundId, cancellationToken));
    }
}
