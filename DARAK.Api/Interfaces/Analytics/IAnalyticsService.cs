using DARAK.Api.DTOs.Analytics;
using DARAK.Api.DTOs.Common;

namespace DARAK.Api.Interfaces;

public interface IAnalyticsService
{
    Task<ServiceResult<AdminDashboardSummaryResponse>> GetAdminDashboardSummaryAsync(
        Guid? currentUserId,
        DateRangeQueryRequest query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<FinancialReportResponse>> GetFinancialReportAsync(
        DateRangeQueryRequest query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<MaintenanceOperationsReportResponse>> GetMaintenanceOperationsReportAsync(
        DateRangeQueryRequest query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<CommunityReportResponse>> GetCommunityReportAsync(
        DateRangeQueryRequest query,
        CancellationToken cancellationToken = default);


    Task<ServiceResult<VisitorsReportResponse>> GetVisitorsReportAsync(
        DateRangeQueryRequest query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<DocumentsReportResponse>> GetDocumentsReportAsync(
        DateRangeQueryRequest query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<OperationsReportResponse>> GetOperationsReportAsync(
        DateRangeQueryRequest query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<IReadOnlyCollection<ChartPointResponse>>> GetPaymentsTrendAsync(
        DateRangeQueryRequest query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<IReadOnlyCollection<ChartPointResponse>>> GetMaintenanceTrendAsync(
        DateRangeQueryRequest query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<IReadOnlyCollection<ChartPointResponse>>> GetWorkOrdersTrendAsync(
        DateRangeQueryRequest query,
        CancellationToken cancellationToken = default);
}
