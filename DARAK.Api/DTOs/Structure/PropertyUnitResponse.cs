using DARAK.Api.Enums;

namespace DARAK.Api.DTOs.PropertyUnits;

public sealed record PropertyUnitResponse(
    Guid Id,
    Guid CompoundId,
    Guid? BuildingId,
    Guid? FloorId,
    string UnitNumber,
    PropertyType PropertyType,
    UnitStatus UnitStatus,
    decimal AreaSquareMeters,
    int Bedrooms,
    int Bathrooms,
    bool HasParking,
    string? Notes,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
