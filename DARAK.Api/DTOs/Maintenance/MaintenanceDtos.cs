using System.ComponentModel.DataAnnotations;
using DARAK.Api.DTOs.Common;
using DARAK.Api.Enums;

namespace DARAK.Api.DTOs.Maintenance;

public sealed record MaintenanceRequestResponse(
    Guid Id,
    Guid ResidentProfileId,
    string ResidentName,
    Guid CompoundId,
    string CompoundName,
    Guid PropertyUnitId,
    string UnitNumber,
    Guid? AssignedToUserId,
    string? AssignedToUserName,
    string Title,
    string Description,
    MaintenancePriority Priority,
    MaintenanceStatus Status,
    decimal? CostEstimate,
    decimal? ActualCost,
    string? ResolutionNotes,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? AssignedAt,
    DateTime? StartedAt,
    DateTime? ResolvedAt,
    DateTime? ClosedAt,
    DateTime? CancelledAt);

public sealed record MaintenanceStatusHistoryResponse(
    Guid Id,
    Guid MaintenanceRequestId,
    MaintenanceStatus? OldStatus,
    MaintenanceStatus NewStatus,
    Guid? ChangedByUserId,
    string? ChangedByUserName,
    string? Notes,
    DateTime CreatedAt);

public sealed class MaintenanceRequestSearchQuery : PaginationQuery
{
    public Guid? CompoundId { get; init; }

    public Guid? ResidentProfileId { get; init; }

    public Guid? PropertyUnitId { get; init; }

    public Guid? AssignedToUserId { get; init; }

    public MaintenanceStatus? Status { get; init; }

    public MaintenancePriority? Priority { get; init; }

    [MaxLength(200)]
    public string? SearchTerm { get; init; }
}

public sealed class MaintenanceStatusHistorySearchQuery : PaginationQuery
{
}

public sealed class CreateMaintenanceRequestRequest
{
    public Guid PropertyUnitId { get; init; }

    [Required]
    [MaxLength(150)]
    public string Title { get; init; } = string.Empty;

    [Required]
    [MaxLength(2000)]
    public string Description { get; init; } = string.Empty;

    public MaintenancePriority Priority { get; init; } = MaintenancePriority.Medium;
}

public sealed class UpdateMaintenanceRequestRequest
{
    [Required]
    [MaxLength(150)]
    public string Title { get; init; } = string.Empty;

    [Required]
    [MaxLength(2000)]
    public string Description { get; init; } = string.Empty;

    public MaintenancePriority Priority { get; init; } = MaintenancePriority.Medium;
}

public sealed class AssignMaintenanceRequestRequest
{
    public Guid AssignedToUserId { get; init; }

    [Range(0, double.MaxValue)]
    public decimal? CostEstimate { get; init; }

    [MaxLength(1000)]
    public string? Notes { get; init; }
}

public sealed class ResolveMaintenanceRequestRequest
{
    [Required]
    [MaxLength(2000)]
    public string ResolutionNotes { get; init; } = string.Empty;

    [Range(0, double.MaxValue)]
    public decimal? ActualCost { get; init; }
}

public sealed class MaintenanceStatusChangeRequest
{
    [MaxLength(1000)]
    public string? Notes { get; init; }
}
