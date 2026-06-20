namespace DARAK.Api.DTOs.System;

public sealed class ReleaseGovernanceQuery
{
    public Guid? CompoundId { get; init; }

    public int Days { get; init; } = 30;

    public int ItemLimit { get; init; } = 20;
}

public sealed record ReleaseReadinessBoardResponse(
    Guid? CompoundId,
    int ReadinessScore,
    string ReleaseStatus,
    int BlockerCount,
    int WarningCount,
    IReadOnlyCollection<ReleaseReadinessItemResponse> Items,
    IReadOnlyCollection<ReleaseGovernanceActionResponse> RecommendedActions,
    DateTime GeneratedAtUtc);

public sealed record ReleaseReadinessItemResponse(
    string Key,
    string Area,
    string Title,
    bool IsPassed,
    bool IsRequired,
    string Severity,
    string Evidence,
    string Recommendation);

public sealed record ReleaseGovernanceActionResponse(
    string Area,
    string Severity,
    string Action,
    string Owner,
    int PriorityRank);

public sealed record AuditEvidenceDashboardResponse(
    Guid? CompoundId,
    DateTime FromUtc,
    DateTime ToUtc,
    int TotalAuditEvents,
    int CriticalEvents,
    int HighEvents,
    int MediumEvents,
    int LowEvents,
    int SystemGeneratedEvents,
    int MissingCorrelationIdEvents,
    DateTime? LatestCriticalEventAtUtc,
    int EvidenceScore,
    IReadOnlyCollection<AuditEvidenceSourceModuleResponse> SourceModules,
    DateTime GeneratedAtUtc);

public sealed record AuditEvidenceSourceModuleResponse(
    string SourceModule,
    int EventCount,
    int CriticalCount,
    int HighCount);

public sealed record ComplianceExceptionQueueResponse(
    Guid? CompoundId,
    int TotalCount,
    int CriticalCount,
    int HighCount,
    IReadOnlyCollection<ComplianceExceptionItemResponse> Items,
    DateTime GeneratedAtUtc);

public sealed record ComplianceExceptionItemResponse(
    string SourceType,
    Guid SourceId,
    Guid? CompoundId,
    string Severity,
    string Title,
    string Evidence,
    string Owner,
    string RecommendedAction,
    DateTime CreatedAtUtc,
    DateTime? DueAtUtc);

public sealed record BuyerHandoffReadinessResponse(
    Guid? CompoundId,
    string HandoffStatus,
    int HandoffScore,
    int CommercialEvidenceCount,
    int OperationalEvidenceCount,
    int TechnicalEvidenceCount,
    IReadOnlyCollection<BuyerHandoffItemResponse> Items,
    IReadOnlyCollection<string> HandoffNotes,
    DateTime GeneratedAtUtc);

public sealed record BuyerHandoffItemResponse(
    string Area,
    string Title,
    bool IsReady,
    string Evidence,
    string Recommendation);

public sealed record GovernanceTimelineResponse(
    Guid? CompoundId,
    int TotalCount,
    IReadOnlyCollection<GovernanceTimelineItemResponse> Items,
    DateTime GeneratedAtUtc);

public sealed record GovernanceTimelineItemResponse(
    string EventType,
    Guid EventId,
    Guid? CompoundId,
    string Severity,
    string Title,
    string Description,
    DateTime OccurredAtUtc,
    string SourceModule);
