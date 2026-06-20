using System.ComponentModel.DataAnnotations;
using DARAK.Api.DTOs.Common;
using DARAK.Api.Enums;

namespace DARAK.Api.DTOs.Communication;

public sealed record CreateConversationRequest(
    Guid CompoundId,
    Guid ResidentProfileId,
    Guid? PropertyUnitId,
    ConversationTopic Topic,
    ConversationIssueType IssueType,
    ConversationLinkedEntityType LinkedEntityType,
    Guid? LinkedEntityId,
    string InitialMessage,
    Guid? CreatedByUserId,
    ConversationPriority? PriorityOverride = null,
    string OpeningSystemMessage = "Conversation opened.");

public sealed class ResidentOpenConversationRequest
{
    public Guid? PropertyUnitId { get; init; }

    public ConversationTopic Topic { get; init; } = ConversationTopic.General;

    public ConversationIssueType IssueType { get; init; } = ConversationIssueType.GeneralInquiry;

    public ConversationLinkedEntityType LinkedEntityType { get; init; } = ConversationLinkedEntityType.None;

    public Guid? LinkedEntityId { get; init; }

    [Required]
    [MaxLength(4000)]
    public string InitialMessage { get; init; } = string.Empty;
}


public sealed class ResidentBillDisputeRequest
{
    public ConversationIssueType IssueType { get; init; } = ConversationIssueType.BillingHighAmount;

    [Required]
    [MaxLength(4000)]
    public string Message { get; init; } = string.Empty;
}

public sealed record ResidentBillDisputeResponse(
    Guid ConversationId,
    ConversationStatus Status,
    ConversationPriority Priority,
    ConversationIssueType IssueType,
    Guid BillId,
    string BillNumber,
    decimal TotalAmount,
    decimal PaidAmount,
    decimal RemainingAmount,
    DateOnly DueDate,
    bool CreatedNew,
    DateTime CreatedAtUtc);

public sealed class ConversationSearchQuery : PaginationQuery
{
    public Guid? CompoundId { get; init; }

    public Guid? ResidentProfileId { get; init; }

    public Guid? PropertyUnitId { get; init; }

    public ConversationStatus? Status { get; init; }

    public ConversationPriority? Priority { get; init; }

    public ConversationTopic? Topic { get; init; }

    public ConversationIssueType? IssueType { get; init; }

    public ConversationEscalationLevel? EscalationLevel { get; init; }

    public Guid? AssignedToUserId { get; init; }

    public bool? IsUnassigned { get; init; }

    public bool? IsEscalated { get; init; }

    [MaxLength(200)]
    public string? SearchTerm { get; init; }
}

public sealed class SendConversationMessageRequest
{
    [Required]
    [MaxLength(4000)]
    public string Body { get; init; } = string.Empty;
}

public sealed class AddInternalNoteRequest
{
    [Required]
    [MaxLength(4000)]
    public string Body { get; init; } = string.Empty;
}

public sealed class AssignConversationRequest
{
    public Guid AssignedToUserId { get; init; }

    [MaxLength(500)]
    public string? Reason { get; init; }
}

public sealed class ChangeConversationPriorityRequest
{
    public ConversationPriority Priority { get; init; } = ConversationPriority.Normal;

    [MaxLength(500)]
    public string? Reason { get; init; }
}

public sealed class CompleteConversationRequest
{
    [MaxLength(1000)]
    public string? Reason { get; init; }
}

public sealed class EscalateConversationRequest
{
    public ConversationEscalationLevel EscalationLevel { get; init; } = ConversationEscalationLevel.NeedsAttention;

    [Required]
    [MaxLength(500)]
    public string Reason { get; init; } = string.Empty;
}

public sealed class ReopenConversationRequest
{
    [Required]
    [MaxLength(1000)]
    public string Reason { get; init; } = string.Empty;
}

