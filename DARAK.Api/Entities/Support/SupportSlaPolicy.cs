using DARAK.Api.Enums;

namespace DARAK.Api.Entities;

public sealed class SupportSlaPolicy
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CompoundId { get; set; }

    public SupportCaseCategory Category { get; set; } = SupportCaseCategory.General;

    public SupportCasePriority Priority { get; set; } = SupportCasePriority.Normal;

    public int ResponseHours { get; set; } = 24;

    public int ResolutionHours { get; set; } = 72;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }

    public Compound Compound { get; set; } = null!;
}
