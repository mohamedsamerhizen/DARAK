using System.ComponentModel.DataAnnotations;
using DARAK.Api.DTOs.Common;
using DARAK.Api.Enums;

namespace DARAK.Api.DTOs.Commercial;

public sealed class CommercialDashboardQuery
{
    public Guid? CompoundId { get; init; }
}

public sealed record CommercialEngineDashboardResponse(
    Guid? CompoundId,
    int ActiveBillingRules,
    int PendingMeterCorrections,
    int PendingOwnershipTransfers,
    int PendingInstallmentReschedules,
    int OpenHandoverChecklists,
    int ContractLifecycleEventsThisMonth,
    decimal PotentialLateFees,
    DateTime GeneratedAtUtc);

public sealed class BillingRuleSearchQuery : PaginationQuery
{
    public Guid? CompoundId { get; init; }

    public Guid? CompoundServiceId { get; init; }

    public BillingRuleStatus? Status { get; init; }

    public BillingChargeMode? ChargeMode { get; init; }

    [MaxLength(100)]
    public string? SearchTerm { get; init; }
}

public sealed class CreateBillingRuleRequest
{
    public Guid CompoundId { get; init; }

    public Guid? CompoundServiceId { get; init; }

    [Required]
    [MaxLength(150)]
    public string Name { get; init; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; init; }

    public BillingRuleStatus Status { get; init; } = BillingRuleStatus.Draft;

    public BillingChargeMode ChargeMode { get; init; } = BillingChargeMode.Fixed;

    public decimal FixedChargeAmount { get; init; }

    public decimal RatePerUnit { get; init; }

    public decimal MinimumChargeAmount { get; init; }

    public decimal LateFeeFlatAmount { get; init; }

    public decimal LateFeePercentage { get; init; }

    public int GracePeriodDays { get; init; }

    public DateOnly EffectiveFrom { get; init; }

    public DateOnly? EffectiveTo { get; init; }

    [MaxLength(1000)]
    public string? Notes { get; init; }
}

public sealed class AddBillingRuleTierRequest
{
    public decimal FromQuantity { get; init; }

    public decimal? ToQuantity { get; init; }

    public decimal RatePerUnit { get; init; }

    public decimal FixedAmount { get; init; }

    public int SortOrder { get; init; }
}

public sealed record BillingRuleTierResponse(
    Guid Id,
    decimal FromQuantity,
    decimal? ToQuantity,
    decimal RatePerUnit,
    decimal FixedAmount,
    int SortOrder);

public sealed record BillingRuleResponse(
    Guid Id,
    Guid CompoundId,
    Guid? CompoundServiceId,
    string Name,
    string? Description,
    BillingRuleStatus Status,
    BillingChargeMode ChargeMode,
    decimal FixedChargeAmount,
    decimal RatePerUnit,
    decimal MinimumChargeAmount,
    decimal LateFeeFlatAmount,
    decimal LateFeePercentage,
    int GracePeriodDays,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    string? Notes,
    DateTime CreatedAtUtc,
    IReadOnlyCollection<BillingRuleTierResponse> Tiers);

public sealed class MeterReadingCorrectionSearchQuery : PaginationQuery
{
    public Guid? CompoundId { get; init; }

    public Guid? MeterReadingId { get; init; }

    public MeterReadingCorrectionStatus? Status { get; init; }
}

public sealed class CreateMeterReadingCorrectionRequest
{
    public Guid MeterReadingId { get; init; }

    public decimal CorrectedPreviousReading { get; init; }

    public decimal CorrectedCurrentReading { get; init; }

    public decimal? CorrectedRatePerUnit { get; init; }

    [Required]
    [MaxLength(1000)]
    public string Reason { get; init; } = string.Empty;
}

public sealed class DecideMeterReadingCorrectionRequest
{
    [Required]
    [MaxLength(1000)]
    public string Reason { get; init; } = string.Empty;
}

public sealed record MeterReadingCorrectionResponse(
    Guid Id,
    Guid CompoundId,
    Guid MeterReadingId,
    Guid MeterId,
    Guid PropertyUnitId,
    MeterReadingCorrectionStatus Status,
    decimal OriginalPreviousReading,
    decimal OriginalCurrentReading,
    decimal OriginalConsumption,
    decimal OriginalAmount,
    decimal CorrectedPreviousReading,
    decimal CorrectedCurrentReading,
    decimal CorrectedConsumption,
    decimal CorrectedAmount,
    string Reason,
    string? DecisionReason,
    Guid RequestedByUserId,
    Guid? ReviewedByUserId,
    DateTime RequestedAtUtc,
    DateTime? ReviewedAtUtc,
    DateTime? AppliedAtUtc);

public sealed class CreateContractLifecycleEventRequest
{
    public Guid CompoundId { get; init; }

