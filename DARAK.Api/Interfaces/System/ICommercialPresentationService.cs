using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.System;

namespace DARAK.Api.Interfaces;

public interface ICommercialPresentationService
{
    Task<ServiceResult<DemoSeedBlueprintResponse>> GetDemoSeedBlueprintAsync(
        CommercialPresentationQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<CommercialDemoModeResponse>> GetCommercialDemoModeAsync(
        CommercialPresentationQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<BuyerPresentationPackResponse>> GetBuyerPresentationPackAsync(
        CommercialPresentationQuery query,
        CancellationToken cancellationToken = default);
}
