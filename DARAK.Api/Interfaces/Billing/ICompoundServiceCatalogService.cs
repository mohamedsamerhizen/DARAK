using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.CompoundServices;

namespace DARAK.Api.Interfaces;

public interface ICompoundServiceCatalogService
{
    Task<PagedResult<CompoundServiceResponse>> SearchCompoundServicesAsync(CompoundServiceSearchQuery query, CancellationToken cancellationToken = default);
    Task<ServiceResult<CompoundServiceResponse>> GetCompoundServiceAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ServiceResult<CompoundServiceResponse>> CreateCompoundServiceAsync(CreateCompoundServiceRequest request, CancellationToken cancellationToken = default);
    Task<ServiceResult<CompoundServiceResponse>> UpdateCompoundServiceAsync(Guid id, UpdateCompoundServiceRequest request, CancellationToken cancellationToken = default);
    Task<ServiceResult<object?>> DeactivateCompoundServiceAsync(Guid id, CancellationToken cancellationToken = default);
}
