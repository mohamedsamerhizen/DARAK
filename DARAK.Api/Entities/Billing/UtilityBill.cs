using DARAK.Api.Enums;

namespace DARAK.Api.Entities;

public sealed class UtilityBill
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CompoundId { get; set; }

    public Guid PropertyUnitId { get; set; }

    public Guid? ResidentProfileId { get; set; }

    public Guid BillingCycleId { get; set; }

    public string BillNumber { get; set; } = string.Empty;

    public BillStatus BillStatus { get; set; } = BillStatus.Unpaid;

    public DateOnly IssueDate { get; set; }

    public DateOnly DueDate { get; set; }

    public decimal SubtotalAmount { get; set; }

    public decimal PreviousBalanceAmount { get; set; }

    public decimal LateFeeAmount { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal TotalAmount { get; set; }

    public decimal PaidAmount { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public DateTime? CancelledAt { get; set; }

    public string? CancellationReason { get; set; }

    public byte[] RowVersion { get; set; } = [];

    public Compound Compound { get; set; } = null!;

    public PropertyUnit PropertyUnit { get; set; } = null!;

    public ResidentProfile? ResidentProfile { get; set; }

    public BillingCycle BillingCycle { get; set; } = null!;

    public ICollection<UtilityBillLine> Lines { get; set; } = new List<UtilityBillLine>();
}
