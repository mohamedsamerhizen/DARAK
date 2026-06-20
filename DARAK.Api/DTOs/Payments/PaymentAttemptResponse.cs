using DARAK.Api.Enums;

namespace DARAK.Api.DTOs.Payments;

public sealed record PaymentAttemptResponse(
    Guid Id,
    Guid PaymentId,
    PaymentStatus AttemptStatus,
    string Provider,
    string? ProviderTransactionId,
    string? Message,
    DateTime CreatedAt);
