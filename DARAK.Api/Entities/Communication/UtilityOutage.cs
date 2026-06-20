using DARAK.Api.Enums;
using DARAK.Api.Identity;

namespace DARAK.Api.Entities;

public sealed class UtilityOutage
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CompoundId { get; set; }

    public Guid? BuildingId { get; set; }

    public Guid? FloorId { get; set; }

    public Guid? PropertyUnitId { get; set; }

    public Guid? AnnouncementId { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public Guid? ResolvedByUserId { get; set; }

    public UtilityOutageServiceType ServiceType { get; set; }

    public UtilityOutageAffectedScope AffectedScope { get; set; } = UtilityOutageAffectedScope.Compound;

    public UtilityOutageStatus Status { get; set; } = UtilityOutageStatus.Active;

    public UtilityOutageSeverity Severity { get; set; } = UtilityOutageSeverity.Medium;

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public DateTime EstimatedStartAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? EstimatedEndAtUtc { get; set; }

    public DateTime? PublishedAtUtc { get; set; }

    public DateTime? ResolvedAtUtc { get; set; }

    public string? ResolutionNotes { get; set; }

    public bool NotifyResidents { get; set; } = true;

    public int RecipientCount { get; set; }

    public int OutboxItemCount { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }

    public Compound Compound { get; set; } = null!;

    public Building? Building { get; set; }

    public Floor? Floor { get; set; }

    public PropertyUnit? PropertyUnit { get; set; }

    public Announcement? Announcement { get; set; }

    public ApplicationUser? CreatedByUser { get; set; }

    public ApplicationUser? ResolvedByUser { get; set; }

    public ICollection<UtilityOutageUpdate> Updates { get; set; } = new List<UtilityOutageUpdate>();
}
