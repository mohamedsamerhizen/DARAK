using DARAK.Api.Enums;
using DARAK.Api.Identity;

namespace DARAK.Api.Entities;

public sealed class NotificationOutbox
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? CompoundId { get; set; }

    public Guid? ResidentProfileId { get; set; }

    public Guid? RecipientUserId { get; set; }

    public NotificationChannel Channel { get; set; }

    public NotificationEventType EventType { get; set; } = NotificationEventType.General;

    public NotificationPriority Priority { get; set; } = NotificationPriority.Normal;

    public NotificationStatus Status { get; set; } = NotificationStatus.Pending;

    public string RecipientName { get; set; } = string.Empty;

    public string? RecipientEmail { get; set; }

    public string? RecipientPhoneNumber { get; set; }

    public string Subject { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public NotificationRelatedEntityType RelatedEntityType { get; set; } = NotificationRelatedEntityType.None;

    public Guid? RelatedEntityId { get; set; }

    public string? MetadataJson { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime ScheduledAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? ProcessingStartedAtUtc { get; set; }

    public DateTime? SentAtUtc { get; set; }

    public DateTime? FailedAtUtc { get; set; }

    public DateTime? CancelledAtUtc { get; set; }

    public DateTime? NextRetryAtUtc { get; set; }

    public int RetryCount { get; set; }

    public int MaxRetryCount { get; set; } = 3;

    public string? LastError { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public string? ProviderName { get; set; }

    public string? ProviderMessageId { get; set; }

    public Compound? Compound { get; set; }

    public ResidentProfile? ResidentProfile { get; set; }

    public ApplicationUser? RecipientUser { get; set; }

    public ApplicationUser? CreatedByUser { get; set; }

    public ICollection<NotificationDeliveryAttempt> DeliveryAttempts { get; set; } = new List<NotificationDeliveryAttempt>();
}
