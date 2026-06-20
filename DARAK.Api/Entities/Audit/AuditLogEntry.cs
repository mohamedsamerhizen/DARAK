using DARAK.Api.Enums;
using DARAK.Api.Identity;

namespace DARAK.Api.Entities;

public sealed class AuditLogEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? CompoundId { get; set; }

    public Guid? ResidentProfileId { get; set; }

    public Guid? ActorUserId { get; set; }

    public string ActorRole { get; set; } = string.Empty;

    public AuditActionType ActionType { get; set; }

    public AuditEntityType EntityType { get; set; } = AuditEntityType.None;

    public Guid? EntityId { get; set; }

    public AuditSeverity Severity { get; set; } = AuditSeverity.Medium;

    public string SourceModule { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string? Reason { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public string? CorrelationId { get; set; }

    public string? BeforeValuesJson { get; set; }

    public string? AfterValuesJson { get; set; }

    public string? MetadataJson { get; set; }

    public bool IsSystemGenerated { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public Compound? Compound { get; set; }

    public ResidentProfile? ResidentProfile { get; set; }

    public ApplicationUser? ActorUser { get; set; }

    public ICollection<AuditLogChange> Changes { get; set; } = new List<AuditLogChange>();
}
