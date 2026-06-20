namespace DARAK.Api.DTOs.AdminPortal;

public sealed record AdminDebtOverviewResponse(
    decimal TotalOutstanding,
    decimal UtilityBillsOutstanding,
    decimal RentOutstanding,
    decimal InstallmentsOutstanding,
    int OverdueUtilityBills,
    int OverdueRentInvoices,
    int OverdueInstallments,
    List<AdminTopDebtorResponse> TopDebtors);
