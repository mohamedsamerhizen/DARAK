namespace DARAK.Api.Entities;

public sealed class BillingCycle
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CompoundId { get; set; }

    public int Year { get; set; }

    public int Month { get; set; }

    public DateOnly PeriodStart { get; set; }

    public DateOnly PeriodEnd { get; set; }

    public DateOnly DueDate { get; set; }

    public bool IsClosed { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public Compound Compound { get; set; } = null!;

    public ICollection<UtilityBill> UtilityBills { get; set; } = new List<UtilityBill>();
}
