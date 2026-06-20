namespace DARAK.Api.Entities;

public sealed class PurchaseOrderItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PurchaseOrderId { get; set; }

    public PurchaseOrder PurchaseOrder { get; set; } = null!;

    public Guid? StockItemId { get; set; }

    public StockItem? StockItem { get; set; }

    public string Description { get; set; } = string.Empty;

    public decimal QuantityOrdered { get; set; }

    public decimal QuantityReceived { get; set; }

    public decimal UnitCost { get; set; }

    public string? Notes { get; set; }

    public ICollection<InventoryMovement> InventoryMovements { get; set; } = new List<InventoryMovement>();
}
