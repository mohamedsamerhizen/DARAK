using DARAK.Api.Enums;
using DARAK.Api.Identity;

namespace DARAK.Api.Entities;

public sealed class StaffMember
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string FullName { get; set; } = string.Empty;

    public string PhoneNumber { get; set; } = string.Empty;

    public string? Email { get; set; }

    public StaffType StaffType { get; set; } = StaffType.Other;

    public StaffStatus Status { get; set; } = StaffStatus.Active;

    public string? Specialization { get; set; }

    public string? NationalId { get; set; }

    public string? Notes { get; set; }

    public Guid? UserId { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }

    public ApplicationUser? User { get; set; }

    public ICollection<WorkOrder> AssignedWorkOrders { get; set; } = new List<WorkOrder>();
}
