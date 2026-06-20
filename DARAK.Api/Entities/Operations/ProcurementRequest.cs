using DARAK.Api.Enums;
using DARAK.Api.Identity;

namespace DARAK.Api.Entities;

public sealed class ProcurementRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CompoundId { get; set; }

    public Compound Compound { get; set; } = null!;

    public Guid? RequestedByUserId { get; set; }

    public ApplicationUser? RequestedByUser { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    public WorkOrderPriority Priority { get; set; } = WorkOrderPriority.Normal;

    public ProcurementRequestStatus Status { get; set; } = ProcurementRequestStatus.PendingApproval;

    public Guid? RelatedWorkOrderId { get; set; }

    public WorkOrder? RelatedWorkOrder { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? ApprovedAtUtc { get; set; }

    public Guid? ApprovedByUserId { get; set; }

    public ApplicationUser? ApprovedByUser { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    public ICollection<ProcurementRequestItem> Items { get; set; } = new List<ProcurementRequestItem>();

    public ICollection<PurchaseOrder> PurchaseOrders { get; set; } = new List<PurchaseOrder>();
}
