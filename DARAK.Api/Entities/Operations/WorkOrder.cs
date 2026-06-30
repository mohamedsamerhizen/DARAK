using DARAK.Api.Enums;
using DARAK.Api.Identity;

namespace DARAK.Api.Entities;

public sealed class WorkOrder
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public WorkOrderSourceType SourceType { get; set; } = WorkOrderSourceType.Manual;

    public Guid? SourceEntityId { get; set; }

    public Guid CompoundId { get; set; }

    public Compound Compound { get; set; } = null!;

    public WorkOrderPriority Priority { get; set; } = WorkOrderPriority.Normal;

    public WorkOrderStatus Status { get; set; } = WorkOrderStatus.New;

    public Guid? AssignedStaffMemberId { get; set; }

    public StaffMember? AssignedStaffMember { get; set; }

    public Guid? AssignedVendorId { get; set; }

    public ServiceVendor? AssignedVendor { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public ApplicationUser? CreatedByUser { get; set; }

    public Guid? PropertyUnitId { get; set; }

    public PropertyUnit? PropertyUnit { get; set; }

    public DateTime? ScheduledAtUtc { get; set; }

    public DateTime? StartedAtUtc { get; set; }

    public DateTime? CompletedAtUtc { get; set; }

    public DateTime? CancelledAtUtc { get; set; }

    public DateTime? DueAtUtc { get; set; }

    public Guid? MaintenanceAssetId { get; set; }

    public MaintenanceAsset? MaintenanceAsset { get; set; }

    public string? PreventiveMaintenanceOccurrenceKey { get; set; }

    public Guid? MaintenanceSlaPolicyId { get; set; }

    public MaintenanceSlaPolicy? MaintenanceSlaPolicy { get; set; }

    public MaintenanceSlaStatus SlaStatus { get; set; } = MaintenanceSlaStatus.NotApplied;

    public DateTime? ResponseDueAtUtc { get; set; }

    public DateTime? ResolutionDueAtUtc { get; set; }

    public DateTime? FirstRespondedAtUtc { get; set; }

    public DateTime? SlaBreachedAtUtc { get; set; }

    public DateTime? SlaEscalatedAtUtc { get; set; }

    public DateTime? LastSlaEscalatedAtUtc { get; set; }

    public int SlaEscalationCount { get; set; }

    public string? SlaBreachReason { get; set; }

    public decimal? EstimatedCost { get; set; }

    public decimal? ActualCost { get; set; }

    public string? CompletionNotes { get; set; }

    public string? CancellationReason { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }

    public byte[] RowVersion { get; set; } = [];

    public ICollection<WorkOrderCostItem> CostItems { get; set; } = new List<WorkOrderCostItem>();

    public ICollection<WorkOrderStatusHistory> StatusHistory { get; set; } = new List<WorkOrderStatusHistory>();

    public ICollection<WorkOrderRating> Ratings { get; set; } = new List<WorkOrderRating>();
}
