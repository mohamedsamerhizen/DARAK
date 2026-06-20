using DARAK.Api.Enums;

namespace DARAK.Api.Entities;

public sealed class UnitDamageLiability
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CompoundId { get; set; }

    public Guid PropertyUnitId { get; set; }

    public Guid ResidentProfileId { get; set; }

    public Guid? ResidentLifecycleProcessId { get; set; }

    public DamageLiabilityStatus Status { get; set; } = DamageLiabilityStatus.Draft;

    public decimal EstimatedAmount { get; set; }

    public string Description { get; set; } = string.Empty;

    public Guid? FinancialAdjustmentId { get; set; }

    public Guid? WorkOrderId { get; set; }

    public string? Notes { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }

    public Compound Compound { get; set; } = null!;

    public PropertyUnit PropertyUnit { get; set; } = null!;

    public ResidentProfile ResidentProfile { get; set; } = null!;

    public ResidentLifecycleProcess? ResidentLifecycleProcess { get; set; }

    public FinancialAdjustment? FinancialAdjustment { get; set; }

    public WorkOrder? WorkOrder { get; set; }
}
