using System.ComponentModel.DataAnnotations;
using DARAK.Api.Enums;

namespace DARAK.Api.DTOs.Occupancy;

public sealed class CreateOccupancyRequest
{
    [Required]
    public Guid ResidentProfileId { get; init; }

    [Required]
    public Guid PropertyUnitId { get; init; }

    public OccupancyType OccupancyType { get; init; }

    [Required]
    public DateOnly StartDate { get; init; }

    public DateOnly? EndDate { get; init; }

    [MaxLength(100)]
    public string? ContractNumber { get; init; }

    [MaxLength(1000)]
    public string? Notes { get; init; }
}
