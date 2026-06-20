using System.ComponentModel.DataAnnotations;

namespace DARAK.Api.DTOs.Buildings;

public sealed class CreateBuildingRequest
{
    [Required]
    public Guid CompoundId { get; init; }

    [Required]
    [MaxLength(150)]
    public string Name { get; init; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Code { get; init; } = string.Empty;

    [Range(0, int.MaxValue)]
    public int NumberOfFloors { get; init; }
}
