using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Complaints;
using DARAK.Api.DTOs.Maintenance;
using DARAK.Api.DTOs.Operations;
using DARAK.Api.DTOs.Visitors;
using DARAK.Api.Helpers;
using DARAK.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DARAK.Api.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.Resident)]
[Route("api/resident/requests")]
public sealed class ResidentRequestsController(
    ICurrentUserService currentUserService,
    IMaintenanceService maintenanceService,
    IComplaintViolationService complaintViolationService,
    IVisitorPassService visitorPassService,
    IOperationsService operationsService)
    : ApiControllerBase
{
    [HttpGet("maintenance")]
    public async Task<ActionResult<PagedResult<MaintenanceRequestResponse>>> SearchMaintenance(
        [FromQuery] MaintenanceRequestSearchQuery query,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId, out var unauthorizedResult))
        {
            return unauthorizedResult;
        }

        return ToActionResult(await maintenanceService.SearchResidentAsync(userId, query, cancellationToken));
    }

    [HttpGet("maintenance/{id:guid}")]
    public async Task<ActionResult<MaintenanceRequestResponse>> GetMaintenance(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId, out var unauthorizedResult))
        {
            return unauthorizedResult;
        }

        return ToActionResult(await maintenanceService.GetResidentAsync(userId, id, cancellationToken));
    }

    [HttpPost("maintenance")]
    public async Task<ActionResult<MaintenanceRequestResponse>> CreateMaintenance(
        CreateMaintenanceRequestRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId, out var unauthorizedResult))
        {
            return unauthorizedResult;
        }

        var result = await maintenanceService.CreateResidentAsync(userId, request, cancellationToken);
        if (!result.IsSuccess)
        {
            return ToActionResult(result);
        }

        return CreatedAtAction(nameof(GetMaintenance), new { id = result.Value!.Id }, result.Value);
    }

    [HttpPut("maintenance/{id:guid}")]
    public async Task<ActionResult<MaintenanceRequestResponse>> UpdateMaintenance(
        Guid id,
        UpdateMaintenanceRequestRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId, out var unauthorizedResult))
        {
            return unauthorizedResult;
        }

        return ToActionResult(await maintenanceService.UpdateResidentAsync(userId, id, request, cancellationToken));
    }

    [HttpPost("maintenance/{id:guid}/cancel")]
    public async Task<ActionResult<MaintenanceRequestResponse>> CancelMaintenance(
        Guid id,
        MaintenanceStatusChangeRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId, out var unauthorizedResult))
        {
            return unauthorizedResult;
        }

        return ToActionResult(await maintenanceService.CancelResidentAsync(userId, id, request, cancellationToken));
    }

    [HttpPost("maintenance/{id:guid}/close")]
    public async Task<ActionResult<MaintenanceRequestResponse>> CloseMaintenance(
        Guid id,
        MaintenanceStatusChangeRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId, out var unauthorizedResult))
        {
            return unauthorizedResult;
        }

        return ToActionResult(await maintenanceService.CloseResidentAsync(userId, id, request, cancellationToken));
    }

    [HttpGet("complaints")]
    public async Task<ActionResult<PagedResult<ComplaintResponse>>> SearchComplaints(
        [FromQuery] ComplaintSearchQuery query,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId, out var unauthorizedResult))
        {
            return unauthorizedResult;
        }

        return ToActionResult(await complaintViolationService.SearchComplaintsResidentAsync(
            userId,
            query,
            cancellationToken));
    }

    [HttpGet("complaints/{id:guid}")]
    public async Task<ActionResult<ComplaintResponse>> GetComplaint(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId, out var unauthorizedResult))
        {
            return unauthorizedResult;
        }

        return ToActionResult(await complaintViolationService.GetComplaintResidentAsync(userId, id, cancellationToken));
    }

    [HttpPost("complaints")]
    public async Task<ActionResult<ComplaintResponse>> CreateComplaint(
        CreateComplaintRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId, out var unauthorizedResult))
        {
            return unauthorizedResult;
        }

        var result = await complaintViolationService.CreateComplaintResidentAsync(userId, request, cancellationToken);
        if (!result.IsSuccess)
        {
            return ToActionResult(result);
        }

        return CreatedAtAction(nameof(GetComplaint), new { id = result.Value!.Id }, result.Value);
    }

    [HttpGet("visitor-passes")]
    public async Task<ActionResult<PagedResult<VisitorPassResponse>>> SearchVisitorPasses(
        [FromQuery] VisitorPassSearchQuery query,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId, out var unauthorizedResult))
        {
            return unauthorizedResult;
        }

        return ToActionResult(await visitorPassService.SearchResidentAsync(userId, query, cancellationToken));
    }

    [HttpGet("visitor-passes/{id:guid}")]
    public async Task<ActionResult<VisitorPassResponse>> GetVisitorPass(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId, out var unauthorizedResult))
        {
            return unauthorizedResult;
        }

        return ToActionResult(await visitorPassService.GetResidentAsync(userId, id, cancellationToken));
    }

    [HttpPost("visitor-passes")]
    public async Task<ActionResult<VisitorPassResponse>> CreateVisitorPass(
        CreateVisitorPassRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId, out var unauthorizedResult))
        {
            return unauthorizedResult;
        }

        var result = await visitorPassService.CreateResidentAsync(userId, request, cancellationToken);
        if (!result.IsSuccess)
        {
            return ToActionResult(result);
        }

        return CreatedAtAction(nameof(GetVisitorPass), new { id = result.Value!.Id }, result.Value);
    }

    [HttpPost("visitor-passes/{id:guid}/cancel")]
    public async Task<ActionResult<VisitorPassResponse>> CancelVisitorPass(
        Guid id,
        CancelVisitorPassRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId, out var unauthorizedResult))
        {
            return unauthorizedResult;
        }

        return ToActionResult(await visitorPassService.CancelResidentAsync(userId, id, request, cancellationToken));
    }


    [HttpPost("work-orders/{id:guid}/ratings")]
    public async Task<ActionResult<WorkOrderRatingResponse>> RateWorkOrder(
        Guid id,
        CreateWorkOrderRatingRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId, out var unauthorizedResult))
        {
            return unauthorizedResult;
        }

        return ToActionResult(await operationsService.RateWorkOrderAsync(
            id,
            userId,
            isManager: false,
            request,
            cancellationToken));
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
