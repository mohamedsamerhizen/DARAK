using DARAK.Api.Enums;

namespace DARAK.Api.DTOs.Occupancy;

public sealed record ResidentOccupancyRecordResponse(
    Guid Id,
    Guid ResidentProfileId,
    string ResidentName,
    Guid CompoundId,
    string CompoundName,
    Guid PropertyUnitId,
    string UnitNumber,
    OccupancyType OccupancyType,
    OccupancyStatus OccupancyStatus,
    DateOnly StartDate,
    DateOnly? EndDate,
    string? ContractNumber,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? EndedAt);
