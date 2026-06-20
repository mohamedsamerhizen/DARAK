namespace DARAK.Api.DTOs.AdminPortal;

public sealed record AdminContractsOverviewResponse(
    int TotalSaleContracts,
    int ActiveSaleContracts,
    int TotalRentContracts,
    int ActiveRentContracts,
    int PendingInstallments,
    int OverdueInstallments,
    int UnpaidRentInvoices,
    int OverdueRentInvoices);
