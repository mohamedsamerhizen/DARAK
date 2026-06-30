using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Finance;

namespace DARAK.Api.Interfaces;

public interface IFinancialControlService
{
    Task<ServiceResult<FinancialControlDashboardResponse>> GetDashboardAsync(
        FinancialDashboardQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ResidentStatementResponse>> GetResidentStatementAsync(
        Guid residentProfileId,
        ResidentStatementQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ResidentStatementResponse>> GetResidentStatementForUserAsync(
        Guid userId,
        ResidentStatementQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PagedResult<FinancialAdjustmentResponse>>> SearchAdjustmentsAsync(
        FinancialAdjustmentSearchQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<FinancialAdjustmentResponse>> GetAdjustmentAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<FinancialAdjustmentResponse>> CreateAdjustmentAsync(
        Guid? currentUserId,
        CreateFinancialAdjustmentRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<FinancialAdjustmentResponse>> ApplyAdjustmentAsync(
        Guid? currentUserId,
        Guid id,
        ApplyFinancialAdjustmentRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<FinancialAdjustmentResponse>> CancelAdjustmentAsync(
        Guid? currentUserId,
        Guid id,
        CancelFinancialAdjustmentRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<FinancialAgingReportResponse>> GetAgingReportAsync(
        FinancialAgingReportQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<FinancialAgingRiskReportResponse>> GetAgingRiskReportAsync(
        FinancialAgingRiskReportQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<FinancialClosureSummaryResponse>> GetFinancialClosureSummaryAsync(
        FinancialClosureSummaryQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<RevenueSummaryResponse>> GetRevenueSummaryAsync(
        RevenueSummaryQuery query,
        CancellationToken cancellationToken = default);
}
