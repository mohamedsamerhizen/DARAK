using System.ComponentModel.DataAnnotations;
using DARAK.Api.DTOs.Common;
using DARAK.Api.Enums;

namespace DARAK.Api.DTOs.Communication;

public sealed record ResidentNotificationPreferenceResponse(
    Guid Id,
    Guid UserId,
    bool InAppEnabled,
    bool EmailEnabled,
    bool SmsEnabled,
    bool BillNotificationsEnabled,
    bool PaymentNotificationsEnabled,
    bool MaintenanceNotificationsEnabled,
    bool ComplaintNotificationsEnabled,
    bool ViolationNotificationsEnabled,
    bool VisitorNotificationsEnabled,
    bool DocumentNotificationsEnabled,
    bool AnnouncementNotificationsEnabled,
    bool CampaignNotificationsEnabled,
    bool DoNotDisturbEnabled,
    TimeSpan? DoNotDisturbStartLocalTime,
    TimeSpan? DoNotDisturbEndLocalTime,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public sealed class UpdateResidentNotificationPreferenceRequest
{
    public bool InAppEnabled { get; init; } = true;

    public bool EmailEnabled { get; init; }

    public bool SmsEnabled { get; init; }

    public bool BillNotificationsEnabled { get; init; } = true;

    public bool PaymentNotificationsEnabled { get; init; } = true;

    public bool MaintenanceNotificationsEnabled { get; init; } = true;

    public bool ComplaintNotificationsEnabled { get; init; } = true;

    public bool ViolationNotificationsEnabled { get; init; } = true;

    public bool VisitorNotificationsEnabled { get; init; } = true;

    public bool DocumentNotificationsEnabled { get; init; } = true;

    public bool AnnouncementNotificationsEnabled { get; init; } = true;

    public bool CampaignNotificationsEnabled { get; init; } = true;

    public bool DoNotDisturbEnabled { get; init; }

    public TimeSpan? DoNotDisturbStartLocalTime { get; init; }

    public TimeSpan? DoNotDisturbEndLocalTime { get; init; }
}

public sealed class CommunicationCampaignSearchQuery : PaginationQuery
{
    public Guid? CompoundId { get; init; }

    public CommunicationCampaignStatus? Status { get; init; }

    public CommunicationCampaignTargetType? TargetType { get; init; }

    public DateTime? FromUtc { get; init; }

    public DateTime? ToUtc { get; init; }

    [MaxLength(150)]
    public string? SearchTerm { get; init; }
}

public sealed class CreateCommunicationCampaignRequest
{
    public Guid CompoundId { get; init; }

    [Required]
    [MaxLength(150)]
    public string Title { get; init; } = string.Empty;

    [Required]
    [MaxLength(4000)]
    public string Body { get; init; } = string.Empty;

    public ResidentNotificationType NotificationType { get; init; } = ResidentNotificationType.Announcement;

    public ResidentNotificationSeverity Severity { get; init; } = ResidentNotificationSeverity.Info;

    public NotificationPriority Priority { get; init; } = NotificationPriority.Normal;

    public CommunicationCampaignTargetType TargetType { get; init; } = CommunicationCampaignTargetType.Compound;

    public Guid? TargetBuildingId { get; init; }

    public Guid? TargetFloorId { get; init; }

    public Guid? TargetPropertyUnitId { get; init; }

    public Guid? TargetResidentProfileId { get; init; }

    public DateTime? ScheduledAtUtc { get; init; }
}

public sealed record CommunicationCampaignResponse(
    Guid Id,
    Guid CompoundId,
    Guid CreatedByUserId,
    string Title,
    string Body,
    ResidentNotificationType NotificationType,
    ResidentNotificationSeverity Severity,
    NotificationPriority Priority,
    CommunicationCampaignTargetType TargetType,
    Guid? TargetBuildingId,
    Guid? TargetFloorId,
    Guid? TargetPropertyUnitId,
    Guid? TargetResidentProfileId,
    CommunicationCampaignStatus Status,
    DateTime? ScheduledAtUtc,
    DateTime? SentAtUtc,
    int RecipientCount,
    int OutboxItemCount,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public sealed record CommunicationCampaignDetailsResponse(
    Guid Id,
    Guid CompoundId,
    Guid CreatedByUserId,
    string Title,
    string Body,
    ResidentNotificationType NotificationType,
    ResidentNotificationSeverity Severity,
    NotificationPriority Priority,
    CommunicationCampaignTargetType TargetType,
    Guid? TargetBuildingId,
    Guid? TargetFloorId,
    Guid? TargetPropertyUnitId,
    Guid? TargetResidentProfileId,
    CommunicationCampaignStatus Status,
    DateTime? ScheduledAtUtc,
    DateTime? SentAtUtc,
    int RecipientCount,
    int OutboxItemCount,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    IReadOnlyCollection<CommunicationCampaignRecipientResponse> Recipients);

public sealed record CommunicationCampaignRecipientResponse(
    Guid Id,
    Guid ResidentProfileId,
    Guid UserId,
    Guid? NotificationOutboxId,
    bool DeliverySuppressed,
    string? SuppressionReason,
    DateTime CreatedAtUtc);

public sealed record CommunicationDeliveryAnalyticsResponse(
    Guid? CompoundId,
    int CampaignCount,
    int SentCampaignCount,
    int TotalRecipientCount,
    int SuppressedRecipientCount,
    int OutboxItemCount,
    int PendingOutboxCount,
    int SentOutboxCount,
    int FailedOutboxCount);
