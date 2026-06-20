using DARAK.Api.Enums;

namespace DARAK.Api.Entities;

public sealed class ServiceVendor
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public string? ContactPersonName { get; set; }

    public string PhoneNumber { get; set; } = string.Empty;

    public string? Email { get; set; }

    public VendorServiceType ServiceType { get; set; } = VendorServiceType.Other;

    public VendorStatus Status { get; set; } = VendorStatus.Active;

    public string? Address { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }

    public ICollection<WorkOrder> AssignedWorkOrders { get; set; } = new List<WorkOrder>();
}
