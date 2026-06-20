using System.ComponentModel.DataAnnotations;
using DARAK.Api.DTOs.Common;
using DARAK.Api.Enums;

namespace DARAK.Api.DTOs.Finance;

public sealed class FinancialDisputeSearchQuery : PaginationQuery
{
    public Guid? CompoundId { get; init; }

    public Guid? ResidentProfileId { get; init; }

    public FinancialDisputeTargetType? TargetType { get; init; }

    public Guid? TargetId { get; init; }

    public FinancialDisputeStatus? Status { get; init; }

    public DateTime? CreatedFromUtc { get; init; }

    public DateTime? CreatedToUtc { get; init; }

    [MaxLength(100)]
    public string? SearchTerm { get; init; }
}

public sealed class CreateFinancialDisputeRequest
{
    [Required]
    public Guid CompoundId { get; init; }

    [Required]
    public Guid ResidentProfileId { get; init; }

    [Required]
    public FinancialDisputeTargetType TargetType { get; init; }

    [Required]
    public Guid TargetId { get; init; }

    public Guid? ConversationId { get; init; }

    [Required]
    [MaxLength(1000)]
    public string Reason { get; init; } = string.Empty;

    [Required]
    [MaxLength(4000)]
    public string Message { get; init; } = string.Empty;
}

public sealed class CreateResidentFinancialDisputeRequest
{
    [Required]
    public FinancialDisputeTargetType TargetType { get; init; }

    [Required]
    public Guid TargetId { get; init; }

    public Guid? ConversationId { get; init; }

    [Required]
    [MaxLength(1000)]
    public string Reason { get; init; } = string.Empty;

    [Required]
    [MaxLength(4000)]
    public string Message { get; init; } = string.Empty;
}

public sealed class TransitionFinancialDisputeRequest
{
    [Required]
    public FinancialDisputeTransition Transition { get; init; }

    [MaxLength(2000)]
    public string? Notes { get; init; }

    [MaxLength(2000)]
    public string? ResolutionSummary { get; init; }
}

public sealed class CreateGovernanceFinancialAdjustmentRequest
{
    [Required]
    public FinancialAdjustmentType AdjustmentType { get; init; }

    [Range(typeof(decimal), "0.01", "999999999999.99")]
    public decimal Amount { get; init; }

    [MaxLength(3)]
    public string Currency { get; init; } = "IQD";

    [Required]
    [MaxLength(1000)]
    public string Reason { get; init; } = string.Empty;
}

public sealed record FinancialDisputeResponse(
    Guid Id,
    Guid CompoundId,
    Guid ResidentProfileId,
    string ResidentName,
    FinancialDisputeTargetType TargetType,
    Guid TargetId,
    string TargetReference,
    decimal? TargetAmount,
    FinancialDisputeStatus Status,
    string Reason,
    string ResidentMessage,
    string? AdminDecisionNotes,
    string? ResolutionSummary,
    Guid? ConversationId,
    Guid? FinancialAdjustmentId,
    Guid CreatedByUserId,
    Guid? ReviewedByUserId,
    Guid? ResolvedByUserId,
    Guid? CancelledByUserId,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    DateTime? ReviewedAtUtc,
    DateTime? ResolvedAtUtc,
    DateTime? CancelledAtUtc);

public sealed class ViolationAppealSearchQuery : PaginationQuery
{
    public Guid? CompoundId { get; init; }

    public Guid? ResidentProfileId { get; init; }

    public Guid? ViolationId { get; init; }

    public Guid? ViolationFineId { get; init; }

    public ViolationAppealStatus? Status { get; init; }

    public DateTime? CreatedFromUtc { get; init; }

    public DateTime? CreatedToUtc { get; init; }

    [MaxLength(100)]
    public string? SearchTerm { get; init; }
}

public sealed class CreateViolationAppealRequest
{
    [Required]
    public Guid CompoundId { get; init; }

    [Required]
    public Guid ResidentProfileId { get; init; }

    [Required]
    public Guid ViolationId { get; init; }

    public Guid? ViolationFineId { get; init; }

    [Required]
    [MaxLength(1000)]
    public string Reason { get; init; } = string.Empty;

