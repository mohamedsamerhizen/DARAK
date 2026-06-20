using DARAK.Api.Enums;
using DARAK.Api.Identity;

namespace DARAK.Api.Entities;

public sealed class FinancialDispute
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CompoundId { get; set; }

    public Guid ResidentProfileId { get; set; }

    public FinancialDisputeTargetType TargetType { get; set; }

    public Guid TargetId { get; set; }

    public FinancialDisputeStatus Status { get; set; } = FinancialDisputeStatus.Open;

    public string Reason { get; set; } = string.Empty;

    public string ResidentMessage { get; set; } = string.Empty;

    public string? AdminDecisionNotes { get; set; }

    public string? ResolutionSummary { get; set; }

    public Guid? ConversationId { get; set; }

    public Guid? FinancialAdjustmentId { get; set; }

    public Guid CreatedByUserId { get; set; }

    public Guid? ReviewedByUserId { get; set; }

    public Guid? ResolvedByUserId { get; set; }

    public Guid? CancelledByUserId { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }

    public DateTime? ReviewedAtUtc { get; set; }

    public DateTime? ResolvedAtUtc { get; set; }

    public DateTime? CancelledAtUtc { get; set; }

    public byte[] RowVersion { get; set; } = [];

    public Compound Compound { get; set; } = null!;

    public ResidentProfile ResidentProfile { get; set; } = null!;

    public Conversation? Conversation { get; set; }

    public FinancialAdjustment? FinancialAdjustment { get; set; }

    public ApplicationUser CreatedByUser { get; set; } = null!;

    public ApplicationUser? ReviewedByUser { get; set; }

    public ApplicationUser? ResolvedByUser { get; set; }

    public ApplicationUser? CancelledByUser { get; set; }
}
