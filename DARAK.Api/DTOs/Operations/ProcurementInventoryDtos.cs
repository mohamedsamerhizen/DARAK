using System.ComponentModel.DataAnnotations;
using DARAK.Api.DTOs.Common;
using DARAK.Api.Enums;

namespace DARAK.Api.DTOs.Operations;

public sealed class StockItemQueryRequest : PaginationQuery
{
    public Guid? CompoundId { get; init; }
    public StockItemStatus? Status { get; init; }
    public bool? LowStockOnly { get; init; }
    [MaxLength(200)]
    public string? SearchTerm { get; init; }
}

public sealed class CreateStockItemRequest
{
    public Guid CompoundId { get; init; }

    [Required]
    [MaxLength(150)]
    public string Name { get; init; } = string.Empty;

    [Required]
    [MaxLength(80)]
    public string Sku { get; init; } = string.Empty;

    [MaxLength(100)]
    public string? Category { get; init; }

    [Required]
    [MaxLength(30)]
    public string UnitOfMeasure { get; init; } = "pcs";

    [Range(0, double.MaxValue)]
    public decimal CurrentQuantity { get; init; }

    [Range(0, double.MaxValue)]
    public decimal MinimumQuantity { get; init; }

    [Range(0, double.MaxValue)]
    public decimal? AverageUnitCost { get; init; }

    public StockItemStatus Status { get; init; } = StockItemStatus.Active;

    [MaxLength(1000)]
    public string? Notes { get; init; }
}

public sealed class InventoryAdjustmentRequest
{
    public InventoryMovementType MovementType { get; init; } = InventoryMovementType.AdjustmentIncrease;

    [Range(0.0001, double.MaxValue)]
    public decimal Quantity { get; init; }

    [Range(0, double.MaxValue)]
    public decimal? UnitCost { get; init; }

    [MaxLength(120)]
    public string? Reference { get; init; }

    [MaxLength(1000)]
    public string? Notes { get; init; }
}

public sealed class IssueStockToWorkOrderRequest
{
    public Guid WorkOrderId { get; init; }

    [Range(0.0001, double.MaxValue)]
    public decimal Quantity { get; init; }

    [Range(0, double.MaxValue)]
    public decimal? UnitCost { get; init; }

    [MaxLength(120)]
    public string? Reference { get; init; }

    [MaxLength(1000)]
    public string? Notes { get; init; }
}

