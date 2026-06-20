using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Finance;
using DARAK.Api.Helpers;
using DARAK.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DARAK.Api.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.Resident)]
[Route("api/resident/financial-governance")]
public sealed class ResidentFinancialGovernanceController(
    ICurrentUserService currentUserService,
    IFinancialGovernanceService financialGovernanceService)
    : ApiControllerBase
{
    [HttpGet("summary")]
    public async Task<ActionResult<ResidentFinancialGovernanceSummaryResponse>> GetSummary(
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId, out var unauthorizedResult))
        {
            return unauthorizedResult;
        }

        return ToActionResult(await financialGovernanceService.GetResidentFinancialGovernanceSummaryAsync(userId, cancellationToken));
    }

    [HttpGet("disputes")]
    public async Task<ActionResult<PagedResult<FinancialDisputeResponse>>> SearchFinancialDisputes(
        [FromQuery] FinancialDisputeSearchQuery query,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId, out var unauthorizedResult))
        {
            return unauthorizedResult;
        }

        return ToActionResult(await financialGovernanceService.SearchResidentFinancialDisputesAsync(userId, query, cancellationToken));
    }

    [HttpPost("disputes")]
    public async Task<ActionResult<FinancialDisputeResponse>> CreateFinancialDispute(
        CreateResidentFinancialDisputeRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId, out var unauthorizedResult))
        {
            return unauthorizedResult;
        }

        var result = await financialGovernanceService.CreateResidentFinancialDisputeAsync(
            userId,
            request,
            cancellationToken);
        if (!result.IsSuccess)
        {
            return ToActionResult(result);
        }

        return CreatedAtAction(nameof(GetFinancialDispute), new { id = result.Value!.Id }, result.Value);
    }

    [HttpGet("disputes/{id:guid}")]
    public async Task<ActionResult<FinancialDisputeResponse>> GetFinancialDispute(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId, out var unauthorizedResult))
        {
            return unauthorizedResult;
        }

        return ToActionResult(await financialGovernanceService.GetResidentFinancialDisputeAsync(userId, id, cancellationToken));
    }

    [HttpGet("violation-appeals")]
    public async Task<ActionResult<PagedResult<ViolationAppealResponse>>> SearchViolationAppeals(
        [FromQuery] ViolationAppealSearchQuery query,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId, out var unauthorizedResult))
        {
            return unauthorizedResult;
        }

        return ToActionResult(await financialGovernanceService.SearchResidentViolationAppealsAsync(userId, query, cancellationToken));
    }

    [HttpPost("violation-appeals")]
    public async Task<ActionResult<ViolationAppealResponse>> CreateViolationAppeal(
        CreateResidentViolationAppealRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId, out var unauthorizedResult))
        {
            return unauthorizedResult;
        }

        var result = await financialGovernanceService.CreateResidentViolationAppealAsync(
            userId,
            request,
            cancellationToken);
        if (!result.IsSuccess)
        {
            return ToActionResult(result);
        }

        return CreatedAtAction(nameof(GetViolationAppeal), new { id = result.Value!.Id }, result.Value);
    }

    [HttpGet("violation-appeals/{id:guid}")]
    public async Task<ActionResult<ViolationAppealResponse>> GetViolationAppeal(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId, out var unauthorizedResult))
        {
            return unauthorizedResult;
        }

        return ToActionResult(await financialGovernanceService.GetResidentViolationAppealAsync(userId, id, cancellationToken));
    }

    private bool TryGetCurrentUserId(out Guid userId, out ObjectResult unauthorizedResult)
    {
        var currentUserId = currentUserService.UserId;
        if (currentUserId.HasValue)
        {
            userId = currentUserId.Value;
            unauthorizedResult = null!;
            return true;
        }

        userId = Guid.Empty;
        unauthorizedResult = Unauthorized(ApiErrorResponseFactory.Create(HttpContext, "Current user is invalid."));
        return false;
    }
}
