namespace DARAK.Api.DTOs.Payments;

public sealed record ReceiptResponse(
    Guid Id,
    Guid PaymentId,
    string ReceiptNumber,
    decimal Amount,
    DateTime IssuedAt);
