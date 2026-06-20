namespace DARAK.Api.DTOs.AdminPortal;

public sealed record AdminBillingOverviewResponse(
    int TotalUtilityBills,
    int UnpaidUtilityBills,
    int PartiallyPaidUtilityBills,
    int PaidUtilityBills,
    int OverdueUtilityBills,
    int CancelledUtilityBills,
    decimal TotalBilled,
    decimal TotalPaid,
    decimal TotalRemaining);
