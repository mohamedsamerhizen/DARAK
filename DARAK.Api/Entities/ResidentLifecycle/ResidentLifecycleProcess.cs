using DARAK.Api.Enums;

namespace DARAK.Api.Entities;

public sealed class ResidentLifecycleProcess
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CompoundId { get; set; }

    public Guid PropertyUnitId { get; set; }

    public Guid ResidentProfileId { get; set; }

    public ResidentLifecycleProcessType ProcessType { get; set; }

    public ResidentLifecycleStatus Status { get; set; } = ResidentLifecycleStatus.InProgress;

    public DateOnly TargetDate { get; set; }

    public bool FinancialClearanceRequired { get; set; }

    public bool FinancialClearanceConfirmed { get; set; }

    public DateTime? FinancialClearanceConfirmedAtUtc { get; set; }

    public Guid? FinancialClearanceConfirmedByUserId { get; set; }

    public string? FinancialClearanceNotes { get; set; }

    public DateTime? CompletedAtUtc { get; set; }

    public Guid? CompletedByUserId { get; set; }

    public string? Notes { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }

    public Compound Compound { get; set; } = null!;

    public PropertyUnit PropertyUnit { get; set; } = null!;

    public ResidentProfile ResidentProfile { get; set; } = null!;

    public ICollection<MoveLogisticsPermit> MoveLogisticsPermits { get; set; } = new List<MoveLogisticsPermit>();

    public ICollection<UnitReadinessRecord> UnitReadinessRecords { get; set; } = new List<UnitReadinessRecord>();

    public ICollection<UnitDamageLiability> DamageLiabilities { get; set; } = new List<UnitDamageLiability>();
}
