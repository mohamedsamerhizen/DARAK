using DARAK.Api.Enums;
using DARAK.Api.Identity;

namespace DARAK.Api.Entities;

public sealed class PenaltyRule
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CompoundId { get; set; }

    public string Name { get; set; } = string.Empty;

    public PenaltyRuleTargetType TargetType { get; set; }

    public PenaltyCalculationType CalculationType { get; set; }

    public PenaltyRuleStatus Status { get; set; } = PenaltyRuleStatus.Active;

    public int GracePeriodDays { get; set; }

    public decimal Amount { get; set; }

    public decimal? PercentageRate { get; set; }

    public decimal? MaxAmount { get; set; }

    public bool PauseWhenDisputed { get; set; } = true;

    public DateOnly? EffectiveFrom { get; set; }

    public DateOnly? EffectiveUntil { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }

    public Compound Compound { get; set; } = null!;

    public ApplicationUser? CreatedByUser { get; set; }
}
