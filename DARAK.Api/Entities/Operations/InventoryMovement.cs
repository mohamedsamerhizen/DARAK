using DARAK.Api.Enums;
using DARAK.Api.Identity;

namespace DARAK.Api.Entities;

public sealed class InventoryMovement
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CompoundId { get; set; }

    public Compound Compound { get; set; } = null!;

    public Guid StockItemId { get; set; }

    public StockItem StockItem { get; set; } = null!;

    public InventoryMovementType MovementType { get; set; }

    public decimal Quantity { get; set; }

    public decimal? UnitCost { get; set; }

    public Guid? WorkOrderId { get; set; }

    public WorkOrder? WorkOrder { get; set; }

    public Guid? PurchaseOrderItemId { get; set; }

    public PurchaseOrderItem? PurchaseOrderItem { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public ApplicationUser? CreatedByUser { get; set; }

    public string? Reference { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
