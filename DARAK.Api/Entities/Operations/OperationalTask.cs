using DARAK.Api.Enums;
using DARAK.Api.Identity;

namespace DARAK.Api.Entities;

public sealed class OperationalTask
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CompoundId { get; set; }

    public OperationalTaskType TaskType { get; set; } = OperationalTaskType.General;

    public OperationalTaskPriority Priority { get; set; } = OperationalTaskPriority.Normal;

    public OperationalTaskStatus Status { get; set; } = OperationalTaskStatus.Open;

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public AuditEntityType? RelatedEntityType { get; set; }

    public Guid? RelatedEntityId { get; set; }

    public Guid? AssignedToUserId { get; set; }

    public Guid CreatedByUserId { get; set; }

    public Guid? CompletedByUserId { get; set; }

    public Guid? CancelledByUserId { get; set; }

    public DateTime? DueAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }

    public DateTime? CompletedAtUtc { get; set; }

    public DateTime? CancelledAtUtc { get; set; }

    public string? CompletionNotes { get; set; }

    public string? CancellationReason { get; set; }

    public byte[] RowVersion { get; set; } = [];

    public Compound Compound { get; set; } = null!;

    public ApplicationUser? AssignedToUser { get; set; }

    public ApplicationUser CreatedByUser { get; set; } = null!;

    public ApplicationUser? CompletedByUser { get; set; }

    public ApplicationUser? CancelledByUser { get; set; }
}
