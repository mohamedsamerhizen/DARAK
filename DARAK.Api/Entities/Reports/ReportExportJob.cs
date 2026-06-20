using DARAK.Api.Enums;
using DARAK.Api.Identity;

namespace DARAK.Api.Entities;

public sealed class ReportExportJob
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? CompoundId { get; set; }

    public Guid RequestedByUserId { get; set; }

    public ManagementReportType ReportType { get; set; }

    public ReportExportFormat Format { get; set; } = ReportExportFormat.Csv;

    public ReportExportJobStatus Status { get; set; } = ReportExportJobStatus.Queued;

    public string FilterJson { get; set; } = "{}";

    public string? FileName { get; set; }

    public string? DownloadPath { get; set; }

    public string? FailureReason { get; set; }

    public DateTime RequestedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? StartedAtUtc { get; set; }

    public DateTime? CompletedAtUtc { get; set; }

    public Compound? Compound { get; set; }

    public ApplicationUser RequestedByUser { get; set; } = null!;
}
