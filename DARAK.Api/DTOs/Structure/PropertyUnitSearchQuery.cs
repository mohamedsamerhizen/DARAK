using DARAK.Api.DTOs.Common;
using DARAK.Api.Enums;
using System.ComponentModel.DataAnnotations;

namespace DARAK.Api.DTOs.PropertyUnits;

public sealed class PropertyUnitSearchQuery : PaginationQuery
{
    public Guid? CompoundId { get; init; }

    public Guid? BuildingId { get; init; }

    public Guid? FloorId { get; init; }

    public PropertyType? PropertyType { get; init; }

    public UnitStatus? UnitStatus { get; init; }

    public decimal? MinArea { get; init; }

    public decimal? MaxArea { get; init; }

    public int? Bedrooms { get; init; }

    public int? Bathrooms { get; init; }

    public bool? HasParking { get; init; }

    public bool? IsActive { get; init; }

    [MaxLength(200)]
    public string? SearchTerm { get; init; }
}
