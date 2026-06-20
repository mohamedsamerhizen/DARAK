using DARAK.Api.Enums;
using DARAK.Api.Identity;

namespace DARAK.Api.Entities;

public sealed class WorkOrderStatusHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid WorkOrderId { get; set; }

    public WorkOrderStatus? OldStatus { get; set; }

    public WorkOrderStatus NewStatus { get; set; }

    public Guid? ChangedByUserId { get; set; }

    public string? Note { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public WorkOrder WorkOrder { get; set; } = null!;

    public ApplicationUser? ChangedByUser { get; set; }
}
