using DARAK.Api.Enums;

namespace DARAK.Api.DTOs.CompoundServices;

public sealed record CompoundServiceResponse(
    Guid Id,
    Guid CompoundId,
    string CompoundName,
    UtilityServiceType ServiceType,
    string Name,
    string? Description,
    decimal DefaultMonthlyFee,
    bool IsMeterBased,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
