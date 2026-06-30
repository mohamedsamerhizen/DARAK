using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Operations;

namespace DARAK.Api.Interfaces;

public interface IProcurementInventoryService
{
    Task<ServiceResult<StockItemResponse>> CreateStockItemAsync(
        CreateStockItemRequest request,
        CancellationToken cancellationToken = default);

    Task<PagedResult<StockItemResponse>> SearchStockItemsAsync(
        StockItemQueryRequest query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<InventoryMovementResponse>> RecordInventoryAdjustmentAsync(
        Guid stockItemId,
        Guid? currentUserId,
        InventoryAdjustmentRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<InventoryMovementResponse>> IssueStockToWorkOrderAsync(
        Guid stockItemId,
        Guid? currentUserId,
        IssueStockToWorkOrderRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProcurementRequestResponse>> CreateProcurementRequestAsync(
        Guid? currentUserId,
        CreateProcurementRequestRequest request,
        CancellationToken cancellationToken = default);

    Task<PagedResult<ProcurementRequestResponse>> SearchProcurementRequestsAsync(
        ProcurementRequestQueryRequest query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProcurementRequestResponse>> ApproveProcurementRequestAsync(
        Guid requestId,
        Guid? currentUserId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PurchaseOrderResponse>> CreatePurchaseOrderAsync(
        Guid? currentUserId,
        CreatePurchaseOrderRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PurchaseOrderResponse>> ApprovePurchaseOrderAsync(
        Guid purchaseOrderId,
        Guid? currentUserId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PurchaseOrderResponse>> CancelPurchaseOrderAsync(
        Guid purchaseOrderId,
        Guid? currentUserId,
        CancelPurchaseOrderRequest request,
        CancellationToken cancellationToken = default);

    Task<PagedResult<PurchaseOrderResponse>> SearchPurchaseOrdersAsync(
        PurchaseOrderQueryRequest query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PurchaseOrderResponse>> ReceivePurchaseOrderItemAsync(
        Guid purchaseOrderId,
        Guid itemId,
        Guid? currentUserId,
        ReceivePurchaseOrderItemRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProcurementInventorySummaryResponse>> GetSummaryAsync(
        Guid? compoundId,
        CancellationToken cancellationToken = default);

    Task<PagedResult<SparePartAvailabilityItemResponse>> SearchSparePartAvailabilityAsync(
        SparePartAvailabilityQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<WorkOrderSparePartConsumptionSummaryResponse>> GetWorkOrderSparePartConsumptionAsync(
        Guid workOrderId,
        CancellationToken cancellationToken = default);

}
