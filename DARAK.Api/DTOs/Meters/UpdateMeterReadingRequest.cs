using System.ComponentModel.DataAnnotations;

namespace DARAK.Api.DTOs.Meters;

public sealed class UpdateMeterReadingRequest
{
    [Range(0, double.MaxValue)]
    public decimal CurrentReading { get; init; }

    [MaxLength(1000)]
    public string? Notes { get; init; }
}
