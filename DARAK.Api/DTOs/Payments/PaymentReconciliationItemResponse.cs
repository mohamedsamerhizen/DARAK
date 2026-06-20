using DARAK.Api.Enums;

namespace DARAK.Api.DTOs.Payments;

public sealed record PaymentReconciliationItemResponse(
    Guid Id,
    Guid PaymentReconciliationBatchId,
    string ProviderTransactionId,
    decimal ProviderAmount,
    PaymentStatus ProviderStatus,
    Guid? MatchedPaymentId,
    Guid? MatchedPaymentAttemptId,
    PaymentReconciliationItemStatus MatchStatus,
    decimal? DifferenceAmount,
    string? IssueReason,
    PaymentReconciliationReviewDecision ReviewDecision,
    string? ReviewNotes,
    DateTime? ReviewedAtUtc,
    Guid? ReviewedByUserId,
    DateTime CreatedAtUtc);
