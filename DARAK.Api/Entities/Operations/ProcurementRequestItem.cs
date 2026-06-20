namespace DARAK.Api.Entities;

public sealed class ProcurementRequestItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProcurementRequestId { get; set; }

    public ProcurementRequest ProcurementRequest { get; set; } = null!;

    public Guid? StockItemId { get; set; }

    public StockItem? StockItem { get; set; }

    public string Description { get; set; } = string.Empty;

    public decimal Quantity { get; set; }

    public decimal? EstimatedUnitCost { get; set; }
}
