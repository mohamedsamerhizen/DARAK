using System.ComponentModel.DataAnnotations;
using DARAK.Api.DTOs.Common;
using DARAK.Api.Enums;

namespace DARAK.Api.DTOs.ResidentLifecycle;

public sealed class ResidentLifecycleQueryRequest : PaginationQuery
{
    public Guid? CompoundId { get; init; }
    public Guid? PropertyUnitId { get; init; }
    public Guid? ResidentProfileId { get; init; }
    public ResidentLifecycleProcessType? ProcessType { get; init; }
    public ResidentLifecycleStatus? Status { get; init; }
}

public sealed class CreateResidentLifecycleProcessRequest
{
    public Guid PropertyUnitId { get; init; }

    public Guid ResidentProfileId { get; init; }

    public ResidentLifecycleProcessType ProcessType { get; init; }

    public DateOnly TargetDate { get; init; }

    public bool FinancialClearanceRequired { get; init; } = true;

    [MaxLength(1000)]
    public string? Notes { get; init; }
}

public sealed class ConfirmLifecycleFinancialClearanceRequest
{
    [MaxLength(1000)]
    public string? Notes { get; init; }
}

public sealed class CompleteResidentLifecycleProcessRequest
{
    [MaxLength(1000)]
    public string? Notes { get; init; }
}

