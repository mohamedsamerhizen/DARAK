using DARAK.Api.Enums;
using DARAK.Api.Identity;

namespace DARAK.Api.Entities;

public sealed class PurchaseOrder
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CompoundId { get; set; }

    public Compound Compound { get; set; } = null!;

    public Guid? ProcurementRequestId { get; set; }

    public ProcurementRequest? ProcurementRequest { get; set; }

    public Guid VendorId { get; set; }

    public ServiceVendor Vendor { get; set; } = null!;

    public string OrderNumber { get; set; } = string.Empty;

    public PurchaseOrderStatus Status { get; set; } = PurchaseOrderStatus.Ordered;

    public DateTime? OrderedAtUtc { get; set; }

    public DateTime? ExpectedDeliveryAtUtc { get; set; }

    public DateTime? ReceivedAtUtc { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public ApplicationUser? CreatedByUser { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }

    public ICollection<PurchaseOrderItem> Items { get; set; } = new List<PurchaseOrderItem>();
}
