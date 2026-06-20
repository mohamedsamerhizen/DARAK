using DARAK.Api.Enums;
using DARAK.Api.Identity;

namespace DARAK.Api.Entities;

public sealed class FinancialAdjustment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CompoundId { get; set; }

    public Guid ResidentProfileId { get; set; }

    public FinancialAdjustmentType AdjustmentType { get; set; }

    public FinancialAdjustmentStatus Status { get; set; } = FinancialAdjustmentStatus.PendingApproval;

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "IQD";

    public string Reason { get; set; } = string.Empty;

    public Guid RequestedByUserId { get; set; }

    public Guid? ApprovalRequestId { get; set; }

    public Guid? AppliedByUserId { get; set; }

    public DateTime? AppliedAtUtc { get; set; }

    public Guid? CancelledByUserId { get; set; }

    public DateTime? CancelledAtUtc { get; set; }

    public string? CancellationReason { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }

    public byte[] RowVersion { get; set; } = [];

    public Compound Compound { get; set; } = null!;

    public ResidentProfile ResidentProfile { get; set; } = null!;

    public ApplicationUser RequestedByUser { get; set; } = null!;

    public ApplicationUser? AppliedByUser { get; set; }

    public ApplicationUser? CancelledByUser { get; set; }

    public ApprovalRequest? ApprovalRequest { get; set; }

    public ICollection<ResidentLedgerEntry> LedgerEntries { get; set; } = new List<ResidentLedgerEntry>();
}
