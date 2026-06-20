namespace DARAK.Api.Entities;

public sealed class BillingRuleTier
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid BillingRuleId { get; set; }

    public decimal FromQuantity { get; set; }

    public decimal? ToQuantity { get; set; }

    public decimal RatePerUnit { get; set; }

    public decimal FixedAmount { get; set; }

    public int SortOrder { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public BillingRule BillingRule { get; set; } = null!;
}
