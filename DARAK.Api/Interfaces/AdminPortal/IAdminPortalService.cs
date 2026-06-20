using DARAK.Api.DTOs.AdminPortal;
using DARAK.Api.DTOs.Common;

namespace DARAK.Api.Interfaces;

public interface IAdminPortalService
{
    Task<ServiceResult<AdminDashboardResponse>> GetDashboardAsync(
        AdminOverviewQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<AdminUnitsOverviewResponse>> GetUnitsOverviewAsync(
        AdminOverviewQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<AdminDebtOverviewResponse>> GetDebtOverviewAsync(
        AdminOverviewQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<AdminRevenueOverviewResponse>> GetRevenueOverviewAsync(
        AdminOverviewQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<AdminOccupancyOverviewResponse>> GetOccupancyOverviewAsync(
        AdminOverviewQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<AdminBillingOverviewResponse>> GetBillingOverviewAsync(
        AdminOverviewQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<AdminPaymentsOverviewResponse>> GetPaymentsOverviewAsync(
        AdminOverviewQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<AdminContractsOverviewResponse>> GetContractsOverviewAsync(
        AdminOverviewQuery query,
        CancellationToken cancellationToken = default);
}
