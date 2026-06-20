using System.ComponentModel.DataAnnotations;
using DARAK.Api.DTOs.Common;
using DARAK.Api.Enums;

namespace DARAK.Api.DTOs.Notifications;

public sealed class EnqueueNotificationRequest
{
    public Guid? CompoundId { get; init; }

    public Guid? ResidentProfileId { get; init; }

    public Guid? RecipientUserId { get; init; }

    [Required]
    public NotificationChannel Channel { get; init; }

    public NotificationEventType EventType { get; init; } = NotificationEventType.General;

    public NotificationPriority Priority { get; init; } = NotificationPriority.Normal;

    [MaxLength(200)]
    public string? RecipientName { get; init; }

    [EmailAddress]
    [MaxLength(256)]
    public string? RecipientEmail { get; init; }

    [MaxLength(40)]
    public string? RecipientPhoneNumber { get; init; }

    [Required]
    [MaxLength(300)]
    public string Subject { get; init; } = string.Empty;

    [Required]
    [MaxLength(4000)]
    public string Body { get; init; } = string.Empty;

    public NotificationRelatedEntityType RelatedEntityType { get; init; } = NotificationRelatedEntityType.None;

    public Guid? RelatedEntityId { get; init; }

    [MaxLength(4000)]
    public string? MetadataJson { get; init; }

    public DateTime? ScheduledAtUtc { get; init; }

    [Range(0, 10)]
    public int MaxRetryCount { get; init; } = 3;
}

public sealed class ManualNotificationRequest
{
    public Guid? CompoundId { get; init; }

    public Guid? ResidentProfileId { get; init; }

    public Guid? RecipientUserId { get; init; }

    [Required]
    public NotificationChannel Channel { get; init; }

    public NotificationPriority Priority { get; init; } = NotificationPriority.Normal;

    [MaxLength(200)]
    public string? RecipientName { get; init; }

    [EmailAddress]
    [MaxLength(256)]
    public string? RecipientEmail { get; init; }

    [MaxLength(40)]
    public string? RecipientPhoneNumber { get; init; }

    [Required]
    [MaxLength(300)]
    public string Subject { get; init; } = string.Empty;

    [Required]
    [MaxLength(4000)]
    public string Body { get; init; } = string.Empty;

    public DateTime? ScheduledAtUtc { get; init; }

    [Range(0, 10)]
    public int MaxRetryCount { get; init; } = 3;
}

public sealed class NotificationSearchQuery : PaginationQuery
{
    public Guid? CompoundId { get; init; }

    public Guid? ResidentProfileId { get; init; }

    public Guid? RecipientUserId { get; init; }

    public NotificationChannel? Channel { get; init; }

    public NotificationEventType? EventType { get; init; }

    public NotificationStatus? Status { get; init; }

    public NotificationPriority? Priority { get; init; }

    public NotificationRelatedEntityType? RelatedEntityType { get; init; }

    public Guid? RelatedEntityId { get; init; }

    public DateTime? CreatedFromUtc { get; init; }

    public DateTime? CreatedToUtc { get; init; }

    [MaxLength(100)]
    public string? SearchTerm { get; init; }
}

public sealed record NotificationOutboxResponse(
    Guid Id,
    Guid? CompoundId,
    Guid? ResidentProfileId,
    Guid? RecipientUserId,
    NotificationChannel Channel,
    NotificationEventType EventType,
    NotificationPriority Priority,
    NotificationStatus Status,
    string RecipientName,
    string? RecipientEmail,
    string? RecipientPhoneNumber,
    string Subject,
    string Body,
    NotificationRelatedEntityType RelatedEntityType,
    Guid? RelatedEntityId,
    string? MetadataJson,
    DateTime CreatedAtUtc,
    DateTime ScheduledAtUtc,
    DateTime? ProcessingStartedAtUtc,
    DateTime? SentAtUtc,
    DateTime? FailedAtUtc,
    DateTime? CancelledAtUtc,
    DateTime? NextRetryAtUtc,
    int RetryCount,
    int MaxRetryCount,
    string? LastError,
    Guid? CreatedByUserId,
    string? ProviderName,
    string? ProviderMessageId,
    IReadOnlyCollection<NotificationDeliveryAttemptResponse> DeliveryAttempts);

public sealed record NotificationDeliveryAttemptResponse(
    Guid Id,
    Guid NotificationOutboxId,
    int AttemptNumber,
    NotificationDeliveryAttemptStatus Status,
    string ProviderName,
    string? ProviderMessageId,
    string? ErrorMessage,
    DateTime StartedAtUtc,
    DateTime? CompletedAtUtc);

public sealed record NotificationDashboardSummaryResponse(
    int PendingCount,
    int ProcessingCount,
    int SentLast24Hours,
    int FailedCount,
    int DueForRetryCount,
    DateTime? OldestPendingScheduledAtUtc);
