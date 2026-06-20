using DARAK.Api.Enums;

namespace DARAK.Api.DTOs.PropertyUnits;

public sealed class UpdatePropertyUnitStatusRequest
{
    public UnitStatus UnitStatus { get; init; }
}