public sealed record ConversationResponse(
    Guid Id,
    Guid CompoundId,
    Guid ResidentProfileId,
    Guid? PropertyUnitId,
    ConversationStatus Status,
    ConversationPriority Priority,
    ConversationTopic Topic,
    ConversationIssueType IssueType,
    ConversationLinkedEntityType LinkedEntityType,
    Guid? LinkedEntityId,
    Guid? AssignedToUserId,
    Guid? AssignedByUserId,
    DateTime? AssignedAtUtc,
    string? LastAssignmentReason,
    ConversationEscalationLevel EscalationLevel,
    DateTime? EscalatedAtUtc,
    string? EscalationReason,
    int ReopenCount,
    string? LastReopenReason,
    DateTime CreatedAtUtc,
    DateTime LastMessageAtUtc,
    IReadOnlyList<ConversationMessageResponse> Messages);

public sealed record ResidentConversationResponse(
    Guid Id,
    Guid CompoundId,
    Guid ResidentProfileId,
    Guid? PropertyUnitId,
    ConversationStatus Status,
    ConversationPriority Priority,
    ConversationTopic Topic,
    ConversationIssueType IssueType,
    ConversationLinkedEntityType LinkedEntityType,
    Guid? LinkedEntityId,
    int ReopenCount,
    string? LastReopenReason,
    DateTime CreatedAtUtc,
    DateTime LastMessageAtUtc,
    IReadOnlyList<ResidentConversationMessageResponse> Messages);

public sealed record ConversationMessageResponse(
    Guid Id,
    Guid ConversationId,
    Guid? SenderUserId,
    ConversationMessageType MessageType,
    ConversationMessageVisibility Visibility,
    string Body,
    DateTime CreatedAtUtc);

public sealed record ResidentConversationMessageResponse(
    Guid Id,
    Guid ConversationId,
    Guid? SenderUserId,
    ConversationMessageType MessageType,
    string Body,
    DateTime CreatedAtUtc);

public sealed record ConversationAdvisoryFlagResponse(
    AdvisoryFlagSeverity Severity,
    string Title,
    string Description,
    string RecommendedAction,
    bool IsBlocking);

public sealed record AdminConversationDetailsResponse(
    ConversationResponse Conversation,
    IReadOnlyList<ConversationAdvisoryFlagResponse> AdvisoryFlags,
    ConversationResidentContextPanelResponse ResidentContext);

public sealed record ConversationResidentContextPanelResponse(
    Guid ResidentProfileId,
    string ResidentName,
    Guid? CurrentUnitId,
    string? CurrentUnitNumber,
    string? OccupancyType,
    decimal OutstandingAmount,
    decimal OverdueAmount,
    DateTime? LastPaymentDate,
    int OpenConversationsCount,
    ResidentFinancialHealthStatus FinancialHealthStatus,
    IReadOnlyList<string> FinancialHealthRiskReasons,
    IReadOnlyList<ActivityEventResponse> RecentActivityEvents);


public sealed class SupportDashboardQuery
{
    public Guid? CompoundId { get; init; }
}

public sealed record SupportDashboardResponse(
    int OpenConversationsCount,
    int UrgentConversationsCount,
    int UnassignedConversationsCount,
    int EscalatedConversationsCount,
    int ReopenedConversationsCount,
    int BillingDisputesCount,
    int AssignedToMeCount,
    ConversationSummaryResponse? OldestOpenConversation,
    IReadOnlyList<HighRiskResidentOpenConversationResponse> HighRiskResidentsWithOpenConversations);

public sealed record ConversationSummaryResponse(
    Guid Id,
    Guid CompoundId,
    Guid ResidentProfileId,
    Guid? PropertyUnitId,
    ConversationStatus Status,
    ConversationPriority Priority,
    ConversationTopic Topic,
    ConversationIssueType IssueType,
    ConversationEscalationLevel EscalationLevel,
    Guid? AssignedToUserId,
    int ReopenCount,
    DateTime CreatedAtUtc,
    DateTime LastMessageAtUtc);

public sealed record HighRiskResidentOpenConversationResponse(
    Guid ResidentProfileId,
    string ResidentName,
    int OpenConversationsCount,
    int UrgentConversationsCount,
    int EscalatedConversationsCount,
    int ReopenedConversationsCount,
    int BillingDisputesCount,
    IReadOnlyList<string> RiskReasons);
