using DARAK.Api.Enums;
using DARAK.Api.Identity;

namespace DARAK.Api.Entities;

public sealed class UserCompoundAssignment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    public Guid CompoundId { get; set; }

    public UserRole Role { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public ApplicationUser User { get; set; } = null!;

    public Compound Compound { get; set; } = null!;

    public ApplicationUser? CreatedByUser { get; set; }
}
