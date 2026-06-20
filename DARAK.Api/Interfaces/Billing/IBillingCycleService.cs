using DARAK.Api.DTOs.BillingCycles;
using DARAK.Api.DTOs.Common;

namespace DARAK.Api.Interfaces;

public interface IBillingCycleService
{
    Task<PagedResult<BillingCycleResponse>> SearchBillingCyclesAsync(BillingCycleSearchQuery query, CancellationToken cancellationToken = default);
    Task<ServiceResult<BillingCycleResponse>> GetBillingCycleAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ServiceResult<BillingCycleResponse>> CreateBillingCycleAsync(CreateBillingCycleRequest request, CancellationToken cancellationToken = default);
    Task<ServiceResult<BillingCycleResponse>> UpdateBillingCycleAsync(Guid id, UpdateBillingCycleRequest request, CancellationToken cancellationToken = default);
    Task<ServiceResult<BillingCycleResponse>> CloseBillingCycleAsync(Guid id, CancellationToken cancellationToken = default);
}
