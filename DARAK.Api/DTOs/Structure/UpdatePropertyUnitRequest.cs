using System.ComponentModel.DataAnnotations;
using DARAK.Api.Enums;

namespace DARAK.Api.DTOs.PropertyUnits;

public sealed class UpdatePropertyUnitRequest
{
    [Required]
    public Guid CompoundId { get; init; }

    public Guid? BuildingId { get; init; }

    public Guid? FloorId { get; init; }

    [Required]
    [MaxLength(50)]
    public string UnitNumber { get; init; } = string.Empty;

    public PropertyType PropertyType { get; init; }

    public UnitStatus UnitStatus { get; init; } = UnitStatus.Available;

    [Range(typeof(decimal), "0.01", "999999999")]
    public decimal AreaSquareMeters { get; init; }

    [Range(0, int.MaxValue)]
    public int Bedrooms { get; init; }

    [Range(0, int.MaxValue)]
    public int Bathrooms { get; init; }

    public bool HasParking { get; init; }

    [MaxLength(1000)]
    public string? Notes { get; init; }

    public bool IsActive { get; init; } = true;
}
