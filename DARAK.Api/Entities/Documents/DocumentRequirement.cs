using DARAK.Api.Enums;
using DARAK.Api.Identity;

namespace DARAK.Api.Entities;

public sealed class DocumentRequirement
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CompoundId { get; set; }

    public DocumentCategory Category { get; set; }

    public DocumentRequirementAppliesTo AppliesTo { get; set; } = DocumentRequirementAppliesTo.Resident;

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsMandatory { get; set; } = true;

    public int? ValidityDays { get; set; }

    public bool RequiresApproval { get; set; } = true;

    public bool IsActive { get; set; } = true;

    public Guid? CreatedByUserId { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }

    public DateTime? DeactivatedAtUtc { get; set; }

    public Compound Compound { get; set; } = null!;

    public ApplicationUser? CreatedByUser { get; set; }
}
