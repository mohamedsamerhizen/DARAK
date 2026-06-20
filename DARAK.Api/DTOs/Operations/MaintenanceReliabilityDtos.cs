using System.ComponentModel.DataAnnotations;
using DARAK.Api.DTOs.Common;
using DARAK.Api.Enums;

namespace DARAK.Api.DTOs.Operations;

public sealed class MaintenanceAssetQueryRequest : PaginationQuery
{
    public Guid? CompoundId { get; init; }
    public Guid? BuildingId { get; init; }
    public Guid? PropertyUnitId { get; init; }
    public MaintenanceAssetType? AssetType { get; init; }
    public MaintenanceAssetStatus? Status { get; init; }
    [MaxLength(200)]
    public string? SearchTerm { get; init; }
}

public sealed class CreateMaintenanceAssetRequest
{
    public Guid CompoundId { get; init; }

    public Guid? BuildingId { get; init; }

    public Guid? FloorId { get; init; }

    public Guid? PropertyUnitId { get; init; }

    [Required]
    [MaxLength(150)]
    public string Name { get; init; } = string.Empty;

    [Required]
    [MaxLength(80)]
    public string Code { get; init; } = string.Empty;

    public MaintenanceAssetType AssetType { get; init; } = MaintenanceAssetType.Other;

    public MaintenanceAssetStatus Status { get; init; } = MaintenanceAssetStatus.Active;

    [MaxLength(300)]
    public string? LocationDescription { get; init; }

    [MaxLength(150)]
    public string? Manufacturer { get; init; }

    [MaxLength(150)]
    public string? Model { get; init; }

    [MaxLength(150)]
    public string? SerialNumber { get; init; }

    public DateTime? InstalledAtUtc { get; init; }

    public DateTime? WarrantyExpiresAtUtc { get; init; }

    [MaxLength(1000)]
    public string? Notes { get; init; }
}

