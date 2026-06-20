namespace DARAK.Api.Entities;

public sealed class MeterReading
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CompoundId { get; set; }

    public Guid MeterId { get; set; }

    public Guid PropertyUnitId { get; set; }

    public int Year { get; set; }

    public int Month { get; set; }

    public decimal PreviousReading { get; set; }

    public decimal CurrentReading { get; set; }

    public decimal Consumption { get; set; }

    public decimal RatePerUnit { get; set; }

    public decimal Amount { get; set; }

    public bool IsBilled { get; set; }

    public Guid? UtilityBillId { get; set; }

    public Guid? UtilityBillLineId { get; set; }

    public DateTime ReadingDate { get; set; } = DateTime.UtcNow;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public DateTime? BilledAt { get; set; }

    public string? Notes { get; set; }

    public Compound Compound { get; set; } = null!;

    public Meter Meter { get; set; } = null!;

    public PropertyUnit PropertyUnit { get; set; } = null!;

    public UtilityBill? UtilityBill { get; set; }

    public UtilityBillLine? UtilityBillLine { get; set; }
}
