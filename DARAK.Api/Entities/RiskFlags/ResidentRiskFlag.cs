using DARAK.Api.Enums;
using DARAK.Api.Identity;

namespace DARAK.Api.Entities;

public sealed class ResidentRiskFlag
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CompoundId { get; set; }

    public Guid ResidentProfileId { get; set; }

    public Guid? PropertyUnitId { get; set; }

    public Guid CreatedByUserId { get; set; }

    public Guid? AssignedToUserId { get; set; }

    public Guid? LastReviewedByUserId { get; set; }

    public Guid? ClosedByUserId { get; set; }

    public ResidentRiskFlagType FlagType { get; set; }

    public ResidentRiskFlagSeverity Severity { get; set; } = ResidentRiskFlagSeverity.Medium;

    public ResidentRiskFlagStatus Status { get; set; } = ResidentRiskFlagStatus.Active;

    public ResidentRiskFlagSource Source { get; set; } = ResidentRiskFlagSource.Manual;

    public ResidentRiskFlagSourceEntityType SourceEntityType { get; set; } = ResidentRiskFlagSourceEntityType.None;

    public Guid? SourceEntityId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string? RecommendedAction { get; set; }

    public string? InternalNotes { get; set; }

    public string? ResolutionNotes { get; set; }

    public string? DismissalReason { get; set; }

    public string? MetadataJson { get; set; }

    public bool RequiresSupervisorReview { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }

    public DateTime? AssignedAtUtc { get; set; }

    public DateTime? LastReviewedAtUtc { get; set; }

    public DateTime? NextReviewAtUtc { get; set; }

    public DateTime? ExpiresAtUtc { get; set; }

    public DateTime? ResolvedAtUtc { get; set; }

    public DateTime? DismissedAtUtc { get; set; }

    public Compound Compound { get; set; } = null!;

    public ResidentProfile ResidentProfile { get; set; } = null!;

    public PropertyUnit? PropertyUnit { get; set; }

    public ApplicationUser CreatedByUser { get; set; } = null!;

    public ApplicationUser? AssignedToUser { get; set; }

    public ApplicationUser? LastReviewedByUser { get; set; }

    public ApplicationUser? ClosedByUser { get; set; }

    public ICollection<ResidentRiskFlagAction> Actions { get; set; } = new List<ResidentRiskFlagAction>();
}
