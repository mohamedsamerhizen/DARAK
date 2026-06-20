using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Maintenance;
using DARAK.Api.Helpers;
using DARAK.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DARAK.Api.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.MaintenanceStaff)]
[Route("api/maintenance/work")]
public sealed class MaintenanceWorkController(
    ICurrentUserService currentUserService,
    IMaintenanceService maintenanceService)
    : ApiControllerBase
{
    [HttpGet("assigned")]
    public async Task<ActionResult<PagedResult<MaintenanceRequestResponse>>> AssignedWork(
        [FromQuery] MaintenanceRequestSearchQuery query,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId, out var unauthorizedResult))
        {
            return unauthorizedResult;
        }

        return ToActionResult(await maintenanceService.SearchAssignedToStaffAsync(userId, query, cancellationToken));
    }

    [HttpGet("assigned/{id:guid}")]
    public async Task<ActionResult<MaintenanceRequestResponse>> GetAssignedWork(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId, out var unauthorizedResult))
        {
            return unauthorizedResult;
        }

        return ToActionResult(await maintenanceService.GetAssignedToStaffAsync(userId, id, cancellationToken));
    }

    [HttpPost("assigned/{id:guid}/start")]
    public async Task<ActionResult<MaintenanceRequestResponse>> StartWork(
        Guid id,
        MaintenanceStatusChangeRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId, out var unauthorizedResult))
        {
            return unauthorizedResult;
        }

        return ToActionResult(await maintenanceService.StartAsync(userId, id, request, cancellationToken));
    }

    [HttpPost("assigned/{id:guid}/resolve")]
    public async Task<ActionResult<MaintenanceRequestResponse>> ResolveWork(
        Guid id,
        ResolveMaintenanceRequestRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId, out var unauthorizedResult))
        {
            return unauthorizedResult;
        }

        return ToActionResult(await maintenanceService.ResolveAsync(userId, id, request, cancellationToken));
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
