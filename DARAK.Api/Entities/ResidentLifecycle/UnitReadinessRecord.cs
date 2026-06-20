using DARAK.Api.Enums;

namespace DARAK.Api.Entities;

public sealed class UnitReadinessRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CompoundId { get; set; }

    public Guid PropertyUnitId { get; set; }

    public Guid? ResidentLifecycleProcessId { get; set; }

    public UnitReadinessStatus Status { get; set; } = UnitReadinessStatus.NeedsInspection;

    public Guid? OperationalChecklistRunId { get; set; }

    public string? Notes { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }

    public Compound Compound { get; set; } = null!;

    public PropertyUnit PropertyUnit { get; set; } = null!;

    public ResidentLifecycleProcess? ResidentLifecycleProcess { get; set; }

    public OperationalChecklistRun? OperationalChecklistRun { get; set; }
}