public sealed record ResidentLifecycleProcessResponse(
    Guid Id,
    Guid CompoundId,
    Guid PropertyUnitId,
    Guid ResidentProfileId,
    ResidentLifecycleProcessType ProcessType,
    ResidentLifecycleStatus Status,
    DateOnly TargetDate,
    bool FinancialClearanceRequired,
    bool FinancialClearanceConfirmed,
    DateTime? FinancialClearanceConfirmedAtUtc,
    DateTime? CompletedAtUtc,
    string? Notes,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public sealed class CustodyItemQueryRequest : PaginationQuery
{
    public Guid? CompoundId { get; init; }
    public Guid? PropertyUnitId { get; init; }
    public Guid? ResidentProfileId { get; init; }
    public CustodyItemStatus? Status { get; init; }
    public CustodyItemType? ItemType { get; init; }
}

public sealed class IssueCustodyItemRequest
{
    public Guid PropertyUnitId { get; init; }

    public Guid ResidentProfileId { get; init; }

    public CustodyItemType ItemType { get; init; } = CustodyItemType.Key;

    [Required]
    [MaxLength(120)]
    public string Identifier { get; init; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; init; }

    public decimal? ReplacementFeeAmount { get; init; }

    public DateTime? IssuedAtUtc { get; init; }

    [MaxLength(1000)]
    public string? Notes { get; init; }
}

public sealed class ReturnCustodyItemRequest
{
    public DateTime? ReturnedAtUtc { get; init; }

    [MaxLength(1000)]
    public string? Notes { get; init; }
}

public sealed record ResidentCustodyItemResponse(
    Guid Id,
    Guid CompoundId,
    Guid PropertyUnitId,
    Guid ResidentProfileId,
    CustodyItemType ItemType,
    CustodyItemStatus Status,
    string Identifier,
    string? Description,
    decimal? ReplacementFeeAmount,
    DateTime IssuedAtUtc,
    DateTime? ReturnedAtUtc,
    string? Notes,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public sealed class MoveLogisticsPermitQueryRequest : PaginationQuery
{
    public Guid? CompoundId { get; init; }
    public Guid? PropertyUnitId { get; init; }
    public Guid? ResidentProfileId { get; init; }
    public ResidentLifecycleProcessType? MoveType { get; init; }
    public MoveLogisticsPermitStatus? Status { get; init; }
}

public sealed class CreateMoveLogisticsPermitRequest
{
    public Guid PropertyUnitId { get; init; }

    public Guid ResidentProfileId { get; init; }

    public Guid? ResidentLifecycleProcessId { get; init; }

    public ResidentLifecycleProcessType MoveType { get; init; }

    public DateTime ScheduledStartAtUtc { get; init; }

    public DateTime ScheduledEndAtUtc { get; init; }

    [MaxLength(300)]
    public string? TruckInfo { get; init; }

    [Range(0, 200)]
    public int WorkersCount { get; init; }

    [MaxLength(1000)]
    public string? Notes { get; init; }
}

public sealed class DecideMoveLogisticsPermitRequest
{
    public bool Approved { get; init; }

    [Required]
    [MaxLength(1000)]
    public string Reason { get; init; } = string.Empty;
}

public sealed class CompleteMoveLogisticsPermitRequest
{
    [MaxLength(1000)]
    public string? CompletionNotes { get; init; }
}

public sealed record MoveLogisticsPermitResponse(
    Guid Id,
    Guid CompoundId,
    Guid PropertyUnitId,
    Guid ResidentProfileId,
    Guid? ResidentLifecycleProcessId,
    ResidentLifecycleProcessType MoveType,
    MoveLogisticsPermitStatus Status,
    DateTime ScheduledStartAtUtc,
    DateTime ScheduledEndAtUtc,
    string? TruckInfo,
    int WorkersCount,
    string? Notes,
    string? DecisionReason,
    DateTime? ApprovedAtUtc,
    DateTime? CompletedAtUtc,
    string? CompletionNotes,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public sealed class UnitReadinessQueryRequest : PaginationQuery
{
    public Guid? CompoundId { get; init; }
    public Guid? PropertyUnitId { get; init; }
    public UnitReadinessStatus? Status { get; init; }
}

public sealed class CreateUnitReadinessRecordRequest
{
    public Guid PropertyUnitId { get; init; }

    public Guid? ResidentLifecycleProcessId { get; init; }

    public UnitReadinessStatus Status { get; init; } = UnitReadinessStatus.NeedsInspection;

    public Guid? OperationalChecklistRunId { get; init; }

    [MaxLength(1000)]
    public string? Notes { get; init; }
}

public sealed class UpdateUnitReadinessStatusRequest
{
    public UnitReadinessStatus Status { get; init; }

    [MaxLength(1000)]
    public string? Notes { get; init; }
}

public sealed record UnitReadinessRecordResponse(
    Guid Id,
    Guid CompoundId,
    Guid PropertyUnitId,
    Guid? ResidentLifecycleProcessId,
    UnitReadinessStatus Status,
    Guid? OperationalChecklistRunId,
    string? Notes,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public sealed class CreateUnitDamageLiabilityRequest
{
    public Guid PropertyUnitId { get; init; }

    public Guid ResidentProfileId { get; init; }

    public Guid? ResidentLifecycleProcessId { get; init; }

    [Range(0.01, 999999999)]
    public decimal EstimatedAmount { get; init; }

    [Required]
    [MaxLength(1000)]
    public string Description { get; init; } = string.Empty;

    public Guid? FinancialAdjustmentId { get; init; }

    public Guid? WorkOrderId { get; init; }

    [MaxLength(1000)]
    public string? Notes { get; init; }
}

public sealed record UnitDamageLiabilityResponse(
    Guid Id,
    Guid CompoundId,
    Guid PropertyUnitId,
    Guid ResidentProfileId,
    Guid? ResidentLifecycleProcessId,
    DamageLiabilityStatus Status,
    decimal EstimatedAmount,
    string Description,
    Guid? FinancialAdjustmentId,
    Guid? WorkOrderId,
    string? Notes,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);


public sealed class MoveOutReadinessQuery
{
    public Guid PropertyUnitId { get; init; }

    public Guid ResidentProfileId { get; init; }

    public DateOnly? AsOfDate { get; init; }
}

public sealed record MoveOutReadinessResponse(
    Guid CompoundId,
    Guid PropertyUnitId,
    string UnitNumber,
    Guid ResidentProfileId,
    string ResidentName,
    DateOnly AsOfDate,
    bool HasActiveOccupancy,
    Guid? ActiveMoveOutProcessId,
    ResidentLifecycleStatus? ActiveMoveOutProcessStatus,
    bool HasFinancialBlockers,
    bool HasOperationalBlockers,
    bool CanStartMoveOutProcess,
    bool CanConfirmFinancialClearance,
    bool CanCompleteMoveOutNow,
    decimal OutstandingAmount,
    int OutstandingItemCount,
    int ActiveFinancialDisputeCount,
    int OpenCollectionCaseCount,
    int ActiveRentContractCount,
    int ActiveSaleContractCount,
    int IssuedCustodyItemCount,
    int OpenDamageLiabilityCount,
    IReadOnlyCollection<MoveOutReadinessBlockerResponse> Blockers,
    IReadOnlyCollection<MoveOutReadinessFinancialItemResponse> FinancialItems);

public sealed record MoveOutReadinessBlockerResponse(
    string Code,
    string Severity,
    string Description,
    string RequiredAction);

public sealed record MoveOutReadinessFinancialItemResponse(
    FinancialLedgerSourceType SourceType,
    Guid SourceId,
    string Reference,
    DateOnly DueDate,
    decimal Amount,
    decimal PaidAmount,
    decimal OutstandingAmount,
    bool IsOverdue,
    bool HasActiveFinancialDispute,
    Guid? FinancialDisputeId,
    FinancialDisputeStatus? FinancialDisputeStatus);


public sealed class RecordMoveOutFinalMeterReadingsRequest
{
    [Required]
    [MinLength(1)]
    public IReadOnlyCollection<RecordMoveOutFinalMeterReadingRequest> Readings { get; init; } = Array.Empty<RecordMoveOutFinalMeterReadingRequest>();
}

public sealed class RecordMoveOutFinalMeterReadingRequest
{
    public Guid MeterId { get; init; }

    [Range(0, 999999999)]
    public decimal CurrentReading { get; init; }

    public DateTime? ReadingDateUtc { get; init; }

    [MaxLength(1000)]
    public string? Notes { get; init; }
}

public sealed class UpdateCustodySettlementStatusRequest
{
    public CustodyItemStatus Status { get; init; }

    public DateTime? ReturnedAtUtc { get; init; }

    [MaxLength(1000)]
    public string? Notes { get; init; }
}

public sealed class UpdateDamageLiabilityStatusRequest
{
    public DamageLiabilityStatus Status { get; init; }

    [MaxLength(1000)]
    public string? Notes { get; init; }
}

public sealed record MoveOutOperationalSettlementResponse(
    Guid ResidentLifecycleProcessId,
    Guid CompoundId,
    Guid PropertyUnitId,
    Guid ResidentProfileId,
    ResidentLifecycleStatus ProcessStatus,
    int ActiveMeterCount,
    int FinalMeterReadingCount,
    int MissingFinalMeterReadingCount,
    int UnbilledFinalMeterReadingCount,
    decimal EstimatedFinalMeterAmount,
    int IssuedCustodyItemCount,
    int LostOrDamagedCustodyItemCount,
    decimal CustodyReplacementAmount,
    int OpenDamageLiabilityCount,
    decimal OpenDamageLiabilityAmount,
    bool CanCompleteOperationalSettlement,
    IReadOnlyCollection<MoveOutSettlementBlockerResponse> Blockers,
    IReadOnlyCollection<MoveOutMeterSettlementItemResponse> MeterItems,
    IReadOnlyCollection<MoveOutCustodySettlementItemResponse> CustodyItems,
    IReadOnlyCollection<MoveOutDamageSettlementItemResponse> DamageLiabilities);

public sealed record MoveOutMeterSettlementItemResponse(
    Guid MeterId,
    MeterType MeterType,
    string MeterNumber,
    bool HasFinalReading,
    Guid? FinalReadingId,
    decimal? PreviousReading,
    decimal? CurrentReading,
    decimal? Consumption,
    decimal? Amount,
    bool? IsBilled,
    DateTime? ReadingDateUtc,
    string RequiredAction);

public sealed record MoveOutFinalMeterReadingResponse(
    Guid MeterReadingId,
    Guid MeterId,
    MeterType MeterType,
    string MeterNumber,
    decimal PreviousReading,
    decimal CurrentReading,
    decimal Consumption,
    decimal RatePerUnit,
    decimal Amount,
    bool IsBilled,
    DateTime ReadingDateUtc);

public sealed record MoveOutCustodySettlementItemResponse(
    Guid CustodyItemId,
    CustodyItemType ItemType,
    CustodyItemStatus Status,
    string Identifier,
    decimal? ReplacementFeeAmount,
    string RequiredAction);

public sealed record MoveOutDamageSettlementItemResponse(
    Guid DamageLiabilityId,
    DamageLiabilityStatus Status,
    decimal EstimatedAmount,
    string Description,
    Guid? FinancialAdjustmentId,
    string RequiredAction);

public sealed record MoveOutSettlementBlockerResponse(
    string Code,
    string Severity,
    string Description,
    string RequiredAction);



public sealed class PrepareMoveOutUnitTurnoverRequest
{
    public UnitReadinessStatus InitialStatus { get; init; } = UnitReadinessStatus.NeedsInspection;

    [MaxLength(1000)]
    public string? Notes { get; init; }
}

public sealed record MoveOutExitCertificateResponse(
    string CertificateNumber,
    Guid ResidentLifecycleProcessId,
    Guid CompoundId,
    Guid PropertyUnitId,
    string UnitNumber,
    Guid ResidentProfileId,
    string ResidentName,
    ResidentLifecycleStatus ProcessStatus,
    DateOnly TargetDate,
    bool FinancialClearanceRequired,
    bool FinancialClearanceConfirmed,
    bool OperationalSettlementComplete,
    bool IsMoveOutCompleted,
    bool ExitCertificateEligible,
    DateTime? CompletedAtUtc,
    DateTime GeneratedAtUtc,
    IReadOnlyCollection<MoveOutSettlementBlockerResponse> Blockers);

public sealed record MoveOutUnitTurnoverTimelineResponse(
    Guid ResidentLifecycleProcessId,
    Guid CompoundId,
    Guid PropertyUnitId,
    string UnitNumber,
    Guid ResidentProfileId,
    string ResidentName,
    ResidentLifecycleStatus ProcessStatus,
    UnitStatus CurrentUnitStatus,
    UnitReadinessStatus? LatestReadinessStatus,
    bool ReadyForNextResident,
    IReadOnlyCollection<MoveOutTurnoverTimelineItemResponse> Items);

public sealed record MoveOutTurnoverTimelineItemResponse(
    string Step,
    string Status,
    DateTime? OccurredAtUtc,
    string Description,
    string RequiredAction);

public sealed class ResidentLifecycleSummaryQuery
{
    public Guid? CompoundId { get; init; }
}

public sealed record ResidentLifecycleSummaryResponse(
    Guid? CompoundId,
    int ActiveLifecycleProcessCount,
    int PendingFinancialClearanceCount,
    int IssuedCustodyItemCount,
    int PendingMovePermitCount,
    int UnitsNotReadyCount,
    int OpenDamageLiabilityCount,
    DateTime GeneratedAtUtc);
