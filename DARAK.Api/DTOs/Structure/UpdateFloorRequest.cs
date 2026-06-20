using System.ComponentModel.DataAnnotations;

namespace DARAK.Api.DTOs.Floors;

public sealed class UpdateFloorRequest
{
    [Required]
    public Guid CompoundId { get; init; }

    [Required]
    public Guid BuildingId { get; init; }

    [Range(0, int.MaxValue)]
    public int FloorNumber { get; init; }

    [MaxLength(100)]
    public string? Name { get; init; }

    public bool IsActive { get; init; } = true;
}
