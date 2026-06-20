using DARAK.Api.Identity;

namespace DARAK.Api.Entities;

public sealed class ResidentNotificationPreference
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    public bool InAppEnabled { get; set; } = true;

    public bool EmailEnabled { get; set; }

    public bool SmsEnabled { get; set; }

    public bool BillNotificationsEnabled { get; set; } = true;

    public bool PaymentNotificationsEnabled { get; set; } = true;

    public bool MaintenanceNotificationsEnabled { get; set; } = true;

    public bool ComplaintNotificationsEnabled { get; set; } = true;

    public bool ViolationNotificationsEnabled { get; set; } = true;

    public bool VisitorNotificationsEnabled { get; set; } = true;

    public bool DocumentNotificationsEnabled { get; set; } = true;

    public bool AnnouncementNotificationsEnabled { get; set; } = true;

    public bool CampaignNotificationsEnabled { get; set; } = true;

    public bool DoNotDisturbEnabled { get; set; }

    public TimeSpan? DoNotDisturbStartLocalTime { get; set; }

    public TimeSpan? DoNotDisturbEndLocalTime { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }

    public ApplicationUser User { get; set; } = null!;
}
