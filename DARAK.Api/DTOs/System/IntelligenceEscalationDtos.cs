namespace DARAK.Api.DTOs.System;

public sealed record IntelligenceEscalationDashboardResponse(
    Guid CompoundId,
    DateTime GeneratedAtUtc,
    int TotalOpenEscalations,
    int CriticalCount,
    int HighCount,
    int MediumCount,
    int LowCount,
    int FinancialEscalations,
    int CommunicationEscalations,
    int OperationsEscalations,
    int LegalEscalations,
    int NotificationEscalations,
    int AverageEscalationScore,
    IReadOnlyCollection<IntelligenceEscalationQueueItemResponse> TopEscalations,
    IReadOnlyCollection<string> ExecutiveActions);

public sealed record IntelligenceEscalationQueueResponse(
    Guid CompoundId,
    string? AreaFilter,
    string? SeverityFilter,
    int TotalItems,
    IReadOnlyCollection<IntelligenceEscalationQueueItemResponse> Items,
    DateTime GeneratedAtUtc);

public sealed record IntelligenceEscalationQueueItemResponse(
    string QueueKey,
    string Area,
    string Severity,
    string EntityType,
    Guid EntityId,
    Guid CompoundId,
    Guid? ResidentProfileId,
    Guid? PropertyUnitId,
    string Title,
    string Reason,
    string RecommendedAction,
    DateTime? DueAtUtc,
    int AgeHours,
    int Score);

public sealed record ResidentDecisionBriefResponse(
    Guid ResidentId,
    Guid CompoundId,
    string FullName,
    DateTime GeneratedAtUtc,
    int EscalationScore,
    string DecisionBand,
    decimal FinancialExposure,
    int OpenFinancialItems,
    int OpenOperationalItems,
    int OpenCommunicationItems,
    int ActiveRiskFlags,
    IReadOnlyCollection<string> DecisionBlockers,
    IReadOnlyCollection<string> RecommendedActions,
    IReadOnlyCollection<IntelligenceEscalationQueueItemResponse> RelatedEscalations);
