using DARAK.Api.Enums;
using DARAK.Api.Identity;

namespace DARAK.Api.Entities;

public sealed class CommunicationCampaign
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CompoundId { get; set; }

    public Guid CreatedByUserId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public ResidentNotificationType NotificationType { get; set; } = ResidentNotificationType.Announcement;

    public ResidentNotificationSeverity Severity { get; set; } = ResidentNotificationSeverity.Info;

    public NotificationPriority Priority { get; set; } = NotificationPriority.Normal;

    public CommunicationCampaignTargetType TargetType { get; set; } = CommunicationCampaignTargetType.Compound;

    public Guid? TargetBuildingId { get; set; }

    public Guid? TargetFloorId { get; set; }

    public Guid? TargetPropertyUnitId { get; set; }

    public Guid? TargetResidentProfileId { get; set; }

    public CommunicationCampaignStatus Status { get; set; } = CommunicationCampaignStatus.Draft;

    public DateTime? ScheduledAtUtc { get; set; }

    public DateTime? SentAtUtc { get; set; }

    public int RecipientCount { get; set; }

    public int OutboxItemCount { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }

    public byte[] RowVersion { get; set; } = [];

    public Compound Compound { get; set; } = null!;

    public ApplicationUser CreatedByUser { get; set; } = null!;

    public ICollection<CommunicationCampaignRecipient> Recipients { get; set; } = new List<CommunicationCampaignRecipient>();
}
