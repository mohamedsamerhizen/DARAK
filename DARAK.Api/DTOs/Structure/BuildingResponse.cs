namespace DARAK.Api.DTOs.Buildings;

public sealed record BuildingResponse(
    Guid Id,
    Guid CompoundId,
    string Name,
    string Code,
    int NumberOfFloors,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
