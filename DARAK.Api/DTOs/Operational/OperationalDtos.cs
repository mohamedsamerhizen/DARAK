using System.ComponentModel.DataAnnotations;
using DARAK.Api.DTOs.Common;
using DARAK.Api.Enums;

namespace DARAK.Api.DTOs.Operational;

public sealed class OperationalCommandCenterQuery
{
    public Guid? CompoundId { get; init; }
}

public sealed record OperationalCommandCenterResponse(
    Guid? CompoundId,
    int OpenMaintenanceRequestCount,
    int EmergencyMaintenanceRequestCount,
    int OpenComplaintCount,
    int CriticalComplaintCount,
    int OpenWorkOrderCount,
    int OverdueWorkOrderCount,
    int PendingApprovalRequestCount,
    int PendingFinancialAdjustmentCount,
    int ActiveRiskFlagCount,
    int CriticalRiskFlagCount,
    int OverdueRiskReviewCount,
    int OpenOperationalTaskCount,
    int OverdueOperationalTaskCount,
    int SlaBreachCount,
    int CompoundHealthScore,
    IReadOnlyCollection<OperationalPriorityItemResponse> PriorityItems,
    DateTime GeneratedAtUtc);

public sealed record OperationalPriorityItemResponse(
    string SourceType,
    Guid SourceId,
    Guid CompoundId,
    string Title,
    string PriorityLabel,
    DateTime CreatedAtUtc,
    DateTime? DueAtUtc,
    int AgeHours,
    string Recommendation);

public sealed class SlaBreachQuery : PaginationQuery
{
    public Guid? CompoundId { get; init; }
}

public sealed record SlaBreachResponse(
    string SourceType,
    Guid SourceId,
    Guid CompoundId,
    string Title,
    string SeverityLabel,
    DateTime CreatedAtUtc,
    DateTime? DueAtUtc,
    int AgeHours,
    int BreachHours,
    string Recommendation);

public sealed class StaffPerformanceQuery
{
    public Guid? CompoundId { get; init; }

    public DateTime? FromUtc { get; init; }

    public DateTime? ToUtc { get; init; }
}

public sealed record StaffPerformanceResponse(
    Guid? CompoundId,
    DateTime FromUtc,
    DateTime ToUtc,
    IReadOnlyCollection<StaffWorkloadResponse> StaffMembers,
    DateTime GeneratedAtUtc);

public sealed record StaffWorkloadResponse(
    Guid StaffMemberId,
    string FullName,
    Guid CompoundId,
    int AssignedWorkOrderCount,
    int CompletedWorkOrderCount,
    int OverdueWorkOrderCount,
    decimal? AverageRating,
    decimal ActualCostTotal);

public sealed class CompoundHealthQuery
{
    public Guid? CompoundId { get; init; }
}

public sealed record CompoundHealthResponse(
    Guid? CompoundId,
    int HealthScore,
    string HealthStatus,
    int OpenMaintenanceRequestCount,
    int OpenComplaintCount,
    int OpenWorkOrderCount,
    int SlaBreachCount,
    int CriticalRiskFlagCount,
    int OverdueFinancialItemCount,
    int PendingApprovalRequestCount,
    IReadOnlyCollection<CompoundHealthFactorResponse> Factors,
    DateTime GeneratedAtUtc);

public sealed record CompoundHealthFactorResponse(
    string Area,
    int Penalty,
    string Reason);

public sealed class OperationalTaskSearchQuery : PaginationQuery
{
    public Guid? CompoundId { get; init; }

    public OperationalTaskStatus? Status { get; init; }

    public OperationalTaskPriority? Priority { get; init; }

    public OperationalTaskType? TaskType { get; init; }

    public Guid? AssignedToUserId { get; init; }

    public bool? IsOverdue { get; init; }

    [MaxLength(100)]
    public string? SearchTerm { get; init; }
}

public sealed class CreateOperationalTaskRequest
{
    [Required]
    public Guid CompoundId { get; init; }

    [Required]
    public OperationalTaskType TaskType { get; init; } = OperationalTaskType.General;

    [Required]
    public OperationalTaskPriority Priority { get; init; } = OperationalTaskPriority.Normal;

    [Required]
    [MaxLength(160)]
    public string Title { get; init; } = string.Empty;

    [Required]
    [MaxLength(2000)]
    public string Description { get; init; } = string.Empty;

    public AuditEntityType? RelatedEntityType { get; init; }

    public Guid? RelatedEntityId { get; init; }

    public Guid? AssignedToUserId { get; init; }

    public DateTime? DueAtUtc { get; init; }
}

public sealed class CompleteOperationalTaskRequest
{
    [Required]
    [MaxLength(1000)]
    public string CompletionNotes { get; init; } = string.Empty;
}

public sealed class CancelOperationalTaskRequest
{
    [Required]
    [MaxLength(1000)]
    public string Reason { get; init; } = string.Empty;
}

public sealed record OperationalTaskResponse(
    Guid Id,
    Guid CompoundId,
    OperationalTaskType TaskType,
    OperationalTaskPriority Priority,
    OperationalTaskStatus Status,
    string Title,
    string Description,
    AuditEntityType? RelatedEntityType,
    Guid? RelatedEntityId,
    Guid? AssignedToUserId,
    Guid CreatedByUserId,
    Guid? CompletedByUserId,
    Guid? CancelledByUserId,
    DateTime? DueAtUtc,
    bool IsOverdue,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    DateTime? CompletedAtUtc,
    DateTime? CancelledAtUtc,
    string? CompletionNotes,
    string? CancellationReason);
