using DARAK.Api.Enums;
using DARAK.Api.Identity;

namespace DARAK.Api.Entities;

public sealed class SavedReport
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? CompoundId { get; set; }

    public Guid CreatedByUserId { get; set; }

    public ManagementReportType ReportType { get; set; }

    public SavedReportVisibility Visibility { get; set; } = SavedReportVisibility.Private;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string FilterJson { get; set; } = "{}";

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }

    public Compound? Compound { get; set; }

    public ApplicationUser CreatedByUser { get; set; } = null!;
}
