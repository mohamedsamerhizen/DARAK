using DARAK.Api.DTOs.Approvals;
using DARAK.Api.DTOs.Common;
using DARAK.Api.Helpers;
using DARAK.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DARAK.Api.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.ApprovalManagers)]
[Route("api/admin/approvals")]
public sealed class AdminApprovalsController(
    IApprovalService approvalService,
    ICurrentUserService currentUserService)
    : ApiControllerBase
{
    [HttpPost("requests")]
    public async Task<ActionResult<ApprovalRequestResponse>> CreateRequest(
        CreateApprovalRequestRequest request,
        CancellationToken cancellationToken)
    {
        var result = await approvalService.CreateRequestAsync(
            currentUserService.UserId,
            request,
            cancellationToken);

        if (!result.IsSuccess)
        {
            return ToActionResult(result);
        }

        return CreatedAtAction(nameof(GetRequest), new { id = result.Value!.Id }, result.Value);
    }

    [HttpGet("requests")]
    public async Task<ActionResult<PagedResult<ApprovalRequestResponse>>> SearchRequests(
        [FromQuery] ApprovalSearchQuery query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await approvalService.SearchRequestsAsync(
            currentUserService.UserId,
            query,
            cancellationToken));
    }

    [HttpGet("requests/{id:guid}")]
    public async Task<ActionResult<ApprovalRequestDetailsResponse>> GetRequest(
        Guid id,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await approvalService.GetDetailsAsync(
            currentUserService.UserId,
            id,
            cancellationToken));
    }

    [Authorize(Roles = RoleNames.ApprovalDecisionManagers)]
    [HttpPost("requests/{id:guid}/approve")]
    public async Task<ActionResult<ApprovalRequestDetailsResponse>> Approve(
        Guid id,
        ApprovalDecisionRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await approvalService.ApproveAsync(
            currentUserService.UserId,
            id,
            request,
            cancellationToken));
    }

    [Authorize(Roles = RoleNames.ApprovalDecisionManagers)]
    [HttpPost("requests/{id:guid}/reject")]
    public async Task<ActionResult<ApprovalRequestDetailsResponse>> Reject(
        Guid id,
        ApprovalDecisionRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await approvalService.RejectAsync(
            currentUserService.UserId,
            id,
            request,
            cancellationToken));
    }

    [HttpPost("requests/{id:guid}/cancel")]
    public async Task<ActionResult<ApprovalRequestDetailsResponse>> Cancel(
        Guid id,
        ApprovalDecisionRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await approvalService.CancelAsync(
            currentUserService.UserId,
            id,
            request,
            cancellationToken));
    }

    [HttpPost("requests/{id:guid}/mark-executed")]
    public async Task<ActionResult<ApprovalRequestDetailsResponse>> MarkExecuted(
        Guid id,
        MarkApprovalExecutedRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await approvalService.MarkExecutedAsync(
            currentUserService.UserId,
            id,
            request,
            cancellationToken));
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<ApprovalDashboardResponse>> GetDashboard(
        [FromQuery] Guid? compoundId,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await approvalService.GetDashboardAsync(
            currentUserService.UserId,
            compoundId,
            cancellationToken));
    }
}
