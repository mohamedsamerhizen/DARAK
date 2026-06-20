using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.System;

namespace DARAK.Api.Interfaces;

public interface ISaasTenantIntelligenceService
{
    Task<ServiceResult<SaasPortfolioOverviewResponse>> GetPortfolioOverviewAsync(
        int limit = 20,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<SaasTenantReadinessResponse>> GetTenantReadinessAsync(
        Guid compoundId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<DarakPrioritizationBrainResponse>> GetPrioritizationBrainAsync(
        string? area = null,
        int limit = 50,
        CancellationToken cancellationToken = default);
}
