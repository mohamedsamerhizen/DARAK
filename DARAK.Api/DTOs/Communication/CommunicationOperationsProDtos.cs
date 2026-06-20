using DARAK.Api.Enums;

namespace DARAK.Api.DTOs.Communication;

public sealed record CommunicationCommandCenterResponse(
    Guid? CompoundId,
    DateTime GeneratedAtUtc,
    int ActiveAnnouncementCount,
    int CriticalAnnouncementCount,
    int AnnouncementAcknowledgementGapCount,
    int ActiveOutageCount,
    int CriticalOutageCount,
    int OverdueOutageCount,
    int PendingOutboxItemCount,
    int FailedOutboxItemCount,
    int OpenConversationCount,
    int UrgentConversationCount,
    int EscalatedConversationCount,
    int UnassignedConversationCount,
    string OverallRiskLevel,
    int CriticalActionCount,
    IReadOnlyList<string> RecommendedActions);

public sealed record AnnouncementAcknowledgementBoardResponse(
    Guid? CompoundId,
    DateTime GeneratedAtUtc,
    int ActiveAnnouncementCount,
    int CriticalAnnouncementCount,
    int TotalExpectedAcknowledgements,
    int TotalAcknowledgedCount,
    int TotalMissingAcknowledgementCount,
    IReadOnlyList<AnnouncementAcknowledgementItemResponse> Items);

public sealed record AnnouncementAcknowledgementItemResponse(
    Guid AnnouncementId,
    Guid CompoundId,
    string Title,
    AnnouncementPriority Priority,
    AnnouncementCategory Category,
    DateTime? PublishedAtUtc,
    DateTime? ExpiresAtUtc,
    bool IsPinned,
    int ExpectedAcknowledgementCount,
    int AcknowledgedCount,
    int MissingAcknowledgementCount,
    decimal AcknowledgementRatePercent,
    string RiskLevel,
    string RecommendedAction);

public sealed record UtilityOutageOperationsBoardResponse(
    Guid? CompoundId,
    DateTime GeneratedAtUtc,
    int ActiveOutageCount,
    int CriticalOutageCount,
    int OverdueOutageCount,
    int OpenOutageUpdateCount,
    int ResidentNotificationOutboxCount,
    IReadOnlyList<UtilityOutageOperationsItemResponse> Items);

public sealed record UtilityOutageOperationsItemResponse(
    Guid OutageId,
    Guid CompoundId,
    UtilityOutageServiceType ServiceType,
    UtilityOutageAffectedScope AffectedScope,
    UtilityOutageStatus Status,
    UtilityOutageSeverity Severity,
    string Title,
    DateTime EstimatedStartAtUtc,
    DateTime? EstimatedEndAtUtc,
    int ElapsedMinutes,
    bool IsOverdue,
    int UpdateCount,
    int RecipientCount,
    int OutboxItemCount,
    string OperationalRisk,
    string RecommendedAction);

public sealed record ResidentCommunicationImpactReportResponse(
    Guid? CompoundId,
    DateTime GeneratedAtUtc,
    int ImpactedResidentCount,
    int ResidentsWithCriticalImpactCount,
    int TotalUnreadNotificationCount,
    IReadOnlyList<ResidentCommunicationImpactItemResponse> Items);

public sealed record ResidentCommunicationImpactItemResponse(
    Guid ResidentProfileId,
    Guid UserId,
    Guid CompoundId,
    string ResidentName,
    int ActiveOutageCount,
    int CriticalOutageCount,
    int UnreadNotificationCount,
    int PendingOutboxItemCount,
    string ImpactLevel,
    string RecommendedAction);

public sealed record CommunicationResponseIntelligenceResponse(
    Guid? CompoundId,
    DateTime GeneratedAtUtc,
    int OpenConversationCount,
    int PendingAdminReplyCount,
    int UrgentConversationCount,
    int EscalatedConversationCount,
    int StaleConversationCount,
    decimal AverageOpenAgeHours,
    IReadOnlyList<CommunicationSlaConversationItemResponse> StaleItems);

public sealed record CommunicationSlaConversationItemResponse(
    Guid ConversationId,
    Guid CompoundId,
    Guid ResidentProfileId,
    Guid? PropertyUnitId,
    ConversationStatus Status,
    ConversationPriority Priority,
    ConversationTopic Topic,
    ConversationEscalationLevel EscalationLevel,
    Guid? AssignedToUserId,
    int AgeHours,
    int HoursSinceLastMessage,
    string SlaRisk,
    string RecommendedAction);

public sealed record CommunicationRiskDashboardResponse(
    Guid? CompoundId,
    DateTime GeneratedAtUtc,
    string OverallRiskLevel,
    int CriticalSignalCount,
    int WarningSignalCount,
    IReadOnlyList<CommunicationRiskSignalResponse> Signals);

public sealed record CommunicationRiskSignalResponse(
    string Area,
    string Severity,
    string Title,
    string Description,
    string RecommendedAction,
    int Count);