    [Required]
    [MaxLength(4000)]
    public string Message { get; init; } = string.Empty;
}

public sealed class CreateResidentViolationAppealRequest
{
    [Required]
    public Guid ViolationId { get; init; }

    public Guid? ViolationFineId { get; init; }

    [Required]
    [MaxLength(1000)]
    public string Reason { get; init; } = string.Empty;

    [Required]
    [MaxLength(4000)]
    public string Message { get; init; } = string.Empty;
}

public sealed class TransitionViolationAppealRequest
{
    [Required]
    public ViolationAppealTransition Transition { get; init; }

    [MaxLength(2000)]
    public string? Notes { get; init; }

    [Range(typeof(decimal), "0.01", "999999999999.99")]
    public decimal? ReducedFineAmount { get; init; }
}

public sealed record ViolationAppealResponse(
    Guid Id,
    Guid CompoundId,
    Guid ResidentProfileId,
    string ResidentName,
    Guid ViolationId,
    Guid? ViolationFineId,
    string ViolationTitle,
    decimal? FineAmount,
    ViolationAppealStatus Status,
    string Reason,
    string ResidentMessage,
    string? AdminDecisionNotes,
    decimal? ReducedFineAmount,
    Guid? FinancialAdjustmentId,
    Guid CreatedByUserId,
    Guid? ReviewedByUserId,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    DateTime? ReviewedAtUtc);

public sealed record ResidentFinancialGovernanceSummaryResponse(
    int ActiveFinancialDisputeCount,
    int OpenFinancialDisputeCount,
    int UnderReviewFinancialDisputeCount,
    int NeedResidentResponseFinancialDisputeCount,
    int AcceptedFinancialDisputeCount,
    int RejectedFinancialDisputeCount,
    int ResolvedFinancialDisputeCount,
    int CancelledFinancialDisputeCount,
    int ActiveViolationAppealCount,
    int SubmittedViolationAppealCount,
    int UnderReviewViolationAppealCount,
    int NeedResidentResponseViolationAppealCount,
    int AcceptedViolationAppealCount,
    int RejectedViolationAppealCount,
    int FineReducedViolationAppealCount,
    int FineCancelledViolationAppealCount,
    int CancelledViolationAppealCount,
    int LinkedFinancialAdjustmentCount,
    int FinancialReviewItemCount);

public sealed class FinancialGovernanceSummaryQuery
{
    public Guid? CompoundId { get; init; }

    public DateTime? CreatedFromUtc { get; init; }

    public DateTime? CreatedToUtc { get; init; }
}

public sealed record AdminFinancialGovernanceSummaryResponse(
    Guid? CompoundId,
    int TotalFinancialDisputeCount,
    int ActiveFinancialDisputeCount,
    int OpenFinancialDisputeCount,
    int UnderReviewFinancialDisputeCount,
    int NeedResidentResponseFinancialDisputeCount,
    int AcceptedFinancialDisputeCount,
    int RejectedFinancialDisputeCount,
    int ResolvedFinancialDisputeCount,
    int CancelledFinancialDisputeCount,
    int TotalViolationAppealCount,
    int ActiveViolationAppealCount,
    int SubmittedViolationAppealCount,
    int UnderReviewViolationAppealCount,
    int NeedResidentResponseViolationAppealCount,
    int AcceptedViolationAppealCount,
    int RejectedViolationAppealCount,
    int FineReducedViolationAppealCount,
    int FineCancelledViolationAppealCount,
    int CancelledViolationAppealCount,
    int LinkedFinancialAdjustmentCount,
    int PendingLinkedFinancialAdjustmentCount,
    int AppliedLinkedFinancialAdjustmentCount,
    int CancelledLinkedFinancialAdjustmentCount,
    int FinancialReviewItemCount);

public sealed record AdminResidentFinancialGovernanceSnapshotResponse(
    Guid ResidentProfileId,
    Guid CompoundId,
    string ResidentName,
    ResidentFinancialGovernanceSummaryResponse Summary,
    DateTime? LatestFinancialDisputeCreatedAtUtc,
    DateTime? LatestViolationAppealCreatedAtUtc);

