using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Occupancy;

namespace DARAK.Api.Interfaces;

public interface IOccupancyService
{
    Task<PagedResult<OccupancyRecordResponse>> SearchOccupanciesAsync(
        OccupancySearchQuery query,
        CancellationToken cancellationToken = default);

    Task<PagedResult<ResidentOccupancyRecordResponse>> SearchOccupanciesForUserAsync(
        Guid userId,
        PaginationQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<OccupancyRecordResponse>> GetOccupancyAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<OccupancyRecordResponse>> CreateOccupancyAsync(
        CreateOccupancyRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<OccupancyRecordResponse>> EndOccupancyAsync(
        Guid id,
        EndOccupancyRequest request,
        CancellationToken cancellationToken = default);
}
