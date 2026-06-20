namespace DARAK.Api.Entities;

public sealed class CommunicationCampaignRecipient
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CampaignId { get; set; }

    public Guid ResidentProfileId { get; set; }

    public Guid UserId { get; set; }

    public Guid? NotificationOutboxId { get; set; }

    public bool DeliverySuppressed { get; set; }

    public string? SuppressionReason { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public CommunicationCampaign Campaign { get; set; } = null!;

    public ResidentProfile ResidentProfile { get; set; } = null!;

    public NotificationOutbox? NotificationOutbox { get; set; }
}
