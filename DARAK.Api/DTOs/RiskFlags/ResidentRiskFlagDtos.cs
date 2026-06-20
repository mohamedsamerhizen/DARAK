using DARAK.Api.DTOs.Common;
using DARAK.Api.Enums;
using System.ComponentModel.DataAnnotations;

namespace DARAK.Api.DTOs.RiskFlags;

public sealed class CreateResidentRiskFlagRequest
{
    public Guid CompoundId { get; init; }

    public Guid ResidentProfileId { get; init; }

    public Guid? PropertyUnitId { get; init; }

    public Guid? AssignedToUserId { get; init; }

    public ResidentRiskFlagType FlagType { get; init; } = ResidentRiskFlagType.Other;

    public ResidentRiskFlagSeverity Severity { get; init; } = ResidentRiskFlagSeverity.Medium;

    public ResidentRiskFlagSource Source { get; init; } = ResidentRiskFlagSource.Manual;

    public ResidentRiskFlagSourceEntityType SourceEntityType { get; init; } = ResidentRiskFlagSourceEntityType.None;

    public Guid? SourceEntityId { get; init; }

    public string Title { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string? RecommendedAction { get; init; }

    public string? InternalNotes { get; init; }

    public string? MetadataJson { get; init; }

    public bool RequiresSupervisorReview { get; init; }

    public DateTime? NextReviewAtUtc { get; init; }

    public DateTime? ExpiresAtUtc { get; init; }
}

public sealed class ResidentRiskFlagSearchQuery : PaginationQuery
{
    public Guid? CompoundId { get; init; }

    public Guid? ResidentProfileId { get; init; }

    public Guid? PropertyUnitId { get; init; }

    public Guid? AssignedToUserId { get; init; }

    public ResidentRiskFlagType? FlagType { get; init; }

    public ResidentRiskFlagSeverity? Severity { get; init; }

    public ResidentRiskFlagStatus? Status { get; init; }

    public ResidentRiskFlagSource? Source { get; init; }

    public bool? RequiresSupervisorReview { get; init; }

    public bool? OverdueReviewOnly { get; init; }

    public bool? ActiveOnly { get; init; }

    [MaxLength(200)]
    public string? SearchTerm { get; init; }
}

public sealed class AssignResidentRiskFlagRequest
{
    public Guid? AssignedToUserId { get; init; }

    public string? Notes { get; init; }
}

public sealed class ChangeResidentRiskFlagSeverityRequest
{
    public ResidentRiskFlagSeverity Severity { get; init; }

    public string Notes { get; init; } = string.Empty;
}

public sealed class ReviewResidentRiskFlagRequest
{
    public string Notes { get; init; } = string.Empty;

    public DateTime? NextReviewAtUtc { get; init; }
}

public sealed class CloseResidentRiskFlagRequest
{
    public string Reason { get; init; } = string.Empty;
}

public sealed class AddResidentRiskFlagNoteRequest
{
    public string Notes { get; init; } = string.Empty;
}

public sealed record ResidentRiskFlagResponse(
    Guid Id,
    Guid CompoundId,
    Guid ResidentProfileId,
    Guid? PropertyUnitId,
    Guid CreatedByUserId,
    Guid? AssignedToUserId,
    ResidentRiskFlagType FlagType,
    ResidentRiskFlagSeverity Severity,
    ResidentRiskFlagStatus Status,
    ResidentRiskFlagSource Source,
    ResidentRiskFlagSourceEntityType SourceEntityType,
    Guid? SourceEntityId,
    string Title,
    string Description,
    string? RecommendedAction,
    bool HasInternalNotes,
    bool RequiresSupervisorReview,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    DateTime? AssignedAtUtc,
    DateTime? LastReviewedAtUtc,
    DateTime? NextReviewAtUtc,
    DateTime? ExpiresAtUtc,
    DateTime? ResolvedAtUtc,
    DateTime? DismissedAtUtc);

public sealed record ResidentRiskFlagDetailsResponse(
    Guid Id,
    Guid CompoundId,
    Guid ResidentProfileId,
    Guid? PropertyUnitId,
    Guid CreatedByUserId,
    Guid? AssignedToUserId,
    Guid? LastReviewedByUserId,
    Guid? ClosedByUserId,
    ResidentRiskFlagType FlagType,
    ResidentRiskFlagSeverity Severity,
    ResidentRiskFlagStatus Status,
    ResidentRiskFlagSource Source,
    ResidentRiskFlagSourceEntityType SourceEntityType,
    Guid? SourceEntityId,
    string Title,
    string Description,
    string? RecommendedAction,
    string? InternalNotes,
    string? ResolutionNotes,
    string? DismissalReason,
    string? MetadataJson,
    bool RequiresSupervisorReview,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    DateTime? AssignedAtUtc,
    DateTime? LastReviewedAtUtc,
    DateTime? NextReviewAtUtc,
    DateTime? ExpiresAtUtc,
    DateTime? ResolvedAtUtc,
    DateTime? DismissedAtUtc,
    IReadOnlyCollection<ResidentRiskFlagActionResponse> Actions);

public sealed record ResidentRiskFlagActionResponse(
    Guid Id,
    Guid ActorUserId,
    ResidentRiskFlagActionType ActionType,
    ResidentRiskFlagStatus? PreviousStatus,
    ResidentRiskFlagStatus? NewStatus,
    ResidentRiskFlagSeverity? PreviousSeverity,
    ResidentRiskFlagSeverity? NewSeverity,
    string Notes,
    DateTime CreatedAtUtc);

public sealed record ResidentRiskFlagDashboardResponse(
    int ActiveCount,
    int MonitoringCount,
    int ResolvedCount,
    int DismissedCount,
    int ExpiredCount,
    int HighOrCriticalActiveCount,
    int CriticalActiveCount,
    int OverdueReviewCount,
    int ExpiringSoonCount,
    int UnassignedActiveCount,
    DateTime? OldestActiveCreatedAtUtc,
    IReadOnlyCollection<ResidentRiskFlagSeverityCountResponse> OpenBySeverity);

public sealed record ResidentRiskFlagSeverityCountResponse(
    ResidentRiskFlagSeverity Severity,
    int Count);
