namespace DARAK.Api.DTOs.ResidentPortal;

public sealed record ResidentDashboardResponse(
    Guid ResidentProfileId,
    string ResidentName,
    int ActivePropertiesCount,
    decimal TotalOutstandingAmount,
    decimal OverdueAmount,
    int UnpaidUtilityBillsCount,
    int OverdueUtilityBillsCount,
    int PendingInstallmentsCount,
    int OverdueInstallmentsCount,
    int UnpaidRentInvoicesCount,
    int OverdueRentInvoicesCount,
    int PendingPaymentsCount,
    int OpenMeterReadingsCount,
    List<ResidentUpcomingDueItemResponse> UpcomingDueItems,
    List<ResidentRecentPaymentResponse> RecentPayments,
    List<ResidentPropertySummaryResponse> Properties);
