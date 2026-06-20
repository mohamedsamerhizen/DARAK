using System.ComponentModel.DataAnnotations;
using DARAK.Api.DTOs.Common;
using DARAK.Api.Enums;

namespace DARAK.Api.DTOs.Finance;

public sealed class FinancialDashboardQuery
{
    public Guid? CompoundId { get; init; }

    public DateOnly? FromDate { get; init; }

    public DateOnly? ToDate { get; init; }
}

public sealed record FinancialControlDashboardResponse(
    Guid? CompoundId,
    DateOnly FromDate,
    DateOnly ToDate,
    int ActiveResidentCount,
    decimal TotalOutstandingAmount,
    decimal TotalOverdueAmount,
    decimal UtilityBillOutstandingAmount,
    decimal RentInvoiceOutstandingAmount,
    decimal InstallmentOutstandingAmount,
    decimal ViolationFineOutstandingAmount,
    decimal CollectedAmount,
    decimal RefundedAmount,
    decimal NetCollectedAmount,
    decimal CreditAdjustmentsAppliedAmount,
    decimal DebitAdjustmentsAppliedAmount,
    int PendingAdjustmentCount,
    int AppliedAdjustmentCount,
    int CancelledAdjustmentCount,
    DateTime GeneratedAtUtc);

public sealed class ResidentStatementQuery
{
    public DateOnly? FromDate { get; init; }

    public DateOnly? ToDate { get; init; }
}

public sealed record ResidentStatementResponse(
    Guid ResidentProfileId,
    Guid CompoundId,
    string ResidentName,
    string Currency,
    DateOnly? FromDate,
    DateOnly? ToDate,
    decimal TotalDebits,
    decimal TotalCredits,
    decimal ClosingBalance,
    int FinancialReviewLineCount,
    decimal AppliedCreditAdjustmentAmount,
    decimal AppliedDebitAdjustmentAmount,
    IReadOnlyCollection<ResidentStatementLineResponse> Lines);

public sealed record ResidentStatementLineResponse(
    DateTime OccurredAtUtc,
    FinancialLedgerSourceType SourceType,
    Guid SourceId,
    string Reference,
    string Description,
    decimal DebitAmount,
    decimal CreditAmount,
    decimal BalanceAfterLine,
    bool IsUnderFinancialReview,
    Guid? FinancialDisputeId,
    FinancialDisputeStatus? FinancialDisputeStatus,
    Guid? ViolationAppealId,
    ViolationAppealStatus? ViolationAppealStatus,
    Guid? FinancialAdjustmentId,
    FinancialAdjustmentStatus? FinancialAdjustmentStatus);

public sealed class CreateFinancialAdjustmentRequest
{
    [Required]
    public Guid CompoundId { get; init; }

    [Required]
    public Guid ResidentProfileId { get; init; }

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

public sealed class ApplyFinancialAdjustmentRequest
{
    [MaxLength(1000)]
    public string? Notes { get; init; }
}

public sealed class CancelFinancialAdjustmentRequest
{
    [Required]
    [MaxLength(1000)]
    public string Reason { get; init; } = string.Empty;
}

public sealed class FinancialAdjustmentSearchQuery : PaginationQuery
{
    public Guid? CompoundId { get; init; }

    public Guid? ResidentProfileId { get; init; }

    public FinancialAdjustmentType? AdjustmentType { get; init; }

    public FinancialAdjustmentStatus? Status { get; init; }

    public DateTime? CreatedFromUtc { get; init; }

    public DateTime? CreatedToUtc { get; init; }

