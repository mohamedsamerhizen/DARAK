using DARAK.Api.Enums;

namespace DARAK.Api.Entities;

public sealed class MoveLogisticsPermit
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CompoundId { get; set; }

    public Guid PropertyUnitId { get; set; }

    public Guid ResidentProfileId { get; set; }

    public Guid? ResidentLifecycleProcessId { get; set; }

    public ResidentLifecycleProcessType MoveType { get; set; }

    public MoveLogisticsPermitStatus Status { get; set; } = MoveLogisticsPermitStatus.PendingApproval;

    public DateTime ScheduledStartAtUtc { get; set; }

    public DateTime ScheduledEndAtUtc { get; set; }

    public string? TruckInfo { get; set; }

    public int WorkersCount { get; set; }

    public string? Notes { get; set; }

    public string? DecisionReason { get; set; }

    public Guid? ApprovedByUserId { get; set; }

    public DateTime? ApprovedAtUtc { get; set; }

    public Guid? CompletedByUserId { get; set; }

    public DateTime? CompletedAtUtc { get; set; }

    public string? CompletionNotes { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }

    public Compound Compound { get; set; } = null!;

    public PropertyUnit PropertyUnit { get; set; } = null!;

    public ResidentProfile ResidentProfile { get; set; } = null!;

    public ResidentLifecycleProcess? ResidentLifecycleProcess { get; set; }
}
