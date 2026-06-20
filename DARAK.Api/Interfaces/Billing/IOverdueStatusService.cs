using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Financial;

namespace DARAK.Api.Interfaces;

public interface IOverdueStatusService
{
    Task<ServiceResult<ProcessOverdueStatusResponse>> ProcessAsync(
        ProcessOverdueStatusRequest request,
        CancellationToken cancellationToken = default);
}
