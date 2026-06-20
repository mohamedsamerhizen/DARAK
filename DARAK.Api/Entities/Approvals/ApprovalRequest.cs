using DARAK.Api.Enums;
using DARAK.Api.Identity;

namespace DARAK.Api.Entities;

public sealed class ApprovalRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CompoundId { get; set; }

    public Guid RequestedByUserId { get; set; }

    public Guid? LastDecisionByUserId { get; set; }

    public ApprovalActionType ActionType { get; set; }

    public ApprovalEntityType EntityType { get; set; } = ApprovalEntityType.None;

    public Guid? EntityId { get; set; }

    public ApprovalStatus Status { get; set; } = ApprovalStatus.Pending;

    public ApprovalPriority Priority { get; set; } = ApprovalPriority.Normal;

    public ApprovalExecutionStatus ExecutionStatus { get; set; } = ApprovalExecutionStatus.NotReady;

    public string Reason { get; set; } = string.Empty;

    public string? RequestPayloadJson { get; set; }

    public string? DecisionReason { get; set; }

    public string? ExecutionNotes { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }

    public DateTime? DueAtUtc { get; set; }

    public DateTime? DecidedAtUtc { get; set; }

    public DateTime? CancelledAtUtc { get; set; }

    public DateTime? ExecutedAtUtc { get; set; }

    public Guid? ExecutedByUserId { get; set; }

    public byte[] RowVersion { get; set; } = [];

    public Compound Compound { get; set; } = null!;

    public ApplicationUser RequestedByUser { get; set; } = null!;

    public ApplicationUser? LastDecisionByUser { get; set; }

    public ApplicationUser? ExecutedByUser { get; set; }

    public ICollection<ApprovalDecision> Decisions { get; set; } = new List<ApprovalDecision>();
}
