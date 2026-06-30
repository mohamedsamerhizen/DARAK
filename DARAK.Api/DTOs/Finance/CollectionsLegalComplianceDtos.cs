using System.ComponentModel.DataAnnotations;
using DARAK.Api.DTOs.Common;
using DARAK.Api.Enums;

namespace DARAK.Api.DTOs.Finance;

public sealed class PenaltyRuleQueryRequest : PaginationQuery
{
    public Guid? CompoundId { get; init; }
    public PenaltyRuleStatus? Status { get; init; }
    public PenaltyRuleTargetType? TargetType { get; init; }
}

public sealed class CreatePenaltyRuleRequest
{
    public Guid CompoundId { get; init; }

    [Required]
    [MaxLength(150)]
    public string Name { get; init; } = string.Empty;

    public PenaltyRuleTargetType TargetType { get; init; }

    public PenaltyCalculationType CalculationType { get; init; }

    [Range(0, 365)]
    public int GracePeriodDays { get; init; }

    [Range(0.01, double.MaxValue)]
    public decimal Amount { get; init; }

    [Range(0, 100)]
    public decimal? PercentageRate { get; init; }

    [Range(0, double.MaxValue)]
    public decimal? MaxAmount { get; init; }

    public bool PauseWhenDisputed { get; init; } = true;

    public DateOnly? EffectiveFrom { get; init; }

    public DateOnly? EffectiveUntil { get; init; }

    public PenaltyRuleStatus Status { get; init; } = PenaltyRuleStatus.Active;
}

