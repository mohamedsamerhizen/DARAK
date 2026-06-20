using DARAK.Api.Enums;
using DARAK.Api.Identity;

namespace DARAK.Api.Entities;

public sealed class Violation
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CompoundId { get; set; }

    public Guid? ResidentProfileId { get; set; }

    public Guid? PropertyUnitId { get; set; }

    public Guid? ComplaintId { get; set; }

    public ViolationType ViolationType { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public Guid? CreatedByUserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public Compound Compound { get; set; } = null!;

    public ResidentProfile? ResidentProfile { get; set; }

    public PropertyUnit? PropertyUnit { get; set; }

    public Complaint? Complaint { get; set; }

    public ApplicationUser? CreatedByUser { get; set; }

    public ICollection<ViolationFine> Fines { get; set; } = new List<ViolationFine>();
}
