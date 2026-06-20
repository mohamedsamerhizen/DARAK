using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Rents;

namespace DARAK.Api.Interfaces;

public interface IRentContractService
{
    Task<PagedResult<RentContractResponse>> SearchRentContractsAsync(RentContractSearchQuery query, CancellationToken cancellationToken = default);
    Task<ServiceResult<RentContractResponse>> GetRentContractAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ServiceResult<RentContractResponse>> CreateRentContractAsync(CreateRentContractRequest request, CancellationToken cancellationToken = default);
    Task<ServiceResult<RentContractResponse>> TerminateRentContractAsync(Guid id, TerminateRentContractRequest request, CancellationToken cancellationToken = default);
    Task<PagedResult<RentContractResponse>> SearchResidentRentContractsAsync(Guid userId, RentContractSearchQuery query, CancellationToken cancellationToken = default);
    Task<ServiceResult<RentContractResponse>> GetResidentRentContractAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);
}
