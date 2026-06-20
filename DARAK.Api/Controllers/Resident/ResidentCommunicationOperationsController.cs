using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Communication;
using DARAK.Api.Helpers;
using DARAK.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DARAK.Api.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.Resident)]
[Route("api/resident/communication-operations")]
public sealed class ResidentCommunicationOperationsController(
    IResidentCommunicationOperationsService residentCommunicationOperationsService,
    ICurrentUserService currentUserService)
    : ApiControllerBase
{
    [HttpGet("summary")]
    public async Task<ActionResult<ResidentCommunicationOperationsSummaryResponse>> GetSummary(
        CancellationToken cancellationToken)
    {
        return ToActionResult(await residentCommunicationOperationsService.GetResidentSummaryAsync(
            currentUserService.UserId,
            cancellationToken));
    }

    [HttpGet("outages")]
    public async Task<ActionResult<PagedResult<UtilityOutageResponse>>> SearchOutages(
        [FromQuery] UtilityOutageQueryRequest query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await residentCommunicationOperationsService.SearchResidentUtilityOutagesAsync(
            currentUserService.UserId,
            query,
            cancellationToken));
    }

    [HttpGet("outages/{id:guid}")]
    public async Task<ActionResult<UtilityOutageDetailsResponse>> GetOutage(
        Guid id,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await residentCommunicationOperationsService.GetResidentUtilityOutageAsync(
            currentUserService.UserId,
            id,
            cancellationToken));
    }
}
