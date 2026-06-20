using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Complaints;
using DARAK.Api.DTOs.Maintenance;
using DARAK.Api.DTOs.Violations;
using DARAK.Api.Helpers;
using DARAK.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DARAK.Api.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.ResidentManagementAdministrators)]
[Route("api/admin/resident-requests")]
public sealed class AdminResidentRequestsController(
    ICurrentUserService currentUserService,
    IMaintenanceService maintenanceService,
    IComplaintViolationService complaintViolationService)
    : ApiControllerBase
{
    [HttpGet("maintenance")]
    public async Task<ActionResult<PagedResult<MaintenanceRequestResponse>>> SearchMaintenance(
        [FromQuery] MaintenanceRequestSearchQuery query,
        CancellationToken cancellationToken)
    {
        return Ok(await maintenanceService.SearchAdminAsync(query, cancellationToken));
    }

    [HttpGet("maintenance/{id:guid}")]
    public async Task<ActionResult<MaintenanceRequestResponse>> GetMaintenance(
        Guid id,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await maintenanceService.GetAdminAsync(id, cancellationToken));
    }

    [HttpPost("maintenance/{id:guid}/assign")]
    public async Task<ActionResult<MaintenanceRequestResponse>> AssignMaintenance(
        Guid id,
        AssignMaintenanceRequestRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await maintenanceService.AssignAsync(
            id,
            currentUserService.UserId,
            request,
            cancellationToken));
    }

    [HttpPost("maintenance/{id:guid}/reject")]
    public async Task<ActionResult<MaintenanceRequestResponse>> RejectMaintenance(
        Guid id,
        MaintenanceStatusChangeRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await maintenanceService.RejectAsync(
            id,
            currentUserService.UserId,
            request,
            cancellationToken));
    }

    [HttpPost("maintenance/{id:guid}/close")]
    public async Task<ActionResult<MaintenanceRequestResponse>> CloseMaintenance(
        Guid id,
        MaintenanceStatusChangeRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await maintenanceService.CloseAdminAsync(
            id,
            currentUserService.UserId,
            request,
            cancellationToken));
    }

    [HttpGet("maintenance/{id:guid}/history")]
    public async Task<ActionResult<PagedResult<MaintenanceStatusHistoryResponse>>> MaintenanceHistory(
        Guid id,
        [FromQuery] MaintenanceStatusHistorySearchQuery query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await maintenanceService.GetHistoryAsync(id, query, cancellationToken));
    }

    [HttpGet("complaints")]
    public async Task<ActionResult<PagedResult<ComplaintResponse>>> SearchComplaints(
        [FromQuery] ComplaintSearchQuery query,
        CancellationToken cancellationToken)
    {
        return Ok(await complaintViolationService.SearchComplaintsAdminAsync(query, cancellationToken));
    }

    [HttpGet("complaints/{id:guid}")]
    public async Task<ActionResult<ComplaintResponse>> GetComplaint(
        Guid id,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await complaintViolationService.GetComplaintAdminAsync(id, cancellationToken));
    }

    [HttpPost("complaints/{id:guid}/review")]
    public async Task<ActionResult<ComplaintResponse>> ReviewComplaint(
        Guid id,
        ComplaintAdminResponseRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await complaintViolationService.MarkComplaintUnderReviewAsync(
            id,
            request,
            cancellationToken));
    }

    [HttpPost("complaints/{id:guid}/resolve")]
    public async Task<ActionResult<ComplaintResponse>> ResolveComplaint(
        Guid id,
        ComplaintAdminResponseRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await complaintViolationService.ResolveComplaintAsync(id, request, cancellationToken));
    }

    [HttpPost("complaints/{id:guid}/reject")]
    public async Task<ActionResult<ComplaintResponse>> RejectComplaint(
        Guid id,
        ComplaintAdminResponseRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await complaintViolationService.RejectComplaintAsync(id, request, cancellationToken));
    }

    [HttpPost("complaints/{id:guid}/escalate-to-violation")]
    public async Task<ActionResult<ViolationResponse>> EscalateComplaintToViolation(
        Guid id,
        ConvertComplaintToViolationRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await complaintViolationService.ConvertComplaintToViolationAsync(
            id,
            currentUserService.UserId,
            request,
            cancellationToken));
    }
}
