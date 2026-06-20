namespace DARAK.Api.DTOs.ParkingSpots;

public sealed record ParkingSpotResponse(
    Guid Id,
    Guid CompoundId,
    string SpotNumber,
    bool IsCovered,
    bool IsReserved,
    bool IsActive,
    string? Notes,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
