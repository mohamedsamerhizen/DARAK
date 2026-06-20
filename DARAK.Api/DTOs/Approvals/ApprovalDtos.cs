using System.ComponentModel.DataAnnotations;
using DARAK.Api.Validation;
using DARAK.Api.DTOs.Common;
using DARAK.Api.Enums;

namespace DARAK.Api.DTOs.Approvals;

public sealed class CreateApprovalRequestRequest
{
    [Required]
    [NotEmptyGuid]
    public Guid CompoundId { get; init; }

    [Required]
    public ApprovalActionType ActionType { get; init; }

    public ApprovalEntityType EntityType { get; init; } = ApprovalEntityType.None;

    public Guid? EntityId { get; init; }

    public ApprovalPriority? Priority { get; init; }

    [Required]
    [MaxLength(1000)]
    public string Reason { get; init; } = string.Empty;

    [MaxLength(8000)]
    public string? RequestPayloadJson { get; init; }

    public DateTime? DueAtUtc { get; init; }
}

public sealed class ApprovalSearchQuery : PaginationQuery
{
    public Guid? CompoundId { get; init; }

    public Guid? RequestedByUserId { get; init; }

    public Guid? LastDecisionByUserId { get; init; }

    public ApprovalActionType? ActionType { get; init; }

    public ApprovalEntityType? EntityType { get; init; }

    public Guid? EntityId { get; init; }

    public ApprovalStatus? Status { get; init; }

    public ApprovalPriority? Priority { get; init; }

    public DateTime? CreatedFromUtc { get; init; }

    public DateTime? CreatedToUtc { get; init; }

    public bool? IsOverdue { get; init; }

    [MaxLength(100)]
    public string? SearchTerm { get; init; }
}

public sealed class ApprovalDecisionRequest
{
    [MaxLength(1000)]
    public string? Reason { get; init; }
}

public sealed class MarkApprovalExecutedRequest
{
    [MaxLength(1000)]
    public string? Notes { get; init; }
}

public sealed record ApprovalRequestResponse(
    Guid Id,
    Guid CompoundId,
    Guid RequestedByUserId,
    Guid? LastDecisionByUserId,
    ApprovalActionType ActionType,
    ApprovalEntityType EntityType,
    Guid? EntityId,
    ApprovalStatus Status,
    ApprovalPriority Priority,
    ApprovalExecutionStatus ExecutionStatus,
    string Reason,
    string? DecisionReason,
    string? ExecutionNotes,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    DateTime? DueAtUtc,
    DateTime? DecidedAtUtc,
    DateTime? CancelledAtUtc,
    DateTime? ExecutedAtUtc,
    Guid? ExecutedByUserId,
    bool IsOverdue);

public sealed record ApprovalRequestDetailsResponse(
    Guid Id,
    Guid CompoundId,
    Guid RequestedByUserId,
    Guid? LastDecisionByUserId,
    ApprovalActionType ActionType,
    ApprovalEntityType EntityType,
    Guid? EntityId,
    ApprovalStatus Status,
    ApprovalPriority Priority,
    ApprovalExecutionStatus ExecutionStatus,
    string Reason,
    string? RequestPayloadJson,
    string? DecisionReason,
    string? ExecutionNotes,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    DateTime? DueAtUtc,
    DateTime? DecidedAtUtc,
    DateTime? CancelledAtUtc,
    DateTime? ExecutedAtUtc,
    Guid? ExecutedByUserId,
    bool IsOverdue,
    IReadOnlyCollection<ApprovalDecisionResponse> Decisions);

public sealed record ApprovalDecisionResponse(
    Guid Id,
    Guid ApprovalRequestId,
    Guid DecidedByUserId,
    ApprovalDecisionType DecisionType,
    string Reason,
    DateTime CreatedAtUtc);

public sealed record ApprovalDashboardResponse(
    int PendingCount,
    int ApprovedCount,
    int RejectedCount,
    int CancelledCount,
    int ExecutedCount,
    int OverdueCount,
    int HighPriorityPendingCount,
    DateTime? OldestPendingCreatedAtUtc);

public sealed record ApprovalPolicyResponse(
    Guid Id,
    Guid? CompoundId,
    ApprovalActionType ActionType,
    bool IsEnabled,
    bool AllowSelfApproval,
    ApprovalPriority DefaultPriority,
    int ExpireAfterHours,
    string RequiredApproverRoles,
    string? Description,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);
