using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.System;

namespace DARAK.Api.Interfaces;

public interface ICommercialProductizationService
{
    Task<ServiceResult<CommercialModuleRegistryResponse>> GetModuleRegistryAsync(
        FinalDeliveryQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProductCapabilityMapResponse>> GetProductCapabilityMapAsync(
        FinalDeliveryQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<BuyerDemoReadinessResponse>> GetBuyerDemoReadinessAsync(
        FinalDeliveryQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ClientOnboardingReadinessResponse>> GetClientOnboardingReadinessAsync(
        FinalDeliveryQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<FinalCommercialDeliveryScorecardResponse>> GetFinalDeliveryScorecardAsync(
        FinalDeliveryQuery query,
        CancellationToken cancellationToken = default);
}
