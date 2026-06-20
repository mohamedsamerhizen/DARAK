using DARAK.Api.Enums;
using DARAK.Api.Identity;

namespace DARAK.Api.Entities;

public sealed class PaymentReconciliationItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PaymentReconciliationBatchId { get; set; }

    public string ProviderTransactionId { get; set; } = string.Empty;

    public decimal ProviderAmount { get; set; }

    public PaymentStatus ProviderStatus { get; set; }

    public Guid? MatchedPaymentId { get; set; }

    public Guid? MatchedPaymentAttemptId { get; set; }

    public PaymentReconciliationItemStatus MatchStatus { get; set; }

    public decimal? DifferenceAmount { get; set; }

    public string? IssueReason { get; set; }

    public PaymentReconciliationReviewDecision ReviewDecision { get; set; } = PaymentReconciliationReviewDecision.None;

    public string? ReviewNotes { get; set; }

    public DateTime? ReviewedAtUtc { get; set; }

    public Guid? ReviewedByUserId { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public PaymentReconciliationBatch Batch { get; set; } = null!;

    public Payment? MatchedPayment { get; set; }

    public PaymentAttempt? MatchedPaymentAttempt { get; set; }

    public ApplicationUser? ReviewedByUser { get; set; }
}
