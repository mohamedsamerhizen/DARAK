using DARAK.Api.Enums;

namespace DARAK.Api.Entities;

public sealed class ApprovalPolicy
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? CompoundId { get; set; }

    public ApprovalActionType ActionType { get; set; }

    public bool IsEnabled { get; set; } = true;

    public bool AllowSelfApproval { get; set; }

    public ApprovalPriority DefaultPriority { get; set; } = ApprovalPriority.Normal;

    public int ExpireAfterHours { get; set; } = 72;

    public string RequiredApproverRoles { get; set; } = "SuperAdmin,CompoundAdmin";

    public string? Description { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }

    public Compound? Compound { get; set; }
}
