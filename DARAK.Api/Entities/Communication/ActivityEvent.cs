using DARAK.Api.Enums;
using DARAK.Api.Identity;

namespace DARAK.Api.Entities;

public sealed class ActivityEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CompoundId { get; set; }

    public Guid? ResidentProfileId { get; set; }

    public Guid? PropertyUnitId { get; set; }

    public Guid? ActorUserId { get; set; }

    public ActivityEventType EventType { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public ActivityEntityType EntityType { get; set; } = ActivityEntityType.None;

    public Guid? EntityId { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public string? MetadataJson { get; set; }

    public Compound Compound { get; set; } = null!;

    public ResidentProfile? ResidentProfile { get; set; }

    public PropertyUnit? PropertyUnit { get; set; }

    public ApplicationUser? ActorUser { get; set; }
}
