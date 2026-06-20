namespace DARAK.Api.DTOs.AdminPortal;

public sealed record AdminPaymentsOverviewResponse(
    int TotalPayments,
    int PendingPayments,
    int SucceededPayments,
    int FailedPayments,
    int CancelledPayments,
    int RefundedPayments,
    decimal TotalSucceededAmount);
