namespace DARAK.Api.DTOs.AdminPortal;

public sealed record AdminRevenueByPaymentMethodResponse(
    string PaymentMethod,
    decimal TotalAmount,
    int Count);
