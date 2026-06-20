using DARAK.Api.Enums;

namespace DARAK.Api.Entities;

public sealed class PropertyUnit
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CompoundId { get; set; }

    public Guid? BuildingId { get; set; }

    public Guid? FloorId { get; set; }

    public string UnitNumber { get; set; } = string.Empty;

    public PropertyType PropertyType { get; set; }

    public UnitStatus UnitStatus { get; set; } = UnitStatus.Available;

    public decimal AreaSquareMeters { get; set; }

    public int Bedrooms { get; set; }

    public int Bathrooms { get; set; }

    public bool HasParking { get; set; }

    public string? Notes { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public Compound Compound { get; set; } = null!;

    public Building? Building { get; set; }

    public Floor? Floor { get; set; }
}
