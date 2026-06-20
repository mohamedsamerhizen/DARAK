using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Operations;
using DARAK.Api.Enums;

namespace DARAK.Api.Interfaces;

public interface IServiceVendorService
{
    Task<PagedResult<ServiceVendorResponse>> SearchServiceVendorsAsync(
        ServiceVendorQueryRequest query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ServiceVendorResponse>> GetServiceVendorAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ServiceVendorResponse>> CreateServiceVendorAsync(
        CreateServiceVendorRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ServiceVendorResponse>> UpdateServiceVendorAsync(
        Guid id,
        UpdateServiceVendorRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ServiceVendorResponse>> SetServiceVendorStatusAsync(
        Guid id,
        VendorStatus status,
        CancellationToken cancellationToken = default);
}
