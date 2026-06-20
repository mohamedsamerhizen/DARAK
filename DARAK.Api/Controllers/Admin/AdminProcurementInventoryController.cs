using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Operations;
using DARAK.Api.Helpers;
using DARAK.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DARAK.Api.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.OperationsManagers)]
[Route("api/admin/procurement-inventory")]
public sealed class AdminProcurementInventoryController(
    ICurrentUserService currentUserService,
    IProcurementInventoryService procurementInventoryService)
    : ApiControllerBase
{
    [HttpGet("summary")]
    public async Task<ActionResult<ProcurementInventorySummaryResponse>> Summary(
        [FromQuery] Guid? compoundId,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await procurementInventoryService.GetSummaryAsync(compoundId, cancellationToken));
    }

    [HttpGet("stock-items")]
    public async Task<ActionResult<PagedResult<StockItemResponse>>> SearchStockItems(
        [FromQuery] StockItemQueryRequest query,
        CancellationToken cancellationToken)
    {
        return Ok(await procurementInventoryService.SearchStockItemsAsync(query, cancellationToken));
    }

    [HttpPost("stock-items")]
    public async Task<ActionResult<StockItemResponse>> CreateStockItem(
        CreateStockItemRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await procurementInventoryService.CreateStockItemAsync(request, cancellationToken));
    }

    [HttpPost("stock-items/{id:guid}/adjustments")]
    public async Task<ActionResult<InventoryMovementResponse>> RecordAdjustment(
        Guid id,
        InventoryAdjustmentRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await procurementInventoryService.RecordInventoryAdjustmentAsync(
            id,
            currentUserService.UserId,
            request,
            cancellationToken));
    }

    [HttpPost("stock-items/{id:guid}/issue-to-work-order")]
    public async Task<ActionResult<InventoryMovementResponse>> IssueToWorkOrder(
        Guid id,
        IssueStockToWorkOrderRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await procurementInventoryService.IssueStockToWorkOrderAsync(
            id,
            currentUserService.UserId,
            request,
            cancellationToken));
    }

    [HttpGet("procurement-requests")]
    public async Task<ActionResult<PagedResult<ProcurementRequestResponse>>> SearchProcurementRequests(
        [FromQuery] ProcurementRequestQueryRequest query,
        CancellationToken cancellationToken)
    {
        return Ok(await procurementInventoryService.SearchProcurementRequestsAsync(query, cancellationToken));
    }

    [HttpPost("procurement-requests")]
    public async Task<ActionResult<ProcurementRequestResponse>> CreateProcurementRequest(
        CreateProcurementRequestRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await procurementInventoryService.CreateProcurementRequestAsync(
            currentUserService.UserId,
            request,
            cancellationToken));
    }

    [HttpPatch("procurement-requests/{id:guid}/approve")]
    public async Task<ActionResult<ProcurementRequestResponse>> ApproveProcurementRequest(
        Guid id,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await procurementInventoryService.ApproveProcurementRequestAsync(
            id,
            currentUserService.UserId,
            cancellationToken));
    }

    [HttpGet("purchase-orders")]
    public async Task<ActionResult<PagedResult<PurchaseOrderResponse>>> SearchPurchaseOrders(
        [FromQuery] PurchaseOrderQueryRequest query,
        CancellationToken cancellationToken)
    {
        return Ok(await procurementInventoryService.SearchPurchaseOrdersAsync(query, cancellationToken));
    }

    [HttpPost("purchase-orders")]
    public async Task<ActionResult<PurchaseOrderResponse>> CreatePurchaseOrder(
        CreatePurchaseOrderRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await procurementInventoryService.CreatePurchaseOrderAsync(
            currentUserService.UserId,
            request,
            cancellationToken));
    }

    [HttpPatch("purchase-orders/{purchaseOrderId:guid}/items/{itemId:guid}/receive")]
    public async Task<ActionResult<PurchaseOrderResponse>> ReceivePurchaseOrderItem(
        Guid purchaseOrderId,
        Guid itemId,
        ReceivePurchaseOrderItemRequest request,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await procurementInventoryService.ReceivePurchaseOrderItemAsync(
            purchaseOrderId,
            itemId,
            currentUserService.UserId,
            request,
            cancellationToken));
    }


    [HttpGet("spare-parts/availability")]
    public async Task<ActionResult<PagedResult<SparePartAvailabilityItemResponse>>> SparePartAvailability(
        [FromQuery] SparePartAvailabilityQuery query,
        CancellationToken cancellationToken)
    {
        return Ok(await procurementInventoryService.SearchSparePartAvailabilityAsync(query, cancellationToken));
    }

    [HttpGet("work-orders/{workOrderId:guid}/spare-parts-consumption")]
    public async Task<ActionResult<WorkOrderSparePartConsumptionSummaryResponse>> WorkOrderSparePartConsumption(
        Guid workOrderId,
        CancellationToken cancellationToken)
    {
        return ToActionResult(await procurementInventoryService.GetWorkOrderSparePartConsumptionAsync(workOrderId, cancellationToken));
    }

}
