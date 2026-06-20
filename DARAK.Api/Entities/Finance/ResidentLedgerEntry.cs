using DARAK.Api.Enums;
using DARAK.Api.Identity;

namespace DARAK.Api.Entities;

public sealed class ResidentLedgerEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CompoundId { get; set; }

    public Guid ResidentProfileId { get; set; }

    public FinancialLedgerEntryDirection Direction { get; set; }

    public FinancialLedgerSourceType SourceType { get; set; }

    public Guid SourceId { get; set; }

    public Guid? FinancialAdjustmentId { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "IQD";

    public string Reference { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public Guid? CreatedByUserId { get; set; }

    public Compound Compound { get; set; } = null!;

    public ResidentProfile ResidentProfile { get; set; } = null!;

    public FinancialAdjustment? FinancialAdjustment { get; set; }

    public ApplicationUser? CreatedByUser { get; set; }
}
