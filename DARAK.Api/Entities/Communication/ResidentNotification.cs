using DARAK.Api.Enums;
using DARAK.Api.Identity;

namespace DARAK.Api.Entities;

public sealed class ResidentNotification
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public ResidentNotificationType Type { get; set; } = ResidentNotificationType.General;

    public ResidentNotificationSeverity Severity { get; set; } = ResidentNotificationSeverity.Info;

    public string? RelatedEntityType { get; set; }

    public Guid? RelatedEntityId { get; set; }

    public bool IsRead { get; set; }

    public DateTime? ReadAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ApplicationUser User { get; set; } = null!;
}
