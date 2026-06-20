using DARAK.Api.Enums;

namespace DARAK.Api.DTOs.Payments;

public sealed record PaymentResponse(
    Guid Id,
    Guid CompoundId,
    string CompoundName,
    Guid? ResidentProfileId,
    string? ResidentFullName,
    PaymentTargetType TargetType,
    Guid TargetId,
    string? TargetReference,
    PaymentMethod PaymentMethod,
    PaymentStatus PaymentStatus,
    decimal Amount,
    string Currency,
    string? IdempotencyKey,
    string PaymentReference,
    string? FailureReason,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? CompletedAt,
    DateTime? CancelledAt,
    DateTime? RefundedAt,
    ReceiptResponse? Receipt,
    IReadOnlyCollection<PaymentAttemptResponse> Attempts);
