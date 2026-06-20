using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Operations;
using DARAK.Api.Helpers;
using DARAK.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DARAK.Api.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.OperationsManagers)]
[Route("api/admin/maintenance-reliability")]
public sealed class AdminMaintenanceReliabilityController(
    ICurrentUserService currentUserService,
    IMaintenanceReliabilityService maintenanceReliabilityService)
    : ApiControllerBase
{
    [HttpPost("sla/refresh-breaches")]
    public async Task<ActionResult<MaintenanceSlaRefreshResponse>> RefreshSlaBreaches(
        [FromQuery] MaintenanceReliabilitySummaryQuery query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await maintenanceReliabilityService.RefreshSlaBreachesAsync(query, cancellationToken));
    }

    [HttpGet("summary")]
    public async Task<ActionResult<MaintenanceReliabilitySummaryResponse>> Summary(
        [FromQuery] MaintenanceReliabilitySummaryQuery query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await maintenanceReliabilityService.GetSummaryAsync(query, cancellationToken));
    }

    [HttpGet("assets")]
    public async Task<ActionResult<PagedResult<MaintenanceAssetResponse>>> SearchAssets(
        [FromQuery] MaintenanceAssetQueryRequest query,
        CancellationToken cancellationToken)
    {
        return Ok(await maintenanceReliabilityService.SearchAssetsAsync(query, cancellationToken));
    }

    [HttpGet("assets/{id:guid}")]
    public async Task<ActionResult<MaintenanceAssetResponse>> GetAsset(
        Guid id,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await maintenanceReliabilityService.GetAssetAsync(id, cancellationToken));
    }

    [HttpPost("assets")]
    public async Task<ActionResult<MaintenanceAssetResponse>> CreateAsset(
        CreateMaintenanceAssetRequest request,
        CancellationToken cancellationToken)
    {
        var result = await maintenanceReliabilityService.CreateAssetAsync(request, cancellationToken);
        if (!result.IsSuccess)
        {
            return ToActionResult(result);
        }

        return CreatedAtAction(nameof(GetAsset), new { id = result.Value!.Id }, result.Value);
    }

    [HttpGet("sla-policies")]
    public async Task<ActionResult<PagedResult<MaintenanceSlaPolicyResponse>>> SearchSlaPolicies(
        [FromQuery] MaintenanceSlaPolicyQueryRequest query,
        CancellationToken cancellationToken)
    {
        return Ok(await maintenanceReliabilityService.SearchSlaPoliciesAsync(query, cancellationToken));
    }

    [HttpPost("sla-policies")]
    public async Task<ActionResult<MaintenanceSlaPolicyResponse>> CreateSlaPolicy(
        CreateMaintenanceSlaPolicyRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await maintenanceReliabilityService.CreateSlaPolicyAsync(request, cancellationToken));
    }

    [HttpPost("work-orders/{workOrderId:guid}/apply-sla")]
    public async Task<ActionResult<WorkOrderSlaSnapshotResponse>> ApplySlaToWorkOrder(
        Guid workOrderId,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await maintenanceReliabilityService.ApplySlaToWorkOrderAsync(workOrderId, cancellationToken));
    }

    [HttpGet("preventive-plans")]
    public async Task<ActionResult<PagedResult<PreventiveMaintenancePlanResponse>>> SearchPreventivePlans(
        [FromQuery] PreventiveMaintenancePlanQueryRequest query,
        CancellationToken cancellationToken)
    {
        return Ok(await maintenanceReliabilityService.SearchPreventivePlansAsync(query, cancellationToken));
    }

    [HttpPost("preventive-plans")]
    public async Task<ActionResult<PreventiveMaintenancePlanResponse>> CreatePreventivePlan(
        CreatePreventiveMaintenancePlanRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await maintenanceReliabilityService.CreatePreventivePlanAsync(request, cancellationToken));
    }

    [HttpPost("preventive-plans/{planId:guid}/generate-work-order")]
    public async Task<ActionResult<WorkOrderSlaSnapshotResponse>> GeneratePreventiveWorkOrder(
        Guid planId,
        GeneratePreventiveWorkOrderRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await maintenanceReliabilityService.GeneratePreventiveWorkOrderAsync(
            planId,
            currentUserService.UserId,
            request,
            cancellationToken));
    }

    [HttpGet("checklist-templates")]
    public async Task<ActionResult<PagedResult<OperationalChecklistTemplateResponse>>> SearchChecklistTemplates(
        [FromQuery] OperationalChecklistTemplateQueryRequest query,
        CancellationToken cancellationToken)
    {
        return Ok(await maintenanceReliabilityService.SearchChecklistTemplatesAsync(query, cancellationToken));
    }

    [HttpPost("checklist-templates")]
    public async Task<ActionResult<OperationalChecklistTemplateResponse>> CreateChecklistTemplate(
        CreateOperationalChecklistTemplateRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await maintenanceReliabilityService.CreateChecklistTemplateAsync(request, cancellationToken));
    }

    [HttpGet("checklist-runs/{id:guid}")]
    public async Task<ActionResult<OperationalChecklistRunResponse>> GetChecklistRun(
        Guid id,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await maintenanceReliabilityService.GetChecklistRunAsync(id, cancellationToken));
    }

    [HttpPost("checklist-runs")]
    public async Task<ActionResult<OperationalChecklistRunResponse>> StartChecklistRun(
        StartOperationalChecklistRunRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await maintenanceReliabilityService.StartChecklistRunAsync(
            currentUserService.UserId,
            request,
            cancellationToken));
    }

    [HttpPatch("checklist-runs/{id:guid}/complete")]
    public async Task<ActionResult<OperationalChecklistRunResponse>> CompleteChecklistRun(
        Guid id,
        CompleteOperationalChecklistRunRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await maintenanceReliabilityService.CompleteChecklistRunAsync(
            id,
            currentUserService.UserId,
            request,
            cancellationToken));
    }


    [HttpGet("pro-dashboard")]
    public async Task<ActionResult<MaintenanceReliabilityDashboardResponse>> ProDashboard(
        [FromQuery] MaintenanceReliabilityDashboardQuery query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await maintenanceReliabilityService.GetProDashboardAsync(query, cancellationToken));
    }

    [HttpGet("assets/{assetId:guid}/reliability-profile")]
    public async Task<ActionResult<MaintenanceAssetReliabilityProfileResponse>> AssetReliabilityProfile(
        Guid assetId,
        [FromQuery] MaintenanceAssetReliabilityQuery query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await maintenanceReliabilityService.GetAssetReliabilityProfileAsync(assetId, query, cancellationToken));
    }

    [HttpGet("preventive-maintenance/due-queue")]
    public async Task<ActionResult<PagedResult<PreventiveMaintenanceDueQueueItemResponse>>> PreventiveMaintenanceDueQueue(
        [FromQuery] PreventiveMaintenanceDueQueueQuery query,
        CancellationToken cancellationToken)
    {
        return Ok(await maintenanceReliabilityService.GetPreventiveMaintenanceDueQueueAsync(query, cancellationToken));
    }

    [HttpGet("sla/escalation-queue")]
    public async Task<ActionResult<PagedResult<MaintenanceSlaEscalationQueueItemResponse>>> SlaEscalationQueue(
        [FromQuery] MaintenanceSlaEscalationQueueQuery query,
        CancellationToken cancellationToken)
    {
        return Ok(await maintenanceReliabilityService.GetSlaEscalationQueueAsync(query, cancellationToken));
    }

    [HttpGet("vendors/performance")]
    public async Task<ActionResult<PagedResult<VendorPerformanceItemResponse>>> VendorPerformance(
        [FromQuery] VendorPerformanceQuery query,
        CancellationToken cancellationToken)
    {
        return Ok(await maintenanceReliabilityService.GetVendorPerformanceAsync(query, cancellationToken));
    }

}
