using DARAK.Api.Enums;

namespace DARAK.Api.DTOs.Meters;

public sealed record MeterResponse(
    Guid Id,
    Guid CompoundId,
    string CompoundName,
    Guid PropertyUnitId,
    string UnitNumber,
    MeterType MeterType,
    string MeterNumber,
    decimal RatePerUnit,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
