using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Communication;

namespace DARAK.Api.Interfaces;

public interface IActivityTimelineService
{
    Task<ServiceResult<ActivityEventResponse>> RecordAsync(
        RecordActivityEventRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<IReadOnlyList<ActivityEventResponse>>> GetRecentForCompoundAsync(
        Guid compoundId,
        int count = 20,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PagedResult<ActivityEventResponse>>> SearchRecentActivityAsync(
        ActivityTimelineQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PagedResult<ActivityEventResponse>>> GetResidentTimelineAsync(
        Guid residentProfileId,
        ActivityTimelineQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PagedResult<ActivityEventResponse>>> GetUnitTimelineAsync(
        Guid propertyUnitId,
        ActivityTimelineQuery query,
        CancellationToken cancellationToken = default);
}
