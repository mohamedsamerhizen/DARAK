using DARAK.Api.Enums;
using DARAK.Api.Identity;

namespace DARAK.Api.Entities;

public sealed class Announcement
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Title { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public AnnouncementCategory Category { get; set; } = AnnouncementCategory.General;

    public AnnouncementPriority Priority { get; set; } = AnnouncementPriority.Normal;

    public AnnouncementAudience Audience { get; set; } = AnnouncementAudience.AllResidents;

    public AnnouncementStatus Status { get; set; } = AnnouncementStatus.Draft;

    public Guid CompoundId { get; set; }

    public Compound Compound { get; set; } = null!;

    public DateTime? PublishedAt { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public bool IsPinned { get; set; }

    public bool IsActive { get; set; } = true;

    public ApplicationUser? CreatedByUser { get; set; }

    public ICollection<AnnouncementReadReceipt> ReadReceipts { get; set; } = new List<AnnouncementReadReceipt>();
}