public sealed record MaintenanceAssetResponse(
    Guid Id,
    Guid CompoundId,
    Guid? BuildingId,
    Guid? FloorId,
    Guid? PropertyUnitId,
    string Name,
    string Code,
    MaintenanceAssetType AssetType,
    MaintenanceAssetStatus Status,
    string? LocationDescription,
    string? Manufacturer,
    string? Model,
    string? SerialNumber,
    DateTime? InstalledAtUtc,
    DateTime? WarrantyExpiresAtUtc,
    DateTime? LastServiceAtUtc,
    DateTime? NextServiceDueAtUtc,
    string? Notes,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public sealed class MaintenanceSlaPolicyQueryRequest : PaginationQuery
{
    public Guid? CompoundId { get; init; }
    public WorkOrderPriority? Priority { get; init; }
    public WorkOrderSourceType? SourceType { get; init; }
    public bool? IsActive { get; init; }
    [MaxLength(200)]
    public string? SearchTerm { get; init; }
}

public sealed class CreateMaintenanceSlaPolicyRequest
{
    public Guid CompoundId { get; init; }

    [Required]
    [MaxLength(150)]
    public string Name { get; init; } = string.Empty;

    public WorkOrderPriority? Priority { get; init; }

    public WorkOrderSourceType? SourceType { get; init; }

    [Range(1, 10080)]
    public int ResponseDueMinutes { get; init; }

    [Range(1, 43200)]
    public int ResolutionDueMinutes { get; init; }

    [Range(1, 43200)]
    public int? EscalationDueMinutes { get; init; }

    public bool IsActive { get; init; } = true;

    [MaxLength(1000)]
    public string? Notes { get; init; }
}

public sealed record MaintenanceSlaPolicyResponse(
    Guid Id,
    Guid CompoundId,
    string Name,
    WorkOrderPriority? Priority,
    WorkOrderSourceType? SourceType,
    int ResponseDueMinutes,
    int ResolutionDueMinutes,
    int? EscalationDueMinutes,
    bool IsActive,
    string? Notes,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public sealed record WorkOrderSlaSnapshotResponse(
    Guid WorkOrderId,
    Guid? MaintenanceAssetId,
    string? MaintenanceAssetName,
    Guid? MaintenanceSlaPolicyId,
    string? MaintenanceSlaPolicyName,
    MaintenanceSlaStatus SlaStatus,
    DateTime? ResponseDueAtUtc,
    DateTime? ResolutionDueAtUtc,
    DateTime? FirstRespondedAtUtc,
    DateTime? SlaBreachedAtUtc,
    string? SlaBreachReason);

public sealed class PreventiveMaintenancePlanQueryRequest : PaginationQuery
{
    public Guid? CompoundId { get; init; }
    public Guid? MaintenanceAssetId { get; init; }
    public bool? IsActive { get; init; }
    public DateTime? DueFromUtc { get; init; }
    public DateTime? DueToUtc { get; init; }
    [MaxLength(200)]
    public string? SearchTerm { get; init; }
}

public sealed class CreatePreventiveMaintenancePlanRequest
{
    public Guid MaintenanceAssetId { get; init; }

    [Required]
    [MaxLength(150)]
    public string Title { get; init; } = string.Empty;

    [Required]
    [MaxLength(2000)]
    public string Description { get; init; } = string.Empty;

    public PreventiveMaintenanceCadence Cadence { get; init; } = PreventiveMaintenanceCadence.Monthly;

    [Range(1, 3650)]
    public int? CustomIntervalDays { get; init; }

    public WorkOrderPriority Priority { get; init; } = WorkOrderPriority.Normal;

    public Guid? AssignedStaffMemberId { get; init; }

    public Guid? AssignedVendorId { get; init; }

    public DateTime NextDueAtUtc { get; init; }

    public bool IsActive { get; init; } = true;

    [MaxLength(1000)]
    public string? Notes { get; init; }
}

public sealed record PreventiveMaintenancePlanResponse(
    Guid Id,
    Guid CompoundId,
    Guid MaintenanceAssetId,
    string MaintenanceAssetName,
    string Title,
    string Description,
    PreventiveMaintenanceCadence Cadence,
    int? CustomIntervalDays,
    WorkOrderPriority Priority,
    Guid? AssignedStaffMemberId,
    string? AssignedStaffMemberName,
    Guid? AssignedVendorId,
    string? AssignedVendorName,
    DateTime NextDueAtUtc,
    DateTime? LastGeneratedAtUtc,
    bool IsActive,
    string? Notes,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public sealed class GeneratePreventiveWorkOrderRequest
{
    public DateTime? ScheduledAtUtc { get; init; }

    public DateTime? DueAtUtc { get; init; }

    [MaxLength(1000)]
    public string? Note { get; init; }
}

public sealed class OperationalChecklistTemplateQueryRequest : PaginationQuery
{
    public Guid? CompoundId { get; init; }
    public bool? IsActive { get; init; }
    [MaxLength(200)]
    public string? SearchTerm { get; init; }
}

public sealed class CreateOperationalChecklistTemplateRequest
{
    public Guid CompoundId { get; init; }

    [Required]
    [MaxLength(150)]
    public string Name { get; init; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; init; }

    public bool IsActive { get; init; } = true;

    [MinLength(1)]
    public IReadOnlyCollection<CreateOperationalChecklistTemplateItemRequest> Items { get; init; } = [];
}

public sealed class CreateOperationalChecklistTemplateItemRequest
{
    [Required]
    [MaxLength(200)]
    public string Title { get; init; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; init; }

    public bool IsRequired { get; init; } = true;

    public int SortOrder { get; init; }
}

public sealed record OperationalChecklistTemplateItemResponse(
    Guid Id,
    string Title,
    string? Description,
    bool IsRequired,
    int SortOrder);

public sealed record OperationalChecklistTemplateResponse(
    Guid Id,
    Guid CompoundId,
    string Name,
    string? Description,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    IReadOnlyCollection<OperationalChecklistTemplateItemResponse> Items);

public sealed class StartOperationalChecklistRunRequest
{
    public Guid TemplateId { get; init; }

    public OperationalChecklistTargetType TargetType { get; init; } = OperationalChecklistTargetType.Other;

    public Guid TargetId { get; init; }
}

public sealed class CompleteOperationalChecklistRunRequest
{
    [MaxLength(1000)]
    public string? SummaryNotes { get; init; }

    public IReadOnlyCollection<CompleteOperationalChecklistRunItemRequest> Items { get; init; } = [];
}

public sealed class CompleteOperationalChecklistRunItemRequest
{
    public Guid ItemId { get; init; }

    public OperationalChecklistItemStatus Status { get; init; }

    [MaxLength(1000)]
    public string? Notes { get; init; }
}

public sealed record OperationalChecklistRunItemResponse(
    Guid Id,
    string Title,
    string? Description,
    bool IsRequired,
    int SortOrder,
    OperationalChecklistItemStatus Status,
    string? Notes);

public sealed record OperationalChecklistRunResponse(
    Guid Id,
    Guid CompoundId,
    Guid TemplateId,
    string TemplateName,
    OperationalChecklistTargetType TargetType,
    Guid TargetId,
    OperationalChecklistRunStatus Status,
    Guid? StartedByUserId,
    Guid? CompletedByUserId,
    DateTime StartedAtUtc,
    DateTime? CompletedAtUtc,
    string? SummaryNotes,
    int RequiredItemCount,
    int FailedRequiredItemCount,
    IReadOnlyCollection<OperationalChecklistRunItemResponse> Items);

public sealed class MaintenanceReliabilitySummaryQuery
{
    public Guid? CompoundId { get; init; }
}

public sealed record MaintenanceSlaRefreshResponse(
    Guid? CompoundId,
    int ResponseBreachedCount,
    int ResolutionBreachedCount,
    int UpdatedCount);

public sealed record MaintenanceReliabilitySummaryResponse(
    Guid? CompoundId,
    int ActiveAssetCount,
    int AssetsOutOfServiceCount,
    int ActiveSlaPolicyCount,
    int PreventivePlansDueCount,
    int OpenChecklistRunCount,
    int ResponseBreachedWorkOrderCount,
    int ResolutionBreachedWorkOrderCount);


public sealed class MaintenanceReliabilityDashboardQuery
{
    public Guid? CompoundId { get; init; }
    public int DueWithinDays { get; init; } = 14;
}

public sealed class MaintenanceAssetReliabilityQuery
{
    public DateTime? FromUtc { get; init; }
    public DateTime? ToUtc { get; init; }
}

public sealed class PreventiveMaintenanceDueQueueQuery : PaginationQuery
{
    public Guid? CompoundId { get; init; }
    public Guid? MaintenanceAssetId { get; init; }
    public int DueWithinDays { get; init; } = 14;
    public bool IncludeOverdue { get; init; } = true;
}

public sealed class MaintenanceSlaEscalationQueueQuery : PaginationQuery
{
    public Guid? CompoundId { get; init; }
    public MaintenanceSlaStatus? Status { get; init; }
    public WorkOrderPriority? MinimumPriority { get; init; }
    public bool OpenOnly { get; init; } = true;
}

public sealed class VendorPerformanceQuery : PaginationQuery
{
    public Guid? CompoundId { get; init; }
    public Guid? VendorId { get; init; }
    public DateTime? FromUtc { get; init; }
    public DateTime? ToUtc { get; init; }
}

public sealed record PreventiveMaintenanceDueQueueItemResponse(
    Guid PlanId,
    Guid CompoundId,
    Guid MaintenanceAssetId,
    string MaintenanceAssetName,
    string MaintenanceAssetCode,
    MaintenanceAssetStatus AssetStatus,
    string Title,
    PreventiveMaintenanceCadence Cadence,
    WorkOrderPriority Priority,
    DateTime NextDueAtUtc,
    int DaysUntilDue,
    bool IsOverdue,
    string RiskLevel,
    Guid? AssignedStaffMemberId,
    string? AssignedStaffMemberName,
    Guid? AssignedVendorId,
    string? AssignedVendorName,
    DateTime? LastGeneratedAtUtc,
    string RecommendedAction);

public sealed record MaintenanceSlaEscalationQueueItemResponse(
    Guid WorkOrderId,
    Guid CompoundId,
    string Title,
    WorkOrderPriority Priority,
    WorkOrderStatus Status,
    WorkOrderSourceType SourceType,
    Guid? MaintenanceAssetId,
    string? MaintenanceAssetName,
    Guid? AssignedStaffMemberId,
    string? AssignedStaffMemberName,
    Guid? AssignedVendorId,
    string? AssignedVendorName,
    MaintenanceSlaStatus SlaStatus,
    DateTime? ResponseDueAtUtc,
    DateTime? ResolutionDueAtUtc,
    DateTime? FirstRespondedAtUtc,
    DateTime? SlaBreachedAtUtc,
    int? ResponseOverdueMinutes,
    int? ResolutionOverdueMinutes,
    string EscalationLevel,
    string RecommendedAction);

public sealed record VendorPerformanceItemResponse(
    Guid VendorId,
    string VendorName,
    VendorServiceType ServiceType,
    VendorStatus Status,
    int AssignedWorkOrderCount,
    int OpenWorkOrderCount,
    int CompletedWorkOrderCount,
    int CancelledWorkOrderCount,
    int SlaBreachedWorkOrderCount,
    decimal TotalCost,
    double? AverageResolutionHours,
    decimal ReliabilityScore,
    string RiskLevel,
    string RecommendedAction);

public sealed record MaintenanceAssetReliabilityProfileResponse(
    Guid AssetId,
    Guid CompoundId,
    string AssetName,
    string AssetCode,
    MaintenanceAssetType AssetType,
    MaintenanceAssetStatus Status,
    DateTime? LastServiceAtUtc,
    DateTime? NextServiceDueAtUtc,
    int WorkOrderCount,
    int OpenWorkOrderCount,
    int CompletedWorkOrderCount,
    int SlaBreachedWorkOrderCount,
    int OverduePreventivePlanCount,
    decimal TotalMaintenanceCost,
    double? AverageResolutionHours,
    string ReliabilityBand,
    string RecommendedAction);

public sealed record MaintenanceReliabilityActionItemResponse(
    string Category,
    string Severity,
    Guid? RelatedEntityId,
    string Title,
    string RecommendedAction);

public sealed record MaintenanceReliabilityDashboardResponse(
    Guid? CompoundId,
    int ActiveAssetCount,
    int OutOfServiceAssetCount,
    int OverduePreventivePlanCount,
    int DueSoonPreventivePlanCount,
    int OpenWorkOrderCount,
    int SlaBreachedWorkOrderCount,
    int EmergencyWorkOrderCount,
    int ActiveVendorCount,
    int VendorAtRiskCount,
    int LowStockItemCount,
    decimal OpenWorkOrderEstimatedCost,
    decimal CompletedWorkOrderActualCost,
    IReadOnlyCollection<PreventiveMaintenanceDueQueueItemResponse> TopPreventiveMaintenanceRisks,
    IReadOnlyCollection<MaintenanceSlaEscalationQueueItemResponse> TopSlaEscalations,
    IReadOnlyCollection<VendorPerformanceItemResponse> TopVendorRisks,
    IReadOnlyCollection<MaintenanceReliabilityActionItemResponse> ActionItems);