public sealed record PenaltyRuleResponse(
    Guid Id,
    Guid CompoundId,
    string Name,
    PenaltyRuleTargetType TargetType,
    PenaltyCalculationType CalculationType,
    PenaltyRuleStatus Status,
    int GracePeriodDays,
    decimal Amount,
    decimal? PercentageRate,
    decimal? MaxAmount,
    bool PauseWhenDisputed,
    DateOnly? EffectiveFrom,
    DateOnly? EffectiveUntil,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public sealed class CollectionCaseQueryRequest : PaginationQuery
{
    public Guid? CompoundId { get; init; }
    public Guid? ResidentProfileId { get; init; }
    public FinancialCollectionSourceType? SourceType { get; init; }
    public CollectionStage? Stage { get; init; }
    public CollectionCaseStatus? Status { get; init; }
}

public sealed class CreateCollectionCaseRequest
{
    public Guid CompoundId { get; init; }

    public Guid ResidentProfileId { get; init; }

    public FinancialCollectionSourceType SourceType { get; init; } = FinancialCollectionSourceType.ManualBalance;

    public Guid? SourceId { get; init; }

    [Range(0.01, double.MaxValue)]
    public decimal AmountDue { get; init; }

    [Required]
    [MaxLength(3)]
    public string Currency { get; init; } = "IQD";

    public DateOnly? DueDate { get; init; }

    [Required]
    [MaxLength(1000)]
    public string Reason { get; init; } = string.Empty;

    [MaxLength(1000)]
    public string? Notes { get; init; }

    public Guid? AssignedToUserId { get; init; }
}

public sealed class AdvanceCollectionCaseRequest
{
    public CollectionStage NewStage { get; init; }

    [MaxLength(1000)]
    public string? Notes { get; init; }

    public bool CloseCase { get; init; }
}

public sealed record CollectionCaseResponse(
    Guid Id,
    Guid CompoundId,
    Guid ResidentProfileId,
    string ResidentName,
    FinancialCollectionSourceType SourceType,
    Guid? SourceId,
    CollectionStage Stage,
    CollectionCaseStatus Status,
    decimal AmountDue,
    string Currency,
    DateOnly? DueDate,
    string Reason,
    string? Notes,
    Guid? AssignedToUserId,
    Guid? CreatedByUserId,
    DateTime OpenedAtUtc,
    DateTime? LastActionAtUtc,
    DateTime? ClosedAtUtc,
    int LegalNoticeCount,
    int PaymentPlanCount);

public sealed class LegalNoticeQueryRequest : PaginationQuery
{
    public Guid? CompoundId { get; init; }
    public Guid? ResidentProfileId { get; init; }
    public Guid? CollectionCaseId { get; init; }
    public LegalNoticeType? NoticeType { get; init; }
    public LegalNoticeStatus? Status { get; init; }
}

public sealed class CreateLegalNoticeRequest
{
    public Guid CompoundId { get; init; }

    public Guid ResidentProfileId { get; init; }

    public Guid? CollectionCaseId { get; init; }

    public LegalNoticeType NoticeType { get; init; }

    public LegalNoticeStatus Status { get; init; } = LegalNoticeStatus.Draft;

    [Required]
    [MaxLength(200)]
    public string Title { get; init; } = string.Empty;

    [Required]
    [MaxLength(4000)]
    public string Body { get; init; } = string.Empty;

    [MaxLength(80)]
    public string? DeliveryChannel { get; init; }

    [MaxLength(160)]
    public string? DeliveryReference { get; init; }

    public DateOnly? DeadlineDate { get; init; }
}

public sealed class IssueLegalNoticeRequest
{
    [MaxLength(80)]
    public string? DeliveryChannel { get; init; }

    [MaxLength(160)]
    public string? DeliveryReference { get; init; }
}

public sealed record LegalNoticeResponse(
    Guid Id,
    Guid CompoundId,
    Guid ResidentProfileId,
    string ResidentName,
    Guid? CollectionCaseId,
    LegalNoticeType NoticeType,
    LegalNoticeStatus Status,
    string Title,
    string Body,
    string? DeliveryChannel,
    string? DeliveryReference,
    DateOnly? DeadlineDate,
    Guid? CreatedByUserId,
    Guid? IssuedByUserId,
    DateTime CreatedAtUtc,
    DateTime? IssuedAtUtc,
    DateTime? UpdatedAtUtc);

public sealed class CreatePaymentPlanRequest
{
    public Guid CollectionCaseId { get; init; }

    [Range(0.01, double.MaxValue)]
    public decimal TotalAmount { get; init; }

    [Required]
    [MaxLength(3)]
    public string Currency { get; init; } = "IQD";

    [Range(1, 36)]
    public int InstallmentCount { get; init; }

    public DateOnly StartDate { get; init; }

    [MaxLength(1000)]
    public string? Notes { get; init; }
}

public sealed class PayPaymentPlanInstallmentRequest
{
    [Range(0.01, double.MaxValue)]
    public decimal Amount { get; init; }

    [MaxLength(120)]
    public string? IdempotencyKey { get; init; }
}

public sealed record PaymentPlanInstallmentResponse(
    Guid Id,
    int InstallmentNumber,
    DateOnly DueDate,
    decimal Amount,
    decimal PaidAmount,
    PaymentPlanInstallmentStatus Status,
    DateTime? PaidAtUtc);

public sealed record PaymentPlanResponse(
    Guid Id,
    Guid CompoundId,
    Guid ResidentProfileId,
    Guid CollectionCaseId,
    PaymentPlanStatus Status,
    decimal TotalAmount,
    string Currency,
    int InstallmentCount,
    DateOnly StartDate,
    string? Notes,
    Guid? CreatedByUserId,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    IReadOnlyCollection<PaymentPlanInstallmentResponse> Installments);

public sealed record ResidentComplianceProfileResponse(
    Guid ResidentProfileId,
    string ResidentName,
    Guid CompoundId,
    decimal TotalOutstandingAmount,
    decimal OverdueAmount,
    int OverdueItemCount,
    int OpenDisputeCount,
    int OpenViolationAppealCount,
    int OpenCollectionCaseCount,
    int ActiveLegalNoticeCount,
    int ActivePaymentPlanCount,
    string ComplianceStatus,
    IReadOnlyCollection<string> Reasons);


public sealed class CollectionFollowUpQueueQueryRequest : PaginationQuery
{
    public Guid? CompoundId { get; init; }
    public Guid? ResidentProfileId { get; init; }
    public Guid? AssignedToUserId { get; init; }
    public CollectionStage? Stage { get; init; }
    public CollectionCaseStatus? Status { get; init; }
    public bool OnlyActionRequired { get; init; }

    [Range(0, 3650)]
    public int MinDaysSinceLastAction { get; init; }
}

public sealed record CollectionFollowUpQueueItemResponse(
    Guid CollectionCaseId,
    Guid CompoundId,
    Guid ResidentProfileId,
    string ResidentName,
    FinancialCollectionSourceType SourceType,
    Guid? SourceId,
    CollectionStage Stage,
    CollectionCaseStatus Status,
    decimal AmountDue,
    string Currency,
    DateOnly? DueDate,
    int? DaysOverdue,
    DateTime OpenedAtUtc,
    DateTime? LastActionAtUtc,
    int DaysSinceLastAction,
    Guid? AssignedToUserId,
    int LegalNoticeCount,
    int PaymentPlanCount,
    bool HasActivePaymentPlan,
    bool HasBrokenPaymentPlan,
    DateOnly? NextPaymentPlanDueDate,
    decimal? NextPaymentPlanOutstandingAmount,
    string FollowUpPriority,
    string RecommendedAction,
    IReadOnlyCollection<string> Reasons);



public sealed record LegalCaseManagementDashboardResponse(
    Guid? CompoundId,
    int OpenCaseCount,
    int LegalEscalatedCaseCount,
    int ReadyForLegalEscalationCount,
    int ActiveLegalNoticeCount,
    int OverdueLegalNoticeCount,
    int DraftLegalNoticeCount,
    int BrokenPaymentPlanCaseCount,
    int HighPriorityCaseCount,
    decimal OpenCollectionAmount,
    int OldestOpenCaseAgeDays,
    IReadOnlyCollection<string> ExecutiveAlerts);

public sealed class LegalCaseEscalationQueueQueryRequest : PaginationQuery
{
    public Guid? CompoundId { get; init; }
    public Guid? ResidentProfileId { get; init; }
    public Guid? AssignedToUserId { get; init; }
    public bool OnlyReadyForEscalation { get; init; }

    [Range(0, 3650)]
    public int MinDaysOverdue { get; init; }
}

public sealed record LegalCaseEscalationQueueItemResponse(
    Guid CollectionCaseId,
    Guid CompoundId,
    Guid ResidentProfileId,
    string ResidentName,
    FinancialCollectionSourceType SourceType,
    Guid? SourceId,
    CollectionStage Stage,
    CollectionCaseStatus Status,
    decimal AmountDue,
    string Currency,
    DateOnly? DueDate,
    int? DaysOverdue,
    int CaseAgeDays,
    DateTime OpenedAtUtc,
    DateTime? LastActionAtUtc,
    Guid? AssignedToUserId,
    int LegalNoticeCount,
    int ActiveLegalNoticeCount,
    int PaymentPlanCount,
    bool HasBrokenPaymentPlan,
    bool IsReadyForLegalEscalation,
    string LegalPriority,
    string RecommendedLegalAction,
    IReadOnlyCollection<string> ReadinessReasons,
    IReadOnlyCollection<string> BlockingIssues);

public sealed class LegalNoticeServiceQueueQueryRequest : PaginationQuery
{
    public Guid? CompoundId { get; init; }
    public Guid? ResidentProfileId { get; init; }
    public Guid? CollectionCaseId { get; init; }
    public LegalNoticeStatus? Status { get; init; }
    public bool OnlyActionRequired { get; init; }

    [Range(0, 3650)]
    public int DeadlineWithinDays { get; init; } = 30;
}

public sealed record LegalNoticeServiceQueueItemResponse(
    Guid LegalNoticeId,
    Guid CompoundId,
    Guid ResidentProfileId,
    string ResidentName,
    Guid? CollectionCaseId,
    LegalNoticeType NoticeType,
    LegalNoticeStatus Status,
    string Title,
    DateOnly? DeadlineDate,
    int? DaysToDeadline,
    bool IsDeadlineOverdue,
    bool IsActionRequired,
    string ServicePriority,
    string RecommendedAction,
    string? DeliveryChannel,
    string? DeliveryReference,
    DateTime CreatedAtUtc,
    DateTime? IssuedAtUtc);

public sealed record LegalCaseFileNoticeResponse(
    Guid LegalNoticeId,
    LegalNoticeType NoticeType,
    LegalNoticeStatus Status,
    string Title,
    DateOnly? DeadlineDate,
    string? DeliveryChannel,
    string? DeliveryReference,
    DateTime CreatedAtUtc,
    DateTime? IssuedAtUtc);

public sealed record LegalCaseFilePaymentPlanResponse(
    Guid PaymentPlanId,
    PaymentPlanStatus Status,
    decimal TotalAmount,
    decimal OutstandingAmount,
    int InstallmentCount,
    int OverdueInstallmentCount,
    DateOnly? NextDueDate);

public sealed record LegalCaseTimelineEventResponse(
    DateTime EventAtUtc,
    string EventType,
    string Title,
    string Description,
    string Severity);

public sealed record LegalCaseFileResponse(
    Guid CollectionCaseId,
    Guid CompoundId,
    Guid ResidentProfileId,
    string ResidentName,
    FinancialCollectionSourceType SourceType,
    Guid? SourceId,
    CollectionStage Stage,
    CollectionCaseStatus Status,
    decimal AmountDue,
    string Currency,
    DateOnly? DueDate,
    int? DaysOverdue,
    int CaseAgeDays,
    Guid? AssignedToUserId,
    string Reason,
    string? Notes,
    int LegalNoticeCount,
    int ActiveLegalNoticeCount,
    int PaymentPlanCount,
    int BrokenPaymentPlanCount,
    bool IsReadyForLegalEscalation,
    string LegalPriority,
    string RecommendedLegalAction,
    IReadOnlyCollection<string> ReadinessReasons,
    IReadOnlyCollection<string> BlockingIssues,
    IReadOnlyCollection<LegalCaseFileNoticeResponse> LegalNotices,
    IReadOnlyCollection<LegalCaseFilePaymentPlanResponse> PaymentPlans,
    IReadOnlyCollection<LegalCaseTimelineEventResponse> Timeline);

public sealed record CollectionsLegalComplianceSummaryResponse(
    int ActivePenaltyRuleCount,
    int OpenCollectionCaseCount,
    int LegalEscalatedCaseCount,
    int DraftLegalNoticeCount,
    int IssuedLegalNoticeCount,
    int ActivePaymentPlanCount,
    int BrokenPaymentPlanCount,
    decimal OpenCollectionAmount);
