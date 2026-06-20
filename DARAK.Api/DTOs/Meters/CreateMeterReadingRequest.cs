using System.ComponentModel.DataAnnotations;

namespace DARAK.Api.DTOs.Meters;

public sealed class CreateMeterReadingRequest
{
    public Guid MeterId { get; init; }

    [Range(2000, 2100)]
    public int Year { get; init; }

    [Range(1, 12)]
    public int Month { get; init; }

    [Range(0, double.MaxValue)]
    public decimal CurrentReading { get; init; }

    public DateTime? ReadingDate { get; init; }

    [MaxLength(1000)]
    public string? Notes { get; init; }
}
