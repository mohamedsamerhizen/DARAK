using System.ComponentModel.DataAnnotations;
using DARAK.Api.Enums;

namespace DARAK.Api.DTOs.Meters;

public sealed class CreateMeterRequest
{
    public Guid CompoundId { get; init; }

    public Guid PropertyUnitId { get; init; }

    public MeterType MeterType { get; init; }

    [Required]
    [MaxLength(80)]
    public string MeterNumber { get; init; } = string.Empty;

    [Range(0, double.MaxValue)]
    public decimal RatePerUnit { get; init; }
}
