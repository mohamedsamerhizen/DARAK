using DARAK.Api.Enums;
using DARAK.Api.Identity;

namespace DARAK.Api.Entities;

public sealed class MaintenanceStatusHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid MaintenanceRequestId { get; set; }

    public MaintenanceStatus? OldStatus { get; set; }

    public MaintenanceStatus NewStatus { get; set; }

    public Guid? ChangedByUserId { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public MaintenanceRequest MaintenanceRequest { get; set; } = null!;

    public ApplicationUser? ChangedByUser { get; set; }
}
