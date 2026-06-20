namespace DARAK.Api.DTOs.ResidentPortal;

public sealed record ResidentAccountSummaryResponse(
    decimal UtilityBillsOutstanding,
    decimal RentOutstanding,
    decimal InstallmentsOutstanding,
    decimal TotalOutstanding);