public sealed record StockItemResponse(
    Guid Id,
    Guid CompoundId,
    string Name,
    string Sku,
    string? Category,
    string UnitOfMeasure,
    decimal CurrentQuantity,
    decimal MinimumQuantity,
    decimal? AverageUnitCost,
    StockItemStatus Status,
    bool IsLowStock,
    string? Notes,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public sealed record InventoryMovementResponse(
    Guid Id,
    Guid CompoundId,
    Guid StockItemId,
    string StockItemName,
    InventoryMovementType MovementType,
    decimal Quantity,
    decimal? UnitCost,
    Guid? WorkOrderId,
    Guid? PurchaseOrderItemId,
    Guid? CreatedByUserId,
    string? Reference,
    string? Notes,
    DateTime CreatedAtUtc);

public sealed class ProcurementRequestQueryRequest : PaginationQuery
{
    public Guid? CompoundId { get; init; }
    public ProcurementRequestStatus? Status { get; init; }
    public WorkOrderPriority? Priority { get; init; }
    public Guid? RelatedWorkOrderId { get; init; }
    [MaxLength(200)]
    public string? SearchTerm { get; init; }
}

public sealed class CreateProcurementRequestRequest
{
    public Guid CompoundId { get; init; }

    [Required]
    [MaxLength(150)]
    public string Title { get; init; } = string.Empty;

    [Required]
    [MaxLength(2000)]
    public string Reason { get; init; } = string.Empty;

    public WorkOrderPriority Priority { get; init; } = WorkOrderPriority.Normal;

    public ProcurementRequestStatus Status { get; init; } = ProcurementRequestStatus.PendingApproval;

    public Guid? RelatedWorkOrderId { get; init; }

    [MaxLength(1000)]
    public string? Notes { get; init; }

    [MinLength(1)]
    public IReadOnlyCollection<CreateProcurementRequestItemRequest> Items { get; init; } = [];
}

public sealed class CreateProcurementRequestItemRequest
{
    public Guid? StockItemId { get; init; }

    [Required]
    [MaxLength(300)]
    public string Description { get; init; } = string.Empty;

    [Range(0.0001, double.MaxValue)]
    public decimal Quantity { get; init; }

    [Range(0, double.MaxValue)]
    public decimal? EstimatedUnitCost { get; init; }
}

public sealed record ProcurementRequestItemResponse(
    Guid Id,
    Guid? StockItemId,
    string? StockItemName,
    string Description,
    decimal Quantity,
    decimal? EstimatedUnitCost,
    decimal? EstimatedTotal);

public sealed record ProcurementRequestResponse(
    Guid Id,
    Guid CompoundId,
    Guid? RequestedByUserId,
    string Title,
    string Reason,
    WorkOrderPriority Priority,
    ProcurementRequestStatus Status,
    Guid? RelatedWorkOrderId,
    string? Notes,
    DateTime CreatedAtUtc,
    DateTime? ApprovedAtUtc,
    Guid? ApprovedByUserId,
    DateTime? UpdatedAtUtc,
    decimal EstimatedTotal,
    IReadOnlyCollection<ProcurementRequestItemResponse> Items);

public sealed class PurchaseOrderQueryRequest : PaginationQuery
{
    public Guid? CompoundId { get; init; }
    public Guid? VendorId { get; init; }
    public Guid? ProcurementRequestId { get; init; }
    public PurchaseOrderStatus? Status { get; init; }
    [MaxLength(200)]
    public string? SearchTerm { get; init; }
}

public sealed class CreatePurchaseOrderRequest
{
    public Guid CompoundId { get; init; }

    public Guid? ProcurementRequestId { get; init; }

    public Guid VendorId { get; init; }

    [Required]
    [MaxLength(80)]
    public string OrderNumber { get; init; } = string.Empty;

    public PurchaseOrderStatus Status { get; init; } = PurchaseOrderStatus.Ordered;

    public DateTime? OrderedAtUtc { get; init; }

    public DateTime? ExpectedDeliveryAtUtc { get; init; }

    [MaxLength(1000)]
    public string? Notes { get; init; }

    [MinLength(1)]
    public IReadOnlyCollection<CreatePurchaseOrderItemRequest> Items { get; init; } = [];
}

public sealed class CreatePurchaseOrderItemRequest
{
    public Guid? StockItemId { get; init; }

    [Required]
    [MaxLength(300)]
    public string Description { get; init; } = string.Empty;

    [Range(0.0001, double.MaxValue)]
    public decimal QuantityOrdered { get; init; }

    [Range(0, double.MaxValue)]
    public decimal UnitCost { get; init; }

    [MaxLength(1000)]
    public string? Notes { get; init; }
}

public sealed class ReceivePurchaseOrderItemRequest
{
    [Range(0.0001, double.MaxValue)]
    public decimal QuantityReceived { get; init; }

    [MaxLength(120)]
    public string? ReceiptReference { get; init; }

    [MaxLength(1000)]
    public string? Notes { get; init; }
}

public sealed class CancelPurchaseOrderRequest
{
    [Required]
    [MaxLength(1000)]
    public string Reason { get; init; } = string.Empty;
}

public sealed record PurchaseOrderItemResponse(
    Guid Id,
    Guid? StockItemId,
    string? StockItemName,
    string Description,
    decimal QuantityOrdered,
    decimal QuantityReceived,
    decimal RemainingQuantity,
    decimal UnitCost,
    decimal LineTotal,
    string? Notes);

public sealed record PurchaseOrderResponse(
    Guid Id,
    Guid CompoundId,
    Guid? ProcurementRequestId,
    Guid VendorId,
    string VendorName,
    string OrderNumber,
    PurchaseOrderStatus Status,
    DateTime? OrderedAtUtc,
    DateTime? ExpectedDeliveryAtUtc,
    DateTime? ReceivedAtUtc,
    Guid? CreatedByUserId,
    string? Notes,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    decimal TotalAmount,
    IReadOnlyCollection<PurchaseOrderItemResponse> Items);

public sealed record ProcurementInventorySummaryResponse(
    Guid? CompoundId,
    int ActiveStockItemCount,
    int LowStockItemCount,
    int PendingProcurementRequestCount,
    int OpenPurchaseOrderCount,
    decimal InventoryEstimatedValue,
    decimal OpenPurchaseOrderValue);


public sealed class SparePartAvailabilityQuery : PaginationQuery
{
    public Guid? CompoundId { get; init; }
    public Guid? WorkOrderId { get; init; }
    public bool LowStockOnly { get; init; }
    [MaxLength(200)]
    public string? SearchTerm { get; init; }
}

public sealed record SparePartAvailabilityItemResponse(
    Guid StockItemId,
    Guid CompoundId,
    string Name,
    string Sku,
    string? Category,
    string UnitOfMeasure,
    decimal CurrentQuantity,
    decimal MinimumQuantity,
    decimal AvailableQuantity,
    decimal? AverageUnitCost,
    bool IsLowStock,
    bool CanIssueToWorkOrder,
    string AvailabilityStatus,
    string RecommendedAction);

public sealed record SparePartConsumptionLineResponse(
    Guid InventoryMovementId,
    Guid StockItemId,
    string StockItemName,
    string Sku,
    decimal Quantity,
    decimal? UnitCost,
    decimal? LineCost,
    Guid? CreatedByUserId,
    string? Notes,
    DateTime CreatedAtUtc);

public sealed record WorkOrderSparePartConsumptionSummaryResponse(
    Guid WorkOrderId,
    Guid CompoundId,
    string WorkOrderTitle,
    int ConsumptionLineCount,
    decimal TotalQuantityIssued,
    decimal TotalMaterialCost,
    IReadOnlyCollection<SparePartConsumptionLineResponse> Lines);
