namespace DARAK.Api.DTOs.AdminPortal;

public sealed record AdminRecentPaymentResponse(
    Guid PaymentId,
    string PaymentReference,
    string? ResidentName,
    string PaymentMethod,
    string PaymentStatus,
    string TargetType,
    decimal Amount,
    DateTime CreatedAt,
    DateTime? CompletedAt);
