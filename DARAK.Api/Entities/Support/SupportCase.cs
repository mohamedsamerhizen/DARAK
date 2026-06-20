using DARAK.Api.Enums;
using DARAK.Api.Identity;

namespace DARAK.Api.Entities;

public sealed class SupportCase
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CompoundId { get; set; }

    public Guid? ResidentProfileId { get; set; }

    public Guid? PropertyUnitId { get; set; }

    public Guid? AssignedToUserId { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public Guid? SourceEntityId { get; set; }

    public SupportCaseSourceType SourceType { get; set; } = SupportCaseSourceType.Manual;

    public SupportCaseCategory Category { get; set; } = SupportCaseCategory.General;

    public SupportCasePriority Priority { get; set; } = SupportCasePriority.Normal;

    public SupportCaseStatus Status { get; set; } = SupportCaseStatus.Open;

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string? AssignmentNote { get; set; }

    public string? EscalationReason { get; set; }

    public string? ResolutionSummary { get; set; }

    public int ReopenCount { get; set; }

    public DateTime DueAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }

    public DateTime? AssignedAtUtc { get; set; }

    public DateTime? EscalatedAtUtc { get; set; }

    public DateTime? ResolvedAtUtc { get; set; }

    public DateTime? ClosedAtUtc { get; set; }

    public byte[] RowVersion { get; set; } = [];

    public Compound Compound { get; set; } = null!;

    public ResidentProfile? ResidentProfile { get; set; }

    public PropertyUnit? PropertyUnit { get; set; }

    public ApplicationUser? AssignedToUser { get; set; }

    public ApplicationUser? CreatedByUser { get; set; }

    public ICollection<SupportCaseEvent> Events { get; set; } = new List<SupportCaseEvent>();
}
