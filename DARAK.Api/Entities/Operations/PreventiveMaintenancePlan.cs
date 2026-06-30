using DARAK.Api.Enums;

namespace DARAK.Api.Entities;

public sealed class PreventiveMaintenancePlan
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CompoundId { get; set; }

    public Compound Compound { get; set; } = null!;

    public Guid MaintenanceAssetId { get; set; }

    public MaintenanceAsset MaintenanceAsset { get; set; } = null!;

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public PreventiveMaintenanceCadence Cadence { get; set; } = PreventiveMaintenanceCadence.Monthly;

    public int? CustomIntervalDays { get; set; }

    public WorkOrderPriority Priority { get; set; } = WorkOrderPriority.Normal;

    public Guid? AssignedStaffMemberId { get; set; }

    public StaffMember? AssignedStaffMember { get; set; }

    public Guid? AssignedVendorId { get; set; }

    public ServiceVendor? AssignedVendor { get; set; }

    public DateTime NextDueAtUtc { get; set; }

    public DateTime? LastGeneratedAtUtc { get; set; }

    public string? LastGeneratedOccurrenceKey { get; set; }

    public bool IsActive { get; set; } = true;

    public string? Notes { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }
}
