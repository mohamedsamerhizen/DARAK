using DARAK.Api.Enums;
using DARAK.Api.Identity;

namespace DARAK.Api.Entities;

public sealed class MaintenanceRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ResidentProfileId { get; set; }

    public Guid CompoundId { get; set; }

    public Guid PropertyUnitId { get; set; }

    public Guid? AssignedToUserId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public MaintenancePriority Priority { get; set; } = MaintenancePriority.Medium;

    public MaintenanceStatus Status { get; set; } = MaintenanceStatus.Open;

    public decimal? CostEstimate { get; set; }

    public decimal? ActualCost { get; set; }

    public string? ResolutionNotes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public DateTime? AssignedAt { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? ResolvedAt { get; set; }

    public DateTime? ClosedAt { get; set; }

    public DateTime? CancelledAt { get; set; }

    public ResidentProfile ResidentProfile { get; set; } = null!;

    public Compound Compound { get; set; } = null!;

    public PropertyUnit PropertyUnit { get; set; } = null!;

    public ApplicationUser? AssignedToUser { get; set; }

    public ICollection<MaintenanceStatusHistory> StatusHistory { get; set; } = new List<MaintenanceStatusHistory>();
}
