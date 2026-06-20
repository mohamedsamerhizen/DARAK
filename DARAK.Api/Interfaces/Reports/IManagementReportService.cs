using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Reports;

namespace DARAK.Api.Interfaces;

public interface IManagementReportService
{
    Task<ServiceResult<FinancialManagementReportResponse>> GetFinancialReportAsync(ManagementReportQuery query, CancellationToken cancellationToken = default);

    Task<ServiceResult<OccupancyManagementReportResponse>> GetOccupancyReportAsync(ManagementReportQuery query, CancellationToken cancellationToken = default);

    Task<ServiceResult<MaintenanceManagementReportResponse>> GetMaintenanceReportAsync(ManagementReportQuery query, CancellationToken cancellationToken = default);

    Task<ServiceResult<SupportManagementReportResponse>> GetSupportReportAsync(ManagementReportQuery query, CancellationToken cancellationToken = default);

    Task<ServiceResult<RiskAuditManagementReportResponse>> GetRiskAuditReportAsync(ManagementReportQuery query, CancellationToken cancellationToken = default);

    Task<ServiceResult<PagedResult<SavedReportResponse>>> SearchSavedReportsAsync(SavedReportSearchQuery query, CancellationToken cancellationToken = default);

    Task<ServiceResult<SavedReportResponse>> CreateSavedReportAsync(Guid? currentUserId, CreateSavedReportRequest request, CancellationToken cancellationToken = default);

    Task<ServiceResult<ReportExportJobResponse>> CreateExportJobAsync(Guid? currentUserId, CreateReportExportJobRequest request, CancellationToken cancellationToken = default);

    Task<ServiceResult<ReportExportJobResponse>> CompleteExportJobAsync(Guid? currentUserId, Guid id, CompleteReportExportJobRequest request, CancellationToken cancellationToken = default);
}
