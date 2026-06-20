using DARAK.Api.DTOs.Common;

namespace DARAK.Api.DTOs.Floors;

public sealed class FloorSearchQuery : PaginationQuery
{
    public Guid? CompoundId { get; init; }

    public Guid? BuildingId { get; init; }

    public bool? IsActive { get; init; }
}
