using DARAK.Api.Enums;

namespace DARAK.Api.DTOs.Meters;

public sealed record MeterReadingResponse(
    Guid Id,
    Guid CompoundId,
    string CompoundName,
    Guid MeterId,
    string MeterNumber,
    MeterType MeterType,
    Guid PropertyUnitId,
    string UnitNumber,
    int Year,
    int Month,
    decimal PreviousReading,
    decimal CurrentReading,
    decimal Consumption,
    decimal RatePerUnit,
    decimal Amount,
    bool IsBilled,
    Guid? UtilityBillId,
    Guid? UtilityBillLineId,
    DateTime ReadingDate,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? BilledAt,
    string? Notes);
