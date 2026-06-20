using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Operations;
using DARAK.Api.Helpers;
using DARAK.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DARAK.Api.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.Guard)]
[Route("api/guard/access/contractors")]
public sealed class GuardContractorAccessController(
    ICurrentUserService currentUserService,
    IAccessControlOperationsService accessControlOperationsService)
    : ApiControllerBase
{
    [HttpGet("today")]
    public async Task<ActionResult<PagedResult<ContractorWorkPermitResponse>>> TodayContractorPermits(
        [FromQuery] ContractorWorkPermitQueryRequest query,
        CancellationToken cancellationToken)
    {
        return Ok(await accessControlOperationsService.SearchTodayContractorWorkPermitsForGuardAsync(
            currentUserService.UserId,
            query,
            cancellationToken));
    }

    [HttpPost("{id:guid}/check-in")]
    public async Task<ActionResult<ContractorWorkPermitResponse>> CheckIn(
        Guid id,
        GuardContractorPermitAccessRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await accessControlOperationsService.GuardCheckInContractorWorkPermitAsync(
            id,
            currentUserService.UserId,
            request,
            cancellationToken));
    }

    [HttpPost("{id:guid}/check-out")]
    public async Task<ActionResult<ContractorWorkPermitResponse>> CheckOut(
        Guid id,
        GuardContractorPermitAccessRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await accessControlOperationsService.GuardCheckOutContractorWorkPermitAsync(
            id,
            currentUserService.UserId,
            request,
            cancellationToken));
    }
}
