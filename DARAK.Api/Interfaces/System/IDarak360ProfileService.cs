using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.System;

namespace DARAK.Api.Interfaces;

public interface IDarak360ProfileService
{
    Task<ServiceResult<Resident360ProfileResponse>> GetResident360ProfileAsync(
        Guid residentId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<Unit360ProfileResponse>> GetUnit360ProfileAsync(
        Guid unitId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<Compound360OverviewResponse>> GetCompound360OverviewAsync(
        Guid compoundId,
        CancellationToken cancellationToken = default);
}
