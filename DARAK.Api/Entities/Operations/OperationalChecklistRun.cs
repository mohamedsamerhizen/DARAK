using DARAK.Api.Enums;
using DARAK.Api.Identity;

namespace DARAK.Api.Entities;

public sealed class OperationalChecklistRun
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CompoundId { get; set; }

    public Compound Compound { get; set; } = null!;

    public Guid OperationalChecklistTemplateId { get; set; }

    public OperationalChecklistTemplate Template { get; set; } = null!;

    public OperationalChecklistTargetType TargetType { get; set; } = OperationalChecklistTargetType.Other;

    public Guid TargetId { get; set; }

    public OperationalChecklistRunStatus Status { get; set; } = OperationalChecklistRunStatus.Open;

    public Guid? StartedByUserId { get; set; }

    public ApplicationUser? StartedByUser { get; set; }

    public Guid? CompletedByUserId { get; set; }

    public ApplicationUser? CompletedByUser { get; set; }

    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAtUtc { get; set; }

    public string? SummaryNotes { get; set; }

    public ICollection<OperationalChecklistRunItem> Items { get; set; } = new List<OperationalChecklistRunItem>();
}
