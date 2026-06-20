using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Governance;
using DARAK.Api.Helpers;
using DARAK.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DARAK.Api.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.SuperAdmin)]
[Route("api/admin/governance/arbitration")]
public sealed class AdminGovernanceArbitrationController(
    IGovernanceArbitrationService governanceArbitrationService,
    ICurrentUserService currentUserService)
    : ApiControllerBase
{
    [HttpGet("summary")]
    public async Task<ActionResult<ArbitrationCaseSummaryResponse>> GetSummary(
        [FromQuery] Guid? compoundId,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await governanceArbitrationService.GetSummaryAsync(compoundId, cancellationToken));
    }

    [HttpGet("cases")]
    public async Task<ActionResult<PagedResult<ArbitrationCaseResponse>>> SearchCases(
        [FromQuery] ArbitrationCaseQueryRequest query,
        CancellationToken cancellationToken)
    {
        return Ok(await governanceArbitrationService.SearchCasesAsync(query, cancellationToken));
    }

    [HttpGet("cases/{id:guid}")]
    public async Task<ActionResult<ArbitrationCaseResponse>> GetCase(
        Guid id,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await governanceArbitrationService.GetCaseAsync(id, cancellationToken));
    }

    [HttpPost("cases")]
    public async Task<ActionResult<ArbitrationCaseResponse>> CreateCase(
        CreateArbitrationCaseRequest request,
        CancellationToken cancellationToken)
    {
        var result = await governanceArbitrationService.CreateCaseAsync(
            currentUserService.UserId,
            request,
            cancellationToken);
        if (!result.IsSuccess)
        {
            return ToActionResult(result);
        }

        return CreatedAtAction(nameof(GetCase), new { id = result.Value!.Id }, result.Value);
    }

    [HttpPost("cases/{id:guid}/events")]
    public async Task<ActionResult<ArbitrationCaseResponse>> AddEvent(
        Guid id,
        AddArbitrationCaseEventRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await governanceArbitrationService.AddEventAsync(
            id,
            currentUserService.UserId,
            request,
            cancellationToken));
    }

    [HttpPost("cases/{id:guid}/final-decision")]
    public async Task<ActionResult<ArbitrationCaseResponse>> IssueFinalDecision(
        Guid id,
        IssueArbitrationFinalDecisionRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await governanceArbitrationService.IssueFinalDecisionAsync(
            id,
            currentUserService.UserId,
            request,
            cancellationToken));
    }

    [HttpPost("cases/{id:guid}/cancel")]
    public async Task<ActionResult<ArbitrationCaseResponse>> CancelCase(
        Guid id,
        CancelArbitrationCaseRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await governanceArbitrationService.CancelCaseAsync(
            id,
            currentUserService.UserId,
            request,
            cancellationToken));
    }
}