    [MaxLength(100)]
    public string? SearchTerm { get; init; }
}

public sealed record FinancialAdjustmentResponse(
    Guid Id,
    Guid CompoundId,
    Guid ResidentProfileId,
    string ResidentName,
    FinancialAdjustmentType AdjustmentType,
    FinancialAdjustmentStatus Status,
    decimal Amount,
    string Currency,
    string Reason,
    Guid RequestedByUserId,
    Guid? ApprovalRequestId,
    Guid? AppliedByUserId,
    DateTime? AppliedAtUtc,
    Guid? CancelledByUserId,
    DateTime? CancelledAtUtc,
    string? CancellationReason,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public sealed class FinancialAgingReportQuery
{
    public Guid? CompoundId { get; init; }

    public DateOnly? AsOfDate { get; init; }
}

public sealed record FinancialAgingReportResponse(
    Guid? CompoundId,
    DateOnly AsOfDate,
    decimal CurrentAmount,
    decimal Days1To30Amount,
    decimal Days31To60Amount,
    decimal Days61To90Amount,
    decimal DaysOver90Amount,
    decimal TotalOutstandingAmount,
    IReadOnlyCollection<FinancialAgingBucketResponse> Buckets);

public sealed record FinancialAgingBucketResponse(
    string Bucket,
    int ItemCount,
    decimal Amount);


public sealed class FinancialAgingRiskReportQuery
{
    public Guid? CompoundId { get; init; }

    public DateOnly? AsOfDate { get; init; }

    [Range(0, 3650)]
    public int MinimumOverdueDays { get; init; }
}

public sealed record FinancialAgingRiskReportResponse(
    Guid? CompoundId,
    DateOnly AsOfDate,
    int ResidentCount,
    int HighRiskResidentCount,
    int OutstandingItemCount,
    int OverdueItemCount,
    decimal TotalOutstandingAmount,
    decimal TotalOverdueAmount,
    decimal UnderFinancialReviewAmount,
    decimal PenaltyPauseRecommendedAmount,
    IReadOnlyCollection<FinancialAgingRiskResidentResponse> Residents);

public sealed record FinancialAgingRiskResidentResponse(
    Guid ResidentProfileId,
    string ResidentName,
    decimal OutstandingAmount,
    decimal OverdueAmount,
    decimal UnderFinancialReviewAmount,
    decimal PenaltyPauseRecommendedAmount,
    DateOnly? OldestDueDate,
    int OldestOverdueDays,
    int ActiveFinancialDisputeCount,
    bool IsHighRisk,
    string RecommendedAction,
    IReadOnlyCollection<FinancialAgingRiskItemResponse> Items);

public sealed record FinancialAgingRiskItemResponse(
    FinancialLedgerSourceType SourceType,
    Guid SourceId,
    DateOnly DueDate,
    int DaysOverdue,
    decimal Amount,
    bool IsOverdue,
    bool HasActiveFinancialDispute,
    Guid? FinancialDisputeId,
    FinancialDisputeStatus? FinancialDisputeStatus,
    bool PenaltyPauseRecommended,
    string RecommendedAction);


public sealed class FinancialClosureSummaryQuery
{
    public Guid? CompoundId { get; init; }

    public DateOnly? AsOfDate { get; init; }

    [Range(1, 365)]
    public int ReconciliationLookbackDays { get; init; } = 30;

    [Range(0, 3650)]
    public int MinimumOverdueDays { get; init; } = 1;
}

public sealed record FinancialClosureSummaryResponse(
    Guid? CompoundId,
    DateOnly AsOfDate,
    DateOnly ReconciliationFromDate,
    int OpenReconciliationBatchCount,
    int ReconciliationIssueItemCount,
    int UnreviewedReconciliationIssueItemCount,
    decimal ReconciliationDifferenceAmount,
    int AgingRiskResidentCount,
    int HighRiskResidentCount,
    decimal TotalOverdueAmount,
    decimal UnderFinancialReviewAmount,
    decimal PenaltyPauseRecommendedAmount,
    int CollectionFollowUpCaseCount,
    int HighPriorityCollectionCaseCount,
    decimal CollectionFollowUpAmount,
    int ResidentsRequiringActionCount,
    IReadOnlyCollection<FinancialClosureActionItemResponse> ActionItems);

public sealed record FinancialClosureActionItemResponse(
    string Category,
    string Severity,
    Guid? ResidentProfileId,
    string? ResidentName,
    Guid? SourceId,
    string SourceType,
    decimal? Amount,
    int? AgeDays,
    string RecommendedAction);

public sealed class RevenueSummaryQuery
{
    public Guid? CompoundId { get; init; }

    public DateOnly? FromDate { get; init; }

    public DateOnly? ToDate { get; init; }
}

public sealed record RevenueSummaryResponse(
    Guid? CompoundId,
    DateOnly FromDate,
    DateOnly ToDate,
    decimal CollectedAmount,
    decimal RefundedAmount,
    decimal NetCollectedAmount,
    IReadOnlyCollection<RevenueByPaymentMethodResponse> ByPaymentMethod,
    IReadOnlyCollection<RevenueByTargetTypeResponse> ByTargetType);

public sealed record RevenueByPaymentMethodResponse(
    PaymentMethod PaymentMethod,
    decimal CollectedAmount,
    int PaymentCount);

public sealed record RevenueByTargetTypeResponse(
    PaymentTargetType TargetType,
    decimal CollectedAmount,
    int PaymentCount);
