using System.ComponentModel.DataAnnotations;
using DARAK.Api.DTOs.Common;
using DARAK.Api.Enums;

namespace DARAK.Api.DTOs.Operations;

public sealed record StaffMemberResponse(
    Guid Id,
    string FullName,
    string PhoneNumber,
    string? Email,
    StaffType StaffType,
    StaffStatus Status,
    string? Specialization,
    string? NationalId,
    string? Notes,
    Guid? UserId,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public sealed record ServiceVendorResponse(
    Guid Id,
    string Name,
    string? ContactPersonName,
    string PhoneNumber,
    string? Email,
    VendorServiceType ServiceType,
    VendorStatus Status,
    string? Address,
    string? Notes,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public sealed record WorkOrderCostItemResponse(
    Guid Id,
    Guid WorkOrderId,
    string Description,
    WorkOrderCostType CostType,
    decimal Amount,
    DateTime CreatedAtUtc);

public sealed record WorkOrderStatusHistoryResponse(
    Guid Id,
    Guid WorkOrderId,
    WorkOrderStatus? OldStatus,
    WorkOrderStatus NewStatus,
    Guid? ChangedByUserId,
    string? ChangedByUserName,
    string? Note,
    DateTime CreatedAtUtc);

public sealed record WorkOrderRatingResponse(
    Guid Id,
    Guid WorkOrderId,
    Guid UserId,
    string? UserName,
    int Rating,
    string? Comment,
    DateTime CreatedAtUtc);

public sealed record WorkOrderResponse(
    Guid Id,
    string Title,
    string Description,
    WorkOrderSourceType SourceType,
    Guid? SourceEntityId,
    Guid CompoundId,
    WorkOrderPriority Priority,
    WorkOrderStatus Status,
    Guid? AssignedStaffMemberId,
    string? AssignedStaffMemberName,
    Guid? AssignedVendorId,
    string? AssignedVendorName,
    Guid? CreatedByUserId,
    string? CreatedByUserName,
    Guid? PropertyUnitId,
    string? UnitNumber,
    DateTime? ScheduledAtUtc,
    DateTime? StartedAtUtc,
    DateTime? CompletedAtUtc,
    DateTime? CancelledAtUtc,
    DateTime? DueAtUtc,
    decimal? EstimatedCost,
    decimal? ActualCost,
    string? CompletionNotes,
    string? CancellationReason,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    bool IsOverdue,
    decimal? AverageRating,
    int RatingCount,
    IReadOnlyCollection<WorkOrderCostItemResponse> CostItems);

public sealed class StaffMemberQueryRequest : PaginationQuery
{
    public StaffType? StaffType { get; init; }

    public StaffStatus? Status { get; init; }

    [MaxLength(200)]
    public string? SearchTerm { get; init; }
}

public sealed class ServiceVendorQueryRequest : PaginationQuery
{
    public VendorServiceType? ServiceType { get; init; }

    public VendorStatus? Status { get; init; }

    [MaxLength(200)]
    public string? SearchTerm { get; init; }
}

public sealed class WorkOrderQueryRequest : PaginationQuery
{
    public WorkOrderStatus? Status { get; init; }

    public WorkOrderPriority? Priority { get; init; }

    public WorkOrderSourceType? SourceType { get; init; }

    public Guid? SourceEntityId { get; init; }

    public Guid? CompoundId { get; init; }

    public Guid? AssignedStaffMemberId { get; init; }

    public Guid? AssignedVendorId { get; init; }

    public Guid? PropertyUnitId { get; init; }

    public DateTime? DueFromUtc { get; init; }

    public DateTime? DueToUtc { get; init; }

    [MaxLength(200)]
    public string? SearchTerm { get; init; }
}

public sealed class WorkOrderStatusHistoryQueryRequest : PaginationQuery
{
}

public sealed class CreateStaffMemberRequest
{
    [Required]
    [MaxLength(150)]
    public string FullName { get; init; } = string.Empty;

    [Required]
    [MaxLength(30)]
    public string PhoneNumber { get; init; } = string.Empty;

    [MaxLength(256)]
    public string? Email { get; init; }

    public StaffType StaffType { get; init; } = StaffType.Other;

    public StaffStatus Status { get; init; } = StaffStatus.Active;

    [MaxLength(150)]
    public string? Specialization { get; init; }

    [MaxLength(50)]
    public string? NationalId { get; init; }

    [MaxLength(1000)]
    public string? Notes { get; init; }

    public Guid? UserId { get; init; }
}

public sealed class UpdateStaffMemberRequest
{
    [Required]
    [MaxLength(150)]
    public string FullName { get; init; } = string.Empty;

    [Required]
    [MaxLength(30)]
    public string PhoneNumber { get; init; } = string.Empty;

    [MaxLength(256)]
    public string? Email { get; init; }

    public StaffType StaffType { get; init; } = StaffType.Other;

    public StaffStatus Status { get; init; } = StaffStatus.Active;

    [MaxLength(150)]
    public string? Specialization { get; init; }

    [MaxLength(50)]
    public string? NationalId { get; init; }

    [MaxLength(1000)]
    public string? Notes { get; init; }

    public Guid? UserId { get; init; }
}

public sealed class CreateServiceVendorRequest
{
    [Required]
    [MaxLength(150)]
    public string Name { get; init; } = string.Empty;

    [MaxLength(150)]
    public string? ContactPersonName { get; init; }

    [Required]
    [MaxLength(30)]
    public string PhoneNumber { get; init; } = string.Empty;

    [MaxLength(256)]
    public string? Email { get; init; }

    public VendorServiceType ServiceType { get; init; } = VendorServiceType.Other;

    public VendorStatus Status { get; init; } = VendorStatus.Active;

    [MaxLength(300)]
    public string? Address { get; init; }

    [MaxLength(1000)]
    public string? Notes { get; init; }
}

public sealed class UpdateServiceVendorRequest
{
    [Required]
    [MaxLength(150)]
    public string Name { get; init; } = string.Empty;

    [MaxLength(150)]
    public string? ContactPersonName { get; init; }

    [Required]
    [MaxLength(30)]
    public string PhoneNumber { get; init; } = string.Empty;

    [MaxLength(256)]
    public string? Email { get; init; }

    public VendorServiceType ServiceType { get; init; } = VendorServiceType.Other;

    public VendorStatus Status { get; init; } = VendorStatus.Active;

    [MaxLength(300)]
    public string? Address { get; init; }

    [MaxLength(1000)]
    public string? Notes { get; init; }
}

public sealed class CreateWorkOrderRequest
{
    [Required]
    [MaxLength(150)]
    public string Title { get; init; } = string.Empty;

    [Required]
    [MaxLength(2000)]
    public string Description { get; init; } = string.Empty;

    public WorkOrderSourceType SourceType { get; init; } = WorkOrderSourceType.Manual;

    public Guid? SourceEntityId { get; init; }

    public Guid? CompoundId { get; init; }

    public WorkOrderPriority Priority { get; init; } = WorkOrderPriority.Normal;

    public Guid? AssignedStaffMemberId { get; init; }

    public Guid? AssignedVendorId { get; init; }

    public Guid? PropertyUnitId { get; init; }

    public DateTime? ScheduledAtUtc { get; init; }

    public DateTime? DueAtUtc { get; init; }

    [Range(0, double.MaxValue)]
    public decimal? EstimatedCost { get; init; }
}

public sealed class UpdateWorkOrderRequest
{
    [Required]
    [MaxLength(150)]
    public string Title { get; init; } = string.Empty;

    [Required]
    [MaxLength(2000)]
    public string Description { get; init; } = string.Empty;

    public WorkOrderSourceType SourceType { get; init; } = WorkOrderSourceType.Manual;

    public Guid? SourceEntityId { get; init; }

    public Guid? CompoundId { get; init; }

    public WorkOrderPriority Priority { get; init; } = WorkOrderPriority.Normal;

    public Guid? PropertyUnitId { get; init; }

    public DateTime? DueAtUtc { get; init; }

    [Range(0, double.MaxValue)]
    public decimal? EstimatedCost { get; init; }
}

public sealed class AssignWorkOrderToStaffRequest
{
    public Guid StaffMemberId { get; init; }

    [MaxLength(1000)]
    public string? Note { get; init; }
}

public sealed class AssignWorkOrderToVendorRequest
{
    public Guid VendorId { get; init; }

    [MaxLength(1000)]
    public string? Note { get; init; }
}

public sealed class ScheduleWorkOrderRequest
{
    public DateTime ScheduledAtUtc { get; init; }

    public DateTime? DueAtUtc { get; init; }

    [MaxLength(1000)]
    public string? Note { get; init; }
}

public sealed class StartWorkOrderRequest
{
    [MaxLength(1000)]
    public string? Note { get; init; }
}

public sealed class CompleteWorkOrderRequest
{
    [MaxLength(2000)]
    public string? CompletionNotes { get; init; }

    [Range(0, double.MaxValue)]
    public decimal? ActualCost { get; init; }
}

public sealed class CancelWorkOrderRequest
{
    [Required]
    [MaxLength(500)]
    public string Reason { get; init; } = string.Empty;
}

public sealed class AddWorkOrderCostItemRequest
{
    [Required]
    [MaxLength(300)]
    public string Description { get; init; } = string.Empty;

    public WorkOrderCostType CostType { get; init; } = WorkOrderCostType.Other;

    [Range(0, double.MaxValue)]
    public decimal Amount { get; init; }
}

public sealed class CreateWorkOrderRatingRequest
{
    [Range(1, 5)]
    public int Rating { get; init; }

    [MaxLength(1000)]
    public string? Comment { get; init; }
}
