using DARAK.Api.Enums;
using DARAK.Api.Identity;

namespace DARAK.Api.Entities;

public sealed class SupportCaseEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid SupportCaseId { get; set; }

    public Guid? ActorUserId { get; set; }

    public SupportCaseEventType EventType { get; set; }

    public SupportCaseStatus? FromStatus { get; set; }

    public SupportCaseStatus? ToStatus { get; set; }

    public string Description { get; set; } = string.Empty;

    public string? InternalNote { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public SupportCase SupportCase { get; set; } = null!;

    public ApplicationUser? ActorUser { get; set; }
}
