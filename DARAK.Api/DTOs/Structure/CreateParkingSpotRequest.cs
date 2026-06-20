using System.ComponentModel.DataAnnotations;

namespace DARAK.Api.DTOs.ParkingSpots;

public sealed class CreateParkingSpotRequest
{
    [Required]
    public Guid CompoundId { get; init; }

    [Required]
    [MaxLength(50)]
    public string SpotNumber { get; init; } = string.Empty;

    public bool IsCovered { get; init; }

    public bool IsReserved { get; init; }

    [MaxLength(1000)]
    public string? Notes { get; init; }
}
