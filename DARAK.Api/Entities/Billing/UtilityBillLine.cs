namespace DARAK.Api.Entities;

public sealed class UtilityBillLine
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UtilityBillId { get; set; }

    public Guid CompoundServiceId { get; set; }

    public string Description { get; set; } = string.Empty;

    public decimal Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal LineTotal { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public UtilityBill UtilityBill { get; set; } = null!;

    public CompoundService CompoundService { get; set; } = null!;
}
