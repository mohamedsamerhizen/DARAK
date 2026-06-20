namespace DARAK.Api.DTOs.Operational;

public sealed class ExecutiveIntelligenceQuery
{
    public Guid? CompoundId { get; init; }

    public int ItemLimit { get; init; } = 20;
}

public sealed record ExecutiveDailySummaryResponse(
    Guid? CompoundId,
    int ExecutiveScore,
    string ExecutiveStatus,
    int CriticalSignalCount,
    int AttentionSignalCount,
    IReadOnlyCollection<ExecutiveDomainSignalResponse> DomainSignals,
    IReadOnlyCollection<ExecutiveCriticalActionResponse> CriticalActions,
    IReadOnlyCollection<ExecutiveDecisionBriefResponse> DecisionBriefs,
    DateTime GeneratedAtUtc);

public sealed record ExecutiveDomainSignalResponse(
    string Domain,
    string Label,
    int CriticalCount,
    int AttentionCount,
    int Score,
    string Status,
    string LeadSignal,
    string RecommendedAction);

public sealed record ExecutiveCriticalActionResponse(
    string Domain,
    string SourceType,
    Guid SourceId,
    Guid CompoundId,
    string Title,
    string Severity,
    string ActionOwner,
    string RecommendedAction,
    DateTime CreatedAtUtc,
    DateTime? DueAtUtc);

public sealed record ExecutiveDecisionBriefResponse(
    string Area,
    string Decision,
    string Impact,
    string NextStep,
    int PriorityRank);

public sealed record DomainSignalBoardResponse(
    Guid? CompoundId,
    int ExecutiveScore,
    string ExecutiveStatus,
    IReadOnlyCollection<ExecutiveDomainSignalResponse> DomainSignals,
    DateTime GeneratedAtUtc);

public sealed record CriticalActionQueueResponse(
    Guid? CompoundId,
    int TotalCount,
    IReadOnlyCollection<ExecutiveCriticalActionResponse> Items,
    DateTime GeneratedAtUtc);
