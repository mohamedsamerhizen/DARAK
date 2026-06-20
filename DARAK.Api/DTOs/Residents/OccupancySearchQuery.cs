using DARAK.Api.DTOs.Common;
using DARAK.Api.Enums;

namespace DARAK.Api.DTOs.Occupancy;

public sealed class OccupancySearchQuery : PaginationQuery
{
    public Guid? CompoundId { get; init; }

    public Guid? ResidentProfileId { get; init; }

    public Guid? PropertyUnitId { get; init; }

    public OccupancyType? OccupancyType { get; init; }

    public OccupancyStatus? OccupancyStatus { get; init; }

    public DateOnly? StartDateFrom { get; init; }

    public DateOnly? StartDateTo { get; init; }
}
