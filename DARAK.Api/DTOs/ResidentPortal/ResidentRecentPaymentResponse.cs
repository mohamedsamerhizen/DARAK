namespace DARAK.Api.DTOs.ResidentPortal;

public sealed record ResidentRecentPaymentResponse(
    Guid PaymentId,
    string PaymentReference,
    string TargetType,
    decimal Amount,
    string Status,
    DateTime CreatedAt,
    DateTime? CompletedAt);
