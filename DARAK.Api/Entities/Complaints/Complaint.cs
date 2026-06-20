using DARAK.Api.Enums;

namespace DARAK.Api.Entities;

public sealed class Complaint
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ResidentProfileId { get; set; }

    public Guid CompoundId { get; set; }

    public Guid? PropertyUnitId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public ComplaintStatus Status { get; set; } = ComplaintStatus.Open;

    public string? AdminResponse { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public DateTime? ResolvedAt { get; set; }

    public DateTime? RejectedAt { get; set; }

    public ResidentProfile ResidentProfile { get; set; } = null!;

    public Compound Compound { get; set; } = null!;

    public PropertyUnit? PropertyUnit { get; set; }

    public ICollection<Violation> Violations { get; set; } = new List<Violation>();
}
