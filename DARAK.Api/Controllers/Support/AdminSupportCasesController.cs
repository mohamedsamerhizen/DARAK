using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Support;
using DARAK.Api.Helpers;
using DARAK.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DARAK.Api.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.SupportCaseReaders)]
[Route("api/admin/support")]
public sealed class AdminSupportCasesController(
    ISupportCaseService supportCaseService,
    ICurrentUserService currentUserService)
    : ApiControllerBase
{
    [HttpGet("cases")]
    public async Task<ActionResult<PagedResult<SupportCaseResponse>>> SearchCases(
        [FromQuery] SupportCaseSearchQuery query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await supportCaseService.SearchCasesAsync(query, cancellationToken));
    }

    [HttpGet("cases/{id:guid}")]
    public async Task<ActionResult<SupportCaseDetailsResponse>> GetCase(Guid id, CancellationToken cancellationToken)
    {
        return ToActionResult(await supportCaseService.GetCaseAsync(id, cancellationToken));
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<SupportDashboardResponse>> GetDashboard(
        [FromQuery] SupportDashboardQuery query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await supportCaseService.GetDashboardAsync(query, cancellationToken));
    }

    [Authorize(Roles = RoleNames.SupportCaseManagers)]
    [HttpPost("cases")]
    public async Task<ActionResult<SupportCaseResponse>> CreateCase(
        CreateSupportCaseRequest request,
        CancellationToken cancellationToken)
    {
        var result = await supportCaseService.CreateCaseAsync(currentUserService.UserId, request, cancellationToken);
        if (!result.IsSuccess)
        {
            return ToActionResult(result);
        }

        return CreatedAtAction(nameof(GetCase), new { id = result.Value!.Id }, result.Value);
    }

    [Authorize(Roles = RoleNames.SupportCaseManagers)]
    [HttpPost("cases/{id:guid}/assign")]
    public async Task<ActionResult<SupportCaseResponse>> AssignCase(Guid id, AssignSupportCaseRequest request, CancellationToken cancellationToken)
    {
        return ToActionResult(await supportCaseService.AssignCaseAsync(currentUserService.UserId, id, request, cancellationToken));
    }

    [Authorize(Roles = RoleNames.SupportCaseManagers)]
    [HttpPost("cases/{id:guid}/escalate")]
    public async Task<ActionResult<SupportCaseResponse>> EscalateCase(Guid id, EscalateSupportCaseRequest request, CancellationToken cancellationToken)
    {
        return ToActionResult(await supportCaseService.EscalateCaseAsync(currentUserService.UserId, id, request, cancellationToken));
    }

    [Authorize(Roles = RoleNames.SupportCaseManagers)]
    [HttpPost("cases/{id:guid}/resolve")]
    public async Task<ActionResult<SupportCaseResponse>> ResolveCase(Guid id, ResolveSupportCaseRequest request, CancellationToken cancellationToken)
    {
        return ToActionResult(await supportCaseService.ResolveCaseAsync(currentUserService.UserId, id, request, cancellationToken));
    }

    [Authorize(Roles = RoleNames.SupportCaseManagers)]
    [HttpPost("cases/{id:guid}/reopen")]
    public async Task<ActionResult<SupportCaseResponse>> ReopenCase(Guid id, ReopenSupportCaseRequest request, CancellationToken cancellationToken)
    {
        return ToActionResult(await supportCaseService.ReopenCaseAsync(currentUserService.UserId, id, request, cancellationToken));
    }

    [Authorize(Roles = RoleNames.SupportCaseManagers)]
    [HttpPost("cases/{id:guid}/notes")]
    public async Task<ActionResult<SupportCaseDetailsResponse>> AddNote(Guid id, AddSupportCaseNoteRequest request, CancellationToken cancellationToken)
    {
        return ToActionResult(await supportCaseService.AddNoteAsync(currentUserService.UserId, id, request, cancellationToken));
    }
}
