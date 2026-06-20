using DARAK.Api.Enums;

namespace DARAK.Api.Entities;

public sealed class BillingRule
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CompoundId { get; set; }

    public Guid? CompoundServiceId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public BillingRuleStatus Status { get; set; } = BillingRuleStatus.Draft;

    public BillingChargeMode ChargeMode { get; set; } = BillingChargeMode.Fixed;

    public decimal FixedChargeAmount { get; set; }

    public decimal RatePerUnit { get; set; }

    public decimal MinimumChargeAmount { get; set; }

    public decimal LateFeeFlatAmount { get; set; }

    public decimal LateFeePercentage { get; set; }

    public int GracePeriodDays { get; set; }

    public DateOnly EffectiveFrom { get; set; }

    public DateOnly? EffectiveTo { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public Compound Compound { get; set; } = null!;

    public CompoundService? CompoundService { get; set; }

    public ICollection<BillingRuleTier> Tiers { get; set; } = new List<BillingRuleTier>();
}
