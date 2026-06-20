using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.RiskFlags;
using DARAK.Api.Helpers;
using DARAK.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DARAK.Api.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.RiskFlagReaders)]
[Route("api/admin/risk-flags")]
public sealed class AdminResidentRiskFlagsController(
    IResidentRiskFlagService residentRiskFlagService,
    ICurrentUserService currentUserService)
    : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<ResidentRiskFlagResponse>>> Search(
        [FromQuery] ResidentRiskFlagSearchQuery query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await residentRiskFlagService.SearchFlagsAsync(
            currentUserService.UserId,
            query,
            cancellationToken));
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<ResidentRiskFlagDashboardResponse>> GetDashboard(
        [FromQuery] Guid? compoundId,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await residentRiskFlagService.GetDashboardAsync(
            currentUserService.UserId,
            compoundId,
            cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ResidentRiskFlagDetailsResponse>> Get(
        Guid id,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await residentRiskFlagService.GetDetailsAsync(
            currentUserService.UserId,
            id,
            cancellationToken));
    }

    [Authorize(Roles = RoleNames.RiskFlagManagers)]
    [HttpPost]
    public async Task<ActionResult<ResidentRiskFlagResponse>> Create(
        CreateResidentRiskFlagRequest request,
        CancellationToken cancellationToken)
    {
        var result = await residentRiskFlagService.CreateFlagAsync(
            currentUserService.UserId,
            request,
            cancellationToken);

        if (!result.IsSuccess)
        {
            return ToActionResult(result);
        }

        return CreatedAtAction(nameof(Get), new { id = result.Value!.Id }, result.Value);
    }

    [Authorize(Roles = RoleNames.RiskFlagManagers)]
    [HttpPost("{id:guid}/assign")]
    public async Task<ActionResult<ResidentRiskFlagDetailsResponse>> Assign(
        Guid id,
        AssignResidentRiskFlagRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await residentRiskFlagService.AssignAsync(
            currentUserService.UserId,
            id,
            request,
            cancellationToken));
    }

    [Authorize(Roles = RoleNames.RiskFlagManagers)]
    [HttpPost("{id:guid}/change-severity")]
    public async Task<ActionResult<ResidentRiskFlagDetailsResponse>> ChangeSeverity(
        Guid id,
        ChangeResidentRiskFlagSeverityRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await residentRiskFlagService.ChangeSeverityAsync(
            currentUserService.UserId,
            id,
            request,
            cancellationToken));
    }

    [Authorize(Roles = RoleNames.RiskFlagManagers)]
    [HttpPost("{id:guid}/review")]
    public async Task<ActionResult<ResidentRiskFlagDetailsResponse>> MarkReviewed(
        Guid id,
        ReviewResidentRiskFlagRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await residentRiskFlagService.MarkReviewedAsync(
            currentUserService.UserId,
            id,
            request,
            cancellationToken));
    }

    [Authorize(Roles = RoleNames.RiskFlagClosureManagers)]
    [HttpPost("{id:guid}/resolve")]
    public async Task<ActionResult<ResidentRiskFlagDetailsResponse>> Resolve(
        Guid id,
        CloseResidentRiskFlagRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await residentRiskFlagService.ResolveAsync(
            currentUserService.UserId,
            id,
            request,
            cancellationToken));
    }

    [Authorize(Roles = RoleNames.RiskFlagClosureManagers)]
    [HttpPost("{id:guid}/dismiss")]
    public async Task<ActionResult<ResidentRiskFlagDetailsResponse>> Dismiss(
        Guid id,
        CloseResidentRiskFlagRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await residentRiskFlagService.DismissAsync(
            currentUserService.UserId,
            id,
            request,
            cancellationToken));
    }

    [Authorize(Roles = RoleNames.RiskFlagManagers)]
    [HttpPost("{id:guid}/notes")]
    public async Task<ActionResult<ResidentRiskFlagDetailsResponse>> AddNote(
        Guid id,
        AddResidentRiskFlagNoteRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await residentRiskFlagService.AddNoteAsync(
            currentUserService.UserId,
            id,
            request,
            cancellationToken));
    }
}
