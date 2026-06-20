using DARAK.Api.Enums;

namespace DARAK.Api.Entities;

public sealed class WorkOrderCostItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid WorkOrderId { get; set; }

    public string Description { get; set; } = string.Empty;

    public WorkOrderCostType CostType { get; set; } = WorkOrderCostType.Other;

    public decimal Amount { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public WorkOrder WorkOrder { get; set; } = null!;
}
