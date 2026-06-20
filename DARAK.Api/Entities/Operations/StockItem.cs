using DARAK.Api.Enums;

namespace DARAK.Api.Entities;

public sealed class StockItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CompoundId { get; set; }

    public Compound Compound { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    public string Sku { get; set; } = string.Empty;

    public string? Category { get; set; }

    public string UnitOfMeasure { get; set; } = "pcs";

    public decimal CurrentQuantity { get; set; }

    public decimal MinimumQuantity { get; set; }

    public decimal? AverageUnitCost { get; set; }

    public StockItemStatus Status { get; set; } = StockItemStatus.Active;

    public string? Notes { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }

    public ICollection<InventoryMovement> InventoryMovements { get; set; } = new List<InventoryMovement>();
}
