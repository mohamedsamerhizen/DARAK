using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Operations;
using DARAK.Api.Enums;
using DARAK.Api.Helpers;
using DARAK.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DARAK.Api.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.OperationsManagers)]
[Route("api/admin/work-orders")]
public sealed class AdminWorkOrdersController(
    ICurrentUserService currentUserService,
    IOperationsService operationsService,
    IStaffMemberService staffMemberService,
    IServiceVendorService serviceVendorService)
    : ApiControllerBase
{
    [Authorize(Roles = RoleNames.OperationsManagers)]
    [HttpGet]
    public async Task<ActionResult<PagedResult<WorkOrderResponse>>> SearchWorkOrders(
        [FromQuery] WorkOrderQueryRequest query,
        CancellationToken cancellationToken)
    {
        return Ok(await operationsService.SearchWorkOrdersAsync(query, cancellationToken));
    }

    [Authorize(Roles = RoleNames.OperationsManagers)]
    [HttpGet("overdue")]
    public async Task<ActionResult<PagedResult<WorkOrderResponse>>> OverdueWorkOrders(
        [FromQuery] WorkOrderQueryRequest query,
        CancellationToken cancellationToken)
    {
        return Ok(await operationsService.SearchOverdueWorkOrdersAsync(query, cancellationToken));
    }

    [Authorize(Roles = RoleNames.OperationsManagers)]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<WorkOrderResponse>> GetWorkOrder(
        Guid id,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await operationsService.GetWorkOrderAsync(id, cancellationToken));
    }

    [Authorize(Roles = RoleNames.OperationsManagers)]
    [HttpPost]
    public async Task<ActionResult<WorkOrderResponse>> CreateWorkOrder(
        CreateWorkOrderRequest request,
        CancellationToken cancellationToken)
    {
        var result = await operationsService.CreateWorkOrderAsync(
            currentUserService.UserId,
            request,
            cancellationToken);
        if (!result.IsSuccess)
        {
            return ToActionResult(result);
        }

        return CreatedAtAction(nameof(GetWorkOrder), new { id = result.Value!.Id }, result.Value);
    }

    [Authorize(Roles = RoleNames.OperationsManagers)]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<WorkOrderResponse>> UpdateWorkOrder(
        Guid id,
        UpdateWorkOrderRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await operationsService.UpdateWorkOrderAsync(id, request, cancellationToken));
    }

    [Authorize(Roles = RoleNames.OperationsManagers)]
    [HttpGet("staff/{staffMemberId:guid}/work-orders")]
    public async Task<ActionResult<PagedResult<WorkOrderResponse>>> StaffWorkOrders(
        Guid staffMemberId,
        [FromQuery] WorkOrderQueryRequest query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await operationsService.SearchWorkOrdersForStaffMemberAsync(
            staffMemberId,
            query,
            cancellationToken));
    }

    [Authorize(Roles = RoleNames.OperationsManagers)]
    [HttpGet("vendors/{vendorId:guid}/work-orders")]
    public async Task<ActionResult<PagedResult<WorkOrderResponse>>> VendorWorkOrders(
        Guid vendorId,
        [FromQuery] WorkOrderQueryRequest query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await operationsService.SearchWorkOrdersForVendorAsync(
            vendorId,
            query,
            cancellationToken));
    }

    [Authorize(Roles = RoleNames.OperationsManagers)]
    [HttpPatch("{id:guid}/assign-staff")]
    public async Task<ActionResult<WorkOrderResponse>> AssignStaff(
        Guid id,
        AssignWorkOrderToStaffRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await operationsService.AssignWorkOrderToStaffAsync(
            id,
            currentUserService.UserId,
            request,
            cancellationToken));
    }

    [Authorize(Roles = RoleNames.OperationsManagers)]
    [HttpPatch("{id:guid}/assign-vendor")]
    public async Task<ActionResult<WorkOrderResponse>> AssignVendor(
        Guid id,
        AssignWorkOrderToVendorRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await operationsService.AssignWorkOrderToVendorAsync(
            id,
            currentUserService.UserId,
            request,
            cancellationToken));
    }

    [Authorize(Roles = RoleNames.OperationsManagers)]
    [HttpPatch("{id:guid}/schedule")]
    public async Task<ActionResult<WorkOrderResponse>> ScheduleWorkOrder(
        Guid id,
        ScheduleWorkOrderRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await operationsService.ScheduleWorkOrderAsync(
            id,
            currentUserService.UserId,
            request,
            cancellationToken));
    }

    [Authorize(Roles = RoleNames.OperationsManagers)]
    [HttpPatch("{id:guid}/start")]
    public async Task<ActionResult<WorkOrderResponse>> StartWorkOrder(
        Guid id,
        StartWorkOrderRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await operationsService.StartWorkOrderAsync(
            id,
            currentUserService.UserId,
            request,
            cancellationToken));
    }

    [Authorize(Roles = RoleNames.OperationsManagers)]
    [HttpPatch("{id:guid}/complete")]
    public async Task<ActionResult<WorkOrderResponse>> CompleteWorkOrder(
        Guid id,
        CompleteWorkOrderRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await operationsService.CompleteWorkOrderAsync(
            id,
            currentUserService.UserId,
            request,
            cancellationToken));
    }

    [Authorize(Roles = RoleNames.OperationsManagers)]
    [HttpPatch("{id:guid}/cancel")]
    public async Task<ActionResult<WorkOrderResponse>> CancelWorkOrder(
        Guid id,
        CancelWorkOrderRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await operationsService.CancelWorkOrderAsync(
            id,
            currentUserService.UserId,
            request,
            cancellationToken));
    }

    [Authorize(Roles = RoleNames.OperationsManagers)]
    [HttpPost("{id:guid}/cost-items")]
    public async Task<ActionResult<WorkOrderCostItemResponse>> AddCostItem(
        Guid id,
        AddWorkOrderCostItemRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await operationsService.AddCostItemAsync(id, request, cancellationToken));
    }

    [Authorize(Roles = RoleNames.OperationsManagers)]
    [HttpGet("{id:guid}/history")]
    public async Task<ActionResult<PagedResult<WorkOrderStatusHistoryResponse>>> WorkOrderHistory(
        Guid id,
        [FromQuery] WorkOrderStatusHistoryQueryRequest query,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await operationsService.GetStatusHistoryAsync(id, query, cancellationToken));
    }

    [Authorize(Roles = RoleNames.OperationsManagers)]
    [HttpPost("{id:guid}/ratings")]
    public async Task<ActionResult<WorkOrderRatingResponse>> RateWorkOrder(
        Guid id,
        CreateWorkOrderRatingRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await operationsService.RateWorkOrderAsync(
            id,
            currentUserService.UserId,
            IsManager(),
            request,
            cancellationToken));
    }

    [Authorize(Roles = RoleNames.OperationsManagers)]
    [HttpGet("staff")]
    public async Task<ActionResult<PagedResult<StaffMemberResponse>>> SearchStaff(
        [FromQuery] StaffMemberQueryRequest query,
        CancellationToken cancellationToken)
    {
        return Ok(await staffMemberService.SearchStaffMembersAsync(query, cancellationToken));
    }

    [Authorize(Roles = RoleNames.OperationsManagers)]
    [HttpGet("staff/{id:guid}")]
    public async Task<ActionResult<StaffMemberResponse>> GetStaff(
        Guid id,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await staffMemberService.GetStaffMemberAsync(id, cancellationToken));
    }

    [Authorize(Roles = RoleNames.OperationsManagers)]
    [HttpPost("staff")]
    public async Task<ActionResult<StaffMemberResponse>> CreateStaff(
        CreateStaffMemberRequest request,
        CancellationToken cancellationToken)
    {
        var result = await staffMemberService.CreateStaffMemberAsync(request, cancellationToken);
        if (!result.IsSuccess)
        {
            return ToActionResult(result);
        }

        return CreatedAtAction(nameof(GetStaff), new { id = result.Value!.Id }, result.Value);
    }

    [Authorize(Roles = RoleNames.OperationsManagers)]
    [HttpPut("staff/{id:guid}")]
    public async Task<ActionResult<StaffMemberResponse>> UpdateStaff(
        Guid id,
        UpdateStaffMemberRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await staffMemberService.UpdateStaffMemberAsync(id, request, cancellationToken));
    }

    [Authorize(Roles = RoleNames.OperationsManagers)]
    [HttpPatch("staff/{id:guid}/activate")]
    public async Task<ActionResult<StaffMemberResponse>> ActivateStaff(
        Guid id,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await staffMemberService.SetStaffMemberStatusAsync(
            id,
            StaffStatus.Active,
            cancellationToken));
    }

    [Authorize(Roles = RoleNames.OperationsManagers)]
    [HttpPatch("staff/{id:guid}/deactivate")]
    public async Task<ActionResult<StaffMemberResponse>> DeactivateStaff(
        Guid id,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await staffMemberService.SetStaffMemberStatusAsync(
            id,
            StaffStatus.Inactive,
            cancellationToken));
    }

    [Authorize(Roles = RoleNames.OperationsManagers)]
    [HttpPatch("staff/{id:guid}/suspend")]
    public async Task<ActionResult<StaffMemberResponse>> SuspendStaff(
        Guid id,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await staffMemberService.SetStaffMemberStatusAsync(
            id,
            StaffStatus.Suspended,
            cancellationToken));
    }

    [Authorize(Roles = RoleNames.OperationsManagers)]
    [HttpGet("vendors")]
    public async Task<ActionResult<PagedResult<ServiceVendorResponse>>> SearchVendors(
        [FromQuery] ServiceVendorQueryRequest query,
        CancellationToken cancellationToken)
    {
        return Ok(await serviceVendorService.SearchServiceVendorsAsync(query, cancellationToken));
    }

    [Authorize(Roles = RoleNames.OperationsManagers)]
    [HttpGet("vendors/{id:guid}")]
    public async Task<ActionResult<ServiceVendorResponse>> GetVendor(
        Guid id,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await serviceVendorService.GetServiceVendorAsync(id, cancellationToken));
    }

    [Authorize(Roles = RoleNames.OperationsManagers)]
    [HttpPost("vendors")]
    public async Task<ActionResult<ServiceVendorResponse>> CreateVendor(
        CreateServiceVendorRequest request,
        CancellationToken cancellationToken)
    {
        var result = await serviceVendorService.CreateServiceVendorAsync(request, cancellationToken);
        if (!result.IsSuccess)
        {
            return ToActionResult(result);
        }

        return CreatedAtAction(nameof(GetVendor), new { id = result.Value!.Id }, result.Value);
    }

    [Authorize(Roles = RoleNames.OperationsManagers)]
    [HttpPut("vendors/{id:guid}")]
    public async Task<ActionResult<ServiceVendorResponse>> UpdateVendor(
        Guid id,
        UpdateServiceVendorRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await serviceVendorService.UpdateServiceVendorAsync(id, request, cancellationToken));
    }

    [Authorize(Roles = RoleNames.OperationsManagers)]
    [HttpPatch("vendors/{id:guid}/activate")]
    public async Task<ActionResult<ServiceVendorResponse>> ActivateVendor(
        Guid id,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await serviceVendorService.SetServiceVendorStatusAsync(
            id,
            VendorStatus.Active,
            cancellationToken));
    }

    [Authorize(Roles = RoleNames.OperationsManagers)]
    [HttpPatch("vendors/{id:guid}/deactivate")]
    public async Task<ActionResult<ServiceVendorResponse>> DeactivateVendor(
        Guid id,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await serviceVendorService.SetServiceVendorStatusAsync(
            id,
            VendorStatus.Inactive,
            cancellationToken));
    }

    [Authorize(Roles = RoleNames.OperationsManagers)]
    [HttpPatch("vendors/{id:guid}/suspend")]
    public async Task<ActionResult<ServiceVendorResponse>> SuspendVendor(
        Guid id,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await serviceVendorService.SetServiceVendorStatusAsync(
            id,
            VendorStatus.Suspended,
            cancellationToken));
    }

    private bool IsManager()
    {
        return User.IsInRole(nameof(UserRole.SuperAdmin))
            || User.IsInRole(nameof(UserRole.CompoundAdmin));
    }
}
