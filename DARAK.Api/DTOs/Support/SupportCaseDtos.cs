using System.ComponentModel.DataAnnotations;
using DARAK.Api.DTOs.Common;
using DARAK.Api.Enums;

namespace DARAK.Api.DTOs.Support;

public sealed class SupportCaseSearchQuery : PaginationQuery
{
    public Guid? CompoundId { get; init; }
    public Guid? ResidentProfileId { get; init; }
    public Guid? AssignedToUserId { get; init; }
    public SupportCaseStatus? Status { get; init; }
    public SupportCasePriority? Priority { get; init; }
    public SupportCaseCategory? Category { get; init; }
    public bool? OverdueOnly { get; init; }
    [MaxLength(200)] public string? SearchTerm { get; init; }
}

public sealed class CreateSupportCaseRequest
{
    public Guid CompoundId { get; init; }
    public Guid? ResidentProfileId { get; init; }
    public Guid? PropertyUnitId { get; init; }
    public SupportCaseSourceType SourceType { get; init; } = SupportCaseSourceType.Manual;
    public Guid? SourceEntityId { get; init; }
    public SupportCaseCategory Category { get; init; } = SupportCaseCategory.General;
    public SupportCasePriority Priority { get; init; } = SupportCasePriority.Normal;
    [Required, MaxLength(200)] public string Title { get; init; } = string.Empty;
    [Required, MaxLength(4000)] public string Description { get; init; } = string.Empty;
    public DateTime? DueAtUtc { get; init; }
}

public sealed class AssignSupportCaseRequest
{
    public Guid AssignedToUserId { get; init; }
    [MaxLength(1000)] public string? Note { get; init; }
}

public sealed class EscalateSupportCaseRequest
{
    public SupportCasePriority Priority { get; init; } = SupportCasePriority.High;
    [Required, MaxLength(1000)] public string Reason { get; init; } = string.Empty;
}

public sealed class ResolveSupportCaseRequest
{
    [Required, MaxLength(2000)] public string ResolutionSummary { get; init; } = string.Empty;
    public bool CloseImmediately { get; init; }
}

public sealed class ReopenSupportCaseRequest
{
    [Required, MaxLength(1000)] public string Reason { get; init; } = string.Empty;
}

public sealed class AddSupportCaseNoteRequest
{
    [Required, MaxLength(2000)] public string Note { get; init; } = string.Empty;
}

public sealed class SupportDashboardQuery
{
    public Guid? CompoundId { get; init; }
}

public sealed record SupportCaseResponse(
    Guid Id,
    Guid CompoundId,
    Guid? ResidentProfileId,
    Guid? PropertyUnitId,
    Guid? AssignedToUserId,
    SupportCaseSourceType SourceType,
    Guid? SourceEntityId,
    SupportCaseCategory Category,
    SupportCasePriority Priority,
    SupportCaseStatus Status,
    string Title,
    string Description,
    string? ResolutionSummary,
    int ReopenCount,
    DateTime DueAtUtc,
    bool IsOverdue,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    DateTime? AssignedAtUtc,
    DateTime? EscalatedAtUtc,
    DateTime? ResolvedAtUtc,
    DateTime? ClosedAtUtc);

public sealed record SupportCaseDetailsResponse(
    Guid Id,
    Guid CompoundId,
    Guid? ResidentProfileId,
    Guid? PropertyUnitId,
    Guid? AssignedToUserId,
    Guid? CreatedByUserId,
    SupportCaseSourceType SourceType,
    Guid? SourceEntityId,
    SupportCaseCategory Category,
    SupportCasePriority Priority,
    SupportCaseStatus Status,
    string Title,
    string Description,
    string? AssignmentNote,
    string? EscalationReason,
    string? ResolutionSummary,
    int ReopenCount,
    DateTime DueAtUtc,
    bool IsOverdue,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    IReadOnlyCollection<SupportCaseEventResponse> Events);

public sealed record SupportCaseEventResponse(
    Guid Id,
    Guid? ActorUserId,
    SupportCaseEventType EventType,
    SupportCaseStatus? FromStatus,
    SupportCaseStatus? ToStatus,
    string Description,
    string? InternalNote,
    DateTime CreatedAtUtc);

public sealed record SupportDashboardResponse(
    Guid? CompoundId,
    int OpenCount,
    int AssignedCount,
    int InProgressCount,
    int EscalatedCount,
    int OverdueCount,
    int CriticalCount,
    int ResolvedTodayCount,
    int ReopenedCount,
    double ResolutionRatePercent,
    IReadOnlyCollection<SupportCasePriorityCountResponse> ByPriority,
    IReadOnlyCollection<SupportCaseCategoryCountResponse> ByCategory,
    DateTime GeneratedAtUtc);

public sealed record SupportCasePriorityCountResponse(SupportCasePriority Priority, int Count);
public sealed record SupportCaseCategoryCountResponse(SupportCaseCategory Category, int Count);