    public CommercialContractType ContractType { get; init; }

    public Guid ContractId { get; init; }

    public ContractLifecycleEventType EventType { get; init; }

    public DateOnly EffectiveDate { get; init; }

    [Required]
    [MaxLength(1000)]
    public string Reason { get; init; } = string.Empty;

    [MaxLength(1000)]
    public string? Notes { get; init; }

    [MaxLength(4000)]
    public string? MetadataJson { get; init; }
}

public sealed class ContractLifecycleTimelineQuery : PaginationQuery
{
    public Guid? CompoundId { get; init; }
}

public sealed record ContractLifecycleEventResponse(
    Guid Id,
    Guid CompoundId,
    CommercialContractType ContractType,
    Guid ContractId,
    Guid PropertyUnitId,
    Guid ResidentProfileId,
    ContractLifecycleEventType EventType,
    Guid? ActorUserId,
    DateOnly EffectiveDate,
    string Reason,
    string? Notes,
    string? MetadataJson,
    DateTime CreatedAtUtc);

public sealed class CreateUnitHandoverChecklistRequest
{
    public Guid PropertyUnitId { get; init; }

    public Guid ResidentProfileId { get; init; }

    public UnitHandoverType HandoverType { get; init; }

    public DateOnly ScheduledDate { get; init; }

    [MaxLength(1000)]
    public string? Notes { get; init; }

    public IReadOnlyCollection<CreateUnitHandoverChecklistItemRequest> Items { get; init; } = [];
}

public sealed class CreateUnitHandoverChecklistItemRequest
{
    [Required]
    [MaxLength(150)]
    public string Title { get; init; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; init; }

    public int SortOrder { get; init; }
}

public sealed class CompleteUnitHandoverChecklistRequest
{
    public DateOnly CompletedDate { get; init; }

    [MaxLength(1000)]
    public string? Notes { get; init; }
}

public sealed record UnitHandoverChecklistItemResponse(
    Guid Id,
    string Title,
    string? Description,
    UnitHandoverItemStatus Status,
    string? Notes,
    int SortOrder);

public sealed record UnitHandoverChecklistResponse(
    Guid Id,
    Guid CompoundId,
    Guid PropertyUnitId,
    Guid ResidentProfileId,
    UnitHandoverType HandoverType,
    UnitHandoverStatus Status,
    DateOnly ScheduledDate,
    DateOnly? CompletedDate,
    string? Notes,
    DateTime CreatedAtUtc,
    IReadOnlyCollection<UnitHandoverChecklistItemResponse> Items);

public sealed class CreateOwnershipTransferRequest
{
    public Guid PropertyUnitId { get; init; }

    public Guid CurrentOwnerResidentProfileId { get; init; }

    public Guid NewOwnerResidentProfileId { get; init; }

    public DateOnly RequestedTransferDate { get; init; }

    [Required]
    [MaxLength(1000)]
    public string Reason { get; init; } = string.Empty;
}

public sealed class DecideOwnershipTransferRequest
{
    [Required]
    [MaxLength(1000)]
    public string Reason { get; init; } = string.Empty;
}

public sealed record OwnershipTransferRequestResponse(
    Guid Id,
    Guid CompoundId,
    Guid PropertyUnitId,
    Guid CurrentOwnerResidentProfileId,
    Guid NewOwnerResidentProfileId,
    OwnershipTransferStatus Status,
    DateOnly RequestedTransferDate,
    string Reason,
    string? DecisionReason,
    Guid RequestedByUserId,
    Guid? ReviewedByUserId,
    DateTime RequestedAtUtc,
    DateTime? ReviewedAtUtc);

public sealed class CreateInstallmentRescheduleRequest
{
    public Guid InstallmentScheduleItemId { get; init; }

    public DateOnly RequestedDueDate { get; init; }

    public decimal? RequestedAmount { get; init; }

    [Required]
    [MaxLength(1000)]
    public string Reason { get; init; } = string.Empty;
}

public sealed class DecideInstallmentRescheduleRequest
{
    [Required]
    [MaxLength(1000)]
    public string Reason { get; init; } = string.Empty;
}

public sealed record InstallmentRescheduleRequestResponse(
    Guid Id,
    Guid CompoundId,
    Guid InstallmentScheduleItemId,
    Guid PropertySaleContractId,
    Guid ResidentProfileId,
    InstallmentRescheduleStatus Status,
    DateOnly OriginalDueDate,
    DateOnly RequestedDueDate,
    decimal OriginalAmount,
    decimal? RequestedAmount,
    string Reason,
    string? DecisionReason,
    Guid RequestedByUserId,
    Guid? ReviewedByUserId,
    DateTime RequestedAtUtc,
    DateTime? ReviewedAtUtc,
    DateTime? AppliedAtUtc);
