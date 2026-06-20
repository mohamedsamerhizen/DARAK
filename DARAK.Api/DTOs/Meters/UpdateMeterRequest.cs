using System.ComponentModel.DataAnnotations;

namespace DARAK.Api.DTOs.Meters;

public sealed class UpdateMeterRequest
{
    [Required]
    [MaxLength(80)]
    public string MeterNumber { get; init; } = string.Empty;

    [Range(0, double.MaxValue)]
    public decimal RatePerUnit { get; init; }

    public bool IsActive { get; init; } = true;
}
