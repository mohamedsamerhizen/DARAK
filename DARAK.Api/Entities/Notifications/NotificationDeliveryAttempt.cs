using DARAK.Api.Enums;

namespace DARAK.Api.Entities;

public sealed class NotificationDeliveryAttempt
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid NotificationOutboxId { get; set; }

    public int AttemptNumber { get; set; }

    public NotificationDeliveryAttemptStatus Status { get; set; } = NotificationDeliveryAttemptStatus.Processing;

    public string ProviderName { get; set; } = string.Empty;

    public string? ProviderMessageId { get; set; }

    public string? ErrorMessage { get; set; }

    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAtUtc { get; set; }

    public NotificationOutbox NotificationOutbox { get; set; } = null!;
}
