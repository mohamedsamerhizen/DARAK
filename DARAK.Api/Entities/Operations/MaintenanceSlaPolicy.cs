using DARAK.Api.Enums;

namespace DARAK.Api.Entities;

public sealed class MaintenanceSlaPolicy
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CompoundId { get; set; }

    public Compound Compound { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    public WorkOrderPriority? Priority { get; set; }

    public WorkOrderSourceType? SourceType { get; set; }

    public int ResponseDueMinutes { get; set; }

    public int ResolutionDueMinutes { get; set; }

    public int? EscalationDueMinutes { get; set; }

    public bool IsActive { get; set; } = true;

    public string? Notes { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }

    public ICollection<WorkOrder> WorkOrders { get; set; } = new List<WorkOrder>();
}
