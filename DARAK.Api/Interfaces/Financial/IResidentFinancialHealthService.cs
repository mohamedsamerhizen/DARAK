using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Financial;

namespace DARAK.Api.Interfaces;

public interface IResidentFinancialHealthService
{
    Task<ServiceResult<ResidentFinancialHealthResponse>> GetAdminResidentFinancialHealthAsync(
        Guid? currentUserId,
        Guid residentProfileId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ResidentFinancialHealthResponse>> GetCurrentResidentFinancialHealthAsync(
        Guid? currentUserId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<FinancialHealthDashboardSummaryResponse>> GetDashboardSummaryAsync(
        Guid? currentUserId,
        FinancialHealthDashboardQuery query,
        CancellationToken cancellationToken = default);
}
