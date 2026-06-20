using DARAK.Api.Enums;

namespace DARAK.Api.Entities;

public sealed class SystemHealthSnapshot
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public SystemHealthStatus Status { get; set; } = SystemHealthStatus.Healthy;

    public int PendingNotifications { get; set; }

    public int FailedNotifications { get; set; }

    public int OpenIntegrationFailures { get; set; }

    public int FailedBackgroundJobs24h { get; set; }

    public string Summary { get; set; } = string.Empty;

    public DateTime CapturedAtUtc { get; set; } = DateTime.UtcNow;
}
