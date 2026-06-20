namespace DARAK.Api.DTOs.ResidentPortal;

public sealed record ResidentPropertySummaryResponse(
    Guid PropertyUnitId,
    string UnitNumber,
    string PropertyType,
    string UnitStatus,
    string OccupancyType,
    DateOnly StartDate,
    string? BuildingName,
    int? FloorNumber);
