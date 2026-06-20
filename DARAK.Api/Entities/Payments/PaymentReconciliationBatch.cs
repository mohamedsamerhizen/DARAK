using DARAK.Api.Enums;
using DARAK.Api.Identity;

namespace DARAK.Api.Entities;

public sealed class PaymentReconciliationBatch
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CompoundId { get; set; }

    public string Provider { get; set; } = string.Empty;

    public string StatementReference { get; set; } = string.Empty;

    public DateOnly StatementDate { get; set; }

    public PaymentReconciliationBatchStatus Status { get; set; } = PaymentReconciliationBatchStatus.Open;

    public string? Notes { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public Guid? CreatedByUserId { get; set; }

    public DateTime? ClosedAtUtc { get; set; }

    public Guid? ClosedByUserId { get; set; }

    public Compound Compound { get; set; } = null!;

    public ApplicationUser? CreatedByUser { get; set; }

    public ApplicationUser? ClosedByUser { get; set; }

    public ICollection<PaymentReconciliationItem> Items { get; set; } = new List<PaymentReconciliationItem>();
}
