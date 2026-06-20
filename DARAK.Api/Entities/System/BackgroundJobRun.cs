using DARAK.Api.Enums;

namespace DARAK.Api.Entities;

public sealed class BackgroundJobRun
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string JobName { get; set; } = string.Empty;

    public string? WorkerName { get; set; }

    public BackgroundJobRunStatus Status { get; set; } = BackgroundJobRunStatus.Running;

    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAtUtc { get; set; }

    public int? DurationMs { get; set; }

    public int ProcessedCount { get; set; }

    public int FailedCount { get; set; }

    public string? ErrorMessage { get; set; }

    public string? MetadataJson { get; set; }
}
