namespace DARAK.Api.DTOs.Floors;

public sealed record FloorResponse(
    Guid Id,
    Guid CompoundId,
    Guid BuildingId,
    int FloorNumber,
    string? Name,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
