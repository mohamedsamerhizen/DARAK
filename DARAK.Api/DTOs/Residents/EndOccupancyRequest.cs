using DARAK.Api.Enums;

namespace DARAK.Api.DTOs.Occupancy;

public sealed class EndOccupancyRequest
{
    public DateOnly EndDate { get; init; } = DateOnly.FromDateTime(DateTime.UtcNow);

    public UnitStatus UnitStatusAfterEnd { get; init; } = UnitStatus.Available;
}
