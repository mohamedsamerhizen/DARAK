using DARAK.Api.Enums;

namespace DARAK.Api.Entities;

public sealed class NotificationTemplate
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Code { get; set; } = string.Empty;

    public NotificationChannel Channel { get; set; }

    public NotificationEventType EventType { get; set; } = NotificationEventType.General;

    public string SubjectTemplate { get; set; } = string.Empty;

    public string BodyTemplate { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }
}
