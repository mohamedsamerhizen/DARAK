using DARAK.Api.Identity;

namespace DARAK.Api.Entities;

public sealed class AnnouncementReadReceipt
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid AnnouncementId { get; set; }

    public Guid UserId { get; set; }

    public DateTime ReadAt { get; set; } = DateTime.UtcNow;

    public Announcement Announcement { get; set; } = null!;

    public ApplicationUser User { get; set; } = null!;
}
