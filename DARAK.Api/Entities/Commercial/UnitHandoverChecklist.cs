using DARAK.Api.Enums;

namespace DARAK.Api.Entities;

public sealed class UnitHandoverChecklist
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CompoundId { get; set; }

    public Guid PropertyUnitId { get; set; }

    public Guid ResidentProfileId { get; set; }

    public UnitHandoverType HandoverType { get; set; }

    public UnitHandoverStatus Status { get; set; } = UnitHandoverStatus.Draft;

    public DateOnly ScheduledDate { get; set; }

    public DateOnly? CompletedDate { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public Guid? CompletedByUserId { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }

    public Compound Compound { get; set; } = null!;

    public PropertyUnit PropertyUnit { get; set; } = null!;

    public ResidentProfile ResidentProfile { get; set; } = null!;

    public ICollection<UnitHandoverChecklistItem> Items { get; set; } = new List<UnitHandoverChecklistItem>();
}
