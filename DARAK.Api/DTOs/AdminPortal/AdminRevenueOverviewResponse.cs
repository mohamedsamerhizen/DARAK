namespace DARAK.Api.DTOs.AdminPortal;

public sealed record AdminRevenueOverviewResponse(
    decimal TotalCollected,
    decimal CollectedThisMonth,
    decimal CollectedToday,
    decimal UtilityCollected,
    decimal RentCollected,
    decimal InstallmentCollected,
    List<AdminRevenueByPaymentMethodResponse> ByPaymentMethod);
