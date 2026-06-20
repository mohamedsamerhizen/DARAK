using DARAK.Api.Enums;

namespace DARAK.Api.DTOs.Occupancy;

public sealed record OccupancyRecordResponse(
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
    string? Notes,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? EndedAt);
