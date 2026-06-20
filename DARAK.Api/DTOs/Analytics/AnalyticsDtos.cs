namespace DARAK.Api.DTOs.Analytics;

public sealed record ChartPointResponse(
    string Label,
    int Count,
    decimal? Amount);

public sealed class DateRangeQueryRequest
{
    public Guid? CompoundId { get; init; }

    public DateTime? FromUtc { get; init; }

    public DateTime? ToUtc { get; init; }
}

public sealed record AdminDashboardSummaryResponse(
    int TotalUsers,
    int TotalResidents,
    int TotalUnits,
    int OccupiedUnits,
    int VacantUnits,
    int TotalBills,
    int PaidBills,
    int UnpaidBills,
    int OverdueBills,
    int TotalPayments,
    decimal TotalPaymentAmount,
    int OpenMaintenanceRequests,
    int OpenComplaints,
    int OpenViolations,
    int PendingVisitors,
    int UnreadNotifications,
    int TotalDocuments,
    int OpenWorkOrders,
    int OverdueWorkOrders);

public sealed record FinancialReportResponse(
    decimal TotalBilledAmount,
    decimal TotalPaidAmount,
    decimal TotalOutstandingAmount,
    int PaidBillsCount,
    int UnpaidBillsCount,
    int OverdueBillsCount,
    int PaymentsCount,
    IReadOnlyCollection<ChartPointResponse> PaymentsByStatus,
    IReadOnlyCollection<ChartPointResponse> PaymentsByTargetType);

public sealed record MaintenanceOperationsReportResponse(
    int TotalMaintenanceRequests,
    int OpenMaintenanceRequests,
    int InProgressMaintenanceRequests,
    int CompletedMaintenanceRequests,
    int CancelledMaintenanceRequests,
    decimal? AverageCompletionHours,
    int TotalWorkOrders,
    int OpenWorkOrders,
    int CompletedWorkOrders,
    int OverdueWorkOrders,
    decimal TotalEstimatedWorkOrderCost,
    decimal TotalActualWorkOrderCost);

public sealed record CommunityReportResponse(
    int TotalAnnouncements,
    int PublishedAnnouncements,
    int ActivePolls,
    int TotalPollVotes,
    int TotalNotifications,
    int UnreadNotifications,
    int TotalComplaints,
    int OpenComplaints,
    int TotalViolations,
    int OpenViolations);


public sealed record VisitorsReportResponse(
    int TotalVisitors,
    int PendingVisitors,
    int ApprovedVisitors,
    int CheckedInVisitors,
    int CheckedOutVisitors,
    int RejectedVisitors);

public sealed record DocumentsReportResponse(
    int TotalDocuments,
    IReadOnlyCollection<ChartPointResponse> DocumentsByCategory,
    IReadOnlyCollection<ChartPointResponse> DocumentsByVisibility,
    long TotalStorageBytes,
    decimal TotalStorageMegabytes);

public sealed record OperationsReportResponse(
    int TotalStaffMembers,
    int ActiveStaffMembers,
    int TotalVendors,
    int ActiveVendors,
    int TotalWorkOrders,
    IReadOnlyCollection<ChartPointResponse> WorkOrdersByStatus,
    IReadOnlyCollection<ChartPointResponse> WorkOrdersByPriority,
    int OverdueWorkOrders,
    decimal TotalActualCost);
